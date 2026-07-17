using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using WinRT.Interop;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImage = System.Drawing.Image;
using XamlImage = Microsoft.UI.Xaml.Controls.Image;

namespace Cachr;

internal sealed class ResultWindow : ChromeWindow
{
    private readonly byte[] _png;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private readonly XamlImage _preview = new() { Stretch = Stretch.Fill };
    private readonly Border _image = new() { Child = null };
    private readonly WorkspaceCanvas _viewport = new() { Background = ThemePalette.Canvas };
    private readonly Canvas _dots = new() { IsHitTestVisible = false };
    private readonly TextBlock _dimensions = new() { FontSize = UiTokens.Text, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _zoomValue = new() { FontSize = UiTokens.Text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Border _zoomControl = new() { CornerRadius = new CornerRadius(UiTokens.Radius), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(10, 4, 10, 4) };
    private readonly TextBlock _zoomCardValue = new() { FontSize = UiTokens.Heading, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _zoomCard = new() { Width = 172, CornerRadius = new CornerRadius(UiTokens.CardRadius), Padding = new Thickness(8), BorderThickness = new Thickness(1), Visibility = Visibility.Collapsed };
    private readonly List<Border> _zoomCardButtons = [];
    private double _displayScale = 1;
    private double _zoom = 1;
    private double _offsetX;
    private double _offsetY;
    private Windows.Foundation.Size _lastViewportSize;
    private bool _positioned;
    private bool _spaceHeld;
    private bool _panning;
    private Windows.Foundation.Point _lastPointer;

    public ResultWindow(DrawingBitmap image) : base("Cachr")
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        _png = stream.ToArray();
        _imageWidth = image.Width;
        _imageHeight = image.Height;
        _dimensions.Text = $"{image.Width} × {image.Height}px";

        AddToolbarButton("\uE8C8", "Copy to clipboard", CopyAndClose);
        AddToolbarButton("\uE74E", "Save as PNG", () => _ = SaveAsync());
        AddToolbarSeparator();
        AddToolbarButton("\uE713", "Settings", WindowManager.ShowSettings);
        AddToolbarButton("\uE711", "Close", Close);
        BuildToolbarStatus();

        _image.Child = _preview;
        _viewport.Children.Add(_dots);
        _viewport.Children.Add(_image);
        BuildZoomCard();
        _viewport.Children.Add(_zoomCard);
        Body.Children.Add(_viewport);
        _viewport.SizeChanged += ViewportSizeChanged;
        _viewport.PointerWheelChanged += PointerWheelChanged;
        _viewport.PointerPressed += PointerPressed;
        _viewport.PointerMoved += PointerMoved;
        _viewport.PointerReleased += PointerReleased;
        _viewport.KeyDown += KeyDown;
        _viewport.KeyUp += KeyUp;
        ApplyContentTheme();
    }

    public async Task ShowAsync()
    {
        Activate();
        _displayScale = Win32.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96d;
        var imageDipWidth = _imageWidth / _displayScale;
        var imageDipHeight = _imageHeight / _displayScale;
        var width = (int)Math.Clamp(imageDipWidth + 32, 560, 1120);
        var height = (int)Math.Clamp(imageDipHeight + 76, 400, 760);
        await ShowChromeAsync(width, height, true);

        using var stream = new MemoryStream(_png);
        var source = new BitmapImage();
        await source.SetSourceAsync(stream.AsRandomAccessStream());
        _preview.Source = source;
        UpdateImageLayout();
        _viewport.Focus(FocusState.Programmatic);
    }

    private void BuildToolbarStatus()
    {
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        _dimensions.Foreground = ThemePalette.SecondaryText;
        status.Children.Add(_dimensions);
        status.Children.Add(new Border { Width = 1, Height = 22, Background = ThemePalette.Separator });
        var zoomStack = new StackPanel { Spacing = -2 };
        zoomStack.Children.Add(_zoomValue);
        zoomStack.Children.Add(new TextBlock { Text = "Zoom", FontSize = UiTokens.SmallText, Opacity = .65, HorizontalAlignment = HorizontalAlignment.Center });
        _zoomControl.Child = zoomStack;
        ToolTipService.SetToolTip(_zoomControl, "Zoom controls");
        _zoomControl.PointerEntered += (_, _) => _zoomControl.Background = ThemePalette.Hover;
        _zoomControl.PointerExited += (_, _) => _zoomControl.Background = ThemePalette.Toolbar;
        _zoomControl.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _zoomCard.Visibility = _zoomCard.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            PositionZoomCard();
        };
        status.Children.Add(_zoomControl);
        ToolbarRight.Children.Add(status);
        UpdateZoomText();
    }

    private void ViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewport.Clip = new RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
        };
        if (!_positioned)
        {
            _offsetX = (e.NewSize.Width - ImageDipWidth) / 2;
            _offsetY = (e.NewSize.Height - ImageDipHeight) / 2;
            _positioned = true;
        }
        else
        {
            _offsetX += (e.NewSize.Width - _lastViewportSize.Width) / 2;
            _offsetY += (e.NewSize.Height - _lastViewportSize.Height) / 2;
        }
        _lastViewportSize = e.NewSize;
        RebuildDots(e.NewSize);
        UpdateImageLayout();
        PositionZoomCard();
    }

    private void PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_viewport);
        SetZoom(_zoom * (point.Properties.MouseWheelDelta > 0 ? 1.1 : 1 / 1.1), point.Position);
        e.Handled = true;
    }

    private void PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_zoomCard.Visibility == Visibility.Visible) _zoomCard.Visibility = Visibility.Collapsed;
        _viewport.Focus(FocusState.Programmatic);
        _spaceHeld = IsSpaceDown;
        if (!_spaceHeld || !e.GetCurrentPoint(_viewport).Properties.IsLeftButtonPressed) return;
        _panning = true;
        _lastPointer = e.GetCurrentPoint(_viewport).Position;
        _viewport.CapturePointer(e.Pointer);
        _viewport.SetPanCursor(true);
        e.Handled = true;
    }

    private void PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _spaceHeld = IsSpaceDown;
        if (!_panning) _viewport.SetCursorForSpace(_spaceHeld);
        if (!_panning) return;
        var point = e.GetCurrentPoint(_viewport).Position;
        _offsetX += point.X - _lastPointer.X;
        _offsetY += point.Y - _lastPointer.Y;
        _lastPointer = point;
        UpdateImageLayout();
        e.Handled = true;
    }

    private void PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_panning) return;
        _panning = false;
        _viewport.ReleasePointerCapture(e.Pointer);
        _viewport.SetPanCursor(false);
        e.Handled = true;
    }

    private void KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Space)
        {
            _spaceHeld = true;
            _viewport.SetPanCursor(false);
            e.Handled = true;
            return;
        }

    }

    private void KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Space) return;
        _spaceHeld = false;
        _panning = false;
        _viewport.SetDefaultCursor();
        e.Handled = true;
    }

    private void ResetZoom()
    {
        _zoom = 1;
        _offsetX = (_viewport.ActualWidth - ImageDipWidth) / 2;
        _offsetY = (_viewport.ActualHeight - ImageDipHeight) / 2;
        UpdateImageLayout();
        UpdateZoomText();
    }

    private void ChangeZoom(double factor) => SetZoom(
        _zoom * factor,
        new Windows.Foundation.Point(_viewport.ActualWidth / 2, _viewport.ActualHeight / 2));

    private void SetZoom(double newZoom, Windows.Foundation.Point anchor)
    {
        var oldWidth = ImageDipWidth;
        var oldHeight = ImageDipHeight;
        var anchorX = oldWidth <= 0 ? .5 : (anchor.X - _offsetX) / oldWidth;
        var anchorY = oldHeight <= 0 ? .5 : (anchor.Y - _offsetY) / oldHeight;
        _zoom = Math.Clamp(newZoom, .1, 8);
        _offsetX = anchor.X - anchorX * ImageDipWidth;
        _offsetY = anchor.Y - anchorY * ImageDipHeight;
        UpdateImageLayout();
        UpdateZoomText();
    }

    private void FitToCanvas()
    {
        var width = _imageWidth / _displayScale;
        var height = _imageHeight / _displayScale;
        if (width <= 0 || height <= 0) return;
        _zoom = Math.Clamp(Math.Min((_viewport.ActualWidth - 64) / width, (_viewport.ActualHeight - 64) / height), .1, 8);
        _offsetX = (_viewport.ActualWidth - ImageDipWidth) / 2;
        _offsetY = (_viewport.ActualHeight - ImageDipHeight) / 2;
        UpdateImageLayout();
        UpdateZoomText();
    }

    private void UpdateImageLayout()
    {
        _image.Width = Math.Max(1, ImageDipWidth);
        _image.Height = Math.Max(1, ImageDipHeight);
        Canvas.SetLeft(_image, _offsetX);
        Canvas.SetTop(_image, _offsetY);
    }

    private void UpdateZoomText()
    {
        var text = $"{Math.Round(_zoom * 100):0}%";
        _zoomValue.Text = text;
        _zoomCardValue.Text = text;
    }

    private void BuildZoomCard()
    {
        var content = new StackPanel { Spacing = 6 };
        var header = new Grid { ColumnSpacing = 6 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        var minus = CreateCardButton("−", "Zoom out", () => ChangeZoom(1 / 1.1));
        var plus = CreateCardButton("+", "Zoom in", () => ChangeZoom(1.1));
        Grid.SetColumn(_zoomCardValue, 1);
        Grid.SetColumn(plus, 2);
        header.Children.Add(minus);
        header.Children.Add(_zoomCardValue);
        header.Children.Add(plus);
        content.Children.Add(header);
        content.Children.Add(new Border { Height = 1, Background = ThemePalette.Separator });
        content.Children.Add(CreateActionRow("Zoom in", () => ChangeZoom(1.1)));
        content.Children.Add(CreateActionRow("Zoom out", () => ChangeZoom(1 / 1.1)));
        content.Children.Add(CreateActionRow("Actual size", ResetZoom));
        content.Children.Add(CreateActionRow("Fit to canvas", FitToCanvas));
        content.Children.Add(new Border { Height = 1, Background = ThemePalette.Separator });
        content.Children.Add(new TextBlock
        {
            Text = "Scroll wheel to zoom\nHold Space + drag to pan",
            FontSize = UiTokens.SmallText,
            LineHeight = 15,
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
        _zoomCard.Child = content;
        _zoomCard.PointerPressed += (_, e) => e.Handled = true;
    }

    private Border CreateCardButton(string text, string tooltip, Action action)
    {
        var button = new Border
        {
            Width = 32,
            Height = 30,
            CornerRadius = new CornerRadius(UiTokens.Radius),
            Child = new TextBlock { Text = text, FontSize = UiTokens.Heading, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        ToolTipService.SetToolTip(button, tooltip);
        button.PointerEntered += (_, _) => button.Background = ThemePalette.Hover;
        button.PointerExited += (_, _) => button.Background = ThemePalette.Toolbar;
        button.PointerPressed += (_, e) => { e.Handled = true; action(); };
        _zoomCardButtons.Add(button);
        return button;
    }

    private Border CreateActionRow(string label, Action action)
    {
        var row = new Border
        {
            CornerRadius = new CornerRadius(UiTokens.Radius),
            Padding = new Thickness(6, 4, 6, 4),
            Child = new TextBlock { Text = label, FontSize = UiTokens.CompactText, VerticalAlignment = VerticalAlignment.Center }
        };
        row.PointerEntered += (_, _) => row.Background = ThemePalette.Hover;
        row.PointerExited += (_, _) => row.Background = ThemePalette.Toolbar;
        row.PointerPressed += (_, e) => { e.Handled = true; action(); };
        _zoomCardButtons.Add(row);
        return row;
    }

    private void PositionZoomCard()
    {
        Canvas.SetLeft(_zoomCard, Math.Max(12, _viewport.ActualWidth - _zoomCard.Width - 12));
        Canvas.SetTop(_zoomCard, 12);
    }

    private void RebuildDots(Windows.Foundation.Size size)
    {
        _dots.Children.Clear();
        _dots.Width = size.Width;
        _dots.Height = size.Height;
        const double spacing = 16;
        var dotBrush = ThemePalette.IsDark ? ThemePalette.Brush(92, 92, 101) : ThemePalette.Brush(148, 153, 162);
        for (double y = spacing / 2; y < size.Height; y += spacing)
        for (double x = spacing / 2; x < size.Width; x += spacing)
        {
            var dot = new Ellipse { Width = 1.5, Height = 1.5, Fill = dotBrush, Opacity = .72 };
            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot, y);
            _dots.Children.Add(dot);
        }
    }

    protected override void ApplyContentTheme()
    {
        _viewport.Background = ThemePalette.Canvas;
        _image.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        _dimensions.Foreground = ThemePalette.SecondaryText;
        _zoomValue.Foreground = ThemePalette.Text;
        _zoomControl.Background = ThemePalette.Toolbar;
        _zoomCard.Background = ThemePalette.Toolbar;
        _zoomCard.BorderBrush = ThemePalette.Separator;
        _zoomCardValue.Foreground = ThemePalette.Text;
        foreach (var button in _zoomCardButtons) button.Background = ThemePalette.Toolbar;
        ApplyTextColor(_zoomCard.Child);
        if (_lastViewportSize.Width > 0) RebuildDots(_lastViewportSize);
    }

    private static void ApplyTextColor(DependencyObject element)
    {
        if (element is TextBlock text) text.Foreground = ThemePalette.Text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            ApplyTextColor(VisualTreeHelper.GetChild(element, i));
    }

    private double ImageDipWidth => _imageWidth / _displayScale * _zoom;
    private double ImageDipHeight => _imageHeight / _displayScale * _zoom;
    private static bool IsSpaceDown => Microsoft.UI.Input.InputKeyboardSource
        .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Space)
        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void CopyAndClose()
    {
        using var image = Decode();
        ClipboardWriter.Copy(image);
        Close();
    }

    private async Task SaveAsync()
    {
        using var image = Decode();
        await FileSaver.SaveAsync(image, this);
    }

    private DrawingBitmap Decode()
    {
        using var stream = new MemoryStream(_png);
        using var loaded = DrawingImage.FromStream(stream);
        return new DrawingBitmap(loaded);
    }

    private sealed class WorkspaceCanvas : Canvas
    {
        public WorkspaceCanvas() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        public void SetPanCursor(bool dragging) => ProtectedCursor = InputSystemCursor.Create(dragging ? InputSystemCursorShape.SizeAll : InputSystemCursorShape.Hand);
        public void SetCursorForSpace(bool spaceHeld) => ProtectedCursor = InputSystemCursor.Create(spaceHeld ? InputSystemCursorShape.Hand : InputSystemCursorShape.Arrow);
        public void SetDefaultCursor() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    }
}
