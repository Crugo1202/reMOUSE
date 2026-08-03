using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shapes;
using ReMouse.Core.Input;

namespace ReMouse.App;

/// <summary>
/// Click-through full-virtual-screen visual for the pixel inspector. Raw hook
/// coordinates remain in physical pixels for the labels; the mapper is used
/// only to place WPF visuals in device-independent pixels.
/// </summary>
public partial class PixelInspectorOverlayWindow : Window, IPixelInspectorOverlayView
{
    private const int GwlExStyle = -20;
    private const nint WsExTransparent = 0x00000020;
    private const nint WsExNoActivate = 0x08000000;
    private const nint WsExToolWindow = 0x00000080;
    private const int WmNcHitTest = 0x0084;
    private const int WmMouseActivate = 0x0021;
    private const nint HtTransparent = -1;
    private const nint MaNoActivate = 3;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    private readonly Func<int, int, ScreenPoint> _coordinateMapper;
    private readonly Func<int, int, int, int, (double Left, double Top, double Right, double Bottom)>? _virtualBoundsProvider;
    private readonly double _virtualLeft;
    private readonly double _virtualTop;
    private readonly double _virtualWidth;
    private readonly double _virtualHeight;
    private HwndSource? _source;

    public PixelInspectorOverlayWindow(
        Func<int, int, ScreenPoint> coordinateMapper,
        Func<int, int, int, int, (double Left, double Top, double Right, double Bottom)>? virtualBoundsProvider = null)
    {
        _coordinateMapper = coordinateMapper ?? throw new ArgumentNullException(nameof(coordinateMapper));
        _virtualBoundsProvider = virtualBoundsProvider;
        InitializeComponent();
        RootCanvas.IsHitTestVisible = false;

        var rawLeft = GetSystemMetrics(SmXVirtualScreen);
        var rawTop = GetSystemMetrics(SmYVirtualScreen);
        var rawRight = rawLeft + GetSystemMetrics(SmCxVirtualScreen);
        var rawBottom = rawTop + GetSystemMetrics(SmCyVirtualScreen);
        var bounds = _virtualBoundsProvider?.Invoke(rawLeft, rawTop, rawRight, rawBottom)
            ?? GetMappedCornerBounds(rawLeft, rawTop, rawRight, rawBottom);
        _virtualLeft = bounds.Left;
        _virtualTop = bounds.Top;
        _virtualWidth = Math.Max(1, bounds.Right - bounds.Left);
        _virtualHeight = Math.Max(1, bounds.Bottom - bounds.Top);

        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        SetWindowLongPtr(
            hwnd,
            GwlExStyle,
            new IntPtr(styles | WsExTransparent | WsExNoActivate | WsExToolWindow));

        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WindowProc);
    }

    private static nint WindowProc(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmNcHitTest)
        {
            handled = true;
            return HtTransparent;
        }

        if (message == WmMouseActivate)
        {
            handled = true;
            return MaNoActivate;
        }

        return 0;
    }

    public void Show(PixelInspectorSnapshot snapshot)
    {
        Width = _virtualWidth;
        Height = _virtualHeight;
        Left = _virtualLeft;
        Top = _virtualTop;
        Render(snapshot);

        if (!IsVisible)
        {
            base.Show();
        }
    }

    public void Update(PixelInspectorSnapshot snapshot)
    {
        if (!IsVisible)
        {
            Show(snapshot);
            return;
        }

        Render(snapshot);
    }

    public void Dismiss()
    {
        if (IsVisible)
        {
            base.Hide();
        }
    }

    private void Render(PixelInspectorSnapshot snapshot)
    {
        var cursor = ToRelativeDip(snapshot.Cursor);
        CursorText.Text = $"X {snapshot.Cursor.X}   Y {snapshot.Cursor.Y}";
        SelectionText.Text = snapshot.Selection is { } selection
            ? $"TL ({selection.TopLeft.X}, {selection.TopLeft.Y})   TR ({selection.TopRight.X}, {selection.TopRight.Y})\n" +
              $"BL ({selection.BottomLeft.X}, {selection.BottomLeft.Y})   BR ({selection.BottomRight.X}, {selection.BottomRight.Y})\n" +
              $"W {selection.Width}   H {selection.Height}"
            : "Drag with the left button to measure a rectangle.";

        var topLeft = snapshot.Selection is { } rectangle
            ? ToRelativeDip(rectangle.TopLeft)
            : default;
        var bottomRight = snapshot.Selection is { } selected
            ? ToRelativeDip(selected.BottomRight)
            : default;
        if (snapshot.Selection is null)
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
        }
        else
        {
            SelectionRectangle.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionRectangle, topLeft.X);
            Canvas.SetTop(SelectionRectangle, topLeft.Y);
            SelectionRectangle.Width = Math.Max(1, bottomRight.X - topLeft.X);
            SelectionRectangle.Height = Math.Max(1, bottomRight.Y - topLeft.Y);
        }

        var panelLeft = Math.Min(Math.Max(0, cursor.X + 16), Math.Max(0, _virtualWidth - InfoPanel.ActualWidth - 12));
        var panelTop = Math.Min(Math.Max(0, cursor.Y + 16), Math.Max(0, _virtualHeight - InfoPanel.ActualHeight - 12));
        Canvas.SetLeft(InfoPanel, panelLeft);
        Canvas.SetTop(InfoPanel, panelTop);
    }

    private ScreenPoint ToRelativeDip(PixelPoint point)
    {
        var dip = _coordinateMapper(point.X, point.Y);
        return new ScreenPoint(dip.X - _virtualLeft, dip.Y - _virtualTop);
    }

    private (double Left, double Top, double Right, double Bottom) GetMappedCornerBounds(
        int rawLeft,
        int rawTop,
        int rawRight,
        int rawBottom)
    {
        var mappedCorners = new[]
        {
            _coordinateMapper(rawLeft, rawTop),
            _coordinateMapper(rawRight, rawTop),
            _coordinateMapper(rawLeft, rawBottom),
            _coordinateMapper(rawRight, rawBottom)
        };
        return (
            mappedCorners.Min(point => point.X),
            mappedCorners.Min(point => point.Y),
            mappedCorners.Max(point => point.X),
            mappedCorners.Max(point => point.Y));
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
