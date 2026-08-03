using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.App;
using ReMouse.Core.Input;
using ReMouse.Windows.Hooks;

namespace ReMouse.App.Tests;

[TestClass]
public sealed class PixelInspectorEventPumpTests
{
    [TestMethod]
    public async Task PumpKeepsOverlayVisibleAfterSelectionAndDismissesOnToggle()
    {
        var view = new RecordingView();
        using var bridge = new PixelInspectorInputBridge();
        var pump = new PixelInspectorEventPump(view, bridge);

        bridge.Handle(Button(GlobalMouseButton.XButton1, true, 100, 100));
        bridge.Handle(Button(GlobalMouseButton.Left, true, 100, 100));
        bridge.Handle(Button(GlobalMouseButton.Left, false, 160, 180));
        bridge.Handle(Button(GlobalMouseButton.XButton1, false, 160, 180));
        bridge.Handle(Button(GlobalMouseButton.XButton1, true, 160, 180));
        bridge.Handle(Button(GlobalMouseButton.XButton1, false, 160, 180));
        bridge.Complete();

        await pump.RunAsync(bridge.ReadAllAsync());

        Assert.AreEqual(1, view.ShowCount);
        Assert.IsTrue(view.UpdateCount >= 2);
        Assert.AreEqual(1, view.DismissCount);
    }

    [TestMethod]
    public async Task PumpFaultClosesBridgeAndDismissesOverlay()
    {
        var view = new ThrowingView();
        using var bridge = new PixelInspectorInputBridge();
        var pump = new PixelInspectorEventPump(view, bridge);
        bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0));
        bridge.Handle(Move(20, 20));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => pump.RunAsync(bridge.ReadAllAsync()));

        Assert.IsTrue(bridge.IsClosed);
        Assert.AreEqual(1, view.DismissCount);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton1, true, 0, 0)).SuppressOriginal);
    }

    private static GlobalMouseEvent Move(int x, int y) =>
        new(GlobalMouseEventKind.Move, null, isDown: false, x, y, isInjected: false, timestamp: 0);

    private static GlobalMouseEvent Button(GlobalMouseButton button, bool isDown, int x, int y) =>
        new(GlobalMouseEventKind.Button, button, isDown, x, y, isInjected: false, timestamp: 0);

    private sealed class RecordingView : IPixelInspectorOverlayView
    {
        public int ShowCount { get; private set; }

        public int UpdateCount { get; private set; }

        public int DismissCount { get; private set; }

        public void Show(PixelInspectorSnapshot snapshot) => ShowCount++;

        public void Update(PixelInspectorSnapshot snapshot) => UpdateCount++;

        public void Dismiss() => DismissCount++;
    }

    private sealed class ThrowingView : IPixelInspectorOverlayView
    {
        public int DismissCount { get; private set; }

        public void Show(PixelInspectorSnapshot snapshot)
        {
        }

        public void Update(PixelInspectorSnapshot snapshot) =>
            throw new InvalidOperationException("test failure");

        public void Dismiss() => DismissCount++;
    }
}
