using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using DrawingColor = System.Drawing.Color;
using XamlPoint = Windows.Foundation.Point;

namespace Cachr;

internal sealed class AnnotationStyleBar : UserControl
{
    private static readonly DrawingColor[] Palette =
    [
        DrawingColor.FromArgb(255, 239, 68, 68),
        DrawingColor.FromArgb(255, 249, 115, 22),
        DrawingColor.FromArgb(255, 234, 179, 8),
        DrawingColor.FromArgb(255, 34, 197, 94),
        DrawingColor.FromArgb(255, 59, 130, 246),
        DrawingColor.FromArgb(255, 168, 85, 247),
        DrawingColor.FromArgb(255, 17, 24, 39),
        DrawingColor.FromArgb(255, 255, 255, 255)
    ];

    private readonly Border _colorButton;
    private readonly Border _root;
    private readonly Border _separator;
    private readonly Flyout _paletteFlyout;
    private readonly Grid _paletteGrid;
    private readonly StrokeWidthControl _widthControl;
    private DrawingColor _color = Palette[0];
    private float _strokeWidth = 4;

    internal event Action<DrawingColor>? ColorChanged;
    internal event Action<float>? StrokeWidthChanged;
    internal DrawingColor Color => _color;
    internal float StrokeWidth => _strokeWidth;

    internal AnnotationStyleBar()
    {
        Width = 169;
        Height = 42;
        _root = new Border
        {
            CornerRadius = new CornerRadius(UiTokens.CardRadius),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 5, 8, 5)
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };

        _colorButton = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(2)
        };
        ToolTipService.SetToolTip(_colorButton, "Annotation color");

        _paletteGrid = new Grid { Padding = new Thickness(6), ColumnSpacing = 5, RowSpacing = 5 };
        for (var column = 0; column < 4; column++)
            _paletteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        for (var gridRow = 0; gridRow < 2; gridRow++)
            _paletteGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });

        _paletteFlyout = new Flyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft
        };

        for (var index = 0; index < Palette.Length; index++)
        {
            var color = Palette[index];
            var swatch = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = Brush(color),
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(swatch, index % 4);
            Grid.SetRow(swatch, index / 4);
            swatch.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                SetColor(color, true);
                _paletteFlyout.Hide();
            };
            _paletteGrid.Children.Add(swatch);
        }

        _paletteFlyout.Content = _paletteGrid;
        _colorButton.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _paletteFlyout.ShowAt(_colorButton);
        };

        _widthControl = new StrokeWidthControl();
        _widthControl.ValueChanged += value =>
        {
            _strokeWidth = value;
            StrokeWidthChanged?.Invoke(value);
        };

        row.Children.Add(_colorButton);
        _separator = new Border { Width = 1, Height = 22 };
        row.Children.Add(_separator);
        row.Children.Add(_widthControl);
        _root.Child = row;
        Content = _root;
        PointerPressed += (_, e) => e.Handled = true;
        SetStyle(_color, _strokeWidth);
        ApplyTheme();
    }

    internal void SetStyle(DrawingColor color, float strokeWidth)
    {
        SetColor(color, false);
        _strokeWidth = strokeWidth;
        _widthControl.SetValue(strokeWidth);
    }

    internal void ApplyTheme()
    {
        RequestedTheme = ThemePalette.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        _root.Background = ThemePalette.Toolbar;
        _root.BorderBrush = ThemePalette.Separator;
        _colorButton.BorderBrush = ThemePalette.Toolbar;
        _paletteGrid.Background = ThemePalette.Toolbar;
        foreach (var child in _paletteGrid.Children.OfType<Border>())
            child.BorderBrush = ThemePalette.Separator;
        _separator.Background = ThemePalette.Separator;
        _widthControl.ApplyTheme();
    }

    private void SetColor(DrawingColor color, bool notify)
    {
        _color = color;
        _colorButton.Background = Brush(color);
        if (notify) ColorChanged?.Invoke(color);
    }

    private static SolidColorBrush Brush(DrawingColor color) => new(
        Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));

    private sealed class StrokeWidthControl : Canvas
    {
        private const float Min = 2;
        private const float Max = 20;
        private const double ControlWidth = 112;
        private readonly Polygon _wedge;
        private readonly Ellipse _thumb;
        private float _value = 4;
        private bool _dragging;

        internal event Action<float>? ValueChanged;

        internal StrokeWidthControl()
        {
            Width = ControlWidth;
            Height = 28;
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0));
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

            _wedge = new Polygon
            {
                Points =
                {
                    new XamlPoint(4, 18),
                    new XamlPoint(ControlWidth - 4, 18),
                    new XamlPoint(ControlWidth - 4, 6)
                },
                Opacity = .72
            };
            _thumb = new Ellipse { Width = 8, Height = 8, StrokeThickness = 2 };
            Children.Add(_wedge);
            Children.Add(_thumb);
            ToolTipService.SetToolTip(this, "Stroke width");

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += (_, _) => _dragging = false;
            SetValue(_value);
        }

        internal void SetValue(float value)
        {
            _value = Math.Clamp(value, Min, Max);
            var ratio = (_value - Min) / (Max - Min);
            var x = 4 + ratio * (ControlWidth - 8);
            Canvas.SetLeft(_thumb, x - _thumb.Width / 2);
            Canvas.SetTop(_thumb, 18 - (2 + ratio * 12) / 2 - _thumb.Height / 2);
        }

        internal void ApplyTheme()
        {
            _wedge.Fill = ThemePalette.SecondaryText;
            _thumb.Fill = ThemePalette.Toolbar;
            _thumb.Stroke = ThemePalette.Text;
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            _dragging = true;
            CapturePointer(e.Pointer);
            UpdateFromPointer(e);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging) return;
            UpdateFromPointer(e);
            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            ReleasePointerCapture(e.Pointer);
            UpdateFromPointer(e);
            e.Handled = true;
        }

        private void UpdateFromPointer(PointerRoutedEventArgs e)
        {
            var ratio = Math.Clamp((e.GetCurrentPoint(this).Position.X - 4) / (ControlWidth - 8), 0, 1);
            SetValue((float)(Min + ratio * (Max - Min)));
            ValueChanged?.Invoke(_value);
        }
    }
}
