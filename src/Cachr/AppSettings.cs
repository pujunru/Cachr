using System.Text.Json;

namespace Cachr;

internal enum AppTheme { Light, Dark }

internal static class AppSettings
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cachr");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");
    private static SettingsData _data = Load();

    internal static event Action? Changed;

    internal static AppTheme Theme
    {
        get => _data.Theme;
        set
        {
            if (_data.Theme == value) return;
            _data = _data with { Theme = value };
            Save();
            Changed?.Invoke();
        }
    }

    internal static HotkeyBinding Hotkey
    {
        get => _data.Hotkey;
        set
        {
            if (_data.Hotkey == value) return;
            _data = _data with { Hotkey = value };
            Save();
            Changed?.Invoke();
        }
    }

    internal static HotkeyBinding FullScreenHotkey
    {
        get => _data.FullScreenHotkey ?? HotkeyBinding.FullScreenDefault;
        set
        {
            if (FullScreenHotkey == value) return;
            _data = _data with { FullScreenHotkey = value };
            Save();
            Changed?.Invoke();
        }
    }

    private static void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(_data));
    }

    private static SettingsData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath));
                if (loaded?.Hotkey is not null) return loaded;
            }
        }
        catch { }
        return new SettingsData(AppTheme.Light, HotkeyBinding.Default, HotkeyBinding.FullScreenDefault);
    }

    private sealed record SettingsData(AppTheme Theme, HotkeyBinding Hotkey, HotkeyBinding? FullScreenHotkey = null);
}
