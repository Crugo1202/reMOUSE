namespace ReMouse.Core.Input;

public enum PixelInspectorInputKind
{
    Toggle,
    Move,
    LeftButton
}

public readonly record struct PixelInspectorInput
{
    public PixelInspectorInput(
        PixelInspectorInputKind kind,
        bool isDown,
        PixelPoint point,
        PixelInspectorModifiers modifiers = PixelInspectorModifiers.None)
    {
        if (!Enum.IsDefined(typeof(PixelInspectorInputKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown pixel inspector input kind.");
        }

        if (kind == PixelInspectorInputKind.Move && isDown)
        {
            throw new ArgumentException("A move input cannot be a button-down transition.", nameof(isDown));
        }

        if (kind == PixelInspectorInputKind.Toggle && !isDown)
        {
            throw new ArgumentException("A toggle input must be a down transition.", nameof(isDown));
        }

        if ((modifiers & ~PixelInspectorModifiers.Shift) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modifiers), modifiers, "Unknown pixel inspector modifier.");
        }

        Kind = kind;
        IsDown = isDown;
        Point = point;
        Modifiers = modifiers;
    }

    public PixelInspectorInputKind Kind { get; }

    public bool IsDown { get; }

    public PixelPoint Point { get; }

    public PixelInspectorModifiers Modifiers { get; }

    public static PixelInspectorInput Toggle(PixelPoint point) =>
        new(PixelInspectorInputKind.Toggle, isDown: true, point);

    public static PixelInspectorInput Move(
        PixelPoint point,
        PixelInspectorModifiers modifiers = PixelInspectorModifiers.None) =>
        new(PixelInspectorInputKind.Move, isDown: false, point, modifiers);

    public static PixelInspectorInput LeftDown(
        PixelPoint point,
        PixelInspectorModifiers modifiers = PixelInspectorModifiers.None) =>
        new(PixelInspectorInputKind.LeftButton, isDown: true, point, modifiers);

    public static PixelInspectorInput LeftUp(
        PixelPoint point,
        PixelInspectorModifiers modifiers = PixelInspectorModifiers.None) =>
        new(PixelInspectorInputKind.LeftButton, isDown: false, point, modifiers);
}
