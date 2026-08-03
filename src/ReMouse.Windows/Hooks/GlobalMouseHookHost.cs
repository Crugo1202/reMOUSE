using System.Runtime.InteropServices;

namespace ReMouse.Windows.Hooks;

/// <summary>
/// Owns one WH_MOUSE_LL hook on a dedicated message-pumping thread. The handler
/// must do only bounded state work and return a suppression decision; it must
/// not perform UI, file, process, or synchronous input injection.
/// </summary>
public sealed class GlobalMouseHookHost : IDisposable
{
    private readonly Func<GlobalMouseEvent, GlobalMouseDecision> _handler;
    private readonly NativeMethods.LowLevelMouseProc _callback;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly object _lifecycleLock = new();
    private Thread? _thread;
    private uint _threadId;
    private nint _hook;
    private Exception? _startupException;
    private int _shutdownRequested;
    private bool _disposed;

    public GlobalMouseHookHost(Func<GlobalMouseEvent, GlobalMouseDecision> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _callback = MouseCallback;
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_thread is not null)
            {
                throw new InvalidOperationException("The global mouse hook is already running.");
            }

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "ReMouse.Windows.GlobalMouseHook"
            };
            _thread.Start();
        }

        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("The global mouse hook thread did not become ready.");
        }

        if (_startupException is not null)
        {
            throw new InvalidOperationException("Unable to start the global mouse hook.", _startupException);
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
                NativeMethods.WhMouseLl,
                _callback,
                moduleHandle,
                0);
            if (_hook == 0)
            {
                throw NativeMethods.LastWin32Error("SetWindowsHookEx(WH_MOUSE_LL)");
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

    private nint MouseCallback(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= NativeMethods.HcAction)
            {
                var hookData = Marshal.PtrToStructure<NativeMethods.MsllHookStruct>(lParam);
                if (GlobalMouseMessageParser.TryParse((uint)wParam, hookData, out var mouseEvent))
                {
                    var decision = _handler(mouseEvent);
                    if (decision.SuppressOriginal)
                    {
                        return 1;
                    }
                }
            }
        }
        catch
        {
            // A global hook must fail open if parsing or the bounded handler fails.
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }
}
