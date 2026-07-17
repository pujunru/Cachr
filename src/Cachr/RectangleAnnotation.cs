using System.Drawing;

namespace Cachr;

internal sealed class RectangleAnnotation
{
    internal RectangleF Bounds { get; set; }
    internal Color Color { get; set; } = Color.FromArgb(255, 239, 68, 68);
    internal float StrokeWidth { get; set; } = 4;
    internal RectangleF OuterBounds => RectangleF.FromLTRB(
        Bounds.Left - StrokeWidth,
        Bounds.Top - StrokeWidth,
        Bounds.Right + StrokeWidth,
        Bounds.Bottom + StrokeWidth);

    internal static RectangleF Resize(RectangleF original, ResizeHandle handle, PointF point)
    {
        const float minimum = 3;
        var left = original.Left;
        var top = original.Top;
        var right = original.Right;
        var bottom = original.Bottom;

        if (handle is ResizeHandle.TopLeft or ResizeHandle.Left or ResizeHandle.BottomLeft)
            left = Math.Min(point.X, right - minimum);
        if (handle is ResizeHandle.TopRight or ResizeHandle.Right or ResizeHandle.BottomRight)
            right = Math.Max(point.X, left + minimum);
        if (handle is ResizeHandle.TopLeft or ResizeHandle.Top or ResizeHandle.TopRight)
            top = Math.Min(point.Y, bottom - minimum);
        if (handle is ResizeHandle.BottomLeft or ResizeHandle.Bottom or ResizeHandle.BottomRight)
            bottom = Math.Max(point.Y, top + minimum);

        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    internal static RectangleF Move(RectangleF original, PointF delta, SizeF imageSize)
    {
        var maxX = Math.Max(0, imageSize.Width - original.Width);
        var maxY = Math.Max(0, imageSize.Height - original.Height);
        return new RectangleF(
            Math.Clamp(original.X + delta.X, 0, maxX),
            Math.Clamp(original.Y + delta.Y, 0, maxY),
            original.Width,
            original.Height);
    }

    internal void Render(Graphics graphics)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        using var brush = new SolidBrush(Color);
        graphics.FillRectangle(brush, Bounds.Left - StrokeWidth, Bounds.Top - StrokeWidth,
            Bounds.Width + StrokeWidth * 2, StrokeWidth);
        graphics.FillRectangle(brush, Bounds.Left - StrokeWidth, Bounds.Bottom,
            Bounds.Width + StrokeWidth * 2, StrokeWidth);
        graphics.FillRectangle(brush, Bounds.Left - StrokeWidth, Bounds.Top,
            StrokeWidth, Bounds.Height);
        graphics.FillRectangle(brush, Bounds.Right, Bounds.Top,
            StrokeWidth, Bounds.Height);
    }
}
