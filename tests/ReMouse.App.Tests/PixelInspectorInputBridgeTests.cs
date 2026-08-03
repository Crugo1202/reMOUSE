using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.App;
using ReMouse.Core.Input;
using ReMouse.Windows.Hooks;

namespace ReMouse.App.Tests;

[TestClass]
public sealed class PixelInspectorInputBridgeTests
{
    [TestMethod]
    public async Task PhysicalLowerSideButtonOpensAndSelectionEventsAreQueued()
    {
        using var bridge = new PixelInspectorInputBridge();

        var lowerDown = bridge.Handle(Button(GlobalMouseButton.XButton1, true, 100, 200));
        var move = bridge.Handle(Move(120, 220));
        var leftDown = bridge.Handle(Button(GlobalMouseButton.Left, true, 120, 220));
        var leftUp = bridge.Handle(Button(GlobalMouseButton.Left, false, 180, 260));
        var lowerUp = bridge.Handle(Button(GlobalMouseButton.XButton1, false, 180, 260));

        Assert.IsTrue(lowerDown.SuppressOriginal);
        Assert.IsFalse(move.SuppressOriginal);
        Assert.IsTrue(leftDown.SuppressOriginal);
        Assert.IsTrue(leftUp.SuppressOriginal);
        Assert.IsTrue(lowerUp.SuppressOriginal);

        bridge.Complete();
        var events = await DrainAsync(bridge);
        Assert.IsInstanceOfType<PixelInspectorUiEvent.Open>(events[0]);
        Assert.IsInstanceOfType<PixelInspectorUiEvent.Update>(events[1]);
        Assert.IsInstanceOfType<PixelInspectorUiEvent.Update>(events[2]);
        Assert.IsInstanceOfType<PixelInspectorUiEvent.SelectionCompleted>(events[3]);
        Assert.IsInstanceOfType<PixelInspectorUiEvent.Dismiss>(events[4]);
        Assert.AreEqual(5, events.Count);
    }

    [TestMethod]
    public void InjectedAndOtherButtonsPassThrough()
    {
        using var bridge = new PixelInspectorInputBridge();

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0, injected: true)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Left, true, 0, 0)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Left, false, 0, 0)).SuppressOriginal);
    }

    [TestMethod]
    public void DuplicateLowerSideDownDoesNotToggleTwice()
    {
        using var bridge = new PixelInspectorInputBridge();

        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.IsActive);

        bridge.Complete();
    }

    [TestMethod]
    public void ConfiguredToggleButtonCanBeUpperSideButton()
    {
        using var bridge = new PixelInspectorInputBridge(GlobalMouseButton.XButton2);

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.IsActive);
    }

    [TestMethod]
    public void CancelKeepsPixelBridgeReusableAfterEmergencyPause()
    {
        using var bridge = new PixelInspectorInputBridge();

        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
        bridge.Cancel();
        Assert.IsFalse(bridge.IsActive);

        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.IsActive);
    }

    [TestMethod]
    public void ReconfigureAppliesNewToggleButtonWithoutRestartingBridge()
    {
        using var bridge = new PixelInspectorInputBridge();

        bridge.Reconfigure(GlobalMouseButton.XButton2);

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0)).SuppressOriginal);
        Assert.IsTrue(bridge.IsActive);
    }

    [TestMethod]
    public async Task ShiftProviderConstrainsLiveSelectionAndClipboardTextIsAvailable()
    {
        using var bridge = new PixelInspectorInputBridge(
            isShiftDown: () => true);

        bridge.Handle(Button(GlobalMouseButton.XButton1, true, 100, 100));
        bridge.Handle(Button(GlobalMouseButton.Left, true, 100, 100));
        bridge.Handle(Move(160, 145));
        bridge.Handle(Button(GlobalMouseButton.Left, false, 160, 145));

        Assert.IsTrue(bridge.TryGetClipboardText(out var clipboardText));
        StringAssert.Contains(clipboardText, "Cursor X=160 Y=145");
        StringAssert.Contains(clipboardText, "W ");

        bridge.Complete();
        var events = await DrainAsyncWithoutCompleting(bridge);
        var completed = events.OfType<PixelInspectorUiEvent.SelectionCompleted>().Single();
        Assert.AreEqual(
            completed.Snapshot.Selection!.Value.Width,
            completed.Snapshot.Selection.Value.Height);
    }

    [TestMethod]
    public async Task EmergencyCancelClearsConsumedLeftPairingBeforeResume()
    {
        using var bridge = new PixelInspectorInputBridge();

        bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0));
        bridge.Handle(Button(GlobalMouseButton.Left, true, 10, 10));
        bridge.Cancel();

        // The physical Up is intentionally not observed while paused.
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 20, 20)).SuppressOriginal);
        var resumedDown = bridge.Handle(Button(GlobalMouseButton.Left, true, 20, 20));
        Assert.IsTrue(resumedDown.SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Left, false, 30, 30)).SuppressOriginal);

        bridge.Complete();
        var events = await DrainAsyncWithoutCompleting(bridge);
        Assert.IsTrue(events.Any(uiEvent => uiEvent is PixelInspectorUiEvent.SelectionCompleted));
    }

    [TestMethod]
    public void EmergencyCancelClearsTogglePairingWhenInspectorAlreadyClosed()
    {
        using var bridge = new PixelInspectorInputBridge();

        bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0));
        bridge.Handle(Button(GlobalMouseButton.XButton1, false, 0, 0));
        bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0));
        Assert.IsFalse(bridge.IsActive);

        bridge.Cancel();
        var resumedToggle = bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0));

        Assert.IsTrue(resumedToggle.SuppressOriginal);
        Assert.IsTrue(bridge.IsActive);
    }

    [TestMethod]
    public void LeftDownBeforeOpeningKeepsMatchingUpPassThrough()
    {
        using var bridge = new PixelInspectorInputBridge();

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Left, true, 1, 1)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 2, 2)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Left, false, 3, 3)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, false, 3, 3)).SuppressOriginal);
    }

    [TestMethod]
    public void ConsumedLeftDownKeepsMatchingUpSuppressedAfterClose()
    {
        using var bridge = new PixelInspectorInputBridge();

        bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0));
        bridge.Handle(Button(GlobalMouseButton.XButton1, false, 0, 0));
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Left, true, 10, 10)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 20, 20)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Left, false, 30, 30)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.XButton1, false, 30, 30)).SuppressOriginal);
        Assert.IsFalse(bridge.IsActive);
    }

    [TestMethod]
    public async Task CompletingActiveBridgePublishesDismiss()
    {
        using var bridge = new PixelInspectorInputBridge();
        bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0));

        bridge.Complete();
        var events = await DrainAsyncWithoutCompleting(bridge);

        Assert.AreEqual(2, events.Count);
        Assert.IsInstanceOfType<PixelInspectorUiEvent.Open>(events[0]);
        Assert.IsInstanceOfType<PixelInspectorUiEvent.Dismiss>(events[1]);
    }

    private static GlobalMouseEvent Move(int x, int y) =>
        new(GlobalMouseEventKind.Move, null, isDown: false, x, y, isInjected: false, timestamp: 0);

    private static GlobalMouseEvent Button(
        GlobalMouseButton button,
        bool isDown,
        int x,
        int y,
        bool injected = false) =>
        new(GlobalMouseEventKind.Button, button, isDown, x, y, injected, timestamp: 0);

    private static async Task<List<PixelInspectorUiEvent>> DrainAsync(PixelInspectorInputBridge bridge)
    {
        bridge.Complete();
        return await DrainAsyncWithoutCompleting(bridge);
    }

    private static async Task<List<PixelInspectorUiEvent>> DrainAsyncWithoutCompleting(
        PixelInspectorInputBridge bridge)
    {
        var events = new List<PixelInspectorUiEvent>();
        await foreach (var uiEvent in bridge.ReadAllAsync())
        {
            events.Add(uiEvent);
        }

        return events;
    }
}
