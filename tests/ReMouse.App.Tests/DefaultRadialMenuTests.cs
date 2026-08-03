using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.App;
using ReMouse.Core.Input;
using ReMouse.Windows.Displays;

namespace ReMouse.App.Tests;

[TestClass]
public sealed class DefaultRadialMenuTests
{
    [TestMethod]
    public void DefaultMenuHasUsefulShortcutsAndSafeNoOpSlots()
    {
        var layout = DefaultRadialMenu.Create();

        Assert.AreEqual(8, layout.Items.Count);
        Assert.IsInstanceOfType<RadialMenuAction.Shortcut>(layout.Items[0].Action);
        Assert.IsInstanceOfType<RadialMenuAction.Shortcut>(layout.Items[1].Action);
        Assert.IsInstanceOfType<RadialMenuAction.Shortcut>(layout.Items[2].Action);
        Assert.IsInstanceOfType<RadialMenuAction.NoOp>(layout.Items[6].Action);
        Assert.IsInstanceOfType<RadialMenuAction.NoOp>(layout.Items[7].Action);
        Assert.AreEqual(32, layout.DeadZoneRadius);
    }

    [TestMethod]
    public void ScreenCoordinateMapperConvertsPhysicalPixelsToDip()
    {
        var mapper = new ScreenCoordinateMapper(1.5, 2);

        var point = mapper.ToDip(300, 200);

        Assert.AreEqual(200, point.X, 0.001);
        Assert.AreEqual(100, point.Y, 0.001);
    }

    [TestMethod]
    public void ScreenCoordinateMapperRejectsInvalidScales()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ScreenCoordinateMapper(0, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ScreenCoordinateMapper(1, double.NaN));
    }

    [TestMethod]
    public void MonitorLayoutUsesTheContainingMonitorScale()
    {
        var layout = MonitorDpiCoordinateLayout.Create(
            new[]
            {
                new MonitorDpiInfo(0, 0, 1920, 1080, 1, 1, IsPrimary: true),
                new MonitorDpiInfo(1920, 0, 3840, 1440, 1.5, 1.5, IsPrimary: false)
            },
            fallbackScaleX: 1,
            fallbackScaleY: 1);

        Assert.AreEqual(new ScreenPoint(960, 540), layout.ToDip(960, 540));
        Assert.AreEqual(new ScreenPoint(1920, 0), layout.ToDip(1920, 0));
        Assert.AreEqual(new ScreenPoint(2560, 480), layout.ToDip(2880, 720));

        var bounds = layout.GetVirtualDipBounds();
        Assert.AreEqual(0, bounds.Left, 0.001);
        Assert.AreEqual(3200, bounds.Right, 0.001);
        Assert.AreEqual(1080, bounds.Bottom, 0.001);
    }

    [TestMethod]
    public void MonitorLayoutSupportsASecondaryMonitorToTheLeft()
    {
        var layout = MonitorDpiCoordinateLayout.Create(
            new[]
            {
                new MonitorDpiInfo(0, 0, 1920, 1080, 1, 1, IsPrimary: true),
                new MonitorDpiInfo(-1280, 0, 0, 1024, 2, 2, IsPrimary: false)
            },
            fallbackScaleX: 1,
            fallbackScaleY: 1);

        Assert.AreEqual(-640, layout.ToDip(-1280, 0).X, 0.001);
        Assert.AreEqual(-320, layout.ToDip(-640, 512).X, 0.001);
        Assert.AreEqual(256, layout.ToDip(-640, 512).Y, 0.001);
    }

    [TestMethod]
    public void MonitorLayoutAccumulatesMixedDpiThreeMonitorChain()
    {
        var layout = MonitorDpiCoordinateLayout.Create(
            new[]
            {
                new MonitorDpiInfo(0, 0, 1920, 1080, 1, 1, IsPrimary: true),
                new MonitorDpiInfo(1920, 0, 4480, 1080, 2, 2, IsPrimary: false),
                new MonitorDpiInfo(4480, 0, 6400, 1080, 1, 1, IsPrimary: false)
            },
            fallbackScaleX: 1,
            fallbackScaleY: 1);

        // M3 begins after M2's 1280-DIP width, not after its physical width.
        Assert.AreEqual(3200, layout.ToDip(4480, 0).X, 0.001);
        Assert.AreEqual(4160, layout.ToDip(5440, 540).X, 0.001);
    }

    [TestMethod]
    public void MonitorLayoutUsesExclusiveRightAndBottomSeams()
    {
        var layout = MonitorDpiCoordinateLayout.Create(
            new[]
            {
                new MonitorDpiInfo(0, 0, 100, 100, 1, 1, IsPrimary: true),
                new MonitorDpiInfo(100, 0, 200, 100, 2, 2, IsPrimary: false)
            },
            fallbackScaleX: 1,
            fallbackScaleY: 1);

        Assert.AreEqual(100, layout.ToDip(100, 0).X, 0.001);
        Assert.AreEqual(149.5, layout.ToDip(199, 99).X, 0.001);
        Assert.AreEqual(49.5, layout.ToDip(199, 99).Y, 0.001);
    }
}
