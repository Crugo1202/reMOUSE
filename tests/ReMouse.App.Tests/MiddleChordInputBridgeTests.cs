using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.App;
using ReMouse.Core.Input;
using ReMouse.Windows.Hooks;

namespace ReMouse.App.Tests;

[TestClass]
public sealed class MiddleChordInputBridgeTests
{
    [TestMethod]
    public async Task PlainMiddleClickIsSuppressedAndReplayedAsOneEffect()
    {
        using var bridge = new MiddleChordInputBridge();

        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Middle, true)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Middle, false)).SuppressOriginal);

        bridge.Complete();
        var effects = await DrainAsync(bridge);

        Assert.AreEqual(1, effects.Count);
        Assert.IsInstanceOfType<InputEffect.MiddleClick>(effects[0]);
    }

    [TestMethod]
    public async Task LeftFlickQueuesNegativeWheelAndSuppressesChordButtons()
    {
        using var bridge = new MiddleChordInputBridge(240);

        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Middle, true)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Left, true)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Left, false)).SuppressOriginal);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Middle, false)).SuppressOriginal);

        bridge.Complete();
        var effects = await DrainAsync(bridge);
        var wheel = (InputEffect.HorizontalWheel)effects.Single();
        Assert.AreEqual(-240, wheel.Delta);
    }

    [TestMethod]
    public async Task RightFlickQueuesPositiveWheel()
    {
        using var bridge = new MiddleChordInputBridge(180);

        bridge.Handle(Button(GlobalMouseButton.Middle, true));
        bridge.Handle(Button(GlobalMouseButton.Right, true));
        bridge.Handle(Button(GlobalMouseButton.Right, false));
        bridge.Handle(Button(GlobalMouseButton.Middle, false));
        bridge.Complete();

        var wheel = (InputEffect.HorizontalWheel)(await DrainAsync(bridge)).Single();
        Assert.AreEqual(180, wheel.Delta);
    }

    [TestMethod]
    public void NonChordAndInjectedButtonsPassThrough()
    {
        using var bridge = new MiddleChordInputBridge();

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Left, true)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Left, false)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.XButton1, true)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Middle, true, injected: true)).SuppressOriginal);
    }

    [TestMethod]
    public async Task DuplicateDownDoesNotCreateDuplicateFlick()
    {
        using var bridge = new MiddleChordInputBridge();

        bridge.Handle(Button(GlobalMouseButton.Middle, true));
        bridge.Handle(Button(GlobalMouseButton.Middle, true));
        bridge.Handle(Button(GlobalMouseButton.Left, true));
        bridge.Handle(Button(GlobalMouseButton.Left, true));
        bridge.Handle(Button(GlobalMouseButton.Left, false));
        bridge.Handle(Button(GlobalMouseButton.Middle, false));
        bridge.Complete();

        var effects = await DrainAsync(bridge);
        Assert.AreEqual(1, effects.Count);
        Assert.AreEqual(-120, ((InputEffect.HorizontalWheel)effects[0]).Delta);
    }

    [TestMethod]
    public void ClosedBridgeFailsOpen()
    {
        using var bridge = new MiddleChordInputBridge();
        bridge.Complete();

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Middle, true)).SuppressOriginal);
    }

    [TestMethod]
    public async Task CancelClearsCrossingBoundaryStateBeforeNextOrdinaryClick()
    {
        using var bridge = new MiddleChordInputBridge();
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Middle, true)).SuppressOriginal);
        bridge.Cancel();

        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Middle, false)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Left, true)).SuppressOriginal);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Left, false)).SuppressOriginal);

        bridge.Complete();
        Assert.AreEqual(0, (await DrainAsync(bridge)).Count);
    }

    [TestMethod]
    public async Task MiddleMoveCrossingThresholdQueuesSyntheticDownAndUp()
    {
        using var bridge = new MiddleChordInputBridge();
        bridge.Handle(Button(GlobalMouseButton.Middle, true, x: 100, y: 100));

        var move = bridge.Handle(Move(110, 100));
        var up = bridge.Handle(Button(GlobalMouseButton.Middle, false, x: 110, y: 100));
        bridge.Complete();

        Assert.IsTrue(move.SuppressOriginal);
        Assert.IsTrue(up.SuppressOriginal);
        var effects = await DrainAsync(bridge);
        Assert.IsInstanceOfType<InputEffect.MiddleButtonDown>(effects[0]);
        Assert.IsInstanceOfType<InputEffect.MouseMove>(effects[1]);
        Assert.IsInstanceOfType<InputEffect.MiddleDragReady>(effects[2]);
        Assert.IsInstanceOfType<InputEffect.MiddleButtonUp>(effects[3]);
    }

    [TestMethod]
    public async Task DragMovesCannotOvertakeSyntheticBoundary()
    {
        using var bridge = new MiddleChordInputBridge();
        bridge.Handle(Button(GlobalMouseButton.Middle, true, x: 100, y: 100));

        var crossing = bridge.Handle(Move(110, 100));
        var following = bridge.Handle(Move(140, 120));

        Assert.IsTrue(crossing.SuppressOriginal);
        Assert.IsTrue(following.SuppressOriginal);

        var sink = new RecordingSink();
        bridge.Complete();
        await new MiddleChordEffectPump(sink, bridge).RunAsync(bridge.ReadEffectsAsync());

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(InputEffect.MiddleButtonDown),
                typeof(InputEffect.MouseMove),
                typeof(InputEffect.MouseMove),
                typeof(InputEffect.MiddleButtonUp)
            },
            sink.Effects.Select(effect => effect.GetType()).ToArray());
    }

    [TestMethod]
    public async Task CancelAfterDragStartQueuesReleaseBeforeChannelCompletion()
    {
        using var bridge = new MiddleChordInputBridge();
        bridge.Handle(Button(GlobalMouseButton.Middle, true, x: 100, y: 100));
        bridge.Handle(Move(110, 100));

        bridge.Cancel();
        // The physical Up crosses the modal boundary after Cancel and is
        // therefore native; the queued synthetic Up still owns the replayed
        // middle Down.
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Middle, false, x: 110, y: 100)).SuppressOriginal);

        var sink = new RecordingSink();
        bridge.Complete();
        await new MiddleChordEffectPump(sink, bridge).RunAsync(bridge.ReadEffectsAsync());

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
    public async Task PumpFaultBestEffortReleasesPartiallyDeliveredMiddleDown()
    {
        using var bridge = new MiddleChordInputBridge();
        bridge.Handle(Button(GlobalMouseButton.Middle, true, x: 100, y: 100));
        bridge.Handle(Move(110, 100));
        bridge.Complete();

        var sink = new ThrowAfterRecordingDownSink();
        var pump = new MiddleChordEffectPump(sink, bridge);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => pump.RunAsync(bridge.ReadEffectsAsync()));

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(InputEffect.MiddleButtonDown),
                typeof(InputEffect.MiddleButtonUp)
            },
            sink.Effects.Select(effect => effect.GetType()).ToArray());
    }

    [TestMethod]
    public async Task ReconfigureCancelsOldChordAndUsesNewDeltaImmediately()
    {
        using var bridge = new MiddleChordInputBridge(120);
        bridge.Handle(Button(GlobalMouseButton.Middle, true));
        bridge.Handle(Button(GlobalMouseButton.Left, true));

        bridge.Reconfigure(240);
        Assert.IsFalse(bridge.Handle(Button(GlobalMouseButton.Middle, false)).SuppressOriginal);

        bridge.Handle(Button(GlobalMouseButton.Middle, true));
        bridge.Handle(Button(GlobalMouseButton.Right, true));
        bridge.Handle(Button(GlobalMouseButton.Right, false));
        bridge.Handle(Button(GlobalMouseButton.Middle, false));
        bridge.Complete();

        var effects = await DrainAsync(bridge);
        CollectionAssert.AreEqual(
            new[] { -120, 240 },
            effects.OfType<InputEffect.HorizontalWheel>().Select(effect => effect.Delta).ToArray());
    }

    [TestMethod]
    public async Task LatestReplayMarkerKeepsLaterMovesBehindThePump()
    {
        using var bridge = new MiddleChordInputBridge();
        bridge.Handle(Button(GlobalMouseButton.Middle, true, x: 100, y: 100));
        bridge.Handle(Move(110, 100));

        var sink = new BlockingReplaySink();
        var pumpTask = Task.Run(() => new MiddleChordEffectPump(sink, bridge)
            .RunAsync(bridge.ReadEffectsAsync()));

        Assert.IsTrue(sink.FirstMoveEntered.Wait(TimeSpan.FromSeconds(1)));
        var second = bridge.Handle(Move(140, 120));
        Assert.IsTrue(second.SuppressOriginal);

        sink.AllowFirstMove.Set();
        Assert.IsTrue(sink.SecondMoveEntered.Wait(TimeSpan.FromSeconds(1)));

        // Marker 1 has already been consumed, but Move 2 is blocked behind
        // Marker 1. A new physical move must still be replay-queued.
        var third = bridge.Handle(Move(160, 140));
        Assert.IsTrue(third.SuppressOriginal);

        sink.AllowSecondMove.Set();
        GlobalMouseDecision? nativeMove = null;
        for (var attempt = 0; attempt < 200 && nativeMove is null; attempt++)
        {
            var candidate = bridge.Handle(Move(200 + attempt, 200 + attempt));
            if (!candidate.SuppressOriginal)
            {
                nativeMove = candidate;
                break;
            }

            await Task.Delay(5);
        }

        Assert.IsNotNull(nativeMove);
        Assert.IsTrue(bridge.Handle(Button(GlobalMouseButton.Middle, false, x: 220, y: 220)).SuppressOriginal);
        bridge.Complete();
        await pumpTask;
    }

    [TestMethod]
    public async Task ChannelReaderPumpCoalescesBufferedReplayMovesToLatestPoint()
    {
        using var bridge = new MiddleChordInputBridge();
        bridge.Handle(Button(GlobalMouseButton.Middle, true, x: 0, y: 0));
        bridge.Handle(Move(8, 0));
        bridge.Handle(Move(16, 0));
        bridge.Handle(Move(24, 0));
        bridge.Handle(Button(GlobalMouseButton.Middle, false, x: 24, y: 0));
        bridge.Complete();

        var sink = new RecordingSink();
        await new MiddleChordEffectPump(sink, bridge).RunAsync(bridge.EffectReader);

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(InputEffect.MiddleButtonDown),
                typeof(InputEffect.MouseMove),
                typeof(InputEffect.MiddleButtonUp)
            },
            sink.Effects.Select(effect => effect.GetType()).ToArray());
        var replay = (InputEffect.MouseMove)sink.Effects[1];
        Assert.AreEqual(24, replay.X);
        Assert.AreEqual(0, replay.Y);
    }

    [TestMethod]
    public async Task EffectPumpAppliesQueuedEffectsOffTheHookPath()
    {
        using var bridge = new MiddleChordInputBridge();
        var sink = new RecordingSink();
        var pump = new MiddleChordEffectPump(sink, bridge);
        bridge.Handle(Button(GlobalMouseButton.Middle, true));
        bridge.Handle(Button(GlobalMouseButton.Middle, false));
        bridge.Complete();

        await pump.RunAsync(bridge.ReadEffectsAsync());

        Assert.AreEqual(1, sink.Effects.Count);
        Assert.IsTrue(bridge.IsClosed);
    }

    [TestMethod]
    public async Task EffectPumpFailureClosesBridge()
    {
        using var bridge = new MiddleChordInputBridge();
        var pump = new MiddleChordEffectPump(new ThrowingSink(), bridge);
        bridge.Handle(Button(GlobalMouseButton.Middle, true));
        bridge.Handle(Button(GlobalMouseButton.Middle, false));
        bridge.Complete();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => pump.RunAsync(bridge.ReadEffectsAsync()));
        Assert.IsTrue(bridge.IsClosed);
    }

    private static GlobalMouseEvent Button(
        GlobalMouseButton button,
        bool isDown,
        bool injected = false,
        int x = 0,
        int y = 0) =>
        new(GlobalMouseEventKind.Button, button, isDown, x, y, injected, timestamp: 0);

    private static GlobalMouseEvent Move(int x, int y) =>
        new(GlobalMouseEventKind.Move, null, isDown: false, x, y, isInjected: false, timestamp: 0);

    private static async Task<List<InputEffect>> DrainAsync(MiddleChordInputBridge bridge)
    {
        var effects = new List<InputEffect>();
        await foreach (var effect in bridge.ReadEffectsAsync())
        {
            effects.Add(effect);
        }

        return effects;
    }

    private sealed class RecordingSink : IInputEffectSink
    {
        public List<InputEffect> Effects { get; } = new();

        public void Apply(InputEffect effect) => Effects.Add(effect);
    }

    private sealed class ThrowingSink : IInputEffectSink
    {
        public void Apply(InputEffect effect) => throw new InvalidOperationException("test failure");
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
