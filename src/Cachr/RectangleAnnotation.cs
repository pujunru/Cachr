using System.Drawing;
using System.Drawing.Drawing2D;

namespace Cachr;

internal sealed class RectangleAnnotation
{
    internal RectangleF Bounds { get; set; }
    internal Color Color { get; set; } = Color.FromArgb(255, 239, 68, 68);
    internal float StrokeWidth { get; set; } = 4;

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

    internal void Render(Graphics graphics)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        using var pen = new Pen(Color, StrokeWidth) { Alignment = PenAlignment.Inset };
        graphics.DrawRectangle(pen, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
    }
}
