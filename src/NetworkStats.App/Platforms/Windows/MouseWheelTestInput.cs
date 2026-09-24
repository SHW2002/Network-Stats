using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace NetworkStats.App.Platforms.Windows;

// 仅由隔离桌面的验证调用，不移动系统光标，也不向用户桌面发送输入。
internal static class MouseWheelTestInput
{
    internal static async Task SendAsync(Microsoft.UI.Xaml.Window window, FrameworkElement target, int delta,
        bool horizontal = false, int count = 1)
    {
        var handle = FindInputWindow(window);
        var point = target.TransformToVisual(window.Content).TransformPoint(new(target.ActualWidth / 2, target.ActualHeight / 2));
        var scale = window.Content.XamlRoot.RasterizationScale;
        var position = PackPoint((int)(point.X * scale), (int)(point.Y * scale));
        var events = 0;
        var totalDelta = 0;
        var hitTarget = true;
        PointerEventHandler observer = (_, args) =>
        {
            events++;
            var actual = args.GetCurrentPoint(window.Content);
            totalDelta += actual.Properties.MouseWheelDelta;
            hitTarget &= Math.Abs(actual.Position.X - point.X) <= 1 && Math.Abs(actual.Position.Y - point.Y) <= 1;
        };
        window.Content.AddHandler(UIElement.PointerWheelChangedEvent, observer, true);
        try
        {
            // WinUI 的 InputSiteWindowClass 接收 XamlRoot 内的物理坐标。
            Post(handle, 0x0200, 0, position); // WM_MOUSEMOVE
            for (var index = 0; index < count; index++)
                Post(handle, horizontal ? 0x020Eu : 0x020Au, unchecked((nuint)((uint)(ushort)delta << 16)), position);
            await Task.Delay(350);
            // Windows 可以合并连续滚轮消息，但必须保留完整的滚动量。
            if (events == 0 || totalDelta != delta * count || !hitTarget)
                throw new InvalidOperationException($"Wheel input missed its target: events={events}, delta={totalDelta}/{delta * count}, hit={hitTarget}.");
        }
        finally { window.Content.RemoveHandler(UIElement.PointerWheelChangedEvent, observer); }
    }

    internal static async Task MoveAwayAsync(Microsoft.UI.Xaml.Window window)
    {
        Post(FindInputWindow(window), 0x0200, 0, PackPoint(4, 4));
        await Task.Delay(100);
    }

    private static nint FindInputWindow(Microsoft.UI.Xaml.Window window)
    {
        nint inputWindow = 0;
        EnumChildWindows(WinRT.Interop.WindowNative.GetWindowHandle(window), (child, _) =>
        {
            var name = new StringBuilder(256);
            GetClassName(child, name, name.Capacity);
            if (name.ToString() == "InputSiteWindowClass") inputWindow = child;
            return true;
        }, 0);
        return inputWindow != 0 ? inputWindow : throw new InvalidOperationException("WinUI input window not found.");
    }

    private static void Post(nint window, uint message, nuint parameter, nint position)
    {
        if (!Native.PostMessage(window, message, parameter, position)) throw new System.ComponentModel.Win32Exception();
    }

    private static nint PackPoint(int x, int y) => unchecked((nint)((uint)(ushort)x | ((uint)(ushort)y << 16)));
    private delegate bool EnumWindow(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint window, EnumWindow callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassName(nint window, StringBuilder name, int length);
}
