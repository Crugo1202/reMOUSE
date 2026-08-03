using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;

namespace ReMouse.Core.Tests;

[TestClass]
public sealed class PixelInspectorTests
{
    [TestMethod]
    public void LowerSideToggleOpensAndClosesInspector()
    {
        var session = new PixelInspectorSession();

        var opened = session.Handle(PixelInspectorInput.Toggle(new PixelPoint(100, 200)));
        var closed = session.Handle(PixelInspectorInput.Toggle(new PixelPoint(110, 210)));

        Assert.IsTrue(opened.SuppressOriginal);
        Assert.IsTrue(opened.ModeChanged);
        Assert.IsTrue(opened.Snapshot.IsActive);
        Assert.IsTrue(closed.SuppressOriginal);
        Assert.IsTrue(closed.ModeChanged);
        Assert.IsFalse(closed.Snapshot.IsActive);
        Assert.IsFalse(session.IsActive);
    }

    [TestMethod]
    public void ActiveMoveTracksCursorWithoutSuppressingMovement()
    {
        var session = new PixelInspectorSession();
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(0, 0)));

        var update = session.Handle(PixelInspectorInput.Move(new PixelPoint(321, 456)));

        Assert.IsTrue(update.Snapshot.IsActive);
        Assert.AreEqual(new PixelPoint(321, 456), update.Snapshot.Cursor);
        Assert.IsFalse(update.SuppressOriginal);
    }

    [TestMethod]
    public void LeftDragProducesNormalizedCornersAndDimensions()
    {
        var session = new PixelInspectorSession();
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(0, 0)));
        session.Handle(PixelInspectorInput.LeftDown(new PixelPoint(300, 400)));
        session.Handle(PixelInspectorInput.Move(new PixelPoint(120, 180)));

        var completed = session.Handle(PixelInspectorInput.LeftUp(new PixelPoint(120, 180)));
        var rectangle = completed.Snapshot.Selection!.Value;

        Assert.IsTrue(completed.SuppressOriginal);
        Assert.IsTrue(completed.SelectionCompleted);
        Assert.AreEqual(new PixelPoint(120, 180), rectangle.TopLeft);
        Assert.AreEqual(new PixelPoint(300, 180), rectangle.TopRight);
        Assert.AreEqual(new PixelPoint(120, 400), rectangle.BottomLeft);
        Assert.AreEqual(new PixelPoint(300, 400), rectangle.BottomRight);
        Assert.AreEqual(180, rectangle.Width);
        Assert.AreEqual(220, rectangle.Height);
    }

    [TestMethod]
    public void ShiftConstrainsSelectionToNearestFortyFiveDegreeAxis()
    {
        var session = new PixelInspectorSession();
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(100, 100)));
        session.Handle(PixelInspectorInput.LeftDown(new PixelPoint(100, 100)));

        var completed = session.Handle(PixelInspectorInput.LeftUp(
            new PixelPoint(160, 145),
            PixelInspectorModifiers.Shift));
        var rectangle = completed.Snapshot.Selection!.Value;

        Assert.AreEqual(rectangle.Width, rectangle.Height);
        Assert.IsTrue(rectangle.Width > 0);
    }

    [TestMethod]
    public void ActiveLeftButtonsAreConsumedButInactiveButtonsPassThrough()
    {
        var session = new PixelInspectorSession();

        Assert.IsFalse(session.Handle(PixelInspectorInput.LeftDown(new PixelPoint(1, 1))).SuppressOriginal);
        Assert.IsFalse(session.Handle(PixelInspectorInput.LeftUp(new PixelPoint(1, 1))).SuppressOriginal);
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(1, 1)));
        Assert.IsTrue(session.Handle(PixelInspectorInput.LeftDown(new PixelPoint(1, 1))).SuppressOriginal);
        Assert.IsTrue(session.Handle(PixelInspectorInput.LeftUp(new PixelPoint(2, 2))).SuppressOriginal);
    }

    [TestMethod]
    public void ActiveOrphanLeftUpPassesThrough()
    {
        var session = new PixelInspectorSession();
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(0, 0)));

        var decision = session.Handle(PixelInspectorInput.LeftUp(new PixelPoint(10, 10)));

        Assert.IsFalse(decision.SuppressOriginal);
        Assert.IsFalse(decision.SelectionCompleted);
    }

    [TestMethod]
    public void DownBeforeToggleKeepsItsMatchingUpPassThrough()
    {
        var session = new PixelInspectorSession();
        session.Handle(PixelInspectorInput.LeftDown(new PixelPoint(1, 1)));
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(2, 2)));

        var decision = session.Handle(PixelInspectorInput.LeftUp(new PixelPoint(3, 3)));

        Assert.IsFalse(decision.SuppressOriginal);
    }

    [TestMethod]
    public void DuplicateActiveDownDoesNotMoveSelectionAnchor()
    {
        var session = new PixelInspectorSession();
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(0, 0)));
        session.Handle(PixelInspectorInput.LeftDown(new PixelPoint(100, 100)));
        session.Handle(PixelInspectorInput.LeftDown(new PixelPoint(300, 300)));

        var completed = session.Handle(PixelInspectorInput.LeftUp(new PixelPoint(120, 130)));

        Assert.AreEqual(new PixelPoint(100, 100), completed.Snapshot.Selection!.Value.TopLeft);
        Assert.AreEqual(new PixelPoint(120, 130), completed.Snapshot.Selection.Value.BottomRight);
    }

    [TestMethod]
    public void ConsumedDownStillSuppressesLateUpAfterToggleClose()
    {
        var session = new PixelInspectorSession();
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(0, 0)));
        Assert.IsTrue(session.Handle(PixelInspectorInput.LeftDown(new PixelPoint(10, 10))).SuppressOriginal);
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(20, 20)));

        var lateUp = session.Handle(PixelInspectorInput.LeftUp(new PixelPoint(30, 30)));

        Assert.IsTrue(lateUp.SuppressOriginal);
        Assert.IsFalse(lateUp.SelectionCompleted);
        Assert.IsFalse(session.IsActive);
    }

    [TestMethod]
    public void ClosingInspectorClearsPreviousSelection()
    {
        var session = new PixelInspectorSession();
        session.Handle(PixelInspectorInput.Toggle(new PixelPoint(0, 0)));
        session.Handle(PixelInspectorInput.LeftDown(new PixelPoint(0, 0)));
        session.Handle(PixelInspectorInput.LeftUp(new PixelPoint(10, 10)));
        var closed = session.Handle(PixelInspectorInput.Toggle(new PixelPoint(20, 20)));
        var reopened = session.Handle(PixelInspectorInput.Toggle(new PixelPoint(30, 30)));

        Assert.IsNull(closed.Snapshot.Selection);
        Assert.IsTrue(reopened.Snapshot.IsActive);
        Assert.IsNull(reopened.Snapshot.Selection);
    }

    [TestMethod]
    public void PixelRectangleRejectsUnnormalizedConstructorCorners()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new PixelRectangle(new PixelPoint(10, 10), new PixelPoint(0, 0)));
        Assert.AreEqual(10, PixelRectangle.FromCorners(new PixelPoint(10, 10), new PixelPoint(0, 0)).Width);
    }

    [TestMethod]
    public void InputContractRejectsInvalidKindsAndTransitions()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new PixelInspectorInput(PixelInspectorInputKind.Move, isDown: true, new PixelPoint(0, 0)));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new PixelInspectorInput((PixelInspectorInputKind)99, isDown: false, new PixelPoint(0, 0)));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            PixelInspectorInput.Move(new PixelPoint(0, 0), (PixelInspectorModifiers)2));
    }

    [TestMethod]
    public void ClipboardTextIncludesCursorAndAllRectangleCorners()
    {
        var snapshot = new PixelInspectorSnapshot(
            true,
            new PixelPoint(20, 30),
            new PixelPoint(10, 15),
            PixelRectangle.FromCorners(new PixelPoint(10, 15), new PixelPoint(40, 55)));

        var text = PixelInspectorClipboardText.Format(snapshot);

        StringAssert.Contains(text, "Cursor X=20 Y=30");
        StringAssert.Contains(text, "TL (10, 15)");
        StringAssert.Contains(text, "TR (40, 15)");
        StringAssert.Contains(text, "BL (10, 55)");
        StringAssert.Contains(text, "BR (40, 55)");
        StringAssert.Contains(text, "W 30 H 40");
    }
}
