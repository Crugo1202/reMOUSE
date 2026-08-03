using ReMouse.Core.Input;

namespace ReMouse.Core.Settings;

public sealed record ReMouseSettings
{
    public ReMouseSettings(int schemaVersion, SideButtonBindings sideButtonBindings)
        : this(schemaVersion, sideButtonBindings, DefaultSettings.Flick, DefaultSettings.RadialMenu)
    {
    }

    public ReMouseSettings(
        int schemaVersion,
        SideButtonBindings sideButtonBindings,
        FlickSettings flick,
        RadialMenuSettings radialMenu)
    {
        SchemaVersion = schemaVersion;
        SideButtonBindings = sideButtonBindings ?? throw new ArgumentNullException(nameof(sideButtonBindings));
        Flick = flick ?? throw new ArgumentNullException(nameof(flick));
        RadialMenu = radialMenu ?? throw new ArgumentNullException(nameof(radialMenu));
    }

    public int SchemaVersion { get; }

    public SideButtonBindings SideButtonBindings { get; }

    public FlickSettings Flick { get; }

    public RadialMenuSettings RadialMenu { get; }

    public static ReMouseSettings CreateDefault() => new(
        DefaultSettings.CurrentSchemaVersion,
        DefaultSettings.SideButtonBindings,
        DefaultSettings.Flick,
        DefaultSettings.RadialMenu);

    public ReMouseSettings Normalize()
    {
        if (SchemaVersion != DefaultSettings.CurrentSchemaVersion)
        {
            return CreateDefault();
        }

        if (SideButtonBindings is null || Flick is null || RadialMenu is null)
        {
            return CreateDefault();
        }

        try
        {
            SideButtonBindings.Validate();
            Flick.Validate();
            _ = new RadialMenuSettings(
                RadialMenu.Slots,
                RadialMenu.DeadZoneRadius,
                RadialMenu.StartAngleDegrees);
            return this;
        }
        catch (ArgumentException)
        {
            return CreateDefault();
        }
    }
}
