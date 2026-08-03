using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;

namespace ReMouse.InputProbe.Tests;

[TestClass]
public sealed class ProbeEffectProcessorTests
{
    [TestMethod]
    public async Task ProcessorAppliesQueuedEffectsOutsideTheHookCallback()
    {
        using var events = new ProbeEventChannel();
        var sink = new RecordingSink();
        var options = new ProbeOptions(KeyboardLoggingMode.None, false, false);
        await using var processor = new ProbeEventProcessor(
            events,
            options,
            effectSink: sink);

        processor.Start();
        Assert.IsTrue(events.TryWrite(new InputEffect.HorizontalWheel(-120)));
        await processor.StopAsync();

        Assert.AreEqual(1, sink.Effects.Count);
        Assert.AreEqual(-120, ((InputEffect.HorizontalWheel)sink.Effects[0]).Delta);
    }

    [TestMethod]
    public async Task ProcessorDrainsCancelledDragWithSyntheticRelease()
    {
        using var events = new ProbeEventChannel();
        var controller = new MiddleChordHookController(events, 120);
        var sink = new RecordingSink();
        var options = new ProbeOptions(KeyboardLoggingMode.None, false, true);
        await using var processor = new ProbeEventProcessor(
            events,
            options,
            effectSink: sink,
            middleChord: controller);

        processor.Start();
        controller.Handle(new MouseButtonEvent(MouseButtonId.Middle, true, 100, 100));
        controller.HandleMove(new MouseMoveEvent(110, 100));
        controller.Cancel();

        await processor.StopAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(InputEffect.MiddleButtonDown),
                typeof(InputEffect.MouseMove),
                typeof(InputEffect.MiddleButtonUp)
            },
            sink.Effects.Select(effect => effect.GetType()).ToArray());
    }

    [TestMethod]
    public async Task ProcessorFaultBestEffortReleasesPartiallyDeliveredMiddleDown()
    {
        using var events = new ProbeEventChannel();
        var controller = new MiddleChordHookController(events, 120);
        var sink = new ThrowAfterRecordingDownSink();
        var options = new ProbeOptions(KeyboardLoggingMode.None, false, true);
        await using var processor = new ProbeEventProcessor(
            events,
            options,
            effectSink: sink,
            middleChord: controller);

        processor.Start();
        controller.Handle(new MouseButtonEvent(MouseButtonId.Middle, true, 100, 100));
        controller.HandleMove(new MouseMoveEvent(110, 100));
        await processor.StopAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(InputEffect.MiddleButtonDown),
                typeof(InputEffect.MiddleButtonUp)
            },
            sink.Effects.Select(effect => effect.GetType()).ToArray());
        Assert.IsTrue(processor.HasProcessingErrors);
    }

    [TestMethod]
    public async Task ProcessorKeepsMovesBehindLatestReplayMarker()
    {
        using var events = new ProbeEventChannel();
        var controller = new MiddleChordHookController(events, 120);
        var sink = new BlockingReplaySink();
        var options = new ProbeOptions(KeyboardLoggingMode.None, false, true);
        await using var processor = new ProbeEventProcessor(
            events,
            options,
            effectSink: sink,
            middleChord: controller);

        processor.Start();
        controller.Handle(new MouseButtonEvent(MouseButtonId.Middle, true, 100, 100));
        controller.HandleMove(new MouseMoveEvent(110, 100));

        Assert.IsTrue(sink.FirstMoveEntered.Wait(TimeSpan.FromSeconds(1)));
        var second = controller.HandleMove(new MouseMoveEvent(140, 120));
        Assert.IsTrue(second.SuppressOriginal);

        sink.AllowFirstMove.Set();
        Assert.IsTrue(sink.SecondMoveEntered.Wait(TimeSpan.FromSeconds(1)));
        var third = controller.HandleMove(new MouseMoveEvent(160, 140));
        Assert.IsTrue(third.SuppressOriginal);

        sink.AllowSecondMove.Set();
        var nativeMoveObserved = false;
        for (var attempt = 0; attempt < 200 && !nativeMoveObserved; attempt++)
        {
            nativeMoveObserved = !controller
                .HandleMove(new MouseMoveEvent(200 + attempt, 200 + attempt))
                .SuppressOriginal;
            if (!nativeMoveObserved)
            {
                await Task.Delay(5);
            }
        }

        Assert.IsTrue(nativeMoveObserved);
        controller.Handle(new MouseButtonEvent(MouseButtonId.Middle, false, 220, 220));
        await processor.StopAsync();
    }

    private sealed class RecordingSink : IInputEffectSink
    {
        public List<InputEffect> Effects { get; } = new();

        public void Apply(InputEffect effect)
        {
            Effects.Add(effect);
        }
    }

    private sealed class ThrowAfterRecordingDownSink : IInputEffectSink
    {
        private bool _hasFailed;

        public List<InputEffect> Effects { get; } = new();

        public void Apply(InputEffect effect)
        {
            Effects.Add(effect);
            if (effect is InputEffect.MiddleButtonDown && !_hasFailed)
            {
                _hasFailed = true;
                throw new InvalidOperationException("partial SendInput failure");
            }
        }
    }

    private sealed class BlockingReplaySink : IInputEffectSink
    {
        private int _moveCount;

        public ManualResetEventSlim FirstMoveEntered { get; } = new(false);

        public ManualResetEventSlim AllowFirstMove { get; } = new(false);

        public ManualResetEventSlim SecondMoveEntered { get; } = new(false);

        public ManualResetEventSlim AllowSecondMove { get; } = new(false);

        public void Apply(InputEffect effect)
        {
            if (effect is not InputEffect.MouseMove)
            {
                return;
            }

            switch (Interlocked.Increment(ref _moveCount))
            {
                case 1:
                    FirstMoveEntered.Set();
                    AllowFirstMove.Wait(TimeSpan.FromSeconds(2));
                    break;
                case 2:
                    SecondMoveEntered.Set();
                    AllowSecondMove.Wait(TimeSpan.FromSeconds(2));
                    break;
            }
        }
    }
}
