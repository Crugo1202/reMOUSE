namespace ReMouse.Core.Input;

public readonly record struct RadialMenuHit(
    bool IsInDeadZone,
    int? SlotIndex,
    double Distance,
    double AngleDegrees)
{
    public bool HasSelection => SlotIndex is not null;
}

public static class RadialMenuHitTester
{
    public static RadialMenuHit HitTest(
        RadialMenuLayout layout,
        ScreenPoint center,
        ScreenPoint cursor)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var dx = cursor.X - center.X;
        var dy = cursor.Y - center.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var angle = NormalizeAngle(Math.Atan2(dy, dx) * (180 / Math.PI));

        if (distance <= layout.DeadZoneRadius)
        {
            return new RadialMenuHit(true, null, distance, angle);
        }

        var relativeAngle = NormalizeAngle(angle - layout.StartAngleDegrees);
        var sectorSize = 360d / layout.Items.Count;
        var slotIndex = Math.Min(layout.Items.Count - 1, (int)Math.Floor(relativeAngle / sectorSize));
        return new RadialMenuHit(false, slotIndex, distance, angle);
    }

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
