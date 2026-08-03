using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;

namespace ReMouse.InputProbe.Tests;

[TestClass]
public sealed class MiddleChordHookControllerTests
{
    [TestMethod]
    public async Task ControllerQueuesEffectsAndReturnsSuppressionDecision()
    {
        using var events = new ProbeEventChannel();
        var controller = new MiddleChordHookController(events, 240);

        var middleDown = controller.Handle(new MouseButtonEvent(MouseButtonId.Middle, isDown: true));
        var leftDown = controller.Handle(new MouseButtonEvent(MouseButtonId.Left, isDown: true));
        var leftUp = controller.Handle(new MouseButtonEvent(MouseButtonId.Left, isDown: false));
        var middleUp = controller.Handle(new MouseButtonEvent(MouseButtonId.Middle, isDown: false));

        Assert.IsTrue(middleDown.SuppressOriginal);
        Assert.IsTrue(leftDown.SuppressOriginal);
        Assert.IsTrue(leftUp.SuppressOriginal);
        Assert.IsTrue(middleUp.SuppressOriginal);

        var workItems = await DrainAsync(events);
        var effect = workItems.Single(item => item.EffectToApply is not null).EffectToApply;
        Assert.AreEqual(-240, ((InputEffect.HorizontalWheel)effect!).Delta);
    }

    [TestMethod]
    public async Task PlainMiddleClickQueuesOnlySyntheticMiddleClick()
    {
        using var events = new ProbeEventChannel();
        var controller = new MiddleChordHookController(events, 120);

        controller.Handle(new MouseButtonEvent(MouseButtonId.Middle, isDown: true));
        var middleUp = controller.Handle(new MouseButtonEvent(MouseButtonId.Middle, isDown: false));

        Assert.IsTrue(middleUp.SuppressOriginal);
        var workItems = await DrainAsync(events);
        var effect = workItems.Single(item => item.EffectToApply is not null).EffectToApply;
        Assert.IsInstanceOfType<InputEffect.MiddleClick>(effect);
    }

    [TestMethod]
    public void ClosedEffectChannelFailsOpenForFutureEvents()
    {
        using var events = new ProbeEventChannel();
        var controller = new MiddleChordHookController(events, 120);
        events.Complete();

        var decision = controller.Handle(new MouseButtonEvent(MouseButtonId.Middle, isDown: true));

        Assert.IsFalse(decision.SuppressOriginal);
        Assert.AreEqual(0, decision.Effects.Count);
    }

    [TestMethod]
    public async Task ControllerQueuesSyntheticBoundaryEffectsForMiddleDrag()
    {
        using var events = new ProbeEventChannel();
        var controller = new MiddleChordHookController(events, 120);

        var middleDown = controller.Handle(new MouseButtonEvent(
            MouseButtonId.Middle,
            isDown: true,
            x: 100,
            y: 100));
        var belowThreshold = controller.HandleMove(new MouseMoveEvent(105, 105));
        var crossedThreshold = controller.HandleMove(new MouseMoveEvent(108, 100));
        var middleUp = controller.Handle(new MouseButtonEvent(
            MouseButtonId.Middle,
            isDown: false,
            x: 108,
            y: 100));

        Assert.IsTrue(middleDown.SuppressOriginal);
        Assert.IsFalse(belowThreshold.SuppressOriginal);
        Assert.IsTrue(crossedThreshold.SuppressOriginal);
        Assert.IsTrue(middleUp.SuppressOriginal);

        var effects = (await DrainAsync(events))
            .Where(item => item.EffectToApply is not null)
            .Select(item => item.EffectToApply)
            .ToArray();

        Assert.AreEqual(4, effects.Length);
        Assert.IsInstanceOfType<InputEffect.MiddleButtonDown>(effects[0]);
        Assert.IsInstanceOfType<InputEffect.MouseMove>(effects[1]);
        Assert.IsInstanceOfType<InputEffect.MiddleDragReady>(effects[2]);
        Assert.IsInstanceOfType<InputEffect.MiddleButtonUp>(effects[3]);
    }

    private static async Task<List<ProbeWorkItem>> DrainAsync(ProbeEventChannel events)
    {
        events.Complete();
        var result = new List<ProbeWorkItem>();
        await foreach (var workItem in events.ReadAllAsync(CancellationToken.None))
        {
            result.Add(workItem);
        }

        return result;
    }
}
