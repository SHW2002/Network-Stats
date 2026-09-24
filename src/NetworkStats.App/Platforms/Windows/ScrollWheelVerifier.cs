using Microsoft.UI.Xaml;
using NetworkStats.App.Views;
using NetworkStats.Monitoring;
using NativeScrollViewer = Microsoft.UI.Xaml.Controls.ScrollViewer;

namespace NetworkStats.App.Platforms.Windows;

// 在隔离桌面发送真实窗口滚轮消息，验证原生命中测试及嵌套控件的事件路由。
internal static class ScrollWheelVerifier
{
    internal static async Task VerifyAsync(MainPage page, MonitorEngine monitor, Microsoft.UI.Xaml.Window window)
    {
        var size = window.AppWindow.Size;
        var range = Descendants(page).OfType<Picker>().Single();
        var originalRange = range.SelectedIndex;
        try
        {
            window.AppWindow.Resize(new(size.Width, (int)(480 * window.Content.XamlRoot.RasterizationScale)));
            await Task.Delay(150);
            var outer = (ScrollView)page.Content;
            var timeline = Descendants(page).OfType<ScrollView>().First(view => view.Orientation == ScrollOrientation.Horizontal);
            var nativeTimeline = (NativeScrollViewer)timeline.Handler!.PlatformView!;
            foreach (var index in new[] { 0, 3 })
            {
                range.SelectedIndex = index;
                await Task.Delay(100);
                await outer.ScrollToAsync(timeline, ScrollToPosition.Center, false);
                await Task.Delay(100);
                var label = Descendants(page).OfType<Label>().First(label => label.Text == "目标网站");
                await CheckVerticalAsync(outer, (FrameworkElement)label.Handler!.PlatformView!, window, "timeline label");
                var horizontal = nativeTimeline.HorizontalOffset;
                await CheckVerticalAsync(outer, nativeTimeline, window, $"timeline range {index}");
                if (Math.Abs(nativeTimeline.HorizontalOffset - horizontal) > 1)
                    throw new InvalidOperationException("Vertical wheel unexpectedly scrolled the timeline horizontally.");
            }
            await CheckContinuousAsync((NativeScrollViewer)outer.Handler!.PlatformView!, nativeTimeline, window);
            await CheckHorizontalAsync((NativeScrollViewer)outer.Handler!.PlatformView!, nativeTimeline, window);
            await MouseWheelTestInput.MoveAwayAsync(window);

            var settings = new SettingsPage(monitor);
            await page.Navigation.PushAsync(settings, false);
            try
            {
                await Task.Delay(100);
                var scroll = (ScrollView)settings.Content;
                var input = Descendants(settings).OfType<Entry>().First();
                await scroll.ScrollToAsync(input, ScrollToPosition.Center, false);
                await Task.Delay(100);
                await CheckVerticalAsync(scroll, (FrameworkElement)input.Handler!.PlatformView!, window, "settings input");
                await MouseWheelTestInput.MoveAwayAsync(window);
            }
            finally { await page.Navigation.PopAsync(false); }
        }
        finally
        {
            range.SelectedIndex = originalRange;
            window.AppWindow.Resize(size);
            await Task.Delay(200);
            await ((ScrollView)page.Content).ScrollToAsync(0, 0, false);
            await Task.Delay(100);
        }
    }

    private static async Task CheckVerticalAsync(ScrollView scroll, FrameworkElement target, Microsoft.UI.Xaml.Window window, string scenario)
    {
        var native = (NativeScrollViewer)scroll.Handler!.PlatformView!;
        foreach (var delta in new[] { -120, 120 })
        {
            var before = native.VerticalOffset;
            if (delta < 0 ? before >= native.ScrollableHeight - 1 : before <= 1)
                throw new InvalidOperationException($"Wheel test has no scroll room: {scenario}, {before}/{native.ScrollableHeight}.");
            await MouseWheelTestInput.SendAsync(window, target, delta);
            var after = native.VerticalOffset;
            if (delta < 0 ? after <= before + 1 : after >= before - 1)
                throw new InvalidOperationException($"Mouse wheel failed over {scenario}: delta={delta}, vertical offset {before:F1} -> {after:F1}.");
        }
    }

    private static async Task CheckHorizontalAsync(NativeScrollViewer outer, NativeScrollViewer timeline, Microsoft.UI.Xaml.Window window)
    {
        timeline.ChangeView(timeline.ScrollableWidth / 2, null, null, true);
        await Task.Delay(100);
        var vertical = outer.VerticalOffset;
        foreach (var delta in new[] { -120, 120 })
        {
            var before = timeline.HorizontalOffset;
            await MouseWheelTestInput.SendAsync(window, timeline, delta, horizontal: true);
            if (delta < 0 ? timeline.HorizontalOffset >= before - 1 : timeline.HorizontalOffset <= before + 1)
                throw new InvalidOperationException("Horizontal mouse wheel did not scroll the timeline.");
            if (Math.Abs(outer.VerticalOffset - vertical) > 1)
                throw new InvalidOperationException("Horizontal mouse wheel unexpectedly scrolled the page vertically.");
        }
    }

    private static async Task CheckContinuousAsync(NativeScrollViewer outer, NativeScrollViewer timeline, Microsoft.UI.Xaml.Window window)
    {
        var before = outer.VerticalOffset;
        await MouseWheelTestInput.SendAsync(window, timeline, -30);
        var single = outer.VerticalOffset - before;
        if (single <= 0) throw new InvalidOperationException("High-resolution wheel delta was ignored.");
        await MouseWheelTestInput.SendAsync(window, timeline, 30);
        before = outer.VerticalOffset;
        await MouseWheelTestInput.SendAsync(window, timeline, -30, count: 3);
        if (Math.Abs(outer.VerticalOffset - before - single * 3) > 1)
            throw new InvalidOperationException($"Continuous wheel input lost distance: single={single}, burst={outer.VerticalOffset - before}.");
    }

    private static IEnumerable<IVisualTreeElement> Descendants(IVisualTreeElement parent)
    {
        foreach (var child in parent.GetVisualChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

}
