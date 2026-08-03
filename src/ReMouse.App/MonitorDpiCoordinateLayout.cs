using ReMouse.Core.Input;
using ReMouse.Windows.Displays;

namespace ReMouse.App;

/// <summary>
/// Maps physical screen coordinates to a virtual DIP plane. Each monitor is
/// scaled around its own physical origin; the primary monitor anchors the
/// virtual plane. This avoids applying the main-window DPI to a secondary
/// monitor with a different scale.
/// </summary>
public sealed class MonitorDpiCoordinateLayout
{
    private readonly IReadOnlyList<Segment> _segments;

    private MonitorDpiCoordinateLayout(IReadOnlyList<Segment> segments)
    {
        _segments = segments;
    }

    public static MonitorDpiCoordinateLayout Create(
        IReadOnlyList<MonitorDpiInfo> monitors,
        double fallbackScaleX,
        double fallbackScaleY)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor is required.", nameof(monitors));
        }

        var validMonitors = monitors
            .Where(monitor => monitor.Width > 0 && monitor.Height > 0)
            .ToArray();
        if (validMonitors.Length == 0)
        {
            throw new ArgumentException("Monitor rectangles must be non-empty.", nameof(monitors));
        }

        var primary = validMonitors.FirstOrDefault(monitor => monitor.IsPrimary);
        if (primary.Width <= 0 || primary.Height <= 0)
        {
            primary = validMonitors[0];
        }

        var primaryScaleX = ValidScale(primary.ScaleX, fallbackScaleX);
        var primaryScaleY = ValidScale(primary.ScaleY, fallbackScaleY);
        var pending = validMonitors
            .Where(monitor => monitor != primary)
            .ToList();
        var segments = new List<Segment>(validMonitors.Length)
        {
            new Segment(primary, 0, 0, primaryScaleX, primaryScaleY)
        };

        while (pending.Count > 0)
        {
            var best = FindBestPlacement(pending, segments, primary, primaryScaleX, primaryScaleY);
            pending.Remove(best.Monitor);
            segments.Add(new Segment(
                best.Monitor,
                best.DipLeft,
                best.DipTop,
                ValidScale(best.Monitor.ScaleX, primaryScaleX),
                ValidScale(best.Monitor.ScaleY, primaryScaleY)));
        }

        return new MonitorDpiCoordinateLayout(segments);
    }

    public ScreenPoint ToDip(int x, int y)
    {
        var segment = FindSegment(x, y);
        return new ScreenPoint(
            segment.DipLeft + ((x - segment.Physical.Left) / segment.ScaleX),
            segment.DipTop + ((y - segment.Physical.Top) / segment.ScaleY));
    }

    public (double Left, double Top, double Right, double Bottom) GetVirtualDipBounds()
    {
        var left = _segments.Min(segment => segment.DipLeft);
        var top = _segments.Min(segment => segment.DipTop);
        var right = _segments.Max(segment => segment.DipRight);
        var bottom = _segments.Max(segment => segment.DipBottom);
        return (left, top, right, bottom);
    }

    private Segment FindSegment(int x, int y)
    {
        var containing = _segments.FirstOrDefault(segment =>
            x >= segment.Physical.Left &&
            x < segment.Physical.Right &&
            y >= segment.Physical.Top &&
            y < segment.Physical.Bottom);
        if (containing.Physical.Width > 0)
        {
            return containing;
        }

        return _segments
            .OrderBy(segment => DistanceSquared(segment.Physical, x, y))
            .First();
    }

    private static Placement FindBestPlacement(
        IReadOnlyList<MonitorDpiInfo> pending,
        IReadOnlyList<Segment> placed,
        MonitorDpiInfo primary,
        double primaryScaleX,
        double primaryScaleY)
    {
        Placement? best = null;
        foreach (var monitor in pending)
        {
            foreach (var anchor in placed)
            {
                if (TryPlaceHorizontally(monitor, anchor, out var horizontal))
                {
                    best = ChooseBetter(best, horizontal);
                }

                if (TryPlaceVertically(monitor, anchor, out var vertical))
                {
                    best = ChooseBetter(best, vertical);
                }
            }
        }

        if (best is not null)
        {
            return best.Value;
        }

        // Disconnected display rectangles are unusual but valid. Keep a
        // deterministic fallback anchored to the primary rather than failing
        // startup; connected monitors use the adjacency path above.
        var fallback = pending[0];
        return new Placement(
            fallback,
            (fallback.Left - primary.Left) / primaryScaleX,
            (fallback.Top - primary.Top) / primaryScaleY,
            double.MaxValue);
    }

    private static Placement? ChooseBetter(Placement? current, Placement candidate) =>
        current is null || candidate.Cost < current.Value.Cost
            ? candidate
            : current;

    private static bool TryPlaceHorizontally(
        MonitorDpiInfo monitor,
        Segment anchor,
        out Placement placement)
    {
        var overlapTop = Math.Max(monitor.Top, anchor.Physical.Top);
        var overlapBottom = Math.Min(monitor.Bottom, anchor.Physical.Bottom);
        if (overlapTop >= overlapBottom)
        {
            placement = default;
            return false;
        }

        var scaleX = ValidScale(monitor.ScaleX, anchor.ScaleX);
        var scaleY = ValidScale(monitor.ScaleY, anchor.ScaleY);
        if (monitor.Left >= anchor.Physical.Right)
        {
            var gap = monitor.Left - anchor.Physical.Right;
            placement = new Placement(
                monitor,
                anchor.DipRight + (gap / anchor.ScaleX),
                anchor.DipTop + ((monitor.Top - anchor.Physical.Top) / anchor.ScaleY),
                gap);
            return true;
        }

        if (monitor.Right <= anchor.Physical.Left)
        {
            var gap = anchor.Physical.Left - monitor.Right;
            placement = new Placement(
                monitor,
                anchor.DipLeft - (gap / anchor.ScaleX) - (monitor.Width / scaleX),
                anchor.DipTop + ((monitor.Top - anchor.Physical.Top) / anchor.ScaleY),
                gap);
            return true;
        }

        placement = default;
        return false;
    }

    private static bool TryPlaceVertically(
        MonitorDpiInfo monitor,
        Segment anchor,
        out Placement placement)
    {
        var overlapLeft = Math.Max(monitor.Left, anchor.Physical.Left);
        var overlapRight = Math.Min(monitor.Right, anchor.Physical.Right);
        if (overlapLeft >= overlapRight)
        {
            placement = default;
            return false;
        }

        var scaleX = ValidScale(monitor.ScaleX, anchor.ScaleX);
        var scaleY = ValidScale(monitor.ScaleY, anchor.ScaleY);
        if (monitor.Top >= anchor.Physical.Bottom)
        {
            var gap = monitor.Top - anchor.Physical.Bottom;
            placement = new Placement(
                monitor,
                anchor.DipLeft + ((monitor.Left - anchor.Physical.Left) / anchor.ScaleX),
                anchor.DipBottom + (gap / anchor.ScaleY),
                gap);
            return true;
        }

        if (monitor.Bottom <= anchor.Physical.Top)
        {
            var gap = anchor.Physical.Top - monitor.Bottom;
            placement = new Placement(
                monitor,
                anchor.DipLeft + ((monitor.Left - anchor.Physical.Left) / anchor.ScaleX),
                anchor.DipTop - (gap / anchor.ScaleY) - (monitor.Height / scaleY),
                gap);
            return true;
        }

        placement = default;
        return false;
    }

    private static double ValidScale(double candidate, double fallback) =>
        double.IsFinite(candidate) && candidate > 0
            ? candidate
            : double.IsFinite(fallback) && fallback > 0
                ? fallback
                : 1;

    private static double DistanceSquared(MonitorDpiInfo monitor, int x, int y)
    {
        var dx = x < monitor.Left ? (long)monitor.Left - x : x > monitor.Right ? (long)x - monitor.Right : 0;
        var dy = y < monitor.Top ? (long)monitor.Top - y : y > monitor.Bottom ? (long)y - monitor.Bottom : 0;
        return ((double)dx * dx) + ((double)dy * dy);
    }

    private readonly record struct Segment(
        MonitorDpiInfo Physical,
        double DipLeft,
        double DipTop,
        double ScaleX,
        double ScaleY)
    {
        public double DipRight => DipLeft + (Physical.Width / ScaleX);

        public double DipBottom => DipTop + (Physical.Height / ScaleY);
    }

    private readonly record struct Placement(
        MonitorDpiInfo Monitor,
        double DipLeft,
        double DipTop,
        double Cost);
}
