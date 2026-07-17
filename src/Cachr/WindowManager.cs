namespace Cachr;

internal static class WindowManager
{
    private static SettingsWindow? _settings;

    internal static void ShowSettings()
    {
        if (_settings is null)
        {
            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => _settings = null;
            _ = _settings.ShowAsync();
        }
        else _settings.Activate();
    }
}
