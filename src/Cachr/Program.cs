using Microsoft.UI.Xaml;

namespace Cachr;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        using var singleInstance = new Mutex(true, "Local\\Cachr.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance) return;
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ => new App());
    }
}
