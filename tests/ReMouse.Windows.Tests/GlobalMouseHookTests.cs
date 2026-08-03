using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Windows.Hooks;

namespace ReMouse.Windows.Tests;

[TestClass]
public sealed class GlobalMouseHookTests
{
    [TestMethod]
    public void XButtonMessagesPreserveRawButtonAndCoordinates()
    {
        var data = new NativeMethods.MsllHookStruct
        {
            Point = new NativeMethods.Point { X = 320, Y = 240 },
            MouseData = 0x0002u << 16,
            Time = 123
        };

        var parsed = GlobalMouseMessageParser.TryParse(
            GlobalMouseMessageParser.WmXButtonDown,
            data,
            out var mouseEvent);

        Assert.IsTrue(parsed);
        Assert.AreEqual(GlobalMouseEventKind.Button, mouseEvent.Kind);
        Assert.AreEqual(GlobalMouseButton.XButton2, mouseEvent.Button);
        Assert.IsTrue(mouseEvent.IsDown);
        Assert.AreEqual(320, mouseEvent.X);
        Assert.AreEqual(240, mouseEvent.Y);
        Assert.AreEqual((uint)123, mouseEvent.Timestamp);
    }

    [TestMethod]
    public void InjectedFlagIsCopiedToMoveAndButtonEvents()
    {
        var data = new NativeMethods.MsllHookStruct
        {
            Point = new NativeMethods.Point { X = 1, Y = 2 },
            Flags = GlobalMouseMessageParser.LlmhfInjected
        };

        Assert.IsTrue(GlobalMouseMessageParser.TryParse(
            GlobalMouseMessageParser.WmMouseMove,
            data,
            out var move));
        Assert.IsTrue(move.IsInjected);
        Assert.IsNull(move.Button);

        Assert.IsTrue(GlobalMouseMessageParser.TryParse(
            GlobalMouseMessageParser.WmMButtonUp,
            data,
            out var button));
        Assert.IsTrue(button.IsInjected);
        Assert.AreEqual(GlobalMouseButton.Middle, button.Button);
        Assert.IsFalse(button.IsDown);
    }

    [TestMethod]
    public void UnknownMessagesAndUnknownXButtonsAreIgnored()
    {
        var data = new NativeMethods.MsllHookStruct
        {
            MouseData = 0x0003u << 16
        };

        Assert.IsFalse(GlobalMouseMessageParser.TryParse(0x9999, data, out _));
        Assert.IsFalse(GlobalMouseMessageParser.TryParse(
            GlobalMouseMessageParser.WmXButtonUp,
            data,
            out _));
    }

    [TestMethod]
    public void EventContractRejectsMismatchedButtonAndMoveKinds()
    {
        Assert.ThrowsException<ArgumentException>(() => new GlobalMouseEvent(
            GlobalMouseEventKind.Button,
            null,
            isDown: true,
            0,
            0,
            isInjected: false,
            timestamp: 0));
        Assert.ThrowsException<ArgumentException>(() => new GlobalMouseEvent(
            GlobalMouseEventKind.Move,
            GlobalMouseButton.Left,
            isDown: false,
            0,
            0,
            isInjected: false,
            timestamp: 0));
        Assert.ThrowsException<ArgumentException>(() => new GlobalMouseEvent(
            GlobalMouseEventKind.Move,
            null,
            isDown: true,
            0,
            0,
            isInjected: false,
            timestamp: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new GlobalMouseEvent(
            (GlobalMouseEventKind)99,
            null,
            isDown: false,
            0,
            0,
            isInjected: false,
            timestamp: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new GlobalMouseEvent(
            GlobalMouseEventKind.Button,
            (GlobalMouseButton)99,
            isDown: true,
            0,
            0,
            isInjected: false,
            timestamp: 0));
    }

    [TestMethod]
    public void HookHostCanBeDisposedBeforeStartAndRejectsNullHandler()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new GlobalMouseHookHost(null!));

        using var host = new GlobalMouseHookHost(_ => new GlobalMouseDecision(false));
        host.Dispose();
        host.Dispose();
    }

    [TestMethod]
    public void KeyboardHookHostCanBeDisposedBeforeStartAndRejectsNullHandler()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new GlobalKeyboardHookHost(null!));

        using var host = new GlobalKeyboardHookHost(() => { });
        host.Dispose();
        host.Dispose();
    }

    [TestMethod]
    public void EmergencyPauseRequiresControlAltAndF12()
    {
        Assert.IsTrue(EmergencyPauseGesture.IsTriggered(NativeMethods.VkF12, true, true));
        Assert.IsFalse(EmergencyPauseGesture.IsTriggered(NativeMethods.VkF12, false, true));
        Assert.IsFalse(EmergencyPauseGesture.IsTriggered(NativeMethods.VkF12, true, false));
        Assert.IsFalse(EmergencyPauseGesture.IsTriggered(0x1B, true, true));
    }
}
