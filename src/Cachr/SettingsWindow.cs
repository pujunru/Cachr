using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Cachr;

internal sealed class SettingsWindow : ChromeWindow
{
    private readonly Border _card = new()
    {
        MaxWidth = 408,
        Padding = UiTokens.CardPadding,
        CornerRadius = new CornerRadius(UiTokens.CardRadius),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Top
    };
    private readonly Border _light = new() { Height = 62, CornerRadius = new CornerRadius(UiTokens.Radius) };
    private readonly Border _dark = new() { Height = 62, CornerRadius = new CornerRadius(UiTokens.Radius) };
    private readonly Button _shortcutButton = new()
    {
        Height = UiTokens.ControlHeight,
        MinWidth = 112,
        Padding = new Thickness(10, 0, 10, 0),
        CornerRadius = new CornerRadius(UiTokens.Radius),
        FontSize = UiTokens.Text
    };
    private readonly TextBlock _shortcutStatus = new() { FontSize = UiTokens.SmallText, Opacity = .7, Visibility = Visibility.Collapsed };
    private bool _recording;

    public SettingsWindow() : base("Settings")
    {
        AddToolbarButton("\uE711", "Close settings", Close);
        var content = new StackPanel { Spacing = UiTokens.SectionGap };

        content.Children.Add(SectionTitle("Appearance"));
        var themes = new Grid { ColumnSpacing = UiTokens.Gap };
        themes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        themes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        BuildThemeTile(_light, "\uE706", "Light", AppTheme.Light);
        BuildThemeTile(_dark, "\uE708", "Dark", AppTheme.Dark);
        themes.Children.Add(_light);
        Grid.SetColumn(_dark, 1);
        themes.Children.Add(_dark);
        content.Children.Add(themes);

        content.Children.Add(SectionTitle("Capture shortcut"));
        _shortcutButton.Content = AppSettings.Hotkey.DisplayText;
        _shortcutButton.Click += (_, _) => BeginShortcutCapture();
        _shortcutButton.KeyDown += ShortcutKeyDown;
        content.Children.Add(SettingRow("Global shortcut", "Click, then press a new key combination.", _shortcutButton));
        content.Children.Add(_shortcutStatus);

        content.Children.Add(SectionTitle("General"));
        var startup = new ToggleSwitch { IsOn = StartupService.IsEnabled, VerticalAlignment = VerticalAlignment.Center };
        startup.Toggled += (_, _) => StartupService.IsEnabled = startup.IsOn;
        content.Children.Add(SettingRow("Open at startup", "Start Cachr in the system tray when you sign in.", startup));

        _card.Child = content;
        var scroll = new ScrollViewer
        {
            Content = _card,
            Padding = UiTokens.PagePadding,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Body.Children.Add(scroll);
        Closed += (_, _) => { if (_recording) HotkeyService.CancelCapture(); };
        ApplyContentTheme();
    }

    internal Task ShowAsync() => ShowChromeAsync(440, 390, false);

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = UiTokens.Heading,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
    };

    private static Grid SettingRow(string title, string description, FrameworkElement control)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = title, FontSize = UiTokens.Text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        label.Children.Add(new TextBlock { Text = description, FontSize = UiTokens.SmallText, Opacity = .68, TextWrapping = TextWrapping.Wrap });
        row.Children.Add(label);
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private void BuildThemeTile(Border tile, string glyph, string label, AppTheme theme)
    {
        var content = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 17 });
        content.Children.Add(new TextBlock { Text = label, FontSize = UiTokens.Text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        tile.Child = content;
        tile.PointerPressed += (_, e) => { e.Handled = true; AppSettings.Theme = theme; };
    }

    private void BeginShortcutCapture()
    {
        if (_recording) return;
        _recording = true;
        HotkeyService.BeginCapture();
        _shortcutButton.Content = "Press shortcut…";
        _shortcutStatus.Text = "Use at least one modifier. Esc cancels.";
        _shortcutStatus.Visibility = Visibility.Visible;
        _shortcutButton.Focus(FocusState.Programmatic);
    }

    private void ShortcutKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_recording) return;
        e.Handled = true;
        if (e.Key == VirtualKey.Escape)
        {
            _recording = false;
            HotkeyService.CancelCapture();
            _shortcutButton.Content = AppSettings.Hotkey.DisplayText;
            _shortcutStatus.Visibility = Visibility.Collapsed;
            return;
        }
        if (!HotkeyBinding.TryFromKey(e.Key, out var binding)) return;
        if (HotkeyService.TryChange(binding!, out var error))
        {
            _recording = false;
            _shortcutButton.Content = binding!.DisplayText;
            _shortcutStatus.Text = "Shortcut updated.";
        }
        else
        {
            _recording = false;
            _shortcutButton.Content = AppSettings.Hotkey.DisplayText;
            _shortcutStatus.Text = error ?? "Shortcut unavailable.";
        }
    }

    protected override void ApplyContentTheme()
    {
        _card.Background = ThemePalette.Surface;
        StyleThemeTile(_light, AppTheme.Light);
        StyleThemeTile(_dark, AppTheme.Dark);
        ApplyTextColor(_card);
    }

    private static void StyleThemeTile(Border tile, AppTheme theme)
    {
        tile.Background = AppSettings.Theme == theme ? ThemePalette.Hover : ThemePalette.Toolbar;
        tile.BorderBrush = AppSettings.Theme == theme ? ThemePalette.Accent : ThemePalette.Separator;
        tile.BorderThickness = new Thickness(AppSettings.Theme == theme ? 2 : 1);
    }

    private static void ApplyTextColor(DependencyObject element)
    {
        if (element is TextBlock text) text.Foreground = ThemePalette.Text;
        if (element is FontIcon icon) icon.Foreground = ThemePalette.Text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            ApplyTextColor(VisualTreeHelper.GetChild(element, i));
    }
}
