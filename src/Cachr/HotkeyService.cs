using Microsoft.UI.Dispatching;

namespace Cachr;

internal enum CaptureHotkey { Region, FullScreen, Window }

internal static class HotkeyService
{
    private static DispatcherQueue? _dispatcher;
    private static Action? _regionAction;
    private static Action? _fullScreenAction;
    private static Action? _windowAction;
    private static GlobalHotkey? _regionHotkey;
    private static GlobalHotkey? _fullScreenHotkey;
    private static GlobalHotkey? _windowHotkey;

    internal static void Start(DispatcherQueue dispatcher, Action regionAction, Action fullScreenAction, Action windowAction)
    {
        _dispatcher = dispatcher;
        _regionAction = regionAction;
        _fullScreenAction = fullScreenAction;
        _windowAction = windowAction;
        CreateHotkeys(AppSettings.Hotkey, AppSettings.FullScreenHotkey, AppSettings.WindowHotkey);
    }

    internal static void BeginCapture() => DisposeHotkeys();

    internal static void CancelCapture()
    {
        if (_regionHotkey is not null || _dispatcher is null) return;
        try { CreateHotkeys(AppSettings.Hotkey, AppSettings.FullScreenHotkey, AppSettings.WindowHotkey); }
        catch { DisposeHotkeys(); }
    }

    internal static bool TryChange(CaptureHotkey target, HotkeyBinding binding, out string? error)
    {
        error = null;
        var regionBinding = target == CaptureHotkey.Region ? binding : AppSettings.Hotkey;
        var fullScreenBinding = target == CaptureHotkey.FullScreen ? binding : AppSettings.FullScreenHotkey;
        var windowBinding = target == CaptureHotkey.Window ? binding : AppSettings.WindowHotkey;
        if (new HashSet<HotkeyBinding> { regionBinding, fullScreenBinding, windowBinding }.Count < 3)
        {
            error = $"{binding.DisplayText} is already used by the other capture mode.";
            CancelCapture();
            return false;
        }

        try
        {
            DisposeHotkeys();
            CreateHotkeys(regionBinding, fullScreenBinding, windowBinding);
            if (target == CaptureHotkey.Region) AppSettings.Hotkey = binding;
            else if (target == CaptureHotkey.FullScreen) AppSettings.FullScreenHotkey = binding;
            else AppSettings.WindowHotkey = binding;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            DisposeHotkeys();
            try { CreateHotkeys(AppSettings.Hotkey, AppSettings.FullScreenHotkey, AppSettings.WindowHotkey); } catch { DisposeHotkeys(); }
            return false;
        }
    }

    internal static void Stop() => DisposeHotkeys();

    private static void CreateHotkeys(HotkeyBinding regionBinding, HotkeyBinding fullScreenBinding, HotkeyBinding windowBinding)
    {
        try
        {
            _regionHotkey = new GlobalHotkey(_dispatcher!, _regionAction!, regionBinding);
            _fullScreenHotkey = new GlobalHotkey(_dispatcher!, _fullScreenAction!, fullScreenBinding);
            _windowHotkey = new GlobalHotkey(_dispatcher!, _windowAction!, windowBinding);
        }
        catch
        {
            DisposeHotkeys();
            throw;
        }
    }

    private static void DisposeHotkeys()
    {
        _regionHotkey?.Dispose();
        _fullScreenHotkey?.Dispose();
        _windowHotkey?.Dispose();
        _regionHotkey = null;
        _fullScreenHotkey = null;
        _windowHotkey = null;
    }
}
