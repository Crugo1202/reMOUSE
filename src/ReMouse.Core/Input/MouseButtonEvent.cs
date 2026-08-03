namespace ReMouse.Core.Input;

public readonly record struct MouseButtonEvent
{
    public MouseButtonEvent(MouseButtonId button, bool isDown)
        : this(button, isDown, 0, 0)
    {
    }

    public MouseButtonEvent(MouseButtonId button, bool isDown, int x, int y)
    {
        if (button is not MouseButtonId.Left and not MouseButtonId.Right and not MouseButtonId.Middle)
        {
            throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown mouse button.");
        }

        Button = button;
        IsDown = isDown;
        X = x;
        Y = y;
    }

    public MouseButtonId Button { get; }

    public bool IsDown { get; }

    public int X { get; }

    public int Y { get; }
}
