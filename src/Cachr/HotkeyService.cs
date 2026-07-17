using Microsoft.UI.Dispatching;

namespace Cachr;

internal static class HotkeyService
{
    private static DispatcherQueue? _dispatcher;
    private static Action? _action;
    private static GlobalHotkey? _hotkey;

    internal static void Start(DispatcherQueue dispatcher, Action action)
    {
        _dispatcher = dispatcher;
        _action = action;
        _hotkey = new GlobalHotkey(dispatcher, action, AppSettings.Hotkey);
    }

    internal static void BeginCapture()
    {
        _hotkey?.Dispose();
        _hotkey = null;
    }

    internal static void CancelCapture()
    {
        if (_hotkey is null && _dispatcher is not null && _action is not null)
            try { _hotkey = new GlobalHotkey(_dispatcher, _action, AppSettings.Hotkey); }
            catch { _hotkey = null; }
    }

    internal static bool TryChange(HotkeyBinding binding, out string? error)
    {
        error = null;
        try
        {
            _hotkey?.Dispose();
            _hotkey = new GlobalHotkey(_dispatcher!, _action!, binding);
            AppSettings.Hotkey = binding;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try { _hotkey = new GlobalHotkey(_dispatcher!, _action!, AppSettings.Hotkey); } catch { _hotkey = null; }
            return false;
        }
    }

    internal static void Stop()
    {
        _hotkey?.Dispose();
        _hotkey = null;
    }
}
