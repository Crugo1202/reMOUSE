using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.App;
using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.App.Tests;

[TestClass]
public sealed class ConfiguredRadialMenuTests
{
    [TestMethod]
    public void ConfiguredShortcutAndApplicationActionsBecomeRuntimeActions()
    {
        var settings = new RadialMenuSettings(
            new[]
            {
                new RadialMenuSlotSettings(
                    "copy",
                    "Copy",
                    ConfiguredRadialActionKind.Shortcut,
                    new ushort[] { 0x11, 0x43 }),
                new RadialMenuSlotSettings(
                    "tool",
                    "Tool",
                    ConfiguredRadialActionKind.LaunchApplication,
                    executablePath: "C:\\Tools\\tool.exe",
                    arguments: "--safe")
            },
            deadZoneRadius: 20,
            startAngleDegrees: 10);

        var layout = ConfiguredRadialMenu.Create(settings);

        Assert.AreEqual(2, layout.Items.Count);
        Assert.AreEqual(20, layout.DeadZoneRadius);
        Assert.AreEqual(10, layout.StartAngleDegrees);
        Assert.IsInstanceOfType<RadialMenuAction.Shortcut>(layout.Items[0].Action);
        var launch = (RadialMenuAction.LaunchApplication)layout.Items[1].Action;
        Assert.AreEqual("C:\\Tools\\tool.exe", launch.ExecutablePath);
        Assert.AreEqual("--safe", launch.Arguments);
    }
}
