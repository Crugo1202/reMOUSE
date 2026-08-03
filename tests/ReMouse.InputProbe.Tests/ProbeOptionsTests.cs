using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ReMouse.InputProbe.Tests;

[TestClass]
public sealed class ProbeOptionsTests
{
    [TestMethod]
    public void BareRelativeSettingsPathIsAccepted()
    {
        var options = ProbeOptions.Parse(new[] { "--settings", "settings.json" });

        Assert.AreEqual("settings.json", options.SettingsPath);
        Assert.IsTrue(options.ShowBindings);
    }

    [TestMethod]
    public void SettingsOptionCannotConsumeAnotherOption()
    {
        Assert.ThrowsException<ArgumentException>(
            () => ProbeOptions.Parse(new[] { "--settings", "--keyboard" }));
    }

    [TestMethod]
    public void SettingsOptionRequiresAValue()
    {
        Assert.ThrowsException<ArgumentException>(
            () => ProbeOptions.Parse(new[] { "--settings" }));
    }

    [TestMethod]
    public void MiddleFlickOptionsParseWithConfiguredDelta()
    {
        var options = ProbeOptions.Parse(new[] { "--middle-flick", "--flick-delta", "240" });

        Assert.IsTrue(options.MiddleFlickEnabled);
        Assert.AreEqual(240, options.FlickDelta);
    }

    [TestMethod]
    public void FlickDeltaRequiresPositiveInteger()
    {
        Assert.ThrowsException<ArgumentException>(
            () => ProbeOptions.Parse(new[] { "--flick-delta", "0" }));
        Assert.ThrowsException<ArgumentException>(
            () => ProbeOptions.Parse(new[] { "--flick-delta", "-1" }));
        Assert.ThrowsException<ArgumentException>(
            () => ProbeOptions.Parse(new[] { "--flick-delta", "not-a-number" }));
    }

    [TestMethod]
    public void FlickDeltaCannotExceedCoreMaximum()
    {
        Assert.ThrowsException<ArgumentException>(
            () => ProbeOptions.Parse(new[] { "--flick-delta", "1201" }));
    }
}
