using System.Runtime.InteropServices;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using ScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode;

namespace NetworkStats.App.Platforms.Windows;

internal sealed class MouseWheelScrollViewHandler : ScrollViewHandler
{
    private UIElement? _content;
    private long _contentChangedToken;
    private readonly PointerEventHandler _wheelHandler;

    public MouseWheelScrollViewHandler() => _wheelHandler = OnPointerWheelChanged;

    protected override void ConnectHandler(ScrollViewer platformView)
    {
        base.ConnectHandler(platformView);
        _contentChangedToken = platformView.RegisterPropertyChangedCallback(ContentControl.ContentProperty, (_, _) => AttachContent());
        AttachContent();
    }

    protected override void DisconnectHandler(ScrollViewer platformView)
    {
        platformView.UnregisterPropertyChangedCallback(ContentControl.ContentProperty, _contentChangedToken);
        DetachContent();
        base.DisconnectHandler(platformView);
    }

    private void AttachContent()
    {
        DetachContent();
        _content = PlatformView.Content as UIElement;
        // 在事件到达横向 ScrollViewer 前处理，否则 WinUI 会把普通滚轮用于横向滚动。
        _content?.AddHandler(UIElement.PointerWheelChangedEvent, _wheelHandler, false);
    }

    private void DetachContent()
    {
        _content?.RemoveHandler(UIElement.PointerWheelChangedEvent, _wheelHandler);
        _content = null;
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        if (VirtualView.Orientation != ScrollOrientation.Horizontal || args.Handled ||
            (args.KeyModifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)) != 0) return;
        var properties = args.GetCurrentPoint(PlatformView).Properties;
        if (properties.IsHorizontalMouseWheel || properties.MouseWheelDelta == 0) return;

        for (var parent = VisualTreeHelper.GetParent(PlatformView); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is not ScrollViewer { VerticalScrollMode: not ScrollMode.Disabled, ScrollableHeight: > 0 } scroll) continue;
            if (!SystemParametersInfo(0x0068, 0, out var lines, 0)) lines = 3; // SPI_GETWHEELSCROLLLINES
            var distance = lines == uint.MaxValue ? scroll.ViewportHeight : lines * 16d;
            var offset = Math.Clamp(scroll.VerticalOffset - properties.MouseWheelDelta / 120d * distance, 0, scroll.ScrollableHeight);
            // 顶部和底部也消费纵向滚轮，避免意外转成横向滚动；横向滚轮和触控保持原生行为。
            args.Handled = true;
            if (offset != scroll.VerticalOffset) scroll.ChangeView(null, offset, null, true);
            return;
        }
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, out uint value, uint flags);
}
