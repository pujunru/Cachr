using System.Buffers.Binary;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Cachr;

internal static class ClipboardWriter
{
    private const uint CfDib = 8;
    private const uint CfDibV5 = 17;
    private const uint GmemMoveable = 0x0002;
    private const uint BiRgb = 0;
    private const uint BiBitfields = 3;
    private const uint LcsSrgb = 0x73524742;
    private const uint LcsGmImages = 4;

    internal static void Copy(Image image)
    {
        using var bitmap = ToStraightArgb(image);
        var pixels = ReadBottomUpBgra(bitmap);
        var dibV5 = BuildDibV5(bitmap.Width, bitmap.Height, pixels);
        var dib = BuildDib(bitmap.Width, bitmap.Height, pixels);

        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        var pngBytes = png.ToArray();

        var dibV5Handle = IntPtr.Zero;
        var dibHandle = IntPtr.Zero;
        var pngHandle = IntPtr.Zero;
        var clipboardOpen = false;

        try
        {
            dibV5Handle = Allocate(dibV5);
            dibHandle = Allocate(dib);
            pngHandle = Allocate(pngBytes);

            OpenWithRetry();
            clipboardOpen = true;
            if (!EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error());

            TransferToClipboard(CfDibV5, ref dibV5Handle, "CF_DIBV5");
            TransferToClipboard(CfDib, ref dibHandle, "CF_DIB");

            var pngFormat = RegisterClipboardFormat("PNG");
            if (pngFormat == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the PNG clipboard format.");
            TransferToClipboard(pngFormat, ref pngHandle, "PNG");
        }
        finally
        {
            if (clipboardOpen) CloseClipboard();
            if (dibV5Handle != IntPtr.Zero) GlobalFree(dibV5Handle);
            if (dibHandle != IntPtr.Zero) GlobalFree(dibHandle);
            if (pngHandle != IntPtr.Zero) GlobalFree(pngHandle);
        }
    }

    private static Bitmap ToStraightArgb(Image image)
    {
        var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(image, 0, 0);
        return bitmap;
    }

    private static byte[] ReadBottomUpBgra(Bitmap bitmap)
    {
        var rowBytes = checked(bitmap.Width * 4);
        var pixels = new byte[checked(rowBytes * bitmap.Height)];
        var row = new byte[rowBytes];
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, rowBytes);
                Buffer.BlockCopy(row, 0, pixels, (bitmap.Height - 1 - y) * rowBytes, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return pixels;
    }

    private static byte[] BuildDibV5(int width, int height, byte[] pixels)
    {
        const int headerSize = 124;
        var dib = new byte[checked(headerSize + pixels.Length)];
        var header = dib.AsSpan(0, headerSize);

        BinaryPrimitives.WriteUInt32LittleEndian(header[0..], headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], width);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], height);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], 32);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], BiBitfields);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], checked((uint)pixels.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], 0x00FF0000); // Red
        BinaryPrimitives.WriteUInt32LittleEndian(header[44..], 0x0000FF00); // Green
        BinaryPrimitives.WriteUInt32LittleEndian(header[48..], 0x000000FF); // Blue
        BinaryPrimitives.WriteUInt32LittleEndian(header[52..], 0xFF000000); // Alpha
        BinaryPrimitives.WriteUInt32LittleEndian(header[56..], LcsSrgb);
        BinaryPrimitives.WriteUInt32LittleEndian(header[108..], LcsGmImages);
        pixels.CopyTo(dib, headerSize);
        return dib;
    }

    private static byte[] BuildDib(int width, int height, byte[] pixels)
    {
        const int headerSize = 40;
        var dib = new byte[checked(headerSize + pixels.Length)];
        var header = dib.AsSpan(0, headerSize);

        BinaryPrimitives.WriteUInt32LittleEndian(header[0..], headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], width);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], height);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], 32);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], BiRgb);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], checked((uint)pixels.Length));
        pixels.CopyTo(dib, headerSize);

        // BI_RGB does not define alpha. An opaque reserved byte avoids legacy
        // consumers interpreting otherwise valid pixels as fully transparent.
        for (var offset = headerSize + 3; offset < dib.Length; offset += 4)
            dib[offset] = 0xFF;

        return dib;
    }

    private static IntPtr Allocate(byte[] bytes)
    {
        var handle = GlobalAlloc(GmemMoveable, checked((UIntPtr)(nuint)bytes.Length));
        if (handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not allocate clipboard memory.");

        var destination = GlobalLock(handle);
        if (destination == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            GlobalFree(handle);
            throw new Win32Exception(error, "Could not lock clipboard memory.");
        }

        Marshal.Copy(bytes, 0, destination, bytes.Length);
        GlobalUnlock(handle);
        return handle;
    }

    private static void TransferToClipboard(uint format, ref IntPtr handle, string name)
    {
        if (SetClipboardData(format, handle) == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not place {name} data on the clipboard.");
        handle = IntPtr.Zero; // The system owns the HGLOBAL after success.
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
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint RegisterClipboardFormat(string format);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr memory);
}
