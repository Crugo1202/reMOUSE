using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.App;
using ReMouse.Core.Input;

namespace ReMouse.App.Tests;

[TestClass]
public sealed class RadialMenuOverlayControllerTests
{
    [TestMethod]
    public async Task ReleaseExecutesSelectedActionAfterHidingOverlay()
    {
        var layout = CreateLayout();
        var view = new RecordingView();
        var executor = new RecordingExecutor();
        var controller = new RadialMenuOverlayController(layout, view, executor);

        controller.Begin(new ScreenPoint(100, 100));
        controller.Update(new ScreenPoint(150, 150));
        var selected = await controller.ReleaseAsync(new ScreenPoint(150, 150));

        Assert.AreEqual("right", selected!.Id);
        Assert.AreEqual(1, view.ShowCount);
        Assert.AreEqual(1, view.HideCount);
        Assert.AreEqual(1, executor.Actions.Count);
        Assert.AreSame(selected.Action, executor.Actions[0]);
        Assert.IsFalse(controller.IsOpen);
    }

    [TestMethod]
    public async Task DeadZoneReleaseHidesWithoutExecutingAction()
    {
        var layout = CreateLayout();
        var view = new RecordingView();
        var executor = new RecordingExecutor();
        var controller = new RadialMenuOverlayController(layout, view, executor);

        controller.Begin(new ScreenPoint(100, 100));
        var selected = await controller.ReleaseAsync(new ScreenPoint(102, 102));

        Assert.IsNull(selected);
        Assert.AreEqual(1, view.HideCount);
        Assert.AreEqual(0, executor.Actions.Count);
    }

    [TestMethod]
    public void CancelIsIdempotentAndHidesOnce()
    {
        var view = new RecordingView();
        var controller = new RadialMenuOverlayController(CreateLayout(), view, new RecordingExecutor());

        controller.Cancel();
        controller.Begin(new ScreenPoint(0, 0));
        controller.Cancel();
        controller.Cancel();

        Assert.AreEqual(1, view.HideCount);
        Assert.IsFalse(controller.IsOpen);
    }

    [TestMethod]
    public async Task OverlayIsHiddenWhenActionExecutorFails()
    {
        var view = new RecordingView();
        var executor = new ThrowingExecutor();
        var controller = new RadialMenuOverlayController(CreateLayout(), view, executor);
        controller.Begin(new ScreenPoint(0, 0));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => controller.ReleaseAsync(new ScreenPoint(50, 0)).AsTask());

        Assert.AreEqual(1, view.HideCount);
        Assert.IsFalse(controller.IsOpen);
    }

    private static RadialMenuLayout CreateLayout() => new(
        new[]
        {
            new RadialMenuItem("top", "Top"),
            new RadialMenuItem("right", "Right", new RadialMenuAction.NoOp()),
            new RadialMenuItem("bottom", "Bottom")
        },
        deadZoneRadius: 16);

    private sealed class RecordingView : IRadialMenuOverlayView
    {
        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public List<RadialMenuHit> Hits { get; } = new();

        public void Show(RadialMenuLayout layout, ScreenPoint center) => ShowCount++;

        public void Update(RadialMenuHit hit) => Hits.Add(hit);

        public void Dismiss() => HideCount++;
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
