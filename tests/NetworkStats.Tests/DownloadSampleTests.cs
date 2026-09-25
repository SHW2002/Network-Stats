using NetworkStats.Models;
using NetworkStats.Probing;
using NetworkStats.Storage;

namespace NetworkStats.Tests;

internal static class DownloadSampleTests
{
    public static Task BufferedRatesAndBoundariesAsync()
    {
        // 70 KB 在缓冲区内用 0.1 ms 读完，旧算法会显示 700 MB/s。
        foreach (var (bytes, milliseconds) in new (long, double)[]
        {
            (70_000, 0.1), (1_000_000, 10), (63_999, 3000), (64_000, 199.9), (0, 1000)
        })
        {
            var progress = new DownloadProgress(bytes, TimeSpan.FromMilliseconds(milliseconds), TimeSpan.FromSeconds(5));
            var saved = new UrlTransfer(bytes, 5000, DownloadCompletion.EndOfFile, "https://example.com/", milliseconds);
            Check.That(progress.MegabytesPerSecond is null && progress.MegabitsPerSecond is null &&
                saved.MegabytesPerSecond is null && saved.MegabitsPerSecond is null,
                "Buffered or undersized response still produced a numeric speed");
            Check.That(progress.InsufficientSample == (bytes > 0) && saved.InsufficientSample == (bytes > 0),
                "Empty body and insufficient speed sample were confused");
        }
        var boundary = new DownloadProgress(64_000, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5));
        Check.That(boundary.MegabytesPerSecond == 0.32 && boundary.ShortSample && !boundary.InsufficientSample,
            "Measurable short transfer lost its actual body rate or caution label");
        var sustained = new DownloadProgress(50_000_000, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
        Check.That(sustained.MegabytesPerSecond == 25 && !sustained.ShortSample,
            "Valid sustained speed was capped or included response waiting");
        return Task.CompletedTask;
    }

    public static async Task SmallResponsesThroughProxiesAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        await using var socks = new SocksProxy(new byte[4096]);
        var direct = new RouteDefinition("direct", "直连", null);
        foreach (var (url, route) in new[]
        {
            (server.Address + "/download-small-buffered", direct),
            ("http://target.invalid/download-small-buffered", new RouteDefinition("http", "HTTP", server.Address)),
            ("http://target.invalid/page", new RouteDefinition("socks", "SOCKS5", $"socks5://127.0.0.1:{socks.Port}")),
            (server.Address + "/download-small-slow", direct)
        })
        {
            var capture = new ProgressCapture();
            var result = await new DownloadSpeedProbe().MeasureAsync(new(url, TimeSpan.FromSeconds(5)), route, capture);
            Check.That(result is { Succeeded: true, Completion: DownloadCompletion.EndOfFile,
                Download: { BytesReceived: 4096, InsufficientSample: true, MegabytesPerSecond: null } },
                "Small direct/proxy response was marked failed or assigned a fictitious speed");
            Check.That(capture.Values.Count > 0 && capture.Values.All(value => value.MegabytesPerSecond is null),
                "Live progress leaked a buffered speed before the final result");
        }
        var sample = await new WebsiteProbe().CheckAsync(new("small", server.Address + "/download-small-buffered"),
            direct, new MonitorSettings { SpeedMeasurementEnabled = true, TimeoutSeconds = 5, SlowThresholdMs = 100 }, default);
        Check.That(sample is { Status: ProbeStatus.Slow, LatencyMs: >= 250,
            Transfer: { BytesReceived: 4096, InsufficientSample: true, MegabytesPerSecond: null } },
            "Automatic small-page sample lost its reachability, bytes or visit latency");
    }

    public static async Task BufferedHistoryAsync()
    {
        using var directory = new TemporaryDirectory();
        var now = DateTimeOffset.UtcNow.AddSeconds(-1);
        var path = Path.Combine(directory.Path, "history");
        Directory.CreateDirectory(path);
        // 使用旧版原始 JSON，确认重启后历史中的 700 MB/s 也不会重新出现。
        await File.WriteAllTextAsync(Path.Combine(path, now.UtcDateTime.ToString("yyyy-MM-dd") + ".jsonl"),
            $$$"""{"checkedAt":"{{{now:O}}}","siteId":"buffered","routeId":"proxy","status":"Healthy","latencyMs":800,"httpStatus":200,"transfer":{"bytesReceived":70000,"totalMilliseconds":800,"completion":"EndOfFile","url":"https://baidu.com/","transferMilliseconds":0.1}}""" + "\n");
        var history = new HistoryStore(directory.Path);
        await history.LoadAsync(1);
        var restored = history.Query(now.AddMinutes(-1), DateTimeOffset.UtcNow).Single();
        Check.That(restored is { Status: ProbeStatus.Healthy, LatencyMs: 800,
            Transfer: { BytesReceived: 70_000, TransferMilliseconds: 0.1, InsufficientSample: true, MegabytesPerSecond: null } },
            "Historical buffered rate reappeared or the original measurement was changed");
        Check.That(history.Warning is null, "Old buffered history was treated as corrupt");
    }

    private sealed class ProgressCapture : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Values { get; } = [];
        public void Report(DownloadProgress value) => Values.Add(value);
    }
}
