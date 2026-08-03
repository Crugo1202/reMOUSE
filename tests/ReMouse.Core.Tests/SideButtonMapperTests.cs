using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.Core.Tests;

[TestClass]
public sealed class SideButtonMapperTests
{
    [TestMethod]
    public void DefaultBindingsMapEachButtonToTheExpectedAction()
    {
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);

        var xButton1Down = mapper.OnDown(XButtonId.XButton1);
        var xButton1Up = mapper.OnUp(XButtonId.XButton1);
        var xButton2Down = mapper.OnDown(XButtonId.XButton2);
        var xButton2Up = mapper.OnUp(XButtonId.XButton2);

        AssertDispatch(xButton1Down, SideButtonAction.PixelInspector);
        AssertDispatch(xButton1Up, SideButtonAction.PixelInspector);
        AssertDispatch(xButton2Down, SideButtonAction.RadialMenu);
        AssertDispatch(xButton2Up, SideButtonAction.RadialMenu);
    }

    [TestMethod]
    public void SwapExchangesActionsWithoutChangingRawButtonIds()
    {
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);

        mapper.Swap();

        AssertDispatch(mapper.OnDown(XButtonId.XButton1), SideButtonAction.RadialMenu);
        AssertDispatch(mapper.OnUp(XButtonId.XButton1), SideButtonAction.RadialMenu);
        AssertDispatch(mapper.OnDown(XButtonId.XButton2), SideButtonAction.PixelInspector);
        AssertDispatch(mapper.OnUp(XButtonId.XButton2), SideButtonAction.PixelInspector);
    }

    [TestMethod]
    public void SwappingTwiceRestoresDefaultBindings()
    {
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);

        mapper.Swap();
        mapper.Swap();

        Assert.AreEqual(DefaultSettings.SideButtonBindings, mapper.Bindings);
    }

    [TestMethod]
    public void SwapWhileHeldKeepsDownAndUpOnTheOriginalAction()
    {
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);

        var down = mapper.OnDown(XButtonId.XButton1);
        mapper.Swap();
        var up = mapper.OnUp(XButtonId.XButton1);

        AssertDispatch(down, SideButtonAction.PixelInspector);
        AssertDispatch(up, SideButtonAction.PixelInspector);
        Assert.AreEqual(SideButtonAction.RadialMenu, mapper.OnDown(XButtonId.XButton1).Action);
        mapper.OnUp(XButtonId.XButton1);
    }

    [TestMethod]
    public void TwoButtonsRemainIndependentWhenSwappedWhileBothAreHeld()
    {
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);

        var xButton1Down = mapper.OnDown(XButtonId.XButton1);
        var xButton2Down = mapper.OnDown(XButtonId.XButton2);
        mapper.Swap();
        var xButton1Up = mapper.OnUp(XButtonId.XButton1);
        var xButton2Up = mapper.OnUp(XButtonId.XButton2);

        AssertDispatch(xButton1Down, SideButtonAction.PixelInspector);
        AssertDispatch(xButton2Down, SideButtonAction.RadialMenu);
        AssertDispatch(xButton1Up, SideButtonAction.PixelInspector);
        AssertDispatch(xButton2Up, SideButtonAction.RadialMenu);
    }

    [TestMethod]
    public void DuplicateDownIsNotDispatchedTwiceAndOrphanUpIsReported()
    {
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);

        var firstDown = mapper.OnDown(XButtonId.XButton1);
        var duplicateDown = mapper.OnDown(XButtonId.XButton1);
        var up = mapper.OnUp(XButtonId.XButton1);
        var orphanUp = mapper.OnUp(XButtonId.XButton1);

        AssertDispatch(firstDown, SideButtonAction.PixelInspector);
        Assert.IsFalse(duplicateDown.ShouldDispatch);
        Assert.IsTrue(duplicateDown.IsDuplicateDown);
        AssertDispatch(up, SideButtonAction.PixelInspector);
        Assert.IsFalse(orphanUp.ShouldDispatch);
        Assert.IsTrue(orphanUp.IsOrphanUp);
    }

    private static void AssertDispatch(SideButtonDispatch dispatch, SideButtonAction action)
    {
        Assert.AreEqual(action, dispatch.Action);
        Assert.IsTrue(dispatch.ShouldDispatch);
        Assert.IsFalse(dispatch.IsDuplicateDown);
        Assert.IsFalse(dispatch.IsOrphanUp);
    }
}
