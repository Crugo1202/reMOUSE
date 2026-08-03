using System.Runtime.InteropServices;

namespace ReMouse.Windows.Displays;

/// <summary>
/// Reads physical virtual-desktop rectangles and effective DPI for each
/// monitor. If a DPI query is unavailable, that monitor safely falls back to
/// 96 DPI so overlays can still start.
/// </summary>
public static class WindowsMonitorDpiProvider
{
    private const uint MonitorInfoPrimary = 1;
    private const int DpiTypeEffective = 0;

    public static IReadOnlyList<MonitorDpiInfo> GetMonitors()
    {
        var monitors = new List<MonitorDpiInfo>();
        var handle = GCHandle.Alloc(monitors);
        try
        {
            EnumDisplayMonitors(
                0,
                0,
                CollectMonitor,
                GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return monitors;
    }

    private static bool CollectMonitor(
        nint monitor,
        nint hdc,
        ref NativeRect clip,
        nint data)
    {
        var monitors = (List<MonitorDpiInfo>)GCHandle.FromIntPtr(data).Target!;
        var info = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return true;
        }

        var scaleX = 1d;
        var scaleY = 1d;
        try
        {
            if (GetDpiForMonitor(monitor, DpiTypeEffective, out var dpiX, out var dpiY) == 0)
            {
                scaleX = Math.Max(0.01, dpiX / 96d);
                scaleY = Math.Max(0.01, dpiY / 96d);
            }
        }
        catch (DllNotFoundException)
        {
            // Keep the 96-DPI fallback on restricted compatibility hosts.
        }
        catch (EntryPointNotFoundException)
        {
            // Same fallback for older Windows compatibility layers.
        }

        monitors.Add(new MonitorDpiInfo(
            info.Monitor.Left,
            info.Monitor.Top,
            info.Monitor.Right,
            info.Monitor.Bottom,
            scaleX,
            scaleY,
            (info.Flags & MonitorInfoPrimary) != 0));
        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool MonitorEnumProc(nint monitor, nint hdc, ref NativeRect clip, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        internal int Size;
        internal NativeRect Monitor;
        internal NativeRect Work;
        internal uint Flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint hdc,
        nint clip,
        MonitorEnumProc callback,
        nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);
}
