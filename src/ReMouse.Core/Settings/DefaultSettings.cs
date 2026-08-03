using ReMouse.Core.Input;

namespace ReMouse.Core.Settings;

public static class DefaultSettings
{
    public const int CurrentSchemaVersion = 1;

    public static SideButtonBindings SideButtonBindings { get; } = new(
        XButton1: SideButtonAction.PixelInspector,
        XButton2: SideButtonAction.RadialMenu);

    public static FlickSettings Flick { get; } = new(120);

    public static RadialMenuSettings RadialMenu { get; } = new(
        new[]
        {
            new RadialMenuSlotSettings("copy", "Copy", ConfiguredRadialActionKind.Shortcut, new ushort[] { 0x11, 0x43 }),
            new RadialMenuSlotSettings("paste", "Paste", ConfiguredRadialActionKind.Shortcut, new ushort[] { 0x11, 0x56 }),
            new RadialMenuSlotSettings("plain-paste", "Plain\nPaste", ConfiguredRadialActionKind.Shortcut, new ushort[] { 0x11, 0x10, 0x56 }),
            new RadialMenuSlotSettings("undo", "Undo", ConfiguredRadialActionKind.Shortcut, new ushort[] { 0x11, 0x5A }),
            new RadialMenuSlotSettings("redo", "Redo", ConfiguredRadialActionKind.Shortcut, new ushort[] { 0x11, 0x59 }),
            new RadialMenuSlotSettings("cut", "Cut", ConfiguredRadialActionKind.Shortcut, new ushort[] { 0x11, 0x58 }),
            new RadialMenuSlotSettings("app", "App", ConfiguredRadialActionKind.NoOp),
            new RadialMenuSlotSettings("noop", "No-op", ConfiguredRadialActionKind.NoOp)
        });
}
