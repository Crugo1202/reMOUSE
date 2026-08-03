using ReMouse.Core.Input;

namespace ReMouse.InputProbe;

internal enum ProbeEventKind
{
    XButton,
    Keyboard
}

internal readonly record struct ProbeEvent(
    ProbeEventKind Kind,
    bool IsDown,
    XButtonId? XButton,
    uint VirtualKey,
    uint ScanCode,
    bool IsInjected,
    uint NativeTimestamp);
