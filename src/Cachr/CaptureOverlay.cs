using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Input;
using System.Runtime.InteropServices.WindowsRuntime;
using WinRT.Interop;
using ShapeRectangle = Microsoft.UI.Xaml.Shapes.Rectangle;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Cachr;

internal sealed class CaptureOverlay : Window
{
    private readonly CaptureCanvas _canvas = new();
    private readonly Image _preview = new() { Stretch = Stretch.Fill };
    private readonly ShapeRectangle[] _shade = Enumerable.Range(0, 4).Select(_ => new ShapeRectangle { Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(135, 15, 23, 42)) }).ToArray();
    private readonly Border _selection = new() { BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 59, 130, 246)), BorderThickness = new Thickness(2), Visibility = Visibility.Collapsed };
    private readonly TextBlock _sizeText = new() { Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 250, 252)), FontSize = UiTokens.Text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly Border _size = new() { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(245, 15, 23, 42)), CornerRadius = new CornerRadius(UiTokens.Radius), Padding = new Thickness(8, 4, 8, 4), Child = null, Visibility = Visibility.Collapsed };
    private readonly Border _hint = new() { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(245, 15, 23, 42)), CornerRadius = new CornerRadius(UiTokens.Radius), Padding = new Thickness(10, 6, 10, 6), Child = new TextBlock { Text = "Drag to select  •  Esc to cancel", Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 250, 252)), FontSize = UiTokens.Heading } };
    private readonly int _left = Win32.GetSystemMetrics(76), _top = Win32.GetSystemMetrics(77), _width = Win32.GetSystemMetrics(78), _height = Win32.GetSystemMetrics(79);
    private Win32.Point _start, _end;
    private bool _dragging;
    private double _scale = 1;

    public event EventHandler<DrawingRectangle>? Selected;

    public CaptureOverlay()
    {
        _canvas.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        _size.Child = _sizeText;
        _canvas.Children.Add(_preview);
        foreach (var shade in _shade) _canvas.Children.Add(shade);
        _canvas.Children.Add(_selection);
        _canvas.Children.Add(_size);
        _canvas.Children.Add(_hint);
        _canvas.PointerPressed += Pressed;
        _canvas.PointerMoved += Moved;
        _canvas.PointerReleased += Released;
        _canvas.KeyDown += KeyDown;
        Content = _canvas;
    }

    public async Task ShowAsync()
    {
        byte[] snapshot;
        using (var bitmap = ScreenCapture.Take(new DrawingRectangle(_left, _top, _width, _height)))
        using (var png = new MemoryStream())
        {
            bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            snapshot = png.ToArray();
        }

        Activate();
        var hwnd = WindowNative.GetWindowHandle(this);
        AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd)).SetPresenter(OverlappedPresenter.CreateForContextMenu());
        Win32.SetWindowPos(hwnd, Win32.HwndTopmost, _left, _top, _width, _height, Win32.SwpNoActivate | Win32.SwpShowWindow);
        _scale = Win32.GetDpiForWindow(hwnd) / 96d;
        _canvas.Width = _width / _scale;
        _canvas.Height = _height / _scale;
        Set(_preview, 0, 0, _width, _height);
        using (var stream = new MemoryStream(snapshot))
        {
            var source = new BitmapImage();
            await source.SetSourceAsync(stream.AsRandomAccessStream());
            _preview.Source = source;
        }
        Draw();
        _canvas.Focus(FocusState.Programmatic);
    }

    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(_canvas).Properties.IsLeftButtonPressed) return;
        Win32.GetCursorPos(out var point);
        _start = _end = point;
        _dragging = true;
        _hint.Visibility = Visibility.Collapsed;
        _canvas.CapturePointer(e.Pointer);
        Draw();
    }

    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        Win32.GetCursorPos(out _end);
        Draw();
    }

    private void Released(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _canvas.ReleasePointerCapture(e.Pointer);
        Win32.GetCursorPos(out _end);
        if (SelectionBounds.Width < 2 || SelectionBounds.Height < 2) { Close(); return; }
        var bounds = SelectionBounds;
        Close();
        Selected?.Invoke(this, bounds);
    }

    private void KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape) Close();
    }

    private DrawingRectangle SelectionBounds => DrawingRectangle.FromLTRB(Math.Min(_start.X, _end.X), Math.Min(_start.Y, _end.Y), Math.Max(_start.X, _end.X), Math.Max(_start.Y, _end.Y));

    private void Draw()
    {
        var r = SelectionBounds;
        var x = r.Left - _left; var y = r.Top - _top;
        Set(_shade[0], 0, 0, _width, Math.Max(0, y));
        Set(_shade[1], 0, y, Math.Max(0, x), r.Height);
        Set(_shade[2], x + r.Width, y, Math.Max(0, _width - x - r.Width), r.Height);
        Set(_shade[3], 0, y + r.Height, _width, Math.Max(0, _height - y - r.Height));
        _selection.Visibility = r.Width > 0 ? Visibility.Visible : Visibility.Collapsed;
        Set(_selection, x, y, r.Width, r.Height);
        _size.Visibility = _selection.Visibility;
        _sizeText.Text = $"{r.Width} × {r.Height}";
        Canvas.SetLeft(_size, (x + 8) / _scale); Canvas.SetTop(_size, Math.Max(0, y - 30) / _scale);
        Canvas.SetLeft(_hint, (_width / 2d - 115) / _scale);
        Canvas.SetTop(_hint, (_height / 2d - 24) / _scale);
    }

    private void Set(FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, x / _scale); Canvas.SetTop(element, y / _scale);
        element.Width = Math.Max(0, width / _scale); element.Height = Math.Max(0, height / _scale);
    }

    private sealed class CaptureCanvas : Canvas
    {
        public CaptureCanvas() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
    }
}
