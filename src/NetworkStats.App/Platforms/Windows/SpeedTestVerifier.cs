using NetworkStats.App.Views;
using NetworkStats.Models;
using NetworkStats.Monitoring;

namespace NetworkStats.App.Platforms.Windows;

internal static class SpeedTestVerifier
{
    public static async Task VerifyAsync(MainPage mainPage, MonitorEngine monitor)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var server = new UrlTestServer();
        var original = monitor.Settings;
        var site = new SiteDefinition("URL check", server.Url);
        try
        {
            await monitor.SaveSettingsAsync(original with
                { SpeedMeasurementEnabled = true, DirectSpeedMeasurementEnabled = true,
                    Sites = [site], Proxies = [], TimeoutSeconds = 5 });
            while (!mainPage.HasUrlSpeedReadings || !mainPage.HasDrawnTimelines || !mainPage.HasShortSampleHints ||
                !monitor.Snapshot().Samples.Any(sample => sample.SiteId == site.Id &&
                    sample.Transfer is { BytesReceived: UrlTestServer.BodySize, MegabytesPerSecond: > 0 }))
                await Task.Delay(50, timeout.Token);
            if (server.Requests < 1 || server.UnexpectedTarget)
                throw new InvalidOperationException("Automatic URL measurement did not use the configured path/query.");
            var sample = monitor.Snapshot().Samples.Last(sample => sample.SiteId == site.Id);
            if (sample.Transfer?.ResponseWaitMilliseconds < 500 ||
                mainPage.GetSpeedText(site.Id, "direct") != $"下载 {ProbePresentation.Rate(sample.Transfer!.MegabytesPerSecond!.Value)}")
                throw new InvalidOperationException("Main timeline did not display response-body download speed separately.");
            var legacy = sample with { Transfer = sample.Transfer! with { TransferMilliseconds = null } };
            if (ProbePresentation.Speed(legacy) != "未记录下载速度")
                throw new InvalidOperationException("Legacy total-time average was displayed as download speed.");
            var native = (Microsoft.UI.Xaml.Window)mainPage.Window.Handler!.PlatformView!;
            await WindowCapture.SaveAsync(native, "main-speed");
            var page = await mainPage.OpenSpeedTestAsync();
            while (!page.IsLoaded || page.Width <= 0) await Task.Delay(50, timeout.Token);
            await page.RunAsync();
            await Task.Delay(100, timeout.Token);
            if (page.LastResult is not { Succeeded: true, Download.BytesReceived: UrlTestServer.BodySize } ||
                page.LastResult.Url != site.Url || server.Requests < 2 || server.UnexpectedTarget)
                throw new InvalidOperationException($"Configured URL speed page failed: {page.LastResult?.Error}");
            if (page.DisplayedSpeed != ProbePresentation.Rate(page.LastResult.Download.MegabytesPerSecond) ||
                !page.HasTimingBreakdown || !page.HasShortSampleHint)
                throw new InvalidOperationException("Single-site page did not display body speed, timing breakdown and sample hint.");
            await WindowCapture.SaveAsync(native, "speed");
            await mainPage.Navigation.PopAsync(false);
            await VerifyInsufficientSampleAsync(mainPage, monitor, timeout.Token);
        }
        finally { await monitor.SaveSettingsAsync(original); }
    }

    private static async Task VerifyInsufficientSampleAsync(MainPage mainPage, MonitorEngine monitor, CancellationToken token)
    {
        await using var server = new UrlTestServer(smallResponse: true);
        var site = new SiteDefinition("Buffered page", server.Url);
        await monitor.SaveSettingsAsync(monitor.Settings with { Sites = [site] });
        while (mainPage.GetSpeedText(site.Id, "direct") != "样本不足")
            await Task.Delay(50, token);
        var sample = monitor.Snapshot().Samples.Last(sample => sample.SiteId == site.Id);
        if (sample.Status == ProbeStatus.Unreachable ||
            sample.Transfer is not { BytesReceived: UrlTestServer.SmallBodySize, MegabytesPerSecond: null } ||
            !ProbePresentation.Describe(new(site, monitor.Settings.GetRoutes()[0], sample.CheckedAt, sample)).Contains("无法可靠计算下载速度"))
            throw new InvalidOperationException("Buffered response lost its successful visit or sample explanation.");
        var page = await mainPage.OpenSpeedTestAsync();
        while (!page.IsLoaded || page.Width <= 0) await Task.Delay(50, token);
        await page.RunAsync();
        if (page.LastResult is not { Succeeded: true, Download.InsufficientSample: true } ||
            page.DisplayedSpeed != "样本不足" || !page.HasTimingBreakdown || !page.HasShortSampleHint)
            throw new InvalidOperationException("Single-site page displayed a fictitious buffered speed.");
        await mainPage.Navigation.PopAsync(false);
    }
}
