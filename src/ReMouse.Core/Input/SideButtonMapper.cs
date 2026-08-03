namespace ReMouse.Core.Input;

/// <summary>
/// Maps raw XButtons to ReMouse actions while keeping the action selected by a
/// Down event stable until its matching Up event arrives.
/// </summary>
public sealed class SideButtonMapper
{
    private readonly object _gate = new();
    private readonly SideButtonAction[] _activeActions = new SideButtonAction[3];
    private readonly bool[] _active = new bool[3];
    private SideButtonBindings _bindings;

    public SideButtonMapper(SideButtonBindings bindings)
    {
        bindings.Validate();
        _bindings = bindings;
    }

    public SideButtonBindings Bindings
    {
        get
        {
            lock (_gate)
            {
                return _bindings;
            }
        }
    }

    public void SetBindings(SideButtonBindings bindings)
    {
        bindings.Validate();
        lock (_gate)
        {
            // Active actions intentionally remain untouched. A configuration
            // change only affects the next Down event for each button.
            _bindings = bindings;
        }
    }

    public SideButtonBindings Swap()
    {
        lock (_gate)
        {
            _bindings = _bindings.Swap();
            return _bindings;
        }
    }

    public SideButtonDispatch OnDown(XButtonId button)
    {
        var index = GetIndex(button);

        lock (_gate)
        {
            if (_active[index])
            {
                return new SideButtonDispatch(
                    _activeActions[index],
                    false,
                    IsDuplicateDown: true,
                    IsOrphanUp: false);
            }

            var action = _bindings.For(button);
            _active[index] = true;
            _activeActions[index] = action;
            return new SideButtonDispatch(
                action,
                action != SideButtonAction.None,
                IsDuplicateDown: false,
                IsOrphanUp: false);
        }
    }

    public SideButtonDispatch OnUp(XButtonId button)
    {
        var index = GetIndex(button);

        lock (_gate)
        {
            if (!_active[index])
            {
                return new SideButtonDispatch(
                    SideButtonAction.None,
                    ShouldDispatch: false,
                    IsDuplicateDown: false,
                    IsOrphanUp: true);
            }

            var action = _activeActions[index];
            _active[index] = false;
            _activeActions[index] = SideButtonAction.None;
            return new SideButtonDispatch(
                action,
                action != SideButtonAction.None,
                IsDuplicateDown: false,
                IsOrphanUp: false);
        }
    }

    public SideButtonAction? GetActiveAction(XButtonId button)
    {
        var index = GetIndex(button);
        lock (_gate)
        {
            return _active[index] ? _activeActions[index] : null;
        }
    }

    private static int GetIndex(XButtonId button) => button switch
    {
        XButtonId.XButton1 => (int)XButtonId.XButton1,
        XButtonId.XButton2 => (int)XButtonId.XButton2,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown XButton.")
    };
}
