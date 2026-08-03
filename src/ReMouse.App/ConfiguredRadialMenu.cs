using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.App;

public static class ConfiguredRadialMenu
{
    public static RadialMenuLayout Create(RadialMenuSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var items = settings.Slots
            .Select(slot => new RadialMenuItem(slot.Id, slot.Label, ToAction(slot)))
            .ToArray();
        return new RadialMenuLayout(items, settings.DeadZoneRadius, settings.StartAngleDegrees);
    }

    private static RadialMenuAction ToAction(RadialMenuSlotSettings slot) => slot.ActionKind switch
    {
        ConfiguredRadialActionKind.NoOp => new RadialMenuAction.NoOp(),
        ConfiguredRadialActionKind.LaunchApplication =>
            new RadialMenuAction.LaunchApplication(slot.ExecutablePath, slot.Arguments),
        ConfiguredRadialActionKind.Shortcut =>
            new RadialMenuAction.Shortcut(ToKeySequence(slot.ShortcutVirtualKeys)),
        _ => throw new ArgumentOutOfRangeException(nameof(slot.ActionKind), slot.ActionKind, "Unknown configured radial action.")
    };

    private static InputEffect.KeySequence ToKeySequence(IReadOnlyList<ushort> keys)
    {
        var strokes = new List<InputKeyStroke>(keys.Count * 2);
        foreach (var key in keys)
        {
            strokes.Add(InputKeyStroke.Down(key));
        }

        for (var index = keys.Count - 1; index >= 0; index--)
        {
            strokes.Add(InputKeyStroke.Up(keys[index]));
        }

        return new InputEffect.KeySequence(strokes);
    }
}
