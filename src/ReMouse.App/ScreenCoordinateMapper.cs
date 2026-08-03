using System.Windows;
using System.Windows.Media;
using ReMouse.Core.Input;
using ReMouse.Windows.Displays;

namespace ReMouse.App;

/// <summary>
/// Converts low-level hook physical pixels into WPF device-independent pixels.
/// The mapper is created after the main window is loaded and snapshots the
/// physical monitor rectangles/DPI. Points are then mapped using the scale of
/// the monitor containing each point, with a visual-DPI fallback if monitor
/// enumeration is unavailable.
/// </summary>
public sealed class ScreenCoordinateMapper
{
    private readonly MonitorDpiCoordinateLayout? _monitorLayout;

    public ScreenCoordinateMapper(double scaleX, double scaleY)
    {
        if (!double.IsFinite(scaleX) || scaleX <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleX));
        }

        if (!double.IsFinite(scaleY) || scaleY <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleY));
        }

        ScaleX = scaleX;
        ScaleY = scaleY;
    }

    private ScreenCoordinateMapper(
        double scaleX,
        double scaleY,
        MonitorDpiCoordinateLayout monitorLayout)
        : this(scaleX, scaleY)
    {
        _monitorLayout = monitorLayout;
    }

    public double ScaleX { get; }

    public double ScaleY { get; }

    public ScreenPoint ToDip(int x, int y) =>
        _monitorLayout?.ToDip(x, y) ?? new ScreenPoint(x / ScaleX, y / ScaleY);

    public (double Left, double Top, double Right, double Bottom) GetVirtualDipBounds(
        int rawLeft,
        int rawTop,
        int rawRight,
        int rawBottom)
    {
        if (_monitorLayout is not null)
        {
            return _monitorLayout.GetVirtualDipBounds();
        }

        var corners = new[]
        {
            ToDip(rawLeft, rawTop),
            ToDip(rawRight, rawTop),
            ToDip(rawLeft, rawBottom),
            ToDip(rawRight, rawBottom)
        };
        return (
            corners.Min(point => point.X),
            corners.Min(point => point.Y),
            corners.Max(point => point.X),
            corners.Max(point => point.Y));
    }

    public static ScreenCoordinateMapper FromVisual(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        var dpi = VisualTreeHelper.GetDpi(visual);
        try
        {
            var monitors = WindowsMonitorDpiProvider.GetMonitors();
            if (monitors.Count > 0)
            {
                return new ScreenCoordinateMapper(
                    dpi.DpiScaleX,
                    dpi.DpiScaleY,
                    MonitorDpiCoordinateLayout.Create(monitors, dpi.DpiScaleX, dpi.DpiScaleY));
            }
        }
        catch (DllNotFoundException)
        {
            // Fall back to the visual's current monitor scale below.
        }
        catch (EntryPointNotFoundException)
        {
            // Fall back to the visual's current monitor scale below.
        }
        catch (InvalidOperationException)
        {
            // A display can disappear while the window is loading; use the
            // stable visual-DPI mapping for this startup instead.
        }

        return new ScreenCoordinateMapper(dpi.DpiScaleX, dpi.DpiScaleY);
    }
}
