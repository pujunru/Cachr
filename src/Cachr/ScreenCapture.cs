using System.Drawing;

namespace Cachr;

internal static class ScreenCapture
{
    internal static Rectangle CurrentMonitorBounds()
    {
        const uint nearestMonitor = 2;
        if (!Win32.GetCursorPos(out var point)) throw new InvalidOperationException("Could not locate the pointer.");
        var monitor = Win32.MonitorFromPoint(point, nearestMonitor);
        var info = new Win32.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MonitorInfo>() };
        if (monitor == IntPtr.Zero || !Win32.GetMonitorInfo(monitor, ref info))
            throw new InvalidOperationException("Could not identify the current display.");
        return Rectangle.FromLTRB(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom);
    }

    internal static Bitmap Take(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }
}
