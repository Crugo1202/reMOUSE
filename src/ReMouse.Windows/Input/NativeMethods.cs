using System.Runtime.InteropServices;

namespace ReMouse.Windows.Input;

internal static class NativeMethods
{
    internal const uint InputMouse = 0;
    internal const uint InputKeyboard = 1;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(
        uint numberOfInputs,
        [In] INPUT[] inputs,
        int inputSize);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        internal uint Type;
        internal InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        internal MOUSEINPUT MouseInput;

        [FieldOffset(0)]
        internal KEYBDINPUT KeyboardInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        internal int X;
        internal int Y;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }
}
