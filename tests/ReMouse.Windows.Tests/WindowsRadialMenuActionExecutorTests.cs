using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;
using ReMouse.Windows.Input;

namespace ReMouse.Windows.Tests;

[TestClass]
public sealed class WindowsRadialMenuActionExecutorTests
{
    [TestMethod]
    public async Task LaunchApplicationUsesStructuredPathAndArguments()
    {
        var launcher = new RecordingLauncher();
        var sink = new RecordingSink();
        var executor = new WindowsRadialMenuActionExecutor(sink, launcher);

        await executor.ExecuteAsync(new RadialMenuAction.LaunchApplication(
            "C:\\Program Files\\Design\\design.exe",
            "--project \"draft 1\""));

        Assert.AreEqual(1, launcher.Calls.Count);
        Assert.AreEqual("C:\\Program Files\\Design\\design.exe", launcher.Calls[0].Path);
        Assert.AreEqual("--project \"draft 1\"", launcher.Calls[0].Arguments);
        Assert.AreEqual(0, sink.Effects.Count);
    }

    [TestMethod]
    public async Task ShortcutIsSentThroughTheEffectSink()
    {
        var launcher = new RecordingLauncher();
        var sink = new RecordingSink();
        var executor = new WindowsRadialMenuActionExecutor(sink, launcher);
        var sequence = new InputEffect.KeySequence(new[]
        {
            InputKeyStroke.Down(0x11),
            InputKeyStroke.Down(0x43),
            InputKeyStroke.Up(0x43),
            InputKeyStroke.Up(0x11)
        });

        await executor.ExecuteAsync(new RadialMenuAction.Shortcut(sequence));

        Assert.AreEqual(1, sink.Effects.Count);
        Assert.AreSame(sequence, sink.Effects[0]);
        Assert.AreEqual(0, launcher.Calls.Count);
    }

    [TestMethod]
    public async Task NoOpDoesNothing()
    {
        var launcher = new RecordingLauncher();
        var sink = new RecordingSink();
        var executor = new WindowsRadialMenuActionExecutor(sink, launcher);

        await executor.ExecuteAsync(new RadialMenuAction.NoOp());

        Assert.AreEqual(0, launcher.Calls.Count);
        Assert.AreEqual(0, sink.Effects.Count);
    }

    [TestMethod]
    public async Task CancellationIsCheckedBeforeExecutingAnAction()
    {
        var launcher = new RecordingLauncher();
        var sink = new RecordingSink();
        var executor = new WindowsRadialMenuActionExecutor(sink, launcher);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(
                new RadialMenuAction.LaunchApplication("tool.exe"),
                cancellation.Token).AsTask());

        Assert.AreEqual(0, launcher.Calls.Count);
    }

    [TestMethod]
    public void ShellLauncherActivatesExistingApplicationBeforeStartingAnotherCopy()
    {
        var activator = new RecordingActivator(activated: true);
        var launcher = new ShellProcessLauncher(activator);

        launcher.Start("C:\\Design\\design.exe", "--draft");

        Assert.AreEqual(1, activator.Paths.Count);
        Assert.AreEqual("C:\\Design\\design.exe", activator.Paths[0]);
    }

    private sealed class RecordingLauncher : IProcessLauncher
    {
        public List<(string Path, string Arguments)> Calls { get; } = new();

        public void Start(string executablePath, string arguments) =>
            Calls.Add((executablePath, arguments));
    }

    private sealed class RecordingSink : IInputEffectSink
    {
        public List<InputEffect> Effects { get; } = new();

        public void Apply(InputEffect effect) => Effects.Add(effect);
    }

    private sealed class RecordingActivator : IRunningApplicationActivator
    {
        private readonly bool _activated;

        public RecordingActivator(bool activated)
        {
            _activated = activated;
        }

        public List<string> Paths { get; } = new();

        public bool TryActivate(string executablePath)
        {
            Paths.Add(executablePath);
            return _activated;
        }
    }
}
