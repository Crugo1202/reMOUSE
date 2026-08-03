using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;

namespace ReMouse.Core.Tests;

[TestClass]
public sealed class RadialMenuTests
{
    private static readonly RadialMenuLayout FourWayLayout = new(
        new[]
        {
            new RadialMenuItem("top", "Top"),
            new RadialMenuItem("right", "Right"),
            new RadialMenuItem("bottom", "Bottom"),
            new RadialMenuItem("left", "Left")
        },
        deadZoneRadius: 20);

    [TestMethod]
    public void CardinalDirectionsSelectClockwiseSlotsFromTop()
    {
        var center = new ScreenPoint(100, 100);

        Assert.AreEqual("top", SelectId(center, new ScreenPoint(100, 50)));
        Assert.AreEqual("right", SelectId(center, new ScreenPoint(150, 100)));
        Assert.AreEqual("bottom", SelectId(center, new ScreenPoint(100, 150)));
        Assert.AreEqual("left", SelectId(center, new ScreenPoint(50, 100)));
    }

    [TestMethod]
    public void DeadZoneReturnsNoSelection()
    {
        var hit = RadialMenuHitTester.HitTest(
            FourWayLayout,
            new ScreenPoint(100, 100),
            new ScreenPoint(115, 100));

        Assert.IsTrue(hit.IsInDeadZone);
        Assert.IsFalse(hit.HasSelection);
        Assert.IsNull(hit.SlotIndex);
    }

    [TestMethod]
    public void SectorBoundaryUsesTheFollowingClockwiseSlot()
    {
        var center = new ScreenPoint(0, 0);
        var hit = RadialMenuHitTester.HitTest(FourWayLayout, center, new ScreenPoint(0, 50));

        Assert.AreEqual(2, hit.SlotIndex);
    }

    [TestMethod]
    public void StartAngleCanRotateTheMenu()
    {
        var layout = new RadialMenuLayout(
            FourWayLayout.Items,
            deadZoneRadius: 20,
            startAngleDegrees: 0);
        var hit = RadialMenuHitTester.HitTest(layout, new ScreenPoint(0, 0), new ScreenPoint(50, 0));

        Assert.AreEqual(0, hit.SlotIndex);
    }

    [TestMethod]
    public void SessionCommitsSelectionAndCloses()
    {
        var session = new RadialMenuSession(FourWayLayout);
        session.Open(new ScreenPoint(100, 100));
        var preview = session.Update(new ScreenPoint(150, 100));
        var selected = session.Commit(new ScreenPoint(150, 100));

        Assert.IsTrue(preview.HasSelection);
        Assert.AreEqual("right", selected!.Id);
        Assert.IsFalse(session.IsOpen);
    }

    [TestMethod]
    public void ReleasingInDeadZoneCommitsNoAction()
    {
        var session = new RadialMenuSession(FourWayLayout);
        session.Open(new ScreenPoint(100, 100));

        var selected = session.Commit(new ScreenPoint(105, 105));

        Assert.IsNull(selected);
        Assert.IsFalse(session.IsOpen);
    }

    [TestMethod]
    public void CancelClosesWithoutSelection()
    {
        var session = new RadialMenuSession(FourWayLayout);
        session.Open(new ScreenPoint(0, 0));
        session.Cancel();

        Assert.IsFalse(session.IsOpen);
        Assert.ThrowsException<InvalidOperationException>(() => session.Update(new ScreenPoint(1, 1)));
    }

    [TestMethod]
    public void LayoutCopiesItemsAndRejectsInvalidDefinitions()
    {
        var items = new List<RadialMenuItem> { new("one", "One") };
        var layout = new RadialMenuLayout(items);
        items.Clear();

        Assert.AreEqual(1, layout.Items.Count);
        Assert.ThrowsException<ArgumentException>(() => new RadialMenuLayout(Array.Empty<RadialMenuItem>()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new RadialMenuLayout(layout.Items, 0));
        Assert.ThrowsException<ArgumentException>(() => new RadialMenuLayout(new[]
        {
            new RadialMenuItem("same", "A"),
            new RadialMenuItem("same", "B")
        }));
    }

    [TestMethod]
    public void ItemAndPointInputsRejectInvalidValues()
    {
        Assert.ThrowsException<ArgumentException>(() => new RadialMenuItem("", "Label"));
        Assert.ThrowsException<ArgumentException>(() => new RadialMenuItem("id", ""));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ScreenPoint(double.NaN, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ScreenPoint(0, double.PositiveInfinity));
    }

    [TestMethod]
    public void RadialItemDefaultsToNoOpAndCanCarryAnApplicationAction()
    {
        var noOp = new RadialMenuItem("noop", "No-op");
        var application = new RadialMenuAction.LaunchApplication("C:\\Tools\\Design.exe", "--safe");
        var item = new RadialMenuItem("design", "Design", application);

        Assert.IsInstanceOfType<RadialMenuAction.NoOp>(noOp.Action);
        Assert.AreSame(application, item.Action);
        Assert.AreEqual("C:\\Tools\\Design.exe", application.ExecutablePath);
        Assert.AreEqual("--safe", application.Arguments);
    }

    [TestMethod]
    public void ActionsRejectMissingRequiredData()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new RadialMenuAction.LaunchApplication(" "));
        Assert.ThrowsException<ArgumentNullException>(
            () => new RadialMenuAction.Shortcut(null!));
        Assert.ThrowsException<ArgumentNullException>(
            () => new RadialMenuItem("id", "Label", null!));
    }

    private static string? SelectId(ScreenPoint center, ScreenPoint cursor)
    {
        var hit = RadialMenuHitTester.HitTest(FourWayLayout, center, cursor);
        return hit.SlotIndex is { } index ? FourWayLayout.Items[index].Id : null;
    }
}
