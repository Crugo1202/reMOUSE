using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;

namespace ReMouse.Windows.Input;

public enum WindowsApplicationSource
{
    Unknown,
    Running,
    StartMenu
}

public sealed record WindowsApplicationCandidate(
    string DisplayName,
    string ExecutablePath,
    string Arguments = "",
    WindowsApplicationSource Source = WindowsApplicationSource.Unknown)
{
    // Keep the original two-argument construction/deconstruction surface so
    // callers compiled against the first catalog version remain compatible.
    public WindowsApplicationCandidate(string displayName, string executablePath)
        : this(displayName, executablePath, string.Empty, WindowsApplicationSource.Unknown)
    {
    }

    public void Deconstruct(out string displayName, out string executablePath)
    {
        displayName = DisplayName;
        executablePath = ExecutablePath;
    }

    public string SourceLabel => Source switch
    {
        WindowsApplicationSource.Running => "running",
        WindowsApplicationSource.StartMenu => "Start Menu",
        _ => "application"
    };

    public string DisplayText => $"{DisplayName} ({SourceLabel})";
}

internal sealed record WindowsShortcutTarget(string ExecutablePath, string Arguments);

/// <summary>
/// Provides a lightweight, permission-safe catalog of desktop applications.
/// Running apps are read from process metadata and installed apps are read
/// only from the two Windows Start Menu folders. It never scans the whole disk.
/// </summary>
public static class WindowsApplicationCatalog
{
    public static IReadOnlyList<WindowsApplicationCandidate> GetRunningApplications()
    {
        var candidates = new Dictionary<string, WindowsApplicationCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle == 0 || process.MainModule?.FileName is not { } path)
                    {
                        continue;
                    }

                    var displayName = GetDisplayName(path);
                    candidates.TryAdd(
                        path,
                        new WindowsApplicationCandidate(
                            displayName,
                            path,
                            Source: WindowsApplicationSource.Running));
                }
                catch (Exception)
                {
                    // Elevated/system processes can deny MainModule access;
                    // skip them instead of breaking the picker.
                }
            }
        }

        return candidates.Values
            .OrderBy(candidate => candidate.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Lists executable targets exposed by the per-user and all-users Start
    /// Menu. This gives the user a picker before falling back to Browse...
    /// instead of requiring them to hunt for an exe in Explorer.
    /// </summary>
    public static IReadOnlyList<WindowsApplicationCandidate> GetStartMenuApplications()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        return GetStartMenuApplications(roots, TryResolveShortcut);
    }

    internal static IReadOnlyList<WindowsApplicationCandidate> GetStartMenuApplications(
        IEnumerable<string> roots,
        Func<string, WindowsShortcutTarget?> resolver)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(resolver);

        var candidates = new Dictionary<string, WindowsApplicationCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            IEnumerable<string> shortcutPaths;
            try
            {
                shortcutPaths = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories);
            }
            catch (Exception exception) when (IsCatalogFailure(exception))
            {
                continue;
            }

            try
            {
                foreach (var shortcutPath in shortcutPaths)
                {
                    WindowsShortcutTarget? target;
                    try
                    {
                        target = resolver(shortcutPath);
                    }
                    catch (Exception exception) when (IsCatalogFailure(exception))
                    {
                        continue;
                    }

                    if (target is null || !TryNormalizeExecutable(target.ExecutablePath, out var executablePath))
                    {
                        continue;
                    }

                    var arguments = target.Arguments?.Trim() ?? string.Empty;
                    var key = executablePath + "\0" + arguments;
                    var displayName = Path.GetFileNameWithoutExtension(shortcutPath);
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = GetDisplayName(executablePath);
                    }

                    candidates.TryAdd(
                        key,
                        new WindowsApplicationCandidate(
                            displayName.Trim(),
                            executablePath,
                            arguments,
                            WindowsApplicationSource.StartMenu));
                }
            }
            catch (Exception exception) when (IsCatalogFailure(exception))
            {
                // A protected or concurrently removed Start Menu subtree is
                // simply omitted; the picker remains useful with other roots.
            }
        }

        return candidates.Values
            .OrderBy(candidate => candidate.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Arguments, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetDisplayName(string executablePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(executablePath);
            var product = info.ProductName;
            if (!string.IsNullOrWhiteSpace(product))
            {
                return product.Trim();
            }

            var description = info.FileDescription;
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description.Trim();
            }
        }
        catch
        {
            // Fall through to a stable filename.
        }

        return Path.GetFileNameWithoutExtension(executablePath);
    }

    private static WindowsShortcutTarget? TryResolveShortcut(string shortcutPath)
    {
        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null || Activator.CreateInstance(shellType) is not { } shellObject)
            {
                return null;
            }

            shell = shellObject;
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: new object[] { shortcutPath });
            if (shortcut is null)
            {
                return null;
            }

            var shortcutType = shortcut.GetType();
            var target = shortcutType.InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                binder: null,
                target: shortcut,
                args: null) as string;
            var arguments = shortcutType.InvokeMember(
                "Arguments",
                BindingFlags.GetProperty,
                binder: null,
                target: shortcut,
                args: null) as string;
            return string.IsNullOrWhiteSpace(target)
                ? null
                : new WindowsShortcutTarget(target, arguments ?? string.Empty);
        }
        catch (Exception exception) when (IsCatalogFailure(exception))
        {
            return null;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static bool TryNormalizeExecutable(string path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path) ||
            !string.Equals(Path.GetExtension(path.Trim()), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            normalized = Path.GetFullPath(path.Trim());
            return File.Exists(normalized);
        }
        catch (Exception exception) when (IsCatalogFailure(exception))
        {
            return false;
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try
            {
                Marshal.FinalReleaseComObject(value);
            }
            catch (InvalidComObjectException)
            {
                // The COM server may have already released the wrapper.
            }
        }
    }

    private static bool IsCatalogFailure(Exception exception) =>
        exception is IOException ||
        exception is UnauthorizedAccessException ||
        exception is SecurityException ||
        exception is InvalidOperationException ||
        exception is ArgumentException ||
        exception is COMException ||
        exception is TargetInvocationException;
}
