using NetworkStats.App.Views;
using NetworkStats.Models;
using NetworkStats.Monitoring;
using NetworkStats.Storage;

namespace NetworkStats.App.Platforms.Windows;

internal static class TimelineVerifier
{
    public static async Task VerifyAsync(MainPage page, MonitorEngine monitor, HistoryStore history)
    {
        var original = monitor.Settings;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            for (var scenario = 0; scenario < 2; scenario++)
            {
                await using var server = new UrlTestServer(holdResponse: true);
                var site = new SiteDefinition("Timeline check", server.Url);
                DateTimeOffset? expectedMinute = null;
                if (scenario == 1)
                {
                    var sample = new ProbeResult(DateTimeOffset.UtcNow.AddMinutes(-3), site.Id, "direct", ProbeStatus.Healthy, 100, 200, null);
                    expectedMinute = sample.SampleTime;
                    await history.RecordAsync([sample], 168, timeout.Token);
                }
                await monitor.SaveSettingsAsync(original with { Sites = [site], Proxies = [], TimeoutSeconds = 10 });
                while (server.Requests == 0 || page.DisplayedMinute != expectedMinute || page.TimelineVisible != (scenario == 1))
                    await Task.Delay(50, timeout.Token);
                // 即使界面定时刷新，也只能继续显示上一次结果或首次加载提示。
                await Task.Delay(200, timeout.Token);
                if (page.DisplayedMinute != expectedMinute || page.TimelineVisible != (scenario == 1))
                    throw new IOException("Timeline exposed empty pending cells.");
                server.ReleaseResponse.TrySetResult();
                while (!page.TimelineVisible || !page.HasDrawnTimelines ||
                    page.DisplayedMinute != monitor.Snapshot().WindowEnd || page.DisplayedMinute == expectedMinute)
                    await Task.Delay(50, timeout.Token);
            }
        }
        finally { await monitor.SaveSettingsAsync(original); }
    }
}
