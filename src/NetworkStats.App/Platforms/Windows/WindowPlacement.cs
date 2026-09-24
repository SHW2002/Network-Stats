using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace NetworkStats.App.Platforms.Windows;

internal static class WindowPlacement
{
    internal static void FitToWorkArea(Microsoft.UI.Xaml.Window window)
    {
        var appWindow = window.AppWindow;
        var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary)?.WorkArea;
        if (area is not { Width: > 0, Height: > 0 } work) return;
        var dpi = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window));
        var scale = (dpi == 0 ? 96 : dpi) / 96.0;
        var margin = (int)Math.Round(24 * scale);
        var width = Math.Min((int)Math.Round(960 * scale), Math.Max(1, work.Width - margin * 2));
        var height = Math.Min((int)Math.Round(680 * scale), Math.Max(1, work.Height - margin * 2));
        appWindow.MoveAndResize(new RectInt32(work.X + (work.Width - width) / 2,
            work.Y + (work.Height - height) / 2, width, height));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
}
