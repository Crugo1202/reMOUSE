namespace ReMouse.Windows.Input;

internal enum WindowsInputPacketKind
{
    Mouse,
    Keyboard
}

internal static class WindowsInputFlags
{
    internal const uint MouseMove = 0x0001;
    internal const uint MouseMiddleDown = 0x0020;
    internal const uint MouseMiddleUp = 0x0040;
    internal const uint MouseAbsolute = 0x8000;
    internal const uint MouseVirtualDesk = 0x4000;
    internal const uint MouseHorizontalWheel = 0x01000;
    internal const uint KeyboardKeyUp = 0x0002;
}

internal readonly record struct WindowsInputPacket(
    WindowsInputPacketKind Kind,
    int MouseData,
    uint MouseFlags,
    ushort VirtualKey,
    uint KeyboardFlags,
    int MouseX,
    int MouseY)
{
    internal static WindowsInputPacket HorizontalWheel(int delta) =>
        new(
            WindowsInputPacketKind.Mouse,
            delta,
            WindowsInputFlags.MouseHorizontalWheel,
            0,
            0,
            0,
            0);

    internal static WindowsInputPacket MiddleButton(bool isDown) =>
        new(
            WindowsInputPacketKind.Mouse,
            0,
            isDown ? WindowsInputFlags.MouseMiddleDown : WindowsInputFlags.MouseMiddleUp,
            0,
            0,
            0,
            0);

    internal static WindowsInputPacket MouseMove(int x, int y) =>
        new(
            WindowsInputPacketKind.Mouse,
            0,
            WindowsInputFlags.MouseMove |
                WindowsInputFlags.MouseAbsolute |
                WindowsInputFlags.MouseVirtualDesk,
            0,
            0,
            x,
            y);

    internal static WindowsInputPacket Key(ushort virtualKey, bool isDown) =>
        new(
            WindowsInputPacketKind.Keyboard,
            0,
            0,
            virtualKey,
            isDown ? 0 : WindowsInputFlags.KeyboardKeyUp,
            0,
            0);
}
