using ReMouse.Core.Input;
using System.Collections.ObjectModel;

namespace ReMouse.Core.Settings;

public enum ConfiguredRadialActionKind
{
    NoOp,
    Shortcut,
    LaunchApplication
}

public sealed class RadialMenuSlotSettings : IEquatable<RadialMenuSlotSettings>
{
    public RadialMenuSlotSettings(
        string id,
        string label,
        ConfiguredRadialActionKind actionKind,
        IReadOnlyList<ushort>? shortcutVirtualKeys = null,
        string? executablePath = null,
        string? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A radial slot needs a non-empty id.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("A radial slot needs a non-empty label.", nameof(label));
        }

        if (!Enum.IsDefined(actionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(actionKind), actionKind, "Unknown radial action kind.");
        }

        var keys = shortcutVirtualKeys?.ToArray() ?? Array.Empty<ushort>();
        if (keys.Any(key => key == 0) || keys.Length > 16)
        {
            throw new ArgumentException("Shortcut keys must be non-zero and contain at most 16 keys.", nameof(shortcutVirtualKeys));
        }

        var trimmedPath = executablePath?.Trim() ?? string.Empty;
        if (actionKind == ConfiguredRadialActionKind.Shortcut && keys.Length == 0)
        {
            throw new ArgumentException("A shortcut slot needs at least one virtual key.", nameof(shortcutVirtualKeys));
        }

        if (actionKind == ConfiguredRadialActionKind.LaunchApplication && string.IsNullOrWhiteSpace(trimmedPath))
        {
            throw new ArgumentException("An application slot needs an executable path.", nameof(executablePath));
        }

        Id = id.Trim();
        Label = label.Trim();
        ActionKind = actionKind;
        ShortcutVirtualKeys = new ReadOnlyCollection<ushort>(keys);
        ExecutablePath = trimmedPath;
        Arguments = arguments?.Trim() ?? string.Empty;
    }

    public string Id { get; }

    public string Label { get; }

    public ConfiguredRadialActionKind ActionKind { get; }

    public IReadOnlyList<ushort> ShortcutVirtualKeys { get; }

    public string ExecutablePath { get; }

    public string Arguments { get; }

    public bool Equals(RadialMenuSlotSettings? other)
    {
        return other is not null &&
               string.Equals(Id, other.Id, StringComparison.Ordinal) &&
               string.Equals(Label, other.Label, StringComparison.Ordinal) &&
               ActionKind == other.ActionKind &&
               ShortcutVirtualKeys.SequenceEqual(other.ShortcutVirtualKeys) &&
               string.Equals(ExecutablePath, other.ExecutablePath, StringComparison.Ordinal) &&
               string.Equals(Arguments, other.Arguments, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as RadialMenuSlotSettings);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id, StringComparer.Ordinal);
        hash.Add(Label, StringComparer.Ordinal);
        hash.Add(ActionKind);
        foreach (var key in ShortcutVirtualKeys)
        {
            hash.Add(key);
        }

        hash.Add(ExecutablePath, StringComparer.Ordinal);
        hash.Add(Arguments, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}

public sealed class RadialMenuSettings : IEquatable<RadialMenuSettings>
{
    public RadialMenuSettings(
        IReadOnlyList<RadialMenuSlotSettings> slots,
        int deadZoneRadius = 32,
        double startAngleDegrees = -90)
    {
        if (slots is null)
        {
            throw new ArgumentNullException(nameof(slots));
        }

        var snapshot = slots.ToArray();
        if (snapshot.Length is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(slots), "A radial menu needs between 1 and 12 slots.");
        }

        if (snapshot.Any(slot => slot is null))
        {
            throw new ArgumentException("Radial slots cannot be null.", nameof(slots));
        }

        if (deadZoneRadius is < 0 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(deadZoneRadius), deadZoneRadius, "Dead-zone radius is out of range.");
        }

        if (!double.IsFinite(startAngleDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(startAngleDegrees), startAngleDegrees, "Start angle must be finite.");
        }

        if (snapshot.Select(slot => slot.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Length)
        {
            throw new ArgumentException("Radial slot ids must be unique.", nameof(slots));
        }

        Slots = new ReadOnlyCollection<RadialMenuSlotSettings>(snapshot);
        DeadZoneRadius = deadZoneRadius;
        StartAngleDegrees = startAngleDegrees;
    }

    public IReadOnlyList<RadialMenuSlotSettings> Slots { get; }

    public int DeadZoneRadius { get; }

    public double StartAngleDegrees { get; }

    public bool Equals(RadialMenuSettings? other)
    {
        return other is not null &&
               DeadZoneRadius == other.DeadZoneRadius &&
               StartAngleDegrees.Equals(other.StartAngleDegrees) &&
               Slots.SequenceEqual(other.Slots);
    }

    public override bool Equals(object? obj) => Equals(obj as RadialMenuSettings);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(DeadZoneRadius);
        hash.Add(StartAngleDegrees);
        foreach (var slot in Slots)
        {
            hash.Add(slot);
        }

        return hash.ToHashCode();
    }
}
