namespace ReMouse.Windows.Hooks;

public enum GlobalMouseEventKind
{
    Move,
    Button
}

public enum GlobalMouseButton
{
    Left,
    Right,
    Middle,
    XButton1,
    XButton2
}

public readonly record struct GlobalMouseEvent
{
    public GlobalMouseEvent(
        GlobalMouseEventKind kind,
        GlobalMouseButton? button,
        bool isDown,
        int x,
        int y,
        bool isInjected,
        uint timestamp)
    {
        if (kind is not GlobalMouseEventKind.Move and not GlobalMouseEventKind.Button)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown global mouse event kind.");
        }

        if (button is { } buttonValue &&
            !Enum.IsDefined(typeof(GlobalMouseButton), buttonValue))
        {
            throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown global mouse button.");
        }

        if (kind == GlobalMouseEventKind.Button && button is null)
        {
            throw new ArgumentException("A button event must identify a mouse button.", nameof(button));
        }

        if (kind == GlobalMouseEventKind.Move && button is not null)
        {
            throw new ArgumentException("A move event cannot identify a mouse button.", nameof(button));
        }

        if (kind == GlobalMouseEventKind.Move && isDown)
        {
            throw new ArgumentException("A move event cannot be a button-down transition.", nameof(isDown));
        }

        Kind = kind;
        Button = button;
        IsDown = isDown;
        X = x;
        Y = y;
        IsInjected = isInjected;
        Timestamp = timestamp;
    }

    public GlobalMouseEventKind Kind { get; }

    public GlobalMouseButton? Button { get; }

    public bool IsDown { get; }

    public int X { get; }

    public int Y { get; }

    public bool IsInjected { get; }

    public uint Timestamp { get; }
}

public readonly record struct GlobalMouseDecision(bool SuppressOriginal);
