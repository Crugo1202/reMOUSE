namespace ReMouse.Core.Input;

public interface IRadialMenuActionExecutor
{
    ValueTask ExecuteAsync(
        RadialMenuAction action,
        CancellationToken cancellationToken = default);
}
