using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;

namespace ReMouse.InputProbe.Tests;

[TestClass]
public sealed class ProbeEventProcessorTests
{
    [TestMethod]
    public async Task OneClickProducesExactlyOneDownAndOneUp()
    {
        using var events = new ProbeEventChannel();
        var output = new StringWriter(new StringBuilder());
        var warnings = new StringWriter(new StringBuilder());
        await using var processor = new ProbeEventProcessor(
            events,
            new ProbeOptions(KeyboardLoggingMode.None, false, false),
            output,
            warnings);

        processor.Start();
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton1, 0, 0, false, 1));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton1, 0, 0, false, 2));
        await processor.StopAsync();

        Assert.AreEqual("XButton1 Down\r\nXButton1 Up\r\n", output.ToString());
        Assert.AreEqual(string.Empty, warnings.ToString());
    }

    [TestMethod]
    public async Task TwoButtonsKeepIndependentState()
    {
        using var events = new ProbeEventChannel();
        var output = new StringWriter(new StringBuilder());
        var warnings = new StringWriter(new StringBuilder());
        await using var processor = new ProbeEventProcessor(
            events,
            new ProbeOptions(KeyboardLoggingMode.None, false, false),
            output,
            warnings);

        processor.Start();
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton1, 0, 0, false, 1));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton2, 0, 0, false, 2));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton1, 0, 0, false, 3));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton2, 0, 0, false, 4));
        await processor.StopAsync();

        Assert.AreEqual(
            "XButton1 Down\r\nXButton2 Down\r\nXButton1 Up\r\nXButton2 Up\r\n",
            output.ToString());
        Assert.AreEqual(string.Empty, warnings.ToString());
    }

    [TestMethod]
    public async Task DuplicateDownAndOrphanUpAreReportedButNotGenerated()
    {
        using var events = new ProbeEventChannel();
        var output = new StringWriter(new StringBuilder());
        var warnings = new StringWriter(new StringBuilder());
        await using var processor = new ProbeEventProcessor(
            events,
            new ProbeOptions(KeyboardLoggingMode.None, false, false),
            output,
            warnings);

        processor.Start();
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton2, 0, 0, false, 1));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton2, 0, 0, false, 2));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton2, 0, 0, false, 3));
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton2, 0, 0, false, 4));
        await processor.StopAsync();

        Assert.AreEqual(2, output.ToString().Split("XButton2 Down").Length - 1);
        Assert.AreEqual(2, output.ToString().Split("XButton2 Up").Length - 1);
        StringAssert.Contains(warnings.ToString(), "duplicate XButton2 Down");
        StringAssert.Contains(warnings.ToString(), "XButton2 Up without");
    }

    [TestMethod]
    public async Task TwentyRapidClicksProduceTwentyPairsInOrder()
    {
        using var events = new ProbeEventChannel();
        var output = new StringWriter(new StringBuilder());
        var warnings = new StringWriter(new StringBuilder());
        await using var processor = new ProbeEventProcessor(
            events,
            new ProbeOptions(KeyboardLoggingMode.None, false, false),
            output,
            warnings);

        processor.Start();
        for (var i = 0; i < 20; i++)
        {
            events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton1, 0, 0, false, (uint)(i * 2)));
            events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton1, 0, 0, false, (uint)(i * 2 + 1)));
        }

        await processor.StopAsync();

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(40, lines.Length);
        for (var i = 0; i < lines.Length; i += 2)
        {
            Assert.AreEqual("XButton1 Down", lines[i]);
            Assert.AreEqual("XButton1 Up", lines[i + 1]);
        }

        Assert.AreEqual(string.Empty, warnings.ToString());
    }

    [TestMethod]
    public async Task LongHoldProducesOneDownAndOneUp()
    {
        using var events = new ProbeEventChannel();
        var output = new StringWriter(new StringBuilder());
        var warnings = new StringWriter(new StringBuilder());
        await using var processor = new ProbeEventProcessor(
            events,
            new ProbeOptions(KeyboardLoggingMode.None, false, false),
            output,
            warnings);

        processor.Start();
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, true, XButtonId.XButton2, 0, 0, false, 1));
        await Task.Delay(25);
        events.TryWrite(new ProbeEvent(ProbeEventKind.XButton, false, XButtonId.XButton2, 0, 0, false, 2));
        await processor.StopAsync();

        Assert.AreEqual("XButton2 Down" + Environment.NewLine + "XButton2 Up" + Environment.NewLine, output.ToString());
        Assert.AreEqual(string.Empty, warnings.ToString());
    }
}
