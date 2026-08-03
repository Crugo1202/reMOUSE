using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;

namespace ReMouse.Core.Tests;

[TestClass]
public sealed class InputEffectTests
{
    [TestMethod]
    public void HorizontalWheelKeepsSignedDelta()
    {
        var effect = new InputEffect.HorizontalWheel(-120);

        Assert.AreEqual(-120, effect.Delta);
    }

    [TestMethod]
    public void ZeroHorizontalWheelIsRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new InputEffect.HorizontalWheel(0));
    }

    [TestMethod]
    public void MiddleClickIsASeparateEffect()
    {
        Assert.IsInstanceOfType<InputEffect>(new InputEffect.MiddleClick());
    }

    [TestMethod]
    public void KeySequencePreservesExactStrokeOrder()
    {
        var sequence = new InputEffect.KeySequence(new[]
        {
            InputKeyStroke.Down(0x11),
            InputKeyStroke.Down(0x56),
            InputKeyStroke.Up(0x56),
            InputKeyStroke.Up(0x11)
        });

        CollectionAssert.AreEqual(
            new[]
            {
                InputKeyStroke.Down(0x11),
                InputKeyStroke.Down(0x56),
                InputKeyStroke.Up(0x56),
                InputKeyStroke.Up(0x11)
            },
            sequence.Strokes.ToArray());
    }

    [TestMethod]
    public void EmptyKeySequenceIsRejected()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new InputEffect.KeySequence(Array.Empty<InputKeyStroke>()));
    }

    [TestMethod]
    public void KeySequenceTakesADefensiveSnapshot()
    {
        var source = new List<InputKeyStroke>
        {
            InputKeyStroke.Down(0x11),
            InputKeyStroke.Up(0x11)
        };

        var sequence = new InputEffect.KeySequence(source);
        source.Clear();

        Assert.AreEqual(2, sequence.Strokes.Count);
    }

    [TestMethod]
    public void ZeroVirtualKeyIsRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => InputKeyStroke.Down(0));
    }

    [TestMethod]
    public void OrphanKeyUpIsRejected()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new InputEffect.KeySequence(new[] { InputKeyStroke.Up(0x11) }));
    }

    [TestMethod]
    public void DuplicateKeyDownIsRejected()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new InputEffect.KeySequence(new[]
            {
                InputKeyStroke.Down(0x11),
                InputKeyStroke.Down(0x11),
                InputKeyStroke.Up(0x11),
                InputKeyStroke.Up(0x11)
            }));
    }

    [TestMethod]
    public void MissingKeyUpIsRejected()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new InputEffect.KeySequence(new[] { InputKeyStroke.Down(0x11) }));
    }
}
