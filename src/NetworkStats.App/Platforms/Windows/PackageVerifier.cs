using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media;
using NetworkStats.App.Views;
using NetworkStats.Monitoring;

namespace NetworkStats.App.Platforms.Windows;

// 测试脚本在独立 Win32 desktop 中运行。这里始终验证正常 MAUI 窗口启动路径。
internal static class PackageVerifier
{
    public static string? ReportPath { get; } = GetReportPath();

    private static string? GetReportPath()
    {
        var arguments = Environment.GetCommandLineArgs();
        return arguments.Length == 3 && arguments[1] == "--verify-package"
            ? Path.GetFullPath(arguments[2]) : null;
    }

    public static async Task VerifyAsync(IServiceProvider services)
    {
        var report = new List<string> { $"Runtime directory: {AppContext.BaseDirectory}" };
        try
        {
            var page = services.GetRequiredService<MainPage>();
            var app = (App)Microsoft.Maui.Controls.Application.Current!;
            var window = app.Windows.Single();
            var native = (Microsoft.UI.Xaml.Window)window.Handler!.PlatformView!;
            var timeout = Stopwatch.StartNew();
            while (!page.IsLoaded || !page.HasDrawnTimelines || page.Width <= 0)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(20))
                    throw new TimeoutException($"Window not ready: loaded={page.IsLoaded}, drawn={page.HasDrawnTimelines}, width={page.Width}");
                await Task.Delay(50);
            }
            using var process = Process.GetCurrentProcess();
            var startupMs = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalMilliseconds;
            report.Add($"StartupMs: {startupMs:F0}");
            StartupDiagnostics.Write($"Main page and timelines rendered; startup={startupMs:F0} ms");
            if (native.Content.XamlRoot is null || VisualTreeHelper.GetChildrenCount(native.Content) == 0)
                throw new InvalidOperationException("Native visual tree was not attached.");
            if (!app.TrayInitialized) throw new InvalidOperationException("Tray initialization was skipped.");
            var icon = Native.LoadImage(0, Path.Combine(AppContext.BaseDirectory, "app.ico"), 1, 32, 32, 0x10);
            if (icon == 0) throw new InvalidOperationException("Bundled tray icon could not be loaded.");
            Native.DestroyIcon(icon);
            report.Add("MAUI: window loaded, native templates applied, timeline drawing completed");
            report.Add("Tray: window integration initialized; bundled icon loaded");

            await page.Navigation.PushAsync(new SettingsPage(services.GetRequiredService<MonitorEngine>()), false);
            await Task.Delay(300);
            if (!page.Navigation.NavigationStack.Last().IsLoaded)
                throw new InvalidOperationException("Settings page did not load.");
            await page.Navigation.PopAsync(false);
            await Task.Delay(300);
            report.Add("Navigation: settings opened and returned to timeline");
            await SpeedTestVerifier.VerifyAsync(page, services.GetRequiredService<MonitorEngine>());
            report.Add("URL speed: configured URL automatically measured and speed rendered on main timeline");
            report.Add("URL speed: single-site page measured the same configured path and query");
            report.Add("Download metrics: body throughput, separate timings, small-sample hints and legacy history verified");
            await TimelineVerifier.VerifyAsync(page, services.GetRequiredService<MonitorEngine>(), services.GetRequiredService<NetworkStats.Storage.HistoryStore>());
            report.Add("Timeline: initial loading and previous results retained until new probes finish");
            await PreferenceVerifier.VerifyAsync(app, page, services.GetRequiredService<MonitorEngine>(), native);
            report.Add("Preferences: light/dark/system themes, saved settings and close-to-minimize verified");
            report.Add("Startup: settings toggle and persistence verified without changing real login items");
            await services.GetRequiredService<MonitorEngine>().StopAsync();
            report.Insert(0, "PASS");
        }
        catch (Exception exception)
        {
            report.Insert(0, "FAIL");
            report.Add(exception.ToString());
            StartupDiagnostics.Write($"Verification failed: {exception}");
            Environment.ExitCode = 1;
        }
        await File.WriteAllLinesAsync(ReportPath!, report);
        if (Environment.ExitCode == 0)
        {
            var app = (App)Microsoft.Maui.Controls.Application.Current!;
            var native = (Microsoft.UI.Xaml.Window)app.Windows.Single().Handler!.PlatformView!;
            var message = services.GetRequiredService<MonitorEngine>().Settings.MinimizeOnClose
                ? Native.RegisterWindowMessage(TrayIcon.ExitMessageName) : 0x0010u;
            // 脚本必须等到真正退出：分别验证普通关闭和托盘的显式退出可绕过最小化设置。
            Native.PostMessage(WinRT.Interop.WindowNative.GetWindowHandle(native), message, 0, 0);
        }
    }
}
