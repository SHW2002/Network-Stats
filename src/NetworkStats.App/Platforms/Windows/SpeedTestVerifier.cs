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
            await monitor.SaveSettingsAsync(original with { Sites = [site], Proxies = [], TimeoutSeconds = 5 });
            while (!mainPage.HasUrlSpeedReadings || !mainPage.HasDrawnTimelines ||
                !monitor.Snapshot().Samples.Any(sample => sample.SiteId == site.Id &&
                    sample.Transfer is { BytesReceived: UrlTestServer.BodySize, MegabytesPerSecond: > 0 }))
                await Task.Delay(50, timeout.Token);
            if (server.Requests < 1 || server.UnexpectedTarget)
                throw new InvalidOperationException("Automatic URL measurement did not use the configured path/query.");
            var page = await mainPage.OpenSpeedTestAsync();
            while (!page.IsLoaded || page.Width <= 0) await Task.Delay(50, timeout.Token);
            await page.RunAsync();
            if (page.LastResult is not { Succeeded: true, Download.BytesReceived: UrlTestServer.BodySize } ||
                page.LastResult.Url != site.Url || server.Requests < 2 || server.UnexpectedTarget)
                throw new InvalidOperationException($"Configured URL speed page failed: {page.LastResult?.Error}");
            await mainPage.Navigation.PopAsync(false);
        }
        finally { await monitor.SaveSettingsAsync(original); }
    }
}
