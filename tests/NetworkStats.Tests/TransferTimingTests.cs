using NetworkStats.Models;
using NetworkStats.Probing;

namespace NetworkStats.Tests;

internal static class TransferTimingTests
{
    public static async Task SeparateWaitingAndDownloadingAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var url = server.Address + "/download-late-headers";
        var route = new RouteDefinition("direct", "直连", null);
        var result = await new DownloadSpeedProbe().MeasureAsync(new(url, TimeSpan.FromSeconds(5)), route);
        var download = result.Download;
        Check.That(result.Succeeded && download.BytesReceived == DownloadTestEndpoints.Size, "Delayed response did not download");
        Check.That(download.ResponseWaitTime.TotalMilliseconds >= 900 && download.TransferTime.TotalMilliseconds >= 550,
            "Response waiting and body transfer were not measured separately");
        var overallRate = download.BytesReceived / 1_000_000.0 / download.TotalTime.TotalSeconds;
        Check.That(download.MegabytesPerSecond > overallRate * 1.4, "Server wait still reduced the displayed download speed");
        Check.That(result.ShortSample, "Small page was presented as sustained throughput");

        var sample = await new WebsiteProbe().CheckAsync(new("delayed", url), route,
            new MonitorSettings { SpeedMeasurementEnabled = true, TimeoutSeconds = 5, SlowThresholdMs = 1000 }, default);
        var transfer = sample.Transfer!;
        Check.That(sample.Status == ProbeStatus.Slow && sample.LatencyMs >= 1500 && transfer.ResponseWaitMilliseconds >= 900,
            "Visit latency/color lost the server wait while separating download speed");
        Check.That(transfer.TransferMilliseconds is >= 550 && transfer.MegabytesPerSecond is > 0 && transfer.ShortSample,
            "Automatic URL sample lost body timing, rate or small-sample indicator");
        Check.That(Math.Abs(transfer.MegabytesPerSecond!.Value - transfer.BytesReceived / 1000.0 / transfer.TransferMilliseconds!.Value) < 0.000001,
            "Saved automatic download rate differs from response-body throughput");
    }
}
