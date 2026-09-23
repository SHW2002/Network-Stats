using NetworkStats.Models;
using NetworkStats.Probing;
using NetworkStats.Storage;

namespace NetworkStats.Tests;

internal static class UrlProbeTests
{
    private static readonly WebsiteProbe Probe = new();
    private static readonly RouteDefinition Direct = new("direct", "直连", null);
    private static readonly MonitorSettings Settings = new() { TimeoutSeconds = 5, SlowThresholdMs = 300 };

    public static async Task ExactUrlAndBodyTimingAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var site = new SiteDefinition("configured", server.Address + "/download-slow?file=page%20one&x=2");
        var sample = await Probe.CheckAsync(site, Direct, Settings, default);
        Check.That(server.LastRawTarget == "/download-slow?file=page%20one&x=2", "Configured URL path/query changed");
        Check.That(sample.SiteId == site.Id && sample.Transfer is { BytesReceived: DownloadTestEndpoints.Size },
            "URL response bytes were not attributed to the configured site");
        Check.That(sample.Status == ProbeStatus.Slow && sample.LatencyMs >= 600, "Body transfer was excluded from timing/color");
        var transfer = sample.Transfer!;
        Check.That(transfer.Url == site.Url && Math.Abs(transfer.TotalMilliseconds - sample.LatencyMs) < 1,
            "Displayed latency differs from measured request time");
        Check.That(transfer.TransferMilliseconds is >= 600 && transfer.MegabytesPerSecond is > 0,
            "Response-body duration or download speed was not saved");
        var redirect = await Probe.CheckAsync(new("redirect", server.Address + "/download-redirect"), Direct, Settings, default);
        Check.That(redirect.Transfer is { BytesReceived: DownloadTestEndpoints.Size } && redirect.Transfer.Url.EndsWith("/download"),
            "Redirect lost transfer data or final URL");
    }

    public static async Task LimitsAndFailuresAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var bounded = await Probe.CheckAsync(new("limited", server.Address + "/download-endless"), Direct, Settings, default);
        Check.That(bounded.Transfer is { BytesReceived: WebsiteProbe.SampleByteLimit, Completion: DownloadCompletion.ByteLimit },
            "Automatic URL probe exceeded or missed the 1 MB cap");
        var timed = await Probe.CheckAsync(new("timed", server.Address + "/download-endless"), Direct,
            Settings with { TimeoutSeconds = 1 }, default);
        Check.That(timed.Status == ProbeStatus.Slow && timed.Transfer is { BytesReceived: > 0, Completion: DownloadCompletion.TimeLimit },
            "Partial data at the sampling deadline was lost");
        var empty = await Probe.CheckAsync(new("empty", server.Address + "/fast"), Direct, Settings, default);
        Check.That(empty.Status != ProbeStatus.Unreachable && empty.Transfer is { BytesReceived: 0, Completion: DownloadCompletion.EndOfFile },
            "Valid empty response was marked unreachable");
        var broken = await Probe.CheckAsync(new("broken", server.Address + "/download-truncated"), Direct, Settings, default);
        Check.That(broken.Status == ProbeStatus.Unreachable && broken.Transfer?.Completion == DownloadCompletion.Failed,
            "Premature disconnection was treated as success");
    }

    public static async Task HistoryCompatibilityAsync()
    {
        using var directory = new TemporaryDirectory();
        var now = DateTimeOffset.UtcNow.AddSeconds(-1);
        var historyDirectory = Path.Combine(directory.Path, "history");
        Directory.CreateDirectory(historyDirectory);
        // Exact old schema: no transfer field. A legacy success must not acquire an invented speed.
        await File.WriteAllTextAsync(Path.Combine(historyDirectory, now.UtcDateTime.ToString("yyyy-MM-dd") + ".jsonl"),
            $$"""{"checkedAt":"{{now:O}}","siteId":"legacy","routeId":"direct","status":"Healthy","latencyMs":123,"httpStatus":200,"error":null}""" + "\n" +
            $$$"""{"checkedAt":"{{{now:O}}}","siteId":"version11","routeId":"direct","status":"Healthy","latencyMs":250,"httpStatus":200,"error":null,"transfer":{"bytesReceived":125000,"totalMilliseconds":250.5,"completion":"EndOfFile","url":"https://example.com/page?q=1"}}""" + "\n");
        var history = new HistoryStore(directory.Path);
        await history.LoadAsync(1);
        var sample = new ProbeResult(now, "new", "direct", ProbeStatus.Healthy, 250, 200, null,
            new UrlTransfer(125000, 250.5, DownloadCompletion.EndOfFile, "https://example.com/page?q=1", 100.5));
        await history.RecordAsync([sample], 1, default);
        var restored = new HistoryStore(directory.Path);
        await restored.LoadAsync(1);
        var samples = restored.Query(now.AddMinutes(-1), DateTimeOffset.UtcNow);
        Check.That(samples.Single(item => item.SiteId == "legacy").Transfer is null, "Old history gained false speed data");
        var oldTransfer = samples.Single(item => item.SiteId == "version11").Transfer!;
        Check.That(oldTransfer.BytesReceived == 125000 && oldTransfer.TotalMilliseconds == 250.5 &&
            oldTransfer.TransferMilliseconds is null && oldTransfer.MegabytesPerSecond is null,
            "Version 1.1 total-time average was reinterpreted as body download speed");
        Check.That(samples.Single(item => item.SiteId == "new") == sample, "URL transfer history did not survive restart");
        Check.That(restored.Warning is null, "Compatible history produced a warning");
    }
}
