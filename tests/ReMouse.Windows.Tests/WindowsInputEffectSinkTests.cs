using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;
using ReMouse.Windows.Input;

namespace ReMouse.Windows.Tests;

[TestClass]
public sealed class WindowsInputEffectSinkTests
{
    [TestMethod]
    public void HorizontalWheelMapsSignedDeltaToOneMousePacket()
    {
        var sender = new RecordingSender();
        var sink = new WindowsInputEffectSink(sender);

        sink.Apply(new InputEffect.HorizontalWheel(-120));

        Assert.AreEqual(1, sender.Batches.Count);
        var packet = sender.Batches[0].Single();
        Assert.AreEqual(WindowsInputPacketKind.Mouse, packet.Kind);
        Assert.AreEqual(-120, packet.MouseData);
        Assert.AreEqual(WindowsInputFlags.MouseHorizontalWheel, packet.MouseFlags);
    }

    [TestMethod]
    public void MiddleClickMapsToDownThenUpInOneBatch()
    {
        var sender = new RecordingSender();
        var sink = new WindowsInputEffectSink(sender);

        sink.Apply(new InputEffect.MiddleClick());

        CollectionAssert.AreEqual(
            new[]
            {
                WindowsInputPacket.MiddleButton(isDown: true),
                WindowsInputPacket.MiddleButton(isDown: false)
            },
            sender.Batches.Single().ToArray());
    }

    [TestMethod]
    public void MiddleButtonBoundaryEffectsMapToSinglePackets()
    {
        var sender = new RecordingSender();
        var sink = new WindowsInputEffectSink(sender);

        sink.Apply(new InputEffect.MiddleButtonDown());
        sink.Apply(new InputEffect.MiddleButtonUp());

        CollectionAssert.AreEqual(
            new[] { WindowsInputPacket.MiddleButton(isDown: true) },
            sender.Batches[0].ToArray());
        CollectionAssert.AreEqual(
            new[] { WindowsInputPacket.MiddleButton(isDown: false) },
            sender.Batches[1].ToArray());
    }

    [TestMethod]
    public void MouseMoveReplaysAbsoluteVirtualScreenCoordinates()
    {
        var sender = new RecordingSender();
        var sink = new WindowsInputEffectSink(sender);

        sink.Apply(new InputEffect.MouseMove(-25, 300));

        var packet = sender.Batches.Single().Single();
        Assert.AreEqual(WindowsInputPacketKind.Mouse, packet.Kind);
        Assert.AreEqual(-25, packet.MouseX);
        Assert.AreEqual(300, packet.MouseY);
        Assert.AreEqual(
            WindowsInputFlags.MouseMove |
            WindowsInputFlags.MouseAbsolute |
            WindowsInputFlags.MouseVirtualDesk,
            packet.MouseFlags);
    }

    [TestMethod]
    public void DragReadyMarkerDoesNotSendNativeInput()
    {
        var sender = new RecordingSender();
        var sink = new WindowsInputEffectSink(sender);

        sink.Apply(new InputEffect.MiddleDragReady(1));

        Assert.AreEqual(0, sender.Batches.Count);
    }

    [TestMethod]
    public void KeySequencePreservesOrderAndUpFlags()
    {
        var sender = new RecordingSender();
        var sink = new WindowsInputEffectSink(sender);

        sink.Apply(new InputEffect.KeySequence(new[]
        {
            InputKeyStroke.Down(0x11),
            InputKeyStroke.Down(0x56),
            InputKeyStroke.Up(0x56),
            InputKeyStroke.Up(0x11)
        }));

        CollectionAssert.AreEqual(
            new[]
            {
                WindowsInputPacket.Key(0x11, isDown: true),
                WindowsInputPacket.Key(0x56, isDown: true),
                WindowsInputPacket.Key(0x56, isDown: false),
                WindowsInputPacket.Key(0x11, isDown: false)
            },
            sender.Batches.Single().ToArray());
    }

    [TestMethod]
    public void NullEffectIsRejectedWithoutSending()
    {
        var sender = new RecordingSender();
        var sink = new WindowsInputEffectSink(sender);

        Assert.ThrowsException<ArgumentNullException>(() => sink.Apply(null!));
        Assert.AreEqual(0, sender.Batches.Count);
    }

    [TestMethod]
    public void NativeConverterUsesCorrectInputTypes()
    {
        var mouse = NativeInputConverter.ToNative(WindowsInputPacket.HorizontalWheel(120));
        var keyboard = NativeInputConverter.ToNative(WindowsInputPacket.Key(0x11, isDown: false));

        Assert.AreEqual(NativeMethods.InputMouse, mouse.Type);
        Assert.AreEqual((uint)120, mouse.Union.MouseInput.MouseData);
        Assert.AreEqual(NativeMethods.InputKeyboard, keyboard.Type);
        Assert.AreEqual((ushort)0x11, keyboard.Union.KeyboardInput.VirtualKey);
        Assert.AreEqual(WindowsInputFlags.KeyboardKeyUp, keyboard.Union.KeyboardInput.Flags);
    }

    private sealed class RecordingSender : IWindowsInputSender
    {
        public List<IReadOnlyList<WindowsInputPacket>> Batches { get; } = new();

        public void Send(IReadOnlyList<WindowsInputPacket> packets)
        {
            Batches.Add(packets.ToArray());
        }
    }
}
