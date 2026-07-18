using System.Drawing;

namespace Cachr;

internal static class ScreenCapture
{
    internal static Rectangle ForegroundWindowBounds()
    {
        const int extendedFrameBounds = 9;
        var window = Win32.GetForegroundWindow();
        if (window == IntPtr.Zero) throw new InvalidOperationException("Could not identify the active window.");
        if (Win32.DwmGetWindowAttribute(window, extendedFrameBounds, out var rect, System.Runtime.InteropServices.Marshal.SizeOf<Win32.Rect>()) != 0 &&
            !Win32.GetWindowRect(window, out rect))
            throw new InvalidOperationException("Could not read the active window bounds.");
        var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new InvalidOperationException("The active window has no visible area.");
        return bounds;
    }

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
