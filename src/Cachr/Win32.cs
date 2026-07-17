using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Cachr;

internal static class Win32
{
    internal const int WmHotkey = 0x0312;
    internal const int WmQuit = 0x0012;
    internal const int WmTrayIcon = 0x8001;
    internal const int GwlWndProc = -4;
    internal const int GwlExStyle = -20;
    internal const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModWin = 0x0008, ModNoRepeat = 0x4000;
    internal const uint SwpNoActivate = 0x0010, SwpShowWindow = 0x0040;
    internal const int WsExLayered = 0x00080000;
    internal const int VkBack = 0x0008;
    internal const int VkDelete = 0x002E;
    internal const uint LwaColorKey = 0x00000001;
    internal static readonly IntPtr HwndTopmost = new(-1);

    internal delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] internal static extern IntPtr CallWindowProc(IntPtr previous, IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern IntPtr SetWindowLongPtrNative(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern IntPtr LoadCursor(IntPtr instance, IntPtr cursorName);
    [DllImport("user32.dll")] internal static extern IntPtr SetCursor(IntPtr cursor);
    [DllImport("user32.dll")] internal static extern int GetMessage(out WindowMessage message, IntPtr hwnd, uint minFilter, uint maxFilter);
    [DllImport("user32.dll")] internal static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll")] internal static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint load);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] internal static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr item, string text);
    [DllImport("user32.dll")] internal static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);
    [DllImport("user32.dll")] internal static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);

    internal static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, WindowProc proc) => SetWindowLongPtrNative(hwnd, index, Marshal.GetFunctionPointerForDelegate(proc));
    internal static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value) => SetWindowLongPtrNative(hwnd, index, value);
    internal static void SetWindowSize(Window window, int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width, height, SwpNoActivate);
    }

    internal static void HideWindow(Window window) => ShowWindow(WindowNative.GetWindowHandle(window), 0);

    internal static void UseCrossCursor() => SetCursor(LoadCursor(IntPtr.Zero, new IntPtr(32515)));
    internal static void MakeWhiteTransparent(IntPtr hwnd)
    {
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64() | WsExLayered;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
        SetLayeredWindowAttributes(hwnd, 0x00FFFFFF, 0, LwaColorKey);
    }

    internal static void UseRoundedCorners(IntPtr hwnd)
    {
        const int DwmWindowCornerPreference = 33;
        var round = 2;
        DwmSetWindowAttribute(hwnd, DwmWindowCornerPreference, ref round, sizeof(int));
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowMessage
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NotifyIconData
    {
        public int Size;
        public IntPtr Hwnd;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public IntPtr BalloonIcon;
    }
}
