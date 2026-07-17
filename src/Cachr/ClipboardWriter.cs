using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Cachr;

internal static class ClipboardWriter
{
    private const uint CfBitmap = 2;
    private const uint GmemMoveable = 0x0002;

    internal static void Copy(Image image)
    {
        OpenWithRetry();
        IntPtr bitmapHandle = IntPtr.Zero;
        IntPtr pngHandle = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error());

            using var bitmap = new Bitmap(image);
            bitmapHandle = bitmap.GetHbitmap();
            if (SetClipboardData(CfBitmap, bitmapHandle) == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not place bitmap data on the clipboard.");
            bitmapHandle = IntPtr.Zero; // Windows owns it now.

            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            var bytes = png.ToArray();
            pngHandle = GlobalAlloc(GmemMoveable, new UIntPtr((uint)bytes.Length));
            if (pngHandle != IntPtr.Zero)
            {
                var destination = GlobalLock(pngHandle);
                if (destination != IntPtr.Zero)
                {
                    Marshal.Copy(bytes, 0, destination, bytes.Length);
                    GlobalUnlock(pngHandle);
                    var pngFormat = RegisterClipboardFormat("PNG");
                    if (SetClipboardData(pngFormat, pngHandle) != IntPtr.Zero)
                        pngHandle = IntPtr.Zero; // Windows owns it now.
                }
            }
        }
        finally
        {
            if (bitmapHandle != IntPtr.Zero) DeleteObject(bitmapHandle);
            if (pngHandle != IntPtr.Zero) GlobalFree(pngHandle);
            CloseClipboard();
        }
    }

    private static void OpenWithRetry()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero)) return;
            Thread.Sleep(40 + attempt * 20);
        }
        throw new Win32Exception(Marshal.GetLastWin32Error(), "The clipboard is busy. Try Copy again.");
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string format);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr memory);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
}
