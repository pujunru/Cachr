using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Cachr;

internal sealed class TrayService : IDisposable
{
    private const uint NimAdd = 0, NimModify = 1, NimDelete = 2;
    private const uint NifMessage = 1, NifIcon = 2, NifTip = 4, NifInfo = 16;
    private const uint WmRButtonUp = 0x0205, WmLButtonDoubleClick = 0x0203;
    private const uint TpmRightButton = 2, TpmNonotify = 0x80, TpmReturnCmd = 0x100;
    private const uint MfString = 0, MfChecked = 8, MfSeparator = 0x800;
    private const uint CaptureId = 100, SettingsId = 101, StartupId = 102, ExitId = 103;
    private readonly IntPtr _hwnd;
    private readonly Action _capture;
    private readonly Action _settings;
    private readonly Action _exit;
    private readonly Win32.WindowProc _proc;
    private readonly IntPtr _previous;
    private readonly IntPtr _icon;
    private Win32.NotifyIconData _data;

    internal TrayService(Window host, Action capture, Action settings, Action exit)
    {
        _hwnd = WindowNative.GetWindowHandle(host);
        _capture = capture;
        _settings = settings;
        _exit = exit;
        _proc = WndProc;
        _previous = Win32.SetWindowLongPtr(_hwnd, Win32.GwlWndProc, _proc);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Cachr.ico");
        _icon = File.Exists(iconPath) ? Win32.LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, 0x10) : IntPtr.Zero;
        _data = NewData();
        Win32.Shell_NotifyIcon(NimAdd, ref _data);
    }

    internal void ShowNotification(string title, string message)
    {
        _data.Flags = NifInfo;
        _data.InfoTitle = title;
        _data.Info = message;
        _data.InfoFlags = 1;
        Win32.Shell_NotifyIcon(NimModify, ref _data);
        _data = NewData();
    }

    internal void RefreshTip()
    {
        _data = NewData();
        Win32.Shell_NotifyIcon(NimModify, ref _data);
    }

    private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == Win32.WmTrayIcon)
        {
            var mouseMessage = unchecked((uint)lParam.ToInt64());
            if (mouseMessage == WmLButtonDoubleClick) _capture();
            else if (mouseMessage == WmRButtonUp) ShowMenu();
            return IntPtr.Zero;
        }
        return Win32.CallWindowProc(_previous, hwnd, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = Win32.CreatePopupMenu();
        Win32.AppendMenu(menu, MfString, new UIntPtr(CaptureId), "Capture");
        Win32.AppendMenu(menu, MfString, new UIntPtr(SettingsId), "Settings");
        Win32.AppendMenu(menu, MfString | (StartupService.IsEnabled ? MfChecked : 0), new UIntPtr(StartupId), "Open at startup");
        Win32.AppendMenu(menu, MfSeparator, UIntPtr.Zero, string.Empty);
        Win32.AppendMenu(menu, MfString, new UIntPtr(ExitId), "Exit Cachr");
        Win32.GetCursorPos(out var point);
        Win32.SetForegroundWindow(_hwnd);
        var command = Win32.TrackPopupMenu(menu, TpmRightButton | TpmNonotify | TpmReturnCmd, point.X, point.Y, 0, _hwnd, IntPtr.Zero);
        Win32.DestroyMenu(menu);
        switch (command)
        {
            case CaptureId: _capture(); break;
            case SettingsId: _settings(); break;
            case StartupId: StartupService.IsEnabled = !StartupService.IsEnabled; break;
            case ExitId: _exit(); break;
        }
    }

    private Win32.NotifyIconData NewData() => new()
    {
        Size = Marshal.SizeOf<Win32.NotifyIconData>(), Hwnd = _hwnd, Id = 1,
        Flags = NifMessage | NifIcon | NifTip, CallbackMessage = Win32.WmTrayIcon,
        Icon = _icon != IntPtr.Zero ? _icon : Win32.LoadIcon(IntPtr.Zero, new IntPtr(32512)),
        Tip = $"Cachr — Region: {AppSettings.Hotkey.DisplayText} • Screen: {AppSettings.FullScreenHotkey.DisplayText} • Window: {AppSettings.WindowHotkey.DisplayText}",
        Info = string.Empty, InfoTitle = string.Empty
    };

    public void Dispose()
    {
        Win32.Shell_NotifyIcon(NimDelete, ref _data);
        if (_icon != IntPtr.Zero) Win32.DestroyIcon(_icon);
        if (_previous != IntPtr.Zero) Win32.SetWindowLongPtr(_hwnd, Win32.GwlWndProc, _previous);
    }
}
