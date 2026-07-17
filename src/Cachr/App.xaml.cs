using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Cachr;

public sealed partial class App : Application
{
    private HostWindow? _hostWindow;
    private TrayService? _tray;
    private DispatcherQueue? _uiDispatcher;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _hostWindow = new HostWindow();
        _hostWindow.Activate();
        Win32.HideWindow(_hostWindow);
        _uiDispatcher = DispatcherQueue.GetForCurrentThread();
        _tray = new TrayService(_hostWindow, StartCapture, WindowManager.ShowSettings, ExitApp);
        AppSettings.Changed += SettingsChanged;
        try
        {
            HotkeyService.Start(_uiDispatcher, StartCapture);
        }
        catch (Exception ex)
        {
            _tray.ShowNotification("Shortcut unavailable", ex.Message + " Use Capture from the tray menu.");
        }
    }

    private async void StartCapture()
    {
        var overlay = new CaptureOverlay();
        overlay.Selected += (_, bounds) => _ = ShowResultAfterOverlayAsync(bounds);
        await overlay.ShowAsync();
    }

    private async Task ShowResultAfterOverlayAsync(System.Drawing.Rectangle bounds)
    {
        await Task.Delay(100);
        _uiDispatcher!.TryEnqueue(() =>
        {
            try
            {
                using var image = ScreenCapture.Take(bounds);
                var result = new ResultWindow(image);
                _ = result.ShowAsync();
            }
            catch (Exception ex) { _tray?.ShowNotification("Capture failed", ex.Message); }
        });
    }

    private void ExitApp()
    {
        AppSettings.Changed -= SettingsChanged;
        HotkeyService.Stop();
        _tray?.Dispose();
        _hostWindow?.Close();
        Environment.Exit(0);
    }

    private void SettingsChanged() => _tray?.RefreshTip();
}
