using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace Cachr;

internal sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 1;
    private readonly DispatcherQueue _dispatcher;
    private readonly Action _onPressed;
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private Exception? _startupError;
    private uint _threadId;

    public GlobalHotkey(DispatcherQueue dispatcher, Action onPressed, HotkeyBinding binding)
    {
        _dispatcher = dispatcher;
        _onPressed = onPressed;
        _thread = new Thread(() => Pump(binding)) { IsBackground = true, Name = "Cachr hotkey" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
        if (_startupError is not null) throw _startupError;
    }

    private void Pump(HotkeyBinding binding)
    {
        _threadId = Win32.GetCurrentThreadId();
        if (!Win32.RegisterHotKey(IntPtr.Zero, HotkeyId, binding.Modifiers | Win32.ModNoRepeat, binding.Key))
        {
            _startupError = new Win32Exception(Marshal.GetLastWin32Error(), $"{binding.DisplayText} is unavailable.");
            _ready.Set();
            return;
        }

        _ready.Set();
        try
        {
            while (Win32.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                if (message.Message == Win32.WmHotkey && message.WParam.ToInt32() == HotkeyId)
                    _dispatcher.TryEnqueue(() => _onPressed());
        }
        finally { Win32.UnregisterHotKey(IntPtr.Zero, HotkeyId); }
    }

    public void Dispose()
    {
        if (_threadId != 0) Win32.PostThreadMessage(_threadId, Win32.WmQuit, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(1));
        _ready.Dispose();
    }
}
