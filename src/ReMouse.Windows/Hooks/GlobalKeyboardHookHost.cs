using System.Runtime.InteropServices;

namespace ReMouse.Windows.Hooks;

/// <summary>
/// Owns one WH_KEYBOARD_LL hook on a dedicated message-pumping thread.
/// The callback only recognizes the physical emergency chord and key-down
/// notifications, then always passes the original keyboard event to the next
/// hook. Consumers must keep their callbacks bounded; this host never blocks
/// or injects keyboard input.
/// </summary>
public sealed class GlobalKeyboardHookHost : IDisposable
{
    private readonly Action _onEmergencyPause;
    private readonly Action<uint>? _onPhysicalKeyDown;
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly object _lifecycleLock = new();

    private Thread? _thread;
    private uint _threadId;
    private nint _hook;
    private Exception? _startupException;
    private int _shutdownRequested;
    private bool _disposed;

    public GlobalKeyboardHookHost(Action onEmergencyPause, Action<uint>? onPhysicalKeyDown = null)
    {
        _onEmergencyPause = onEmergencyPause ?? throw new ArgumentNullException(nameof(onEmergencyPause));
        _onPhysicalKeyDown = onPhysicalKeyDown;
        _callback = KeyboardCallback;
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_thread is not null)
            {
                throw new InvalidOperationException("The global keyboard hook is already running.");
            }

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "ReMouse.Windows.GlobalKeyboardHook"
            };
            _thread.Start();
        }

        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("The global keyboard hook thread did not become ready.");
        }

        if (_startupException is not null)
        {
            throw new InvalidOperationException(
                "Unable to start the global keyboard hook.",
                _startupException);
        }
    }

    public void Dispose()
    {
        Thread? thread;
        uint threadId;

        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Volatile.Write(ref _shutdownRequested, 1);
            thread = _thread;
            threadId = _threadId;
        }

        if (thread is null)
        {
            _ready.Dispose();
            return;
        }

        if (threadId != 0)
        {
            NativeMethods.PostThreadMessage(threadId, NativeMethods.WmQuit, 0, 0);
        }

        if (!ReferenceEquals(Thread.CurrentThread, thread))
        {
            if (!thread.Join(TimeSpan.FromSeconds(5)))
            {
                UnhookFromShutdownFallback();
                thread.Join(TimeSpan.FromSeconds(1));
            }
        }

        if (!thread.IsAlive)
        {
            _ready.Dispose();
        }
    }

    private void Run()
    {
        _threadId = NativeMethods.GetCurrentThreadId();

        try
        {
            NativeMethods.PeekMessage(out _, 0, 0, 0, NativeMethods.PmNoRemove);
            if (Volatile.Read(ref _shutdownRequested) != 0)
            {
                return;
            }

            var moduleHandle = NativeMethods.GetModuleHandle(null);
            if (moduleHandle == 0)
            {
                throw NativeMethods.LastWin32Error("GetModuleHandle");
            }

            _hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WhKeyboardLl,
                _callback,
                moduleHandle,
                0);
            if (_hook == 0)
            {
                throw NativeMethods.LastWin32Error("SetWindowsHookEx(WH_KEYBOARD_LL)");
            }

            if (Volatile.Read(ref _shutdownRequested) != 0)
            {
                return;
            }

            _ready.Set();
            while (true)
            {
                var result = NativeMethods.GetMessage(out var message, 0, 0, 0);
                if (result == -1)
                {
                    throw NativeMethods.LastWin32Error("GetMessage");
                }

                if (result == 0)
                {
                    break;
                }

                NativeMethods.TranslateMessage(ref message);
                NativeMethods.DispatchMessage(ref message);
            }
        }
        catch (Exception exception)
        {
            _startupException = exception;
            _ready.Set();
        }
        finally
        {
            var hook = Interlocked.Exchange(ref _hook, 0);
            if (hook != 0)
            {
                NativeMethods.UnhookWindowsHookEx(hook);
            }

            _ready.Set();
        }
    }

    private void UnhookFromShutdownFallback()
    {
        var hook = Interlocked.Exchange(ref _hook, 0);
        if (hook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(hook);
        }
    }

    private nint KeyboardCallback(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= NativeMethods.HcAction &&
                (uint)wParam is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown)
            {
                var hookData = Marshal.PtrToStructure<NativeMethods.KbdllHookStruct>(lParam);
                var isInjected = (hookData.Flags & NativeMethods.LlkfhInjected) != 0;
                if (!isInjected)
                {
                    if (EmergencyPauseGesture.IsTriggered(
                            hookData.VirtualKeyCode,
                            IsKeyDown(NativeMethods.VkControl),
                            IsKeyDown(NativeMethods.VkMenu)))
                    {
                        _onEmergencyPause();
                    }

                    _onPhysicalKeyDown?.Invoke(hookData.VirtualKeyCode);
                }
            }
        }
        catch
        {
            // Emergency pause must never break the global keyboard hook chain.
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static bool IsKeyDown(uint virtualKey) =>
        (NativeMethods.GetAsyncKeyState(unchecked((int)virtualKey)) & 0x8000) != 0;
}
