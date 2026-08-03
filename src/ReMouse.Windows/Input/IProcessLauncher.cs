namespace ReMouse.Windows.Input;

internal interface IProcessLauncher
{
    void Start(string executablePath, string arguments);
}
