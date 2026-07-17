using Microsoft.UI.Dispatching;

namespace Cachr;

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
        CreateHotkeys(AppSettings.Hotkey);
    }

    internal static void BeginCapture() => DisposeHotkeys();

    internal static void CancelCapture()
    {
        if (_regionHotkey is not null || _dispatcher is null) return;
        try { CreateHotkeys(AppSettings.Hotkey); }
        catch { DisposeHotkeys(); }
    }

    internal static bool TryChange(HotkeyBinding binding, out string? error)
    {
        error = null;
        if (binding == HotkeyBinding.FullScreen)
        {
            error = $"{binding.DisplayText} is reserved for full-screen capture.";
            CancelCapture();
            return false;
        }

        try
        {
            DisposeHotkeys();
            CreateHotkeys(binding);
            AppSettings.Hotkey = binding;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            DisposeHotkeys();
            try { CreateHotkeys(AppSettings.Hotkey); } catch { DisposeHotkeys(); }
            return false;
        }
    }

    internal static void Stop() => DisposeHotkeys();

    private static void CreateHotkeys(HotkeyBinding regionBinding)
    {
        _regionHotkey = new GlobalHotkey(_dispatcher!, _regionAction!, regionBinding);
        try
        {
            _fullScreenHotkey = new GlobalHotkey(_dispatcher!, _fullScreenAction!, HotkeyBinding.FullScreen);
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
