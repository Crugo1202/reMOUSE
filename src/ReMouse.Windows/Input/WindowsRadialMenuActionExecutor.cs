using ReMouse.Core.Input;

namespace ReMouse.Windows.Input;

public sealed class WindowsRadialMenuActionExecutor : IRadialMenuActionExecutor
{
    private readonly IInputEffectSink _effectSink;
    private readonly IProcessLauncher _processLauncher;

    public WindowsRadialMenuActionExecutor(IInputEffectSink effectSink)
        : this(effectSink, new ShellProcessLauncher())
    {
    }

    internal WindowsRadialMenuActionExecutor(
        IInputEffectSink effectSink,
        IProcessLauncher processLauncher)
    {
        _effectSink = effectSink ?? throw new ArgumentNullException(nameof(effectSink));
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
    }

    public ValueTask ExecuteAsync(
        RadialMenuAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        switch (action)
        {
            case RadialMenuAction.NoOp:
                return ValueTask.CompletedTask;

            case RadialMenuAction.LaunchApplication application:
                _processLauncher.Start(application.ExecutablePath, application.Arguments);
                return ValueTask.CompletedTask;

            case RadialMenuAction.Shortcut shortcut:
                _effectSink.Apply(shortcut.Sequence);
                return ValueTask.CompletedTask;

            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported radial action.");
        }
    }
}
