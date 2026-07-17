using Windows.System;

namespace Cachr;

internal sealed record HotkeyBinding(uint Modifiers, uint Key)
{
    internal static HotkeyBinding Default { get; } = new(Win32.ModControl | Win32.ModAlt | Win32.ModShift, (uint)VirtualKey.Number4);

    internal string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if ((Modifiers & Win32.ModControl) != 0) parts.Add("Ctrl");
            if ((Modifiers & Win32.ModAlt) != 0) parts.Add("Alt");
            if ((Modifiers & Win32.ModShift) != 0) parts.Add("Shift");
            if ((Modifiers & Win32.ModWin) != 0) parts.Add("Win");
            parts.Add(KeyName((VirtualKey)Key));
            return string.Join(" + ", parts);
        }
    }

    internal static bool TryFromKey(VirtualKey key, out HotkeyBinding? binding)
    {
        binding = null;
        if (key is VirtualKey.Control or VirtualKey.Menu or VirtualKey.Shift or VirtualKey.LeftWindows or VirtualKey.RightWindows) return false;
        uint modifiers = 0;
        if (IsDown(VirtualKey.Control)) modifiers |= Win32.ModControl;
        if (IsDown(VirtualKey.Menu)) modifiers |= Win32.ModAlt;
        if (IsDown(VirtualKey.Shift)) modifiers |= Win32.ModShift;
        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows)) modifiers |= Win32.ModWin;
        if (modifiers == 0) return false;
        binding = new HotkeyBinding(modifiers, (uint)key);
        return true;
    }

    private static bool IsDown(VirtualKey key) => Microsoft.UI.Input.InputKeyboardSource
        .GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private static string KeyName(VirtualKey key) => key switch
    {
        >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((int)key - (int)VirtualKey.Number0).ToString(),
        >= VirtualKey.A and <= VirtualKey.Z => key.ToString(),
        >= VirtualKey.F1 and <= VirtualKey.F24 => key.ToString(),
        VirtualKey.Space => "Space",
        VirtualKey.Add => "+",
        VirtualKey.Subtract => "−",
        _ => key.ToString()
    };
}
