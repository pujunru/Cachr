using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Cachr;

internal static class FileSaver
{
    internal static async Task SaveAsync(Image image, Window owner)
    {
        var picker = new FileSavePicker { SuggestedFileName = $"Cachr_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}" };
        picker.FileTypeChoices.Add("PNG image", new List<string> { ".png" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));
        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null) return;
        using var source = new MemoryStream();
        image.Save(source, ImageFormat.Png);
        source.Position = 0;
        using var destination = await file.OpenAsync(FileAccessMode.ReadWrite);
        await source.CopyToAsync(destination.AsStreamForWrite());
    }
}
