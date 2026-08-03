namespace ReMouse.Windows.Input;

/// <summary>
/// Reads physical modifier state for a low-level mouse callback. The call is
/// bounded and does not touch UI, files, or the input injection path.
/// </summary>
public static class WindowsModifierState
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;

    public static bool IsShiftDown() => IsDown(VkShift);

    public static bool IsControlDown() => IsDown(VkControl);

    private static bool IsDown(int virtualKey) =>
        (NativeMethods.GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
}
