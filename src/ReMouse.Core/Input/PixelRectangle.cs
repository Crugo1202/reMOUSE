namespace ReMouse.Core.Input;

public readonly record struct PixelRectangle
{
    public PixelRectangle(PixelPoint topLeft, PixelPoint bottomRight)
    {
        if (topLeft.X > bottomRight.X || topLeft.Y > bottomRight.Y)
        {
            throw new ArgumentException("Pixel rectangle corners must already be normalized.");
        }

        TopLeft = topLeft;
        BottomRight = bottomRight;
    }

    public PixelPoint TopLeft { get; }

    public PixelPoint BottomRight { get; }

    public PixelPoint TopRight => new(BottomRight.X, TopLeft.Y);

    public PixelPoint BottomLeft => new(TopLeft.X, BottomRight.Y);

    public int Width => BottomRight.X - TopLeft.X;

    public int Height => BottomRight.Y - TopLeft.Y;

    public static PixelRectangle FromCorners(PixelPoint first, PixelPoint second) =>
        new(
            new PixelPoint(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y)),
            new PixelPoint(Math.Max(first.X, second.X), Math.Max(first.Y, second.Y)));
}
