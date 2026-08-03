using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.InputProbe.Tests;

[TestClass]
public sealed class ProbeEventProcessorMappingTests
{
    [TestMethod]
    public async Task ProbeOutputKeepsRawIdAndShowsDefaultBinding()
    {
        using var events = new ProbeEventChannel();
        var output = new StringWriter(new StringBuilder());
        var warnings = new StringWriter(new StringBuilder());
        var options = new ProbeOptions(KeyboardLoggingMode.None, false, false)
        {
            ShowBindings = true
        };
        await using var processor = new ProbeEventProcessor(
            events,
            options,
            new SideButtonMapper(DefaultSettings.SideButtonBindings),
            output,
            warnings);

        processor.Start();
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton1, 0, 0, false, 1));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton1, 0, 0, false, 2));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton2, 0, 0, false, 3));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton2, 0, 0, false, 4));
        await processor.StopAsync();

        Assert.AreEqual(
            "Raw XButton1 Down | Binding: PixelInspector" + Environment.NewLine +
            "Raw XButton1 Up | Binding: PixelInspector" + Environment.NewLine +
            "Raw XButton2 Down | Binding: RadialMenu" + Environment.NewLine +
            "Raw XButton2 Up | Binding: RadialMenu" + Environment.NewLine,
            output.ToString());
        Assert.AreEqual(string.Empty, warnings.ToString());
    }

    [TestMethod]
    public async Task ProbeOutputUsesOriginalBindingForUpAfterRuntimeSwap()
    {
        using var events = new ProbeEventChannel();
        var output = new StringWriter(new StringBuilder());
        var warnings = new StringWriter(new StringBuilder());
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);
        var options = new ProbeOptions(KeyboardLoggingMode.None, false, false)
        {
            ShowBindings = true
        };
        await using var processor = new ProbeEventProcessor(events, options, mapper, output, warnings);
        var swappedSettings = new ReMouseSettings(
            DefaultSettings.CurrentSchemaVersion,
            DefaultSettings.SideButtonBindings.Swap());
        var applied = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        processor.Start();
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton1, 0, 0, false, 1));
        events.TryWrite(swappedSettings, applied);
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton1, 0, 0, false, 2));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton1, 0, 0, false, 3));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton1, 0, 0, false, 4));
        await processor.StopAsync();

        Assert.IsTrue(applied.Task.IsCompletedSuccessfully);
        Assert.AreEqual(
            "Raw XButton1 Down | Binding: PixelInspector" + Environment.NewLine +
            "Raw XButton1 Up | Binding: PixelInspector" + Environment.NewLine +
            "Raw XButton1 Down | Binding: RadialMenu" + Environment.NewLine +
            "Raw XButton1 Up | Binding: RadialMenu" + Environment.NewLine,
            output.ToString());
        Assert.AreEqual(string.Empty, warnings.ToString());
    }

    [TestMethod]
    public async Task InjectedXButtonOutputAlsoShowsBindingWithoutTouchingPhysicalState()
    {
        using var events = new ProbeEventChannel();
        var output = new StringWriter(new StringBuilder());
        var warnings = new StringWriter(new StringBuilder());
        var options = new ProbeOptions(KeyboardLoggingMode.None, true, false)
        {
            ShowBindings = true
        };
        await using var processor = new ProbeEventProcessor(
            events,
            options,
            new SideButtonMapper(DefaultSettings.SideButtonBindings),
            output,
            warnings);

        processor.Start();
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton1, 0, 0, true, 1));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton1, 0, 0, true, 2));
        await processor.StopAsync();

        Assert.AreEqual(
            "[Injected] Raw XButton1 Down | Binding: PixelInspector" + Environment.NewLine +
            "[Injected] Raw XButton1 Up | Binding: PixelInspector" + Environment.NewLine,
            output.ToString());
        Assert.AreEqual(string.Empty, warnings.ToString());
    }
}
