using System.Drawing;
using System.Drawing.Drawing2D;

namespace Cachr;

internal sealed class RectangleAnnotation
{
    internal RectangleF Bounds { get; set; }
    internal Color Color { get; set; } = Color.FromArgb(255, 239, 68, 68);
    internal float StrokeWidth { get; set; } = 4;

    internal void Render(Graphics graphics)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        using var pen = new Pen(Color, StrokeWidth) { Alignment = PenAlignment.Inset };
        graphics.DrawRectangle(pen, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
    }
}
