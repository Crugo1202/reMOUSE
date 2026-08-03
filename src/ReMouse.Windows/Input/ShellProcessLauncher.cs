using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReMouse.Windows.Input;

internal sealed class ShellProcessLauncher : IProcessLauncher
{
    private readonly IRunningApplicationActivator _activator;

    public ShellProcessLauncher()
        : this(new WindowsRunningApplicationActivator())
    {
    }

    internal ShellProcessLauncher(IRunningApplicationActivator activator)
    {
        _activator = activator ?? throw new ArgumentNullException(nameof(activator));
    }

    public void Start(string executablePath, string arguments)
    {
        if (_activator.TryActivate(executablePath))
        {
            return;
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            UseShellExecute = true
        });

        if (process is null)
        {
            throw new InvalidOperationException($"Windows could not start '{executablePath}'.");
        }

        process.Dispose();
    }
}

internal interface IRunningApplicationActivator
{
    bool TryActivate(string executablePath);
}

/// <summary>
/// Reuses a visible process for an application action when possible. This
/// turns radial-menu "wake app" into a focus operation instead of opening a
/// second copy every time.
/// </summary>
internal sealed class WindowsRunningApplicationActivator : IRunningApplicationActivator
{
    private const int SwRestore = 9;

    public bool TryActivate(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var targetPath = Path.GetFullPath(executablePath.Trim());
        var processName = Path.GetFileNameWithoutExtension(targetPath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle == 0 ||
                        !string.Equals(
                            process.MainModule?.FileName,
                            targetPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ShowWindow(process.MainWindowHandle, SwRestore);
                    // Windows may reject foreground activation because of its
                    // foreground-lock policy. The process is nevertheless a
                    // valid wake target; never start a duplicate just because
                    // this best-effort focus call returned false.
                    SetForegroundWindow(process.MainWindowHandle);
                    return true;
                }
                catch (Exception)
                {
                    // Access to another process can legitimately fail due to
                    // elevation or a race with its shutdown. Try the next one.
                }
            }
        }

        return false;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);
}
