namespace ReMouse.Windows.Displays;

public readonly record struct MonitorDpiInfo(
    int Left,
    int Top,
    int Right,
    int Bottom,
    double ScaleX,
    double ScaleY,
    bool IsPrimary)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
