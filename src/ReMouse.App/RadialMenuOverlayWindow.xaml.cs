using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ReMouse.Core.Input;

namespace ReMouse.App;

/// <summary>
/// A click-through, topmost visual shell for the radial menu. Coordinates are
/// WPF device-independent pixels; the future hook/host must convert physical
/// cursor coordinates using the active monitor's DPI before calling Show/Update.
/// </summary>
public partial class RadialMenuOverlayWindow : Window, IRadialMenuOverlayView
{
    private const int GwlExStyle = -20;
    private const nint WsExTransparent = 0x00000020;
    private const nint WsExNoActivate = 0x08000000;
    private const nint WsExToolWindow = 0x00000080;
    private const int WmNcHitTest = 0x0084;
    private const int WmMouseActivate = 0x0021;
    private const nint HtTransparent = -1;
    private const nint MaNoActivate = 3;

    private const double MenuRadius = 112;
    private const double ItemRadius = 30;
    private readonly List<Border> _itemViews = new();
    private HwndSource? _source;

    public RadialMenuOverlayWindow()
    {
        InitializeComponent();
        RootCanvas.IsHitTestVisible = false;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        var hwnd = helper.Handle;
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

    public void Show(RadialMenuLayout layout, ScreenPoint center)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var diameter = (MenuRadius + ItemRadius + 12) * 2;
        Width = diameter;
        Height = diameter;
        Left = center.X - (diameter / 2);
        Top = center.Y - (diameter / 2);

        RootCanvas.Children.Clear();
        _itemViews.Clear();

        var centerCoordinate = diameter / 2;
        var deadZone = new Ellipse
        {
            Width = layout.DeadZoneRadius * 2,
            Height = layout.DeadZoneRadius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(220, 31, 41, 55)),
            Stroke = new SolidColorBrush(Color.FromArgb(180, 156, 163, 175)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(deadZone, centerCoordinate - layout.DeadZoneRadius);
        Canvas.SetTop(deadZone, centerCoordinate - layout.DeadZoneRadius);
        RootCanvas.Children.Add(deadZone);

        var sectorSize = 360d / layout.Items.Count;
        for (var index = 0; index < layout.Items.Count; index++)
        {
            var angle = (layout.StartAngleDegrees + ((index + 0.5) * sectorSize)) * (Math.PI / 180);
            var x = centerCoordinate + (Math.Cos(angle) * MenuRadius);
            var y = centerCoordinate + (Math.Sin(angle) * MenuRadius);
            var item = new Border
            {
                Width = ItemRadius * 2,
                Height = ItemRadius * 2,
                CornerRadius = new CornerRadius(ItemRadius),
                Background = new SolidColorBrush(Color.FromArgb(238, 17, 24, 39)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(220, 107, 114, 128)),
                BorderThickness = new Thickness(1),
                Tag = index,
                Child = new TextBlock
                {
                    Text = layout.Items[index].Label,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(5)
                }
            };
            Canvas.SetLeft(item, x - ItemRadius);
            Canvas.SetTop(item, y - ItemRadius);
            RootCanvas.Children.Add(item);
            _itemViews.Add(item);
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    public void Update(RadialMenuHit hit)
    {
        foreach (var item in _itemViews)
        {
            var selected = hit.SlotIndex is { } index && Equals(item.Tag, index);
            item.Background = new SolidColorBrush(selected
                ? Color.FromArgb(245, 37, 99, 235)
                : Color.FromArgb(238, 17, 24, 39));
            item.BorderBrush = new SolidColorBrush(selected
                ? Color.FromArgb(245, 147, 197, 253)
                : Color.FromArgb(220, 107, 114, 128));
        }
    }

    public void Dismiss()
    {
        if (IsVisible)
        {
            base.Hide();
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
