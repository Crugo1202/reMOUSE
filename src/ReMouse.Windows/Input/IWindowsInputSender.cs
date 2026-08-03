namespace ReMouse.Windows.Input;

internal interface IWindowsInputSender
{
    void Send(IReadOnlyList<WindowsInputPacket> packets);
}
