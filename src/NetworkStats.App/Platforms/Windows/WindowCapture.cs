using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace NetworkStats.App.Platforms.Windows;

// 仅由隔离桌面的包验证调用，用于检查真实 WinUI 布局，不切换桌面或激活用户窗口。
internal static class WindowCapture
{
    internal static async Task SaveAsync(Microsoft.UI.Xaml.Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("NETWORKSTATS_SCREENSHOT_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        await Task.Delay(100);
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(window.Content);
        var pixels = await bitmap.GetPixelsAsync();
        var theme = Microsoft.Maui.Controls.Application.Current!.RequestedTheme.ToString().ToLowerInvariant();
        var path = Path.Combine(directory, $"{name}-{theme}.png");
        await File.WriteAllBytesAsync(path, []);
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
        if (name == "main")
        {
            var size = window.AppWindow.Size;
            try
            {
                window.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(
                    (int)(420 * window.Content.XamlRoot.RasterizationScale), size.Height));
                await SaveAsync(window, "main-narrow");
            }
            finally { window.AppWindow.Resize(size); }
        }
    }
}
