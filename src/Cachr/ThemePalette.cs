using Microsoft.UI.Xaml.Media;

namespace Cachr;

internal static class ThemePalette
{
    internal static bool IsDark => AppSettings.Theme == AppTheme.Dark;
    internal static SolidColorBrush Toolbar => IsDark ? Brush(24, 24, 27) : Brush(250, 250, 250);
    internal static SolidColorBrush Canvas => IsDark ? Brush(38, 38, 42) : Brush(226, 228, 232);
    internal static SolidColorBrush Surface => IsDark ? Brush(15, 15, 17) : Brush(255, 255, 255);
    internal static SolidColorBrush Text => IsDark ? Brush(228, 228, 231) : Brush(31, 41, 55);
    internal static SolidColorBrush SecondaryText => IsDark ? Brush(161, 161, 170) : Brush(75, 85, 99);
    internal static SolidColorBrush Hover => IsDark ? Brush(63, 63, 70) : Brush(229, 231, 235);
    internal static SolidColorBrush Separator => IsDark ? Brush(82, 82, 91) : Brush(209, 213, 219);
    internal static SolidColorBrush Accent => Brush(59, 130, 246);
    internal static SolidColorBrush Brush(byte r, byte g, byte b) => new(Windows.UI.Color.FromArgb(255, r, g, b));
}
