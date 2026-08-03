using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.App;
using ReMouse.Core.Input;
using ReMouse.Windows.Hooks;

namespace ReMouse.App.Tests;

[TestClass]
public sealed class RadialMenuInputBridgeTests
{
    [TestMethod]
    public async Task PhysicalUpperSideDownAndUpAreSuppressedAndQueued()
    {
        using var bridge = new RadialMenuInputBridge(CreateLayout());

        var down = bridge.Handle(Button(GlobalMouseButton.XButton2, isDown: true, 100, 100));
        var move = bridge.Handle(Move(150, 150));
        var up = bridge.Handle(Button(GlobalMouseButton.XButton2, isDown: false, 150, 150));

        Assert.IsTrue(down.SuppressOriginal);
        Assert.IsFalse(move.SuppressOriginal);
        Assert.IsTrue(up.SuppressOriginal);

        var events = await DrainAsync(bridge);
        Assert.AreEqual(3, events.Count);
        Assert.IsInstanceOfType<RadialMenuUiEvent.Open>(events[0]);
        Assert.IsInstanceOfType<RadialMenuUiEvent.Preview>(events[1]);
        var commit = (RadialMenuUiEvent.Commit)events[2];
        Assert.AreEqual("right", commit.Item!.Id);
    }

    [TestMethod]
    public void InjectedOrOtherButtonsPassThroughWithoutOpeningMenu()
    {
        using var bridge = new RadialMenuInputBridge(CreateLayout());

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0, injected: true)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Move(10, 10)).SuppressOriginal);
    }

    [TestMethod]
    public void DuplicateUpperSideDownStaysSuppressedWithoutSecondOpen()
    {
        using var bridge = new RadialMenuInputBridge(CreateLayout());

        var first = bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0));
        var second = bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0));

        Assert.IsTrue(first.SuppressOriginal);
        Assert.IsTrue(second.SuppressOriginal);
    }

    [TestMethod]
    public void ClosedBridgeFailsOpen()
    {
        using var bridge = new RadialMenuInputBridge(CreateLayout());
        bridge.Complete();

        var decision = bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0));

        Assert.IsFalse(decision.SuppressOriginal);
    }

    [TestMethod]
    public void ConfiguredToggleButtonCanBeLowerSideButton()
    {
        using var bridge = new RadialMenuInputBridge(CreateLayout(), toggleButton: GlobalMouseButton.XButton1);

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
    }

    [TestMethod]
    public void CancelKeepsRadialBridgeReusableAfterEmergencyPause()
    {
        using var bridge = new RadialMenuInputBridge(CreateLayout());

        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0)).SuppressOriginal);
        bridge.Cancel();
        Assert.IsFalse(bridge.IsOpen);

        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.IsOpen);
    }

    [TestMethod]
    public void ReconfigureAppliesNewToggleButtonWithoutRestartingBridge()
    {
        using var bridge = new RadialMenuInputBridge(CreateLayout());

        bridge.Reconfigure(CreateLayout(), GlobalMouseButton.XButton1);

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
    }

    [TestMethod]
    public async Task CompletingAnActiveBridgePublishesCancelBeforeClosing()
    {
        using var bridge = new RadialMenuInputBridge(CreateLayout());
        bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0));

        bridge.Complete();
        var events = await DrainAsyncWithoutCompleting(bridge);

        Assert.AreEqual(2, events.Count);
        Assert.IsInstanceOfType<RadialMenuUiEvent.Open>(events[0]);
        Assert.IsInstanceOfType<RadialMenuUiEvent.Cancel>(events[1]);
    }

    private static RadialMenuLayout CreateLayout() => new(
        new[]
        {
            new RadialMenuItem("top", "Top"),
            new RadialMenuItem("right", "Right"),
            new RadialMenuItem("bottom", "Bottom"),
            new RadialMenuItem("left", "Left")
        },
        deadZoneRadius: 16);

    private static GlobalMouseEvent Move(int x, int y) =>
        new(GlobalMouseEventKind.Move, null, isDown: false, x, y, isInjected: false, timestamp: 0);

    private static GlobalMouseEvent Button(
        GlobalMouseButton button,
        bool isDown,
        int x,
        int y,
        bool injected = false) =>
        new(GlobalMouseEventKind.Button, button, isDown, x, y, injected, timestamp: 0);

    private static async Task<List<RadialMenuUiEvent>> DrainAsync(RadialMenuInputBridge bridge)
    {
        bridge.Complete();
        var events = new List<RadialMenuUiEvent>();
        await foreach (var uiEvent in bridge.ReadAllAsync())
        {
            events.Add(uiEvent);
        }

        return events;
    }

    private static async Task<List<RadialMenuUiEvent>> DrainAsyncWithoutCompleting(
        RadialMenuInputBridge bridge)
    {
        var events = new List<RadialMenuUiEvent>();
        await foreach (var uiEvent in bridge.ReadAllAsync())
        {
            events.Add(uiEvent);
        }

        return events;
    }
}
