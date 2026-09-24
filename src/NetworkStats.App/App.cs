using NetworkStats.App.Views;
using NetworkStats.Models;
using NetworkStats.Monitoring;

namespace NetworkStats.App;

public sealed class App(MainPage mainPage, MonitorEngine monitor) : Application
{
#if WINDOWS
    private Platforms.Windows.TrayIcon? _tray;
    private Task? _exitTask;
    private bool _exiting;
    internal bool TrayInitialized => _tray is not null;
    internal void MinimizeForStartup() => _tray?.Minimize();
    internal Task ExitForUpdateAsync()
    {
        if (_exitTask is { IsFaulted: true } or { IsCanceled: true }) _exitTask = null;
        return _exitTask ??= StopAndCloseAsync();
    }

    private async Task StopAndCloseAsync()
    {
        _exiting = true;
        try
        {
            mainPage.CancelSpeedTest();
            await monitor.StopAsync();
            Platforms.Windows.StartupDiagnostics.Write("Monitoring stopped and history flushed before window close");
            if (_tray is not null) _tray.Exit();
            else if (Windows.Single().Handler?.PlatformView is Microsoft.UI.Xaml.Window native) native.Close();
        }
        catch { _exiting = false; throw; }
    }

    private async void RequestExit()
    {
        try { await ExitForUpdateAsync(); }
        catch (Exception exception)
        {
            _exitTask = null;
            Platforms.Windows.StartupDiagnostics.Write($"Exit failed: {exception}");
        }
    }
#endif

    protected override Window CreateWindow(IActivationState? activationState)
    {
        RequestedThemeChanged += ThemeChanged;
        monitor.Updated += MonitorUpdated;
        ApplyAppearance();
        var navigation = new NavigationPage(mainPage);
        Ui.Bind(navigation, NavigationPage.BarBackgroundColorProperty, Ui.Background);
        Ui.Bind(navigation, NavigationPage.BarTextColorProperty, Ui.Ink);
        var window = new Window(navigation)
        {
            Title = "Network Stats · 网络观测站",
            Width = 960,
            Height = 680,
            MinimumWidth = 380,
            MinimumHeight = 480
        };
        window.Created += async (_, _) =>
        {
#if WINDOWS
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
            {
                Platforms.Windows.WindowTheme.Apply(native);
                Platforms.Windows.WindowPlacement.FitToWorkArea(native);
            }
#endif
            await ResumeAsync();
        };
        window.Resumed += async (_, _) => await ResumeAsync();
#if ANDROID || IOS
        // 移动端进入后台即停止探测，恢复时重新开始，不伪造缺失分钟。
        window.Stopped += async (_, _) => { mainPage.CancelSpeedTest(); await monitor.StopAsync(); };
#endif
        window.Destroying += async (_, _) =>
        {
            RequestedThemeChanged -= ThemeChanged;
            monitor.Updated -= MonitorUpdated;
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
            {
                _tray ??= new Platforms.Windows.TrayIcon(nativeWindow, () => monitor.Settings.MinimizeOnClose, RequestExit);
                Platforms.Windows.WindowTheme.Apply(nativeWindow);
            }
        };
#endif
        return window;
    }

    private void MonitorUpdated(object? sender, EventArgs args) => Dispatcher.Dispatch(ApplyAppearance);

    private void ApplyAppearance()
    {
        var theme = monitor.Settings.Theme switch
        {
            ThemeMode.Light => AppTheme.Light, ThemeMode.Dark => AppTheme.Dark, _ => AppTheme.Unspecified
        };
        if (UserAppTheme != theme) UserAppTheme = theme;
    }

    private void ThemeChanged(object? sender, AppThemeChangedEventArgs args) => Dispatcher.Dispatch(() =>
    {
        mainPage.RedrawTheme();
#if WINDOWS
        foreach (var window in Windows)
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
                Platforms.Windows.WindowTheme.Apply(native);
#endif
    });

    private async Task ResumeAsync()
    {
#if WINDOWS
        if (_exiting) return;
#endif
        if (monitor.UserPaused) return;
        try { await monitor.StartAsync(); }
        catch (Exception exception)
        {
            await mainPage.DisplayAlertAsync("无法开始探测", exception.Message, "知道了");
        }
    }
}
