namespace ReMouse.Core.Input;

public readonly record struct SideButtonDispatch(
    SideButtonAction Action,
    bool ShouldDispatch,
    bool IsDuplicateDown,
    bool IsOrphanUp);
