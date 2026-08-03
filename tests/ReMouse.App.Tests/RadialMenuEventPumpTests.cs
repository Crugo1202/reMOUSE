using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.App;
using ReMouse.Core.Input;
using ReMouse.Windows.Hooks;

namespace ReMouse.App.Tests;

[TestClass]
public sealed class RadialMenuEventPumpTests
{
    [TestMethod]
    public async Task PumpRendersPreviewDismissesAndExecutesCommittedAction()
    {
        var layout = CreateLayout();
        var view = new RecordingView();
        var executor = new RecordingExecutor();
        using var bridge = new RadialMenuInputBridge(layout);
        var pump = new RadialMenuEventPump(layout, view, executor);

        bridge.Handle(Button(GlobalMouseButton.XButton2, true, 100, 100));
        bridge.Handle(Move(150, 150));
        bridge.Handle(Button(GlobalMouseButton.XButton2, false, 150, 150));
        bridge.Complete();

        await pump.RunAsync(bridge.ReadAllAsync());

        Assert.AreEqual(1, view.ShowCount);
        Assert.AreEqual(1, view.Hits.Count);
        Assert.AreEqual(1, view.DismissCount);
        Assert.AreEqual(1, executor.Actions.Count);
        Assert.IsInstanceOfType<RadialMenuAction.LaunchApplication>(executor.Actions[0]);
    }

    [TestMethod]
    public async Task DeadZoneCommitDismissesWithoutExecutingAction()
    {
        var layout = CreateLayout();
        var view = new RecordingView();
        var executor = new RecordingExecutor();
        using var bridge = new RadialMenuInputBridge(layout);
        var pump = new RadialMenuEventPump(layout, view, executor);

        bridge.Handle(Button(GlobalMouseButton.XButton2, true, 100, 100));
        bridge.Handle(Button(GlobalMouseButton.XButton2, false, 102, 102));
        bridge.Complete();

        await pump.RunAsync(bridge.ReadAllAsync());

        Assert.AreEqual(1, view.DismissCount);
        Assert.AreEqual(0, executor.Actions.Count);
    }

    [TestMethod]
    public async Task ExecutorFaultDismissesOverlayAndClosesInputBridge()
    {
        var layout = CreateLayout();
        var view = new RecordingView();
        using var bridge = new RadialMenuInputBridge(layout);
        var pump = new RadialMenuEventPump(layout, view, new ThrowingExecutor(), bridge);

        bridge.Handle(Button(GlobalMouseButton.XButton2, true, 100, 100));
        bridge.Handle(Button(GlobalMouseButton.XButton2, false, 150, 150));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => pump.RunAsync(bridge.ReadAllAsync()));

        Assert.IsTrue(bridge.IsClosed);
        Assert.AreEqual(1, view.DismissCount);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton2, true, 0, 0)).SuppressOriginal);
    }

    private static RadialMenuLayout CreateLayout() => new(
        new[]
        {
            new RadialMenuItem("top", "Top"),
            new RadialMenuItem(
                "right",
                "Right",
                new RadialMenuAction.LaunchApplication("tool.exe")),
            new RadialMenuItem("bottom", "Bottom"),
            new RadialMenuItem("left", "Left")
        },
        deadZoneRadius: 16);

    private static GlobalMouseEvent Move(int x, int y) =>
        new(GlobalMouseEventKind.Move, null, isDown: false, x, y, isInjected: false, timestamp: 0);

    private static GlobalMouseEvent Button(GlobalMouseButton button, bool isDown, int x, int y) =>
        new(GlobalMouseEventKind.Button, button, isDown, x, y, isInjected: false, timestamp: 0);

    private sealed class RecordingView : IRadialMenuOverlayView
    {
        public int ShowCount { get; private set; }

        public int DismissCount { get; private set; }

        public List<RadialMenuHit> Hits { get; } = new();

        public void Show(RadialMenuLayout layout, ScreenPoint center) => ShowCount++;

        public void Update(RadialMenuHit hit) => Hits.Add(hit);

        public void Dismiss() => DismissCount++;
    }

    private sealed class RecordingExecutor : IRadialMenuActionExecutor
    {
        public List<RadialMenuAction> Actions { get; } = new();

        public ValueTask ExecuteAsync(
            RadialMenuAction action,
            CancellationToken cancellationToken = default)
        {
            Actions.Add(action);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingExecutor : IRadialMenuActionExecutor
    {
        public ValueTask ExecuteAsync(
            RadialMenuAction action,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("test failure"));
    }
}
