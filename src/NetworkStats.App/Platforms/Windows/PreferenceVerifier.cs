using NetworkStats.App.Views;
using NetworkStats.Configuration;
using NetworkStats.Models;
using NetworkStats.Monitoring;

namespace NetworkStats.App.Platforms.Windows;

internal static class PreferenceVerifier
{
    public static async Task VerifyAsync(App app, MainPage mainPage, MonitorEngine monitor, Microsoft.UI.Xaml.Window native)
    {
        var original = monitor.Settings;
        try
        {
            await CheckThemeAsync(original.Theme);
            foreach (var mode in new[] { ThemeMode.Dark, ThemeMode.Light, ThemeMode.System })
            {
                var settings = new SettingsPage(monitor);
                await mainPage.Navigation.PushAsync(settings, false);
                settings.Appearance.SelectedTheme = mode;
                settings.Appearance.MinimizeOnClose = true;
                settings.Startup.Enabled = mode != ThemeMode.Light;
                await settings.SaveAsync(); // 使用实际保存按钮的入口，验证配置与 UI 一起生效。
                var restored = new SettingsStore(AppStorage.DataDirectory, new MonitorSettings()).Current;
                if (restored.Theme != mode || !restored.MinimizeOnClose || restored.LaunchOnStartup != (mode != ThemeMode.Light) ||
                    Startup.StartupServices.Current.Read().Registered != restored.LaunchOnStartup)
                    throw new InvalidOperationException("Appearance preferences were not persisted by the settings page.");
                await CheckThemeAsync(mode);
                var speed = await mainPage.OpenSpeedTestAsync();
                await Task.Delay(100);
                CheckPalette(speed);
                await mainPage.Navigation.PopAsync(false);
            }

            var handle = WinRT.Interop.WindowNative.GetWindowHandle(native);
            Native.PostMessage(handle, 0x0010, 0, 0); // 真实 WM_CLOSE；设置启用后必须保持进程和探测运行。
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!Native.IsIconic(handle)) await Task.Delay(50, timeout.Token);
            if (!monitor.IsActive) throw new InvalidOperationException("Minimizing on close stopped network monitoring.");
            // 仅在发布脚本创建的隔离桌面中恢复，绝不切换到用户桌面。
            Native.ShowWindow(handle, 9);
            app.MinimizeForStartup();
            while (!Native.IsIconic(handle)) await Task.Delay(50, timeout.Token);
            Native.ShowWindow(handle, 9);
        }
        finally { Startup.StartupServices.Current.SetEnabled(original.LaunchOnStartup); await monitor.SaveSettingsAsync(original); }

        async Task CheckThemeAsync(ThemeMode mode)
        {
            var expected = mode switch { ThemeMode.Dark => AppTheme.Dark, ThemeMode.Light => AppTheme.Light, _ => AppTheme.Unspecified };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (app.UserAppTheme != expected || !mainPage.HasDrawnCurrentTheme || mainPage.BackgroundColor != Ui.Background.Current)
                await Task.Delay(50, timeout.Token);
            if (mode == ThemeMode.System && app.RequestedTheme != AppInfo.Current.RequestedTheme)
                throw new InvalidOperationException("System theme preference did not resolve to the device theme.");
            var nativeTheme = app.RequestedTheme == AppTheme.Dark ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light;
            if (native.Content is not Microsoft.UI.Xaml.FrameworkElement root || root.ActualTheme != nativeTheme)
                throw new InvalidOperationException("Native WinUI controls did not follow the selected theme.");
            var titleColor = native.AppWindow.TitleBar.BackgroundColor;
            if (titleColor is null || titleColor.Value.R != (byte)(Ui.Background.Current.Red * 255))
                throw new InvalidOperationException("Native title bar did not update its background.");
            CheckPalette(mainPage);
            var settings = new SettingsPage(monitor);
            await mainPage.Navigation.PushAsync(settings, false);
            await Task.Delay(100);
            CheckPalette(settings);
            await mainPage.Navigation.PopAsync(false);
        }
    }

    private static void CheckPalette(ContentPage page)
    {
        if (page.BackgroundColor != Ui.Background.Current)
            throw new InvalidOperationException($"{page.Title} did not update its background.");
        foreach (var card in Descendants(page).OfType<Border>())
            if (card.BackgroundColor != Ui.Surface.Current)
                throw new InvalidOperationException($"{page.Title} retained a card from the previous theme.");
        foreach (var entry in Descendants(page).OfType<Entry>())
            if (entry.TextColor != Ui.Ink.Current || entry.BackgroundColor != Ui.Background.Current)
                throw new InvalidOperationException($"{page.Title} retained an input from the previous theme.");
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
