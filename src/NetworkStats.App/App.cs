using NetworkStats.App.Views;
using NetworkStats.Monitoring;

namespace NetworkStats.App;

public sealed class App(MainPage mainPage, MonitorEngine monitor) : Application
{
#if WINDOWS
    private Platforms.Windows.TrayIcon? _tray;
    internal bool TrayInitialized => _tray is not null;
#endif

    protected override Window CreateWindow(IActivationState? activationState)
    {
        UserAppTheme = AppTheme.Light;
        var navigation = new NavigationPage(mainPage)
        {
            BarBackgroundColor = Color.FromArgb("#F5F7FB"),
            BarTextColor = Color.FromArgb("#172B45")
        };
        var window = new Window(navigation)
        {
            Title = "Network Stats · 网络观测站",
            Width = 1160,
            Height = 820,
            MinimumWidth = 380,
            MinimumHeight = 560
        };
        window.Created += async (_, _) => await ResumeAsync();
        window.Resumed += async (_, _) => await ResumeAsync();
#if ANDROID || IOS
        // 移动端进入后台即停止探测，恢复时重新开始，不伪造缺失分钟。
        window.Stopped += async (_, _) => { mainPage.CancelSpeedTest(); await monitor.StopAsync(); };
#endif
        window.Destroying += async (_, _) =>
        {
            mainPage.CancelSpeedTest();
#if WINDOWS
            _tray?.Dispose();
#endif
            await monitor.StopAsync();
        };
#if WINDOWS
        window.HandlerChanged += (_, _) =>
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                _tray ??= new Platforms.Windows.TrayIcon(nativeWindow);
        };
#endif
        return window;
    }

    private async Task ResumeAsync()
    {
        if (monitor.UserPaused) return;
        try { await monitor.StartAsync(); }
        catch (Exception exception)
        {
            await mainPage.DisplayAlertAsync("无法开始探测", exception.Message, "知道了");
        }
    }
}
