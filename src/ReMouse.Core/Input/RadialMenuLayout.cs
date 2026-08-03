namespace ReMouse.Core.Input;

public sealed class RadialMenuLayout
{
    public RadialMenuLayout(
        IReadOnlyList<RadialMenuItem> items,
        double deadZoneRadius = 28,
        double startAngleDegrees = -90)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("A radial menu needs at least one item.", nameof(items));
        }

        if (!double.IsFinite(deadZoneRadius) || deadZoneRadius <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deadZoneRadius),
                "The radial menu dead-zone radius must be finite and positive.");
        }

        if (!double.IsFinite(startAngleDegrees))
        {
            throw new ArgumentOutOfRangeException(
                nameof(startAngleDegrees),
                "The radial menu start angle must be finite.");
        }

        var snapshot = items.ToArray();
        if (snapshot.Any(item => item is null))
        {
            throw new ArgumentException("A radial menu cannot contain a null item.", nameof(items));
        }

        if (snapshot.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Radial menu item ids must be unique.", nameof(items));
        }

        Items = Array.AsReadOnly(snapshot);
        DeadZoneRadius = deadZoneRadius;
        StartAngleDegrees = NormalizeAngle(startAngleDegrees);
    }

    public IReadOnlyList<RadialMenuItem> Items { get; }

    public double DeadZoneRadius { get; }

    /// <summary>Zero degrees points right; positive angles follow screen-clockwise direction.</summary>
    public double StartAngleDegrees { get; }

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
