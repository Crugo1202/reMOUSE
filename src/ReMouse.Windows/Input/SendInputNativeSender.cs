using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ReMouse.Windows.Input;

internal sealed class SendInputNativeSender : IWindowsInputSender
{
    public void Send(IReadOnlyList<WindowsInputPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        if (packets.Count == 0)
        {
            throw new ArgumentException("At least one input packet is required.", nameof(packets));
        }

        var nativeInputs = new NativeMethods.INPUT[packets.Count];
        for (var index = 0; index < packets.Count; index++)
        {
            nativeInputs[index] = NativeInputConverter.ToNative(packets[index]);
        }

        var sent = NativeMethods.SendInput(
            (uint)nativeInputs.Length,
            nativeInputs,
            Marshal.SizeOf<NativeMethods.INPUT>());

        if (sent == nativeInputs.Length)
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error != 0)
        {
            throw new Win32Exception(
                error,
                $"SendInput accepted {sent} of {nativeInputs.Length} input packets.");
        }

        throw new InvalidOperationException(
            $"SendInput accepted {sent} of {nativeInputs.Length} input packets.");
    }
}

internal static class NativeInputConverter
{
    internal static NativeMethods.INPUT ToNative(WindowsInputPacket packet)
    {
        return packet.Kind switch
        {
            WindowsInputPacketKind.Mouse => new NativeMethods.INPUT
            {
                Type = NativeMethods.InputMouse,
                Union = new NativeMethods.InputUnion
                {
                        MouseInput = new NativeMethods.MOUSEINPUT
                        {
                            X = (packet.MouseFlags & WindowsInputFlags.MouseAbsolute) != 0
                                ? NativeInputConverter.NormalizeVirtualScreenX(packet.MouseX)
                                : packet.MouseX,
                            Y = (packet.MouseFlags & WindowsInputFlags.MouseAbsolute) != 0
                                ? NativeInputConverter.NormalizeVirtualScreenY(packet.MouseY)
                                : packet.MouseY,
                            MouseData = unchecked((uint)packet.MouseData),
                            Flags = packet.MouseFlags
                        }
                }
            },
            WindowsInputPacketKind.Keyboard => new NativeMethods.INPUT
            {
                Type = NativeMethods.InputKeyboard,
                Union = new NativeMethods.InputUnion
                {
                    KeyboardInput = new NativeMethods.KEYBDINPUT
                    {
                        VirtualKey = packet.VirtualKey,
                        Flags = packet.KeyboardFlags
                    }
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(packet), packet, "Unknown input packet kind.")
        };
    }

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    internal static int NormalizeVirtualScreenX(int x) =>
        NormalizeVirtualScreenCoordinate(
            x,
            NativeMethods.GetSystemMetrics(SmXVirtualScreen),
            NativeMethods.GetSystemMetrics(SmCxVirtualScreen));

    internal static int NormalizeVirtualScreenY(int y) =>
        NormalizeVirtualScreenCoordinate(
            y,
            NativeMethods.GetSystemMetrics(SmYVirtualScreen),
            NativeMethods.GetSystemMetrics(SmCyVirtualScreen));

    private static int NormalizeVirtualScreenCoordinate(int coordinate, int origin, int size)
    {
        if (size <= 1)
        {
            return 0;
        }

        var clamped = Math.Clamp(coordinate, origin, origin + size - 1);
        return (int)Math.Round(
            (double)(clamped - origin) * ushort.MaxValue / (size - 1),
            MidpointRounding.AwayFromZero);
    }
}
