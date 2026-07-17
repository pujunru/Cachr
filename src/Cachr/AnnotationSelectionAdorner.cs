using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using DrawingColor = System.Drawing.Color;
using DrawingRectangleF = System.Drawing.RectangleF;
using XamlPoint = Windows.Foundation.Point;

namespace Cachr;

internal enum ResizeHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

internal sealed class AnnotationSelectionAdorner
{
    private const double HandleSize = 8;
    private const double HitRadius = 8;
    private readonly Dictionary<ResizeHandle, Ellipse> _handles = [];

    internal AnnotationSelectionAdorner(Canvas layer)
    {
        foreach (var handle in Enum.GetValues<ResizeHandle>().Where(handle => handle != ResizeHandle.None))
        {
            var visual = new Ellipse
            {
                Width = HandleSize,
                Height = HandleSize,
                StrokeThickness = 1.5,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            Canvas.SetZIndex(visual, 1000);
            layer.Children.Add(visual);
            _handles.Add(handle, visual);
        }
    }

    internal void Show(DrawingRectangleF bounds, double scale, DrawingColor color)
    {
        var left = bounds.Left * scale;
        var top = bounds.Top * scale;
        var right = bounds.Right * scale;
        var bottom = bounds.Bottom * scale;
        var centerX = (left + right) / 2;
        var centerY = (top + bottom) / 2;

        Position(ResizeHandle.TopLeft, left, top);
        Position(ResizeHandle.Top, centerX, top);
        Position(ResizeHandle.TopRight, right, top);
        Position(ResizeHandle.Right, right, centerY);
        Position(ResizeHandle.BottomRight, right, bottom);
        Position(ResizeHandle.Bottom, centerX, bottom);
        Position(ResizeHandle.BottomLeft, left, bottom);
        Position(ResizeHandle.Left, left, centerY);

        var stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));
        foreach (var visual in _handles.Values)
        {
            visual.Fill = ThemePalette.Toolbar;
            visual.Stroke = stroke;
            visual.Visibility = Visibility.Visible;
        }
    }

    internal void Hide()
    {
        foreach (var visual in _handles.Values) visual.Visibility = Visibility.Collapsed;
    }

    internal ResizeHandle HitTest(XamlPoint point)
    {
        foreach (var (handle, visual) in _handles)
        {
            if (visual.Visibility != Visibility.Visible) continue;
            var centerX = Canvas.GetLeft(visual) + HandleSize / 2;
            var centerY = Canvas.GetTop(visual) + HandleSize / 2;
            if (Math.Abs(point.X - centerX) <= HitRadius && Math.Abs(point.Y - centerY) <= HitRadius)
                return handle;
        }
        return ResizeHandle.None;
    }

    private void Position(ResizeHandle handle, double x, double y)
    {
        var visual = _handles[handle];
        Canvas.SetLeft(visual, x - HandleSize / 2);
        Canvas.SetTop(visual, y - HandleSize / 2);
    }
}
