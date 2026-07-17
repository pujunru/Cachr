using Microsoft.UI.Dispatching;

namespace Cachr;

internal enum CaptureHotkey { Region, FullScreen }

internal static class HotkeyService
{
    private static DispatcherQueue? _dispatcher;
    private static Action? _regionAction;
    private static Action? _fullScreenAction;
    private static GlobalHotkey? _regionHotkey;
    private static GlobalHotkey? _fullScreenHotkey;

    internal static void Start(DispatcherQueue dispatcher, Action regionAction, Action fullScreenAction)
    {
        _dispatcher = dispatcher;
        _regionAction = regionAction;
        _fullScreenAction = fullScreenAction;
        CreateHotkeys(AppSettings.Hotkey, AppSettings.FullScreenHotkey);
    }

    internal static void BeginCapture() => DisposeHotkeys();

    internal static void CancelCapture()
    {
        if (_regionHotkey is not null || _dispatcher is null) return;
        try { CreateHotkeys(AppSettings.Hotkey, AppSettings.FullScreenHotkey); }
        catch { DisposeHotkeys(); }
    }

    internal static bool TryChange(CaptureHotkey target, HotkeyBinding binding, out string? error)
    {
        error = null;
        var regionBinding = target == CaptureHotkey.Region ? binding : AppSettings.Hotkey;
        var fullScreenBinding = target == CaptureHotkey.FullScreen ? binding : AppSettings.FullScreenHotkey;
        if (regionBinding == fullScreenBinding)
        {
            error = $"{binding.DisplayText} is already used by the other capture mode.";
            CancelCapture();
            return false;
        }

        try
        {
            DisposeHotkeys();
            CreateHotkeys(regionBinding, fullScreenBinding);
            if (target == CaptureHotkey.Region) AppSettings.Hotkey = binding;
            else AppSettings.FullScreenHotkey = binding;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            DisposeHotkeys();
            try { CreateHotkeys(AppSettings.Hotkey, AppSettings.FullScreenHotkey); } catch { DisposeHotkeys(); }
            return false;
        }
    }

    internal static void Stop() => DisposeHotkeys();

    private static void CreateHotkeys(HotkeyBinding regionBinding, HotkeyBinding fullScreenBinding)
    {
        _regionHotkey = new GlobalHotkey(_dispatcher!, _regionAction!, regionBinding);
        try
        {
            _fullScreenHotkey = new GlobalHotkey(_dispatcher!, _fullScreenAction!, fullScreenBinding);
        }
        catch
        {
            _regionHotkey.Dispose();
            _regionHotkey = null;
            throw;
        }
    }

    private static void DisposeHotkeys()
    {
        _regionHotkey?.Dispose();
        _fullScreenHotkey?.Dispose();
        _regionHotkey = null;
        _fullScreenHotkey = null;
    }
}
