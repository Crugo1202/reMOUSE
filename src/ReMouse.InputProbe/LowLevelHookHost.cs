using System.Runtime.InteropServices;
using ReMouse.Core.Input;

namespace ReMouse.InputProbe;

internal sealed class LowLevelHookHost : IDisposable
{
    private readonly ProbeEventChannel _events;
    private readonly ProbeOptions _options;
    private readonly NativeMethods.LowLevelMouseProc _mouseCallback;
    private readonly NativeMethods.LowLevelKeyboardProc _keyboardCallback;
    private readonly MiddleChordHookController? _middleChord;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly object _lifecycleLock = new();

    private Thread? _thread;
    private uint _threadId;
    private nint _mouseHook;
    private nint _keyboardHook;
    private Exception? _startupException;
    private int _shutdownRequested;
    private bool _disposed;

    public LowLevelHookHost(ProbeEventChannel events, ProbeOptions options)
    {
        _events = events;
        _options = options;
        _mouseCallback = MouseCallback;
        _keyboardCallback = KeyboardCallback;
        _middleChord = options.MiddleFlickEnabled
            ? new MiddleChordHookController(events, options.FlickDelta)
            : null;
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_thread is not null)
            {
                throw new InvalidOperationException("The hook host is already running.");
            }

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "ReMouse.InputProbe.Hook"
            };
            _thread.Start();
        }

        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("The hook thread did not become ready within five seconds.");
        }

        if (_startupException is not null)
        {
            throw new InvalidOperationException("Unable to start the low-level hooks.", _startupException);
        }
    }

    internal MiddleChordHookController? MiddleChordController => _middleChord;

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
                // The normal path is WM_QUIT -> message loop exits -> finally
                // unhooks. Keep a defensive fallback for a thread that stopped
                // pumping messages so a probe cannot leave a global hook behind.
                UnhookFromShutdownFallback();
                thread.Join(TimeSpan.FromSeconds(1));
            }
        }

        // Do not dispose _ready while the hook thread might still be in Run().
        // Its finally block may still call Set() after a timed-out join.
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
            // Create this thread's message queue before Start() returns. This makes
            // PostThreadMessage(WM_QUIT) reliable during shutdown.
            NativeMethods.PeekMessage(
                out _,
                0,
                0,
                0,
                NativeMethods.PmNoRemove);

            if (Volatile.Read(ref _shutdownRequested) != 0)
            {
                return;
            }

            var moduleHandle = NativeMethods.GetModuleHandle(null);
            if (moduleHandle == 0)
            {
                throw NativeMethods.LastWin32Error("GetModuleHandle");
            }

            _mouseHook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WhMouseLl,
                _mouseCallback,
                moduleHandle,
                0);

            if (_mouseHook == 0)
            {
                throw NativeMethods.LastWin32Error("SetWindowsHookEx(WH_MOUSE_LL)");
            }

            if (_options.KeyboardLogging != KeyboardLoggingMode.None)
            {
                _keyboardHook = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WhKeyboardLl,
                    _keyboardCallback,
                    moduleHandle,
                    0);

                if (_keyboardHook == 0)
                {
                    throw NativeMethods.LastWin32Error("SetWindowsHookEx(WH_KEYBOARD_LL)");
                }
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
            // Keep the effect channel alive until the processor drains the
            // ordered release generated by a pending synthetic drag.
            _middleChord?.Cancel();

            if (_keyboardHook != 0)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = 0;
            }

            if (_mouseHook != 0)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = 0;
            }

            _ready.Set();
        }
    }

    private void UnhookFromShutdownFallback()
    {
        var keyboardHook = Interlocked.Exchange(ref _keyboardHook, 0);
        if (keyboardHook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(keyboardHook);
        }

        var mouseHook = Interlocked.Exchange(ref _mouseHook, 0);
        if (mouseHook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(mouseHook);
        }
    }

    private nint MouseCallback(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= NativeMethods.HcAction &&
                (uint)wParam is NativeMethods.WmXButtonDown or NativeMethods.WmXButtonUp)
            {
                var hookData = Marshal.PtrToStructure<NativeMethods.MsllHookStruct>(lParam);
                var xButton = (ushort)(hookData.MouseData >> 16);
                var button = xButton switch
                {
                    0x0001 => XButtonId.XButton1,
                    0x0002 => XButtonId.XButton2,
                    _ => (XButtonId?)null
                };

                if (button is not null)
                {
                    var isInjected = (hookData.Flags & NativeMethods.LlmhfInjected) != 0;
                    if (!isInjected || _options.IncludeInjected)
                    {
                        _events.TryWrite(new ProbeEvent(
                            ProbeEventKind.XButton,
                            (uint)wParam == NativeMethods.WmXButtonDown,
                            button,
                            0,
                            0,
                            isInjected,
                            hookData.Time));
                    }
                }
            }

            if (_middleChord is not null && code >= NativeMethods.HcAction)
            {
                var hookData = Marshal.PtrToStructure<NativeMethods.MsllHookStruct>(lParam);
                var isInjected = (hookData.Flags & NativeMethods.LlmhfInjected) != 0;
                if (!isInjected)
                {
                    var decision = TryGetMiddleChordEvent(
                        (uint)wParam,
                        hookData.Point.X,
                        hookData.Point.Y,
                        out var mouseButtonEvent)
                        ? _middleChord.Handle(mouseButtonEvent)
                        : (uint)wParam == NativeMethods.WmMouseMove
                            ? _middleChord.HandleMove(new MouseMoveEvent(hookData.Point.X, hookData.Point.Y))
                            : InputHandlingDecision.PassThrough();
                    if (decision.SuppressOriginal)
                    {
                        return 1;
                    }
                }
            }
        }
        catch
        {
            // A probe must never break the global hook chain because of a logging
            // or marshaling failure.
        }

        // This is the defining first-version rule: observe, then always pass on.
        return NativeMethods.CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private static bool TryGetMiddleChordEvent(
        uint message,
        int x,
        int y,
        out MouseButtonEvent mouseButtonEvent)
    {
        switch (message)
        {
            case NativeMethods.WmLButtonDown:
                mouseButtonEvent = new MouseButtonEvent(MouseButtonId.Left, isDown: true, x, y);
                return true;
            case NativeMethods.WmLButtonUp:
                mouseButtonEvent = new MouseButtonEvent(MouseButtonId.Left, isDown: false, x, y);
                return true;
            case NativeMethods.WmRButtonDown:
                mouseButtonEvent = new MouseButtonEvent(MouseButtonId.Right, isDown: true, x, y);
                return true;
            case NativeMethods.WmRButtonUp:
                mouseButtonEvent = new MouseButtonEvent(MouseButtonId.Right, isDown: false, x, y);
                return true;
            case NativeMethods.WmMButtonDown:
                mouseButtonEvent = new MouseButtonEvent(MouseButtonId.Middle, isDown: true, x, y);
                return true;
            case NativeMethods.WmMButtonUp:
                mouseButtonEvent = new MouseButtonEvent(MouseButtonId.Middle, isDown: false, x, y);
                return true;
            default:
                mouseButtonEvent = default;
                return false;
        }
    }

    private nint KeyboardCallback(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= NativeMethods.HcAction &&
                (uint)wParam is NativeMethods.WmKeyDown or NativeMethods.WmKeyUp or
                NativeMethods.WmSysKeyDown or NativeMethods.WmSysKeyUp)
            {
                var hookData = Marshal.PtrToStructure<NativeMethods.KbdllHookStruct>(lParam);
                var isDown = (uint)wParam is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
                var shouldLog = _options.KeyboardLogging == KeyboardLoggingMode.All ||
                    hookData.VirtualKeyCode is 0xA6 or 0xA7;

                if (shouldLog)
                {
                    var isInjected = (hookData.Flags & NativeMethods.LlkfhInjected) != 0;
                    if (!isInjected || _options.IncludeInjected)
                    {
                        _events.TryWrite(new ProbeEvent(
                            ProbeEventKind.Keyboard,
                            isDown,
                            null,
                            hookData.VirtualKeyCode,
                            hookData.ScanCode,
                            isInjected,
                            hookData.Time));
                    }
                }
            }
        }
        catch
        {
            // Keep the hook chain safe even if a diagnostic event cannot be parsed.
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }
}
