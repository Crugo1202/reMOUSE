using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;

namespace ReMouse.Core.Tests;

[TestClass]
public sealed class MiddleChordGestureTests
{
    [TestMethod]
    public void PlainMiddleClickIsDelayedThenReEmittedAsMiddleClick()
    {
        var gesture = new MiddleChordGesture();

        var down = gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true));
        var up = gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, false));

        Assert.IsTrue(down.SuppressOriginal);
        Assert.AreEqual(0, down.Effects.Count);
        Assert.IsTrue(up.SuppressOriginal);
        Assert.AreEqual(1, up.Effects.Count);
        Assert.IsInstanceOfType<InputEffect.MiddleClick>(up.Effects[0]);
    }

    [TestMethod]
    public void MiddlePlusLeftProducesOneNegativeHorizontalFlick()
    {
        var gesture = new MiddleChordGesture();

        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true));
        var leftDown = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, true));
        var leftUp = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, false));
        var middleUp = gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, false));

        Assert.IsTrue(leftDown.SuppressOriginal);
        Assert.AreEqual(-120, ((InputEffect.HorizontalWheel)leftDown.Effects.Single()).Delta);
        Assert.IsTrue(leftUp.SuppressOriginal);
        Assert.AreEqual(0, leftUp.Effects.Count);
        Assert.IsTrue(middleUp.SuppressOriginal);
        Assert.AreEqual(0, middleUp.Effects.Count);
    }

    [TestMethod]
    public void MiddlePlusRightProducesConfiguredPositiveFlick()
    {
        var gesture = new MiddleChordGesture(horizontalWheelDelta: 240);

        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true));
        var rightDown = gesture.Handle(new MouseButtonEvent(MouseButtonId.Right, true));

        Assert.AreEqual(240, ((InputEffect.HorizontalWheel)rightDown.Effects.Single()).Delta);
    }

    [TestMethod]
    public void OrdinaryLeftAndRightClicksPassThrough()
    {
        var gesture = new MiddleChordGesture();

        var leftDown = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, true));
        var leftUp = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, false));
        var rightDown = gesture.Handle(new MouseButtonEvent(MouseButtonId.Right, true));
        var rightUp = gesture.Handle(new MouseButtonEvent(MouseButtonId.Right, false));

        AssertPassThrough(leftDown);
        AssertPassThrough(leftUp);
        AssertPassThrough(rightDown);
        AssertPassThrough(rightUp);
    }

    [TestMethod]
    public void DuplicateChordDownDoesNotEmitAnotherFlick()
    {
        var gesture = new MiddleChordGesture();

        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true));
        var first = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, true));
        var duplicate = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, true));

        Assert.AreEqual(1, first.Effects.Count);
        Assert.IsTrue(duplicate.SuppressOriginal);
        Assert.AreEqual(0, duplicate.Effects.Count);
    }

    [TestMethod]
    public void DuplicateMiddleDownRemainsSuppressed()
    {
        var gesture = new MiddleChordGesture();

        var first = gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true));
        var duplicate = gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true));

        Assert.IsTrue(first.SuppressOriginal);
        Assert.IsTrue(duplicate.SuppressOriginal);
        Assert.AreEqual(0, duplicate.Effects.Count);
    }

    [TestMethod]
    public void ConsumedSideReleaseRemainsSuppressedAfterMiddleRelease()
    {
        var gesture = new MiddleChordGesture();

        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true));
        gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, true));
        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, false));
        var leftUp = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, false));

        Assert.IsTrue(leftUp.SuppressOriginal);
        Assert.AreEqual(0, leftUp.Effects.Count);
    }

    [TestMethod]
    public void BothSidesCanFlickIndependentlyWhileMiddleIsHeld()
    {
        var gesture = new MiddleChordGesture();

        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true));
        var left = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, true));
        var right = gesture.Handle(new MouseButtonEvent(MouseButtonId.Right, true));

        Assert.AreEqual(-120, ((InputEffect.HorizontalWheel)left.Effects.Single()).Delta);
        Assert.AreEqual(120, ((InputEffect.HorizontalWheel)right.Effects.Single()).Delta);
    }

    [TestMethod]
    public void NonPositiveFlickDeltaIsRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new MiddleChordGesture(0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new MiddleChordGesture(-1));
    }

    [TestMethod]
    public void CancelClearsHeldButtonsAcrossModalModeBoundary()
    {
        var gesture = new MiddleChordGesture();
        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, isDown: true));
        gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, isDown: true));

        gesture.Cancel();

        var middleUp = gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, isDown: false));
        var leftDown = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, isDown: true));

        Assert.IsFalse(middleUp.SuppressOriginal);
        Assert.IsFalse(leftDown.SuppressOriginal);
        Assert.AreEqual(0, middleUp.Effects.Count);
    }

    [TestMethod]
    public void MiddleDragReplaysDownThenUpWhileMovementPassesThrough()
    {
        var gesture = new MiddleChordGesture(dragThreshold: 8);

        var down = gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true, 100, 100));
        var smallMove = gesture.HandleMove(new MouseMoveEvent(105, 105));
        var dragMove = gesture.HandleMove(new MouseMoveEvent(108, 100));

        Assert.IsTrue(down.SuppressOriginal);
        Assert.IsFalse(smallMove.SuppressOriginal);
        Assert.AreEqual(0, smallMove.Effects.Count);
        Assert.IsTrue(dragMove.SuppressOriginal);
        Assert.AreEqual(3, dragMove.Effects.Count);
        Assert.IsInstanceOfType<InputEffect.MiddleButtonDown>(dragMove.Effects[0]);
        Assert.IsInstanceOfType<InputEffect.MouseMove>(dragMove.Effects[1]);
        var boundary = (InputEffect.MiddleDragReady)dragMove.Effects[2];
        gesture.MarkDragReady(boundary.SequenceId);

        var nextMove = gesture.HandleMove(new MouseMoveEvent(160, 140));
        var up = gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, false, 160, 140));

        Assert.IsFalse(nextMove.SuppressOriginal);
        Assert.AreEqual(0, nextMove.Effects.Count);
        Assert.IsTrue(up.SuppressOriginal);
        Assert.IsInstanceOfType<InputEffect.MiddleButtonUp>(up.Effects.Single());
    }

    [TestMethod]
    public void ChordTakesPrecedenceOverMiddleDragMovement()
    {
        var gesture = new MiddleChordGesture(dragThreshold: 1);
        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true, 0, 0));
        var flick = gesture.Handle(new MouseButtonEvent(MouseButtonId.Left, true, 1, 0));
        var move = gesture.HandleMove(new MouseMoveEvent(50, 0));

        Assert.IsInstanceOfType<InputEffect.HorizontalWheel>(flick.Effects.Single());
        Assert.IsFalse(move.SuppressOriginal);
        Assert.AreEqual(0, move.Effects.Count);
    }

    [TestMethod]
    public void LatestPendingMoveMarkerMustAckBeforeNativeMovementResumes()
    {
        var gesture = new MiddleChordGesture(dragThreshold: 8);
        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true, 0, 0));

        var first = gesture.HandleMove(new MouseMoveEvent(8, 0));
        var firstMarker = (InputEffect.MiddleDragReady)first.Effects[2];
        var second = gesture.HandleMove(new MouseMoveEvent(16, 0));
        var secondMarker = (InputEffect.MiddleDragReady)second.Effects[1];

        // Marker 1 is stale as soon as Move 2 is queued.
        gesture.MarkDragReady(firstMarker.SequenceId);
        var third = gesture.HandleMove(new MouseMoveEvent(24, 0));
        Assert.IsTrue(third.SuppressOriginal);
        var thirdMarker = (InputEffect.MiddleDragReady)third.Effects[1];

        // Marker 2 is stale as soon as Move 3 is queued.
        gesture.MarkDragReady(secondMarker.SequenceId);
        gesture.MarkDragReady(thirdMarker.SequenceId);
        var native = gesture.HandleMove(new MouseMoveEvent(32, 0));
        Assert.IsFalse(native.SuppressOriginal);
    }

    [TestMethod]
    public void ReconfigureKeepsQueuedMarkerFromUnlockingNewDrag()
    {
        var gesture = new MiddleChordGesture(dragThreshold: 8);
        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true, 0, 0));

        var oldDrag = gesture.HandleMove(new MouseMoveEvent(8, 0));
        var oldMarker = (InputEffect.MiddleDragReady)oldDrag.Effects[2];

        var release = gesture.Reconfigure(horizontalWheelDelta: 240);
        Assert.IsTrue(release.SuppressOriginal);
        Assert.IsInstanceOfType<InputEffect.MiddleButtonUp>(release.Effects.Single());
        Assert.AreEqual(240, gesture.HorizontalWheelDelta);

        gesture.Handle(new MouseButtonEvent(MouseButtonId.Middle, true, 0, 0));
        var newDrag = gesture.HandleMove(new MouseMoveEvent(8, 0));
        var newMarker = (InputEffect.MiddleDragReady)newDrag.Effects[2];

        // The old marker is still allowed to arrive after the settings swap,
        // but it must not acknowledge the new drag's replay barrier.
        gesture.MarkDragReady(oldMarker.SequenceId);
        var pending = gesture.HandleMove(new MouseMoveEvent(16, 0));
        Assert.IsTrue(pending.SuppressOriginal);
        var pendingMarker = (InputEffect.MiddleDragReady)pending.Effects[1];

        gesture.MarkDragReady(newMarker.SequenceId);
        gesture.MarkDragReady(pendingMarker.SequenceId);
        var native = gesture.HandleMove(new MouseMoveEvent(24, 0));
        Assert.IsFalse(native.SuppressOriginal);
    }

    [TestMethod]
    public void ExtremeVirtualScreenCoordinatesStillCrossDragThreshold()
    {
        var gesture = new MiddleChordGesture(dragThreshold: 8);
        gesture.Handle(new MouseButtonEvent(
            MouseButtonId.Middle,
            isDown: true,
            int.MinValue,
            int.MinValue));

        var decision = gesture.HandleMove(new MouseMoveEvent(int.MaxValue, int.MaxValue));

        Assert.IsTrue(decision.SuppressOriginal);
        Assert.IsInstanceOfType<InputEffect.MiddleButtonDown>(decision.Effects[0]);
        Assert.IsInstanceOfType<InputEffect.MouseMove>(decision.Effects[1]);
        Assert.IsInstanceOfType<InputEffect.MiddleDragReady>(decision.Effects[2]);
    }

    [TestMethod]
    public void InvalidDragThresholdIsRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new MiddleChordGesture(dragThreshold: 0));
    }

    [TestMethod]
    public void UnknownMouseButtonIsRejectedAtEventBoundary()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new MouseButtonEvent((MouseButtonId)99, isDown: true));
    }

    private static void AssertPassThrough(InputHandlingDecision decision)
    {
        Assert.IsFalse(decision.SuppressOriginal);
        Assert.AreEqual(0, decision.Effects.Count);
    }
}
