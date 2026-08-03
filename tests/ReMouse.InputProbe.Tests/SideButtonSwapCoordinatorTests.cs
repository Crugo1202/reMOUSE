using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.InputProbe.Tests;

[TestClass]
public sealed class SideButtonSwapCoordinatorTests
{
    [TestMethod]
    public async Task SuccessfulSwapPersistsAndAppliesThroughTheProcessorQueue()
    {
        using var events = new ProbeEventChannel();
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);
        await using var processor = new ProbeEventProcessor(
            events,
            new ProbeOptions(KeyboardLoggingMode.None, false, false),
            mapper,
            new StringWriter(),
            new StringWriter());
        var saved = new List<ReMouseSettings>();
        var coordinator = new SideButtonSwapCoordinator(mapper, events, saved.Add);

        processor.Start();
        var result = await coordinator.TrySwapAndSaveAsync();
        await processor.StopAsync();

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(new SideButtonBindings(SideButtonAction.RadialMenu, SideButtonAction.PixelInspector), mapper.Bindings);
        CollectionAssert.AreEqual(
            new[] { new ReMouseSettings(DefaultSettings.CurrentSchemaVersion, mapper.Bindings) },
            saved);
    }

    [TestMethod]
    public async Task SaveFailureLeavesRuntimeBindingUnchangedAndDoesNotQueueControl()
    {
        using var events = new ProbeEventChannel();
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);
        var coordinator = new SideButtonSwapCoordinator(
            mapper,
            events,
            _ => throw new IOException("simulated save failure"));

        var result = await coordinator.TrySwapAndSaveAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DefaultSettings.SideButtonBindings, mapper.Bindings);
        Assert.IsNotNull(result.Error);
        Assert.IsNull(result.RollbackError);
    }

    [TestMethod]
    public async Task RejectedQueueRollsDiskBackAndLeavesRuntimeBindingUnchanged()
    {
        using var events = new ProbeEventChannel();
        events.Complete();
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);
        var saved = new List<ReMouseSettings>();
        var coordinator = new SideButtonSwapCoordinator(mapper, events, saved.Add);

        var result = await coordinator.TrySwapAndSaveAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DefaultSettings.SideButtonBindings, mapper.Bindings);
        CollectionAssert.AreEqual(
            new[]
            {
                new ReMouseSettings(DefaultSettings.CurrentSchemaVersion, DefaultSettings.SideButtonBindings.Swap()),
                new ReMouseSettings(DefaultSettings.CurrentSchemaVersion, DefaultSettings.SideButtonBindings)
            },
            saved);
        Assert.IsNull(result.RollbackError);
    }

    [TestMethod]
    public async Task SwapPreservesCustomFlickAndRadialSettings()
    {
        using var events = new ProbeEventChannel();
        var mapper = new SideButtonMapper(DefaultSettings.SideButtonBindings);
        var custom = new ReMouseSettings(
            DefaultSettings.CurrentSchemaVersion,
            DefaultSettings.SideButtonBindings,
            new FlickSettings(240),
            new RadialMenuSettings(new[]
            {
                new RadialMenuSlotSettings(
                    "tool",
                    "Tool",
                    ConfiguredRadialActionKind.LaunchApplication,
                    executablePath: "C:\\Tools\\tool.exe",
                    arguments: "--keep")
            }));
        var saved = new List<ReMouseSettings>();
        await using var processor = new ProbeEventProcessor(
            events,
            new ProbeOptions(KeyboardLoggingMode.None, false, false),
            mapper,
            new StringWriter(),
            new StringWriter());
        var coordinator = new SideButtonSwapCoordinator(mapper, events, custom, saved.Add);

        processor.Start();
        var result = await coordinator.TrySwapAndSaveAsync();
        await processor.StopAsync();

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, saved.Count);
        Assert.AreEqual(custom.Flick, saved[0].Flick);
        Assert.AreEqual(custom.RadialMenu, saved[0].RadialMenu);
        Assert.AreEqual(custom.SideButtonBindings.Swap(), saved[0].SideButtonBindings);
    }
}
