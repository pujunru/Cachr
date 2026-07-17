using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace Cachr;

internal abstract class ChromeWindow : Window
{
    private readonly Grid _root = new();
    private readonly TextBlock _title = new() { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = UiTokens.Title, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly List<Border> _buttons = new();
    protected readonly Grid Toolbar = new() { Height = UiTokens.ToolbarHeight };
    protected readonly StackPanel ToolbarActions = new() { Orientation = Orientation.Horizontal, Spacing = 3, Margin = new Thickness(8, 5, 0, 5) };
    protected readonly Grid Body = new();
    protected readonly Grid ToolbarRight = new();

    protected ChromeWindow(string title)
    {
        _title.Text = title;
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Toolbar.Children.Add(ToolbarActions);
        Grid.SetColumn(_title, 1);
        Toolbar.Children.Add(_title);
        Grid.SetColumn(ToolbarRight, 2);
        Toolbar.Children.Add(ToolbarRight);
        _root.Children.Add(Toolbar);
        Grid.SetRow(Body, 1);
        _root.Children.Add(Body);
        Body.SizeChanged += (_, e) => Body.Clip = new RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
        };
        Content = _root;
        ApplyBaseTheme();
        AppSettings.Changed += RefreshTheme;
        Closed += (_, _) => AppSettings.Changed -= RefreshTheme;
    }

    protected Border AddToolbarButton(string glyph, string description, Action action)
    {
        var icon = new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = UiTokens.Icon };
        var button = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(UiTokens.Radius), Child = icon };
        _buttons.Add(button);
        ToolTipService.SetToolTip(button, description);
        button.PointerEntered += (_, _) => button.Background = ThemePalette.Hover;
        button.PointerExited += (_, _) => button.Background = ThemePalette.Toolbar;
        button.PointerPressed += (_, e) => { e.Handled = true; action(); };
        ToolbarActions.Children.Add(button);
        ApplyBaseTheme();
        return button;
    }

    protected void AddToolbarSeparator() => ToolbarActions.Children.Add(new Border
    {
        Width = 1, Height = 20, Margin = new Thickness(4, 7, 4, 7), Background = ThemePalette.Separator
    });

    protected async Task ShowChromeAsync(int width, int height, bool resizable)
    {
        Activate();
        var hwnd = WindowNative.GetWindowHandle(this);
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Cachr.ico");
        if (File.Exists(iconPath)) appWindow.SetIcon(iconPath);
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(resizable, false);
        presenter.IsResizable = resizable;
        presenter.IsMaximizable = resizable;
        presenter.IsMinimizable = true;
        appWindow.SetPresenter(presenter);
        appWindow.IsShownInSwitchers = true;
        Win32.UseRoundedCorners(hwnd);
        var scale = Win32.GetDpiForWindow(hwnd) / 96d;
        var physicalWidth = (int)Math.Round(width * scale);
        var physicalHeight = (int)Math.Round(height * scale);
        var screenWidth = Win32.GetSystemMetrics(0);
        var screenHeight = Win32.GetSystemMetrics(1);
        physicalWidth = Math.Min(physicalWidth, (int)(screenWidth * .92));
        physicalHeight = Math.Min(physicalHeight, (int)(screenHeight * .90));
        Win32.SetWindowPos(hwnd, Win32.HwndTopmost,
            Math.Max(0, (screenWidth - physicalWidth) / 2), Math.Max(0, (screenHeight - physicalHeight) / 2),
            physicalWidth, physicalHeight, Win32.SwpShowWindow);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(Toolbar);
        await Task.CompletedTask;
    }

    private void RefreshTheme()
    {
        ApplyBaseTheme();
        ApplyContentTheme();
    }

    private void ApplyBaseTheme()
    {
        _root.RequestedTheme = ThemePalette.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        _root.Background = ThemePalette.Toolbar;
        Toolbar.Background = ThemePalette.Toolbar;
        Body.Background = ThemePalette.Canvas;
        _title.Foreground = ThemePalette.Text;
        foreach (var button in _buttons)
        {
            button.Background = ThemePalette.Toolbar;
            if (button.Child is FontIcon icon) icon.Foreground = ThemePalette.Text;
        }
    }

    protected virtual void ApplyContentTheme() { }
}
