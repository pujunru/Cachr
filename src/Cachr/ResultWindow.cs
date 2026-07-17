using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
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
using DrawingColor = System.Drawing.Color;
using DrawingImage = System.Drawing.Image;
using DrawingRectangleF = System.Drawing.RectangleF;
using XamlImage = Microsoft.UI.Xaml.Controls.Image;
using XamlRectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace Cachr;

internal sealed class ResultWindow : ChromeWindow
{
    private readonly byte[] _png;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private readonly XamlImage _preview = new() { Stretch = Stretch.Fill };
    private readonly Grid _imageContent = new();
    private readonly Canvas _annotationLayer = new() { IsHitTestVisible = false };
    private readonly Border _image = new();
    private readonly WorkspaceCanvas _viewport = new() { Background = ThemePalette.Canvas };
    private readonly Canvas _dots = new() { IsHitTestVisible = false };
    private readonly TextBlock _dimensions = new() { FontSize = UiTokens.Text, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _zoomValue = new() { FontSize = UiTokens.Text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Border _zoomControl = new() { CornerRadius = new CornerRadius(UiTokens.Radius), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(10, 4, 10, 4) };
    private readonly TextBlock _zoomCardValue = new() { FontSize = UiTokens.Heading, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _zoomCard = new() { Width = 172, CornerRadius = new CornerRadius(UiTokens.CardRadius), Padding = new Thickness(8), BorderThickness = new Thickness(1), Visibility = Visibility.Collapsed };
    private readonly AnnotationStyleBar _annotationStyleBar = new() { Visibility = Visibility.Collapsed };
    private readonly List<Border> _zoomCardButtons = [];
    private readonly List<AnnotationVisual> _annotations = [];
    private readonly Border _annotateButton;
    private readonly AnnotationSelectionAdorner _selectionAdorner;
    private double _displayScale = 1;
    private double _zoom = 1;
    private double _offsetX;
    private double _offsetY;
    private Windows.Foundation.Size _lastViewportSize;
    private bool _positioned;
    private bool _spaceHeld;
    private bool _panning;
    private bool _annotateMode;
    private bool _drawingAnnotation;
    private bool _resizingAnnotation;
    private bool _movingAnnotation;
    private ResizeHandle _activeResizeHandle;
    private DrawingRectangleF _resizeStartBounds;
    private DrawingRectangleF _moveStartBounds;
    private Windows.Foundation.Point _moveStartPoint;
    private Windows.Foundation.Point _annotationStart;
    private AnnotationVisual? _selectedAnnotation;
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
        _annotateButton = AddToolbarButton("\uE70F", "Rectangle annotation", ToggleAnnotationMode);
        _annotateButton.PointerExited += (_, _) => UpdateAnnotateButton();
        AddToolbarSeparator();
        AddToolbarButton("\uE713", "Settings", WindowManager.ShowSettings);
        AddToolbarButton("\uE711", "Close", Close);
        BuildToolbarStatus();

        _imageContent.Children.Add(_preview);
        _imageContent.Children.Add(_annotationLayer);
        _selectionAdorner = new AnnotationSelectionAdorner(_annotationLayer);
        _image.Child = _imageContent;
        _viewport.Children.Add(_dots);
        _viewport.Children.Add(_image);
        BuildZoomCard();
        _viewport.Children.Add(_zoomCard);
        _viewport.Children.Add(_annotationStyleBar);
        Canvas.SetZIndex(_zoomCard, 10);
        Canvas.SetZIndex(_annotationStyleBar, 11);
        _annotationStyleBar.ColorChanged += color =>
        {
            if (_selectedAnnotation is null) return;
            _selectedAnnotation.Model.Color = color;
            UpdateAnnotationVisual(_selectedAnnotation);
        };
        _annotationStyleBar.StrokeWidthChanged += width =>
        {
            if (_selectedAnnotation is null) return;
            _selectedAnnotation.Model.StrokeWidth = width;
            UpdateAnnotationVisual(_selectedAnnotation);
        };
        _annotationStyleBar.SizeChanged += (_, _) => PositionAnnotationStyleBar();
        Body.Children.Add(_viewport);
        _viewport.SizeChanged += ViewportSizeChanged;
        _viewport.PointerWheelChanged += PointerWheelChanged;
        _viewport.PointerPressed += PointerPressed;
        _viewport.PointerMoved += PointerMoved;
        _viewport.PointerReleased += PointerReleased;
        _viewport.KeyDown += KeyDown;
        _viewport.KeyUp += KeyUp;
        var deleteAccelerator = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Delete };
        deleteAccelerator.Invoked += (_, e) =>
        {
            if (_selectedAnnotation is null) return;
            RemoveAnnotation(_selectedAnnotation);
            e.Handled = true;
        };
        if (Content is UIElement root) root.KeyboardAccelerators.Add(deleteAccelerator);
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
        PositionAnnotationStyleBar();
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
        var current = e.GetCurrentPoint(_viewport);
        _spaceHeld = IsSpaceDown;
        if (_spaceHeld && current.Properties.IsLeftButtonPressed)
        {
            _panning = true;
            _lastPointer = current.Position;
            _viewport.CapturePointer(e.Pointer);
            _viewport.SetPanCursor(true);
            e.Handled = true;
            return;
        }

        if (!current.Properties.IsLeftButtonPressed) return;
        var handle = _selectionAdorner.HitTest(ViewportToImageLayer(current.Position));
        if (handle != ResizeHandle.None && _selectedAnnotation is not null)
        {
            BeginResize(handle);
            _viewport.CapturePointer(e.Pointer);
            _viewport.SetResizeCursor(handle);
            e.Handled = true;
            return;
        }

        if (!TryViewportToImage(current.Position, out var imagePoint))
        {
            if (!_annotateMode) SelectAnnotation(null);
            return;
        }

        var hit = HitTestAnnotation(imagePoint);
        if (hit is not null)
        {
            SelectAnnotation(hit);
            BeginMove(hit, imagePoint);
            _viewport.CapturePointer(e.Pointer);
            _viewport.SetObjectMoveCursor(true);
            e.Handled = true;
            return;
        }

        if (!_annotateMode)
        {
            SelectAnnotation(null);
            return;
        }

        BeginAnnotation(imagePoint);
        _viewport.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _spaceHeld = IsSpaceDown;
        var pointer = e.GetCurrentPoint(_viewport).Position;
        if (_resizingAnnotation)
        {
            UpdateResize(ViewportToImageClamped(pointer));
            e.Handled = true;
            return;
        }
        if (_movingAnnotation)
        {
            UpdateMove(ViewportToImageClamped(pointer));
            e.Handled = true;
            return;
        }
        if (!_panning && !_drawingAnnotation)
        {
            var handle = _selectionAdorner.HitTest(ViewportToImageLayer(pointer));
            if (handle != ResizeHandle.None)
            {
                _viewport.SetResizeCursor(handle);
            }
            else if (TryViewportToImage(pointer, out var imagePoint) && HitTestAnnotation(imagePoint) is not null)
            {
                _viewport.SetObjectMoveCursor(false);
            }
            else
            {
                _viewport.SetInteractionCursor(_spaceHeld, _annotateMode);
            }
        }
        if (_drawingAnnotation)
        {
            UpdateDrawingAnnotation(ViewportToImageClamped(pointer));
            e.Handled = true;
            return;
        }
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
        if (_resizingAnnotation)
        {
            UpdateResize(ViewportToImageClamped(e.GetCurrentPoint(_viewport).Position));
            _resizingAnnotation = false;
            _activeResizeHandle = ResizeHandle.None;
            _viewport.ReleasePointerCapture(e.Pointer);
            _viewport.SetInteractionCursor(IsSpaceDown, _annotateMode);
            e.Handled = true;
            return;
        }
        if (_movingAnnotation)
        {
            UpdateMove(ViewportToImageClamped(e.GetCurrentPoint(_viewport).Position));
            _movingAnnotation = false;
            _viewport.ReleasePointerCapture(e.Pointer);
            _viewport.SetObjectMoveCursor(false);
            e.Handled = true;
            return;
        }
        if (_drawingAnnotation)
        {
            UpdateDrawingAnnotation(ViewportToImageClamped(e.GetCurrentPoint(_viewport).Position));
            FinishAnnotation();
            _viewport.ReleasePointerCapture(e.Pointer);
            _viewport.SetInteractionCursor(IsSpaceDown, _annotateMode);
            e.Handled = true;
            return;
        }
        if (_panning)
        {
            _panning = false;
            _viewport.ReleasePointerCapture(e.Pointer);
            _viewport.SetInteractionCursor(IsSpaceDown, _annotateMode);
            e.Handled = true;
        }
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
        if (e.Key == Windows.System.VirtualKey.Escape && _drawingAnnotation)
        {
            CancelDrawingAnnotation();
            e.Handled = true;
            return;
        }
        if (e.Key == Windows.System.VirtualKey.Escape && _selectedAnnotation is not null)
        {
            SelectAnnotation(null);
            e.Handled = true;
            return;
        }
    }

    private void KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Space) return;
        _spaceHeld = false;
        _panning = false;
        _viewport.ReleasePointerCaptures();
        _viewport.SetInteractionCursor(false, _annotateMode);
        e.Handled = true;
    }

    private void ToggleAnnotationMode()
    {
        _annotateMode = !_annotateMode;
        _zoomCard.Visibility = Visibility.Collapsed;
        _viewport.SetInteractionCursor(IsSpaceDown, _annotateMode);
        UpdateAnnotateButton();
    }

    private void UpdateAnnotateButton()
    {
        _annotateButton.Background = _annotateMode ? ThemePalette.Accent : ThemePalette.Toolbar;
        if (_annotateButton.Child is FontIcon icon)
            icon.Foreground = _annotateMode
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
                : ThemePalette.Text;
    }

    private void BeginAnnotation(Windows.Foundation.Point point)
    {
        _annotationStart = point;
        _drawingAnnotation = true;
        var model = new RectangleAnnotation
        {
            Bounds = new DrawingRectangleF((float)point.X, (float)point.Y, 0, 0),
            Color = _annotationStyleBar.Color,
            StrokeWidth = _annotationStyleBar.StrokeWidth
        };
        var visual = new AnnotationVisual(model, new XamlRectangle { Fill = null });
        _annotations.Add(visual);
        _annotationLayer.Children.Add(visual.Shape);
        Canvas.SetZIndex(visual.Shape, _annotations.Count);
        SelectAnnotation(visual, false);
        UpdateAnnotationVisual(visual);
    }

    private void UpdateDrawingAnnotation(Windows.Foundation.Point point)
    {
        if (_selectedAnnotation is null) return;
        var left = Math.Min(_annotationStart.X, point.X);
        var top = Math.Min(_annotationStart.Y, point.Y);
        _selectedAnnotation.Model.Bounds = new DrawingRectangleF(
            (float)left,
            (float)top,
            (float)Math.Abs(point.X - _annotationStart.X),
            (float)Math.Abs(point.Y - _annotationStart.Y));
        UpdateAnnotationVisual(_selectedAnnotation);
    }

    private void FinishAnnotation()
    {
        _drawingAnnotation = false;
        if (_selectedAnnotation is null) return;
        var bounds = _selectedAnnotation.Model.Bounds;
        if (bounds.Width < 3 || bounds.Height < 3)
        {
            RemoveAnnotation(_selectedAnnotation);
            return;
        }

        SelectAnnotation(_selectedAnnotation);
    }

    private void CancelDrawingAnnotation()
    {
        if (_selectedAnnotation is not null) RemoveAnnotation(_selectedAnnotation);
        _drawingAnnotation = false;
        _viewport.ReleasePointerCaptures();
        _viewport.SetInteractionCursor(IsSpaceDown, _annotateMode);
    }

    private void RemoveAnnotation(AnnotationVisual annotation)
    {
        _annotationLayer.Children.Remove(annotation.Shape);
        _annotations.Remove(annotation);
        SelectAnnotation(null);
    }

    private void SelectAnnotation(AnnotationVisual? annotation, bool showStyle = true)
    {
        _selectedAnnotation = annotation;
        if (annotation is null)
        {
            _selectionAdorner.Hide();
            _annotationStyleBar.Visibility = Visibility.Collapsed;
        }
        else
        {
            _annotationStyleBar.SetStyle(annotation.Model.Color, annotation.Model.StrokeWidth);
            _annotationStyleBar.Visibility = showStyle ? Visibility.Visible : Visibility.Collapsed;
            if (showStyle) UpdateSelectionAdorner();
            else _selectionAdorner.Hide();
        }
        PositionAnnotationStyleBar();
        PositionZoomCard();
    }

    private AnnotationVisual? HitTestAnnotation(Windows.Foundation.Point point)
    {
        for (var index = _annotations.Count - 1; index >= 0; index--)
        {
            var bounds = _annotations[index].Model.Bounds;
            if (bounds.Contains((float)point.X, (float)point.Y)) return _annotations[index];
        }
        return null;
    }

    private void BeginResize(ResizeHandle handle)
    {
        if (_selectedAnnotation is null) return;
        _resizingAnnotation = true;
        _activeResizeHandle = handle;
        _resizeStartBounds = _selectedAnnotation.Model.Bounds;
    }

    private void BeginMove(AnnotationVisual annotation, Windows.Foundation.Point point)
    {
        _movingAnnotation = true;
        _moveStartBounds = annotation.Model.Bounds;
        _moveStartPoint = point;
    }

    private void UpdateMove(Windows.Foundation.Point point)
    {
        if (!_movingAnnotation || _selectedAnnotation is null) return;
        _selectedAnnotation.Model.Bounds = RectangleAnnotation.Move(
            _moveStartBounds,
            new System.Drawing.PointF(
                (float)(point.X - _moveStartPoint.X),
                (float)(point.Y - _moveStartPoint.Y)),
            new System.Drawing.SizeF(_imageWidth, _imageHeight));
        UpdateAnnotationVisual(_selectedAnnotation);
    }

    private void UpdateResize(Windows.Foundation.Point point)
    {
        if (!_resizingAnnotation || _selectedAnnotation is null) return;
        _selectedAnnotation.Model.Bounds = RectangleAnnotation.Resize(
            _resizeStartBounds,
            _activeResizeHandle,
            new System.Drawing.PointF((float)point.X, (float)point.Y));
        UpdateAnnotationVisual(_selectedAnnotation);
    }

    private bool TryViewportToImage(Windows.Foundation.Point point, out Windows.Foundation.Point imagePoint)
    {
        var width = ImageDipWidth;
        var height = ImageDipHeight;
        if (width <= 0 || height <= 0 || point.X < _offsetX || point.Y < _offsetY ||
            point.X > _offsetX + width || point.Y > _offsetY + height)
        {
            imagePoint = default;
            return false;
        }

        imagePoint = new Windows.Foundation.Point(
            (point.X - _offsetX) / width * _imageWidth,
            (point.Y - _offsetY) / height * _imageHeight);
        return true;
    }

    private Windows.Foundation.Point ViewportToImageClamped(Windows.Foundation.Point point)
    {
        var width = Math.Max(1, ImageDipWidth);
        var height = Math.Max(1, ImageDipHeight);
        return new Windows.Foundation.Point(
            Math.Clamp((point.X - _offsetX) / width * _imageWidth, 0, _imageWidth),
            Math.Clamp((point.Y - _offsetY) / height * _imageHeight, 0, _imageHeight));
    }

    private Windows.Foundation.Point ViewportToImageLayer(Windows.Foundation.Point point) =>
        new(point.X - _offsetX, point.Y - _offsetY);

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
        _annotationLayer.Width = _image.Width;
        _annotationLayer.Height = _image.Height;
        Canvas.SetLeft(_image, _offsetX);
        Canvas.SetTop(_image, _offsetY);
        foreach (var annotation in _annotations) UpdateAnnotationVisual(annotation);
        UpdateSelectionAdorner();
    }

    private void UpdateAnnotationVisual(AnnotationVisual annotation)
    {
        var scale = ImageDipWidth / _imageWidth;
        var bounds = annotation.Model.Bounds;
        annotation.Shape.Width = Math.Max(0, bounds.Width * scale);
        annotation.Shape.Height = Math.Max(0, bounds.Height * scale);
        annotation.Shape.StrokeThickness = Math.Max(.75, annotation.Model.StrokeWidth * scale);
        annotation.Shape.Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(
            annotation.Model.Color.A,
            annotation.Model.Color.R,
            annotation.Model.Color.G,
            annotation.Model.Color.B));
        Canvas.SetLeft(annotation.Shape, bounds.X * scale);
        Canvas.SetTop(annotation.Shape, bounds.Y * scale);
        if (ReferenceEquals(annotation, _selectedAnnotation) && !_drawingAnnotation)
            UpdateSelectionAdorner();
    }

    private void UpdateSelectionAdorner()
    {
        if (_selectedAnnotation is null || _drawingAnnotation)
        {
            _selectionAdorner.Hide();
            return;
        }
        _selectionAdorner.Show(
            _selectedAnnotation.Model.Bounds,
            ImageDipWidth / _imageWidth,
            _selectedAnnotation.Model.Color);
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
        Canvas.SetTop(_zoomCard, _annotationStyleBar.Visibility == Visibility.Visible ? 62 : 12);
    }

    private void PositionAnnotationStyleBar()
    {
        Canvas.SetLeft(_annotationStyleBar, Math.Max(12, _viewport.ActualWidth - _annotationStyleBar.Width - 12));
        Canvas.SetTop(_annotationStyleBar, 12);
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
        _annotationStyleBar.ApplyTheme();
        UpdateSelectionAdorner();
        UpdateAnnotateButton();
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
        using var image = RenderOutput();
        ClipboardWriter.Copy(image);
        Close();
    }

    private async Task SaveAsync()
    {
        using var image = RenderOutput();
        await FileSaver.SaveAsync(image, this);
    }

    private DrawingBitmap RenderOutput()
    {
        var image = Decode();
        if (_annotations.Count == 0) return image;
        using var graphics = System.Drawing.Graphics.FromImage(image);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        foreach (var annotation in _annotations) annotation.Model.Render(graphics);
        return image;
    }

    private DrawingBitmap Decode()
    {
        using var stream = new MemoryStream(_png);
        using var loaded = DrawingImage.FromStream(stream);
        return new DrawingBitmap(loaded);
    }

    private sealed record AnnotationVisual(RectangleAnnotation Model, XamlRectangle Shape);

    private sealed class WorkspaceCanvas : Canvas
    {
        public WorkspaceCanvas() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        public void SetPanCursor(bool dragging) => ProtectedCursor = InputSystemCursor.Create(dragging ? InputSystemCursorShape.SizeAll : InputSystemCursorShape.Hand);
        public void SetInteractionCursor(bool spaceHeld, bool annotateMode) => ProtectedCursor = InputSystemCursor.Create(
            spaceHeld ? InputSystemCursorShape.Hand : annotateMode ? InputSystemCursorShape.Cross : InputSystemCursorShape.Arrow);
        public void SetResizeCursor(ResizeHandle handle) => ProtectedCursor = InputSystemCursor.Create(handle switch
        {
            ResizeHandle.Top or ResizeHandle.Bottom => InputSystemCursorShape.SizeNorthSouth,
            ResizeHandle.Left or ResizeHandle.Right => InputSystemCursorShape.SizeWestEast,
            ResizeHandle.TopLeft or ResizeHandle.BottomRight => InputSystemCursorShape.SizeNorthwestSoutheast,
            ResizeHandle.TopRight or ResizeHandle.BottomLeft => InputSystemCursorShape.SizeNortheastSouthwest,
            _ => InputSystemCursorShape.Arrow
        });
        public void SetObjectMoveCursor(bool dragging) => ProtectedCursor = InputSystemCursor.Create(
            dragging ? InputSystemCursorShape.SizeAll : InputSystemCursorShape.Hand);
    }
}
