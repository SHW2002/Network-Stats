using NetworkStats.App.Views;
using NetworkStats.Models;
using NetworkStats.Monitoring;

namespace NetworkStats.App.Platforms.Windows;

internal static class BrowserProbeVerifier
{
    public static async Task VerifyAsync(MainPage mainPage, MonitorEngine monitor)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await using var server = new UrlTestServer(requireBrowser: true);
        var original = monitor.Settings;
        var site = new SiteDefinition("Browser check", server.Url);
        try
        {
            await monitor.SaveSettingsAsync(original with { Sites = [site], Proxies = [], TimeoutSeconds = 10 });
            ProbeResult? sample;
            do
            {
                await Task.Delay(50, timeout.Token);
                sample = monitor.Snapshot().Samples.LastOrDefault(value => value.SiteId == site.Id);
                if (sample is { Status: ProbeStatus.Unreachable }) throw new IOException("Browser timeline failed: " + sample.Error);
            } while (sample?.Transfer is not { UsedBrowser: true, BytesReceived: UrlTestServer.BodySize } ||
                mainPage.GetSpeedText(site.Id, "direct") != $"下载 {ProbePresentation.Rate(sample.Transfer.MegabytesPerSecond!.Value)}");
            if (server.UnexpectedTarget || server.Requests < 2)
                throw new IOException($"Browser fallback changed the configured URL: requests={server.Requests}, unexpected={server.UnexpectedRequest}");
            var page = await mainPage.OpenSpeedTestAsync();
            while (!page.IsLoaded || page.Width <= 0) await Task.Delay(50, timeout.Token);
            await page.RunAsync();
            if (page.LastResult is not { Succeeded: true, UsedBrowser: true, Download.BytesReceived: UrlTestServer.BodySize } ||
                page.LastResult.Url != site.Url || !page.HasShortSampleHint)
                throw new IOException($"Single-site browser measurement failed: URL={page.LastResult?.Url}, expected={site.Url}, error={page.LastResult?.Error}");
            await mainPage.Navigation.PopAsync(false);
        }
        finally { await monitor.SaveSettingsAsync(original); }
    }
}
