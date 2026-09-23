using System.Net;
using NetworkStats.Models;
using NetworkStats.Probing;

namespace NetworkStats.Tests;

internal static class DownloadSpeedTests
{
    private static readonly DownloadSpeedProbe Probe = new();
    private static readonly RouteDefinition Direct = new("direct", "直连", null);
    private static DownloadSpeedRequest Request(string url) => new(url, TimeSpan.FromSeconds(5));

    public static async Task StreamingAndDirectAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var original = HttpClient.DefaultProxy;
        var forbidden = new ForbiddenProxy();
        HttpClient.DefaultProxy = forbidden;
        try
        {
            var progress = new ProgressCapture();
            var result = await Probe.MeasureAsync(Request(server.Address + "/download-slow"), Direct, progress);
            Check.That(result.Succeeded && result.Completion == DownloadCompletion.EndOfFile, "Download failed");
            Check.That(result.Download.BytesReceived == DownloadTestEndpoints.Size, "Did not count the actual body bytes");
            Check.That(result.Download.TransferTime.TotalMilliseconds >= 550, "Measurement finished at the headers");
            Check.That(result.Download.MegabitsPerSecond is > 0 and < 10, "Throttled download speed is not plausible");
            Check.That(progress.Values.Count >= 2 && progress.Values.Any(value => value.BytesReceived < DownloadTestEndpoints.Size), "Missing streaming progress");
            Check.That(forbidden.Calls == 0 && server.LastAcceptEncoding == "identity", "Direct route or encoding policy was ignored");
        }
        finally { HttpClient.DefaultProxy = original; }
    }

    public static async Task LimitsAndCancellationAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var capped = await Probe.MeasureAsync(Request(server.Address + "/download-endless") with { ByteLimit = 100_003 }, Direct);
        Check.That(capped.Succeeded && capped.Completion == DownloadCompletion.ByteLimit && capped.Download.BytesReceived == 100_003,
            "Unknown-length stream exceeded the byte cap");
        var timed = await Probe.MeasureAsync(Request(server.Address + "/download-endless") with { TimeLimit = TimeSpan.FromSeconds(1) }, Direct);
        Check.That(timed.Succeeded && timed.Completion == DownloadCompletion.TimeLimit && timed.Download.TotalTime.TotalSeconds < 3,
            "Time-limited streaming download did not finish with a sample");
        var stalled = await Probe.MeasureAsync(Request(server.Address + "/stream") with { TimeLimit = TimeSpan.FromSeconds(1) }, Direct);
        Check.That(!stalled.Succeeded && stalled.Error!.Contains("超时"), "Stalled response body was reported as a speed");
        var beforeHeaders = await Probe.MeasureAsync(Request(server.Address + "/timeout") with { TimeLimit = TimeSpan.FromSeconds(1) }, Direct);
        Check.That(!beforeHeaders.Succeeded && beforeHeaders.HttpStatus is null, "Header timeout was not enforced");
        using var cancel = new CancellationTokenSource();
        var progress = new ProgressCapture(_ => cancel.Cancel());
        try
        {
            await Probe.MeasureAsync(Request(server.Address + "/download-endless"), Direct, progress, cancel.Token);
            throw new InvalidOperationException("User cancellation was reported as a completed speed test");
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        Check.That(progress.Values.Any(value => value.BytesReceived > 0), "Cancellation did not happen during a real transfer");
    }

    public static async Task RedirectErrorsAndEncodingAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var redirect = await Probe.MeasureAsync(Request(server.Address + "/download-redirect"), Direct);
        Check.That(redirect.Succeeded && redirect.Url.EndsWith("/download") && redirect.Download.BytesReceived == DownloadTestEndpoints.Size,
            "Redirect target body was not measured");
        foreach (var path in new[] { "/denied", "/fast", "/download-truncated" })
        {
            var result = await Probe.MeasureAsync(Request(server.Address + path), Direct);
            Check.That(!result.Succeeded && result.Error is not null, $"Invalid download produced a successful speed: {path}");
        }
        var compressed = await Probe.MeasureAsync(Request(server.Address + "/download-compressed"), Direct);
        Check.That(compressed.Succeeded && compressed.Download.BytesReceived == DownloadTestEndpoints.Compressed.Length,
            "Speed used decompressed bytes instead of received response body bytes");
    }

    public static async Task ProxyRoutesAsync()
    {
        await using var http = await LocalHttpServer.StartAsync();
        var throughHttp = await Probe.MeasureAsync(Request("http://download-target.invalid/download"), new("http", "HTTP 线路", http.Address));
        Check.That(throughHttp.Succeeded && throughHttp.RouteName == "HTTP 线路" && throughHttp.Download.BytesReceived == DownloadTestEndpoints.Size,
            "HTTP proxy did not transfer the speed-test body");
        Check.That(http.LastRawTarget == "http://download-target.invalid/download", "Wrong HTTP proxy target");
        await using var socks = new SocksProxy(new byte[100_000]);
        var throughSocks = await Probe.MeasureAsync(Request("http://download-target.invalid/file"),
            new("socks", "SOCKS5 线路", $"socks5://127.0.0.1:{socks.Port}"));
        Check.That(throughSocks.Succeeded && throughSocks.Download.BytesReceived == 100_000 && socks.RequestedHost == "download-target.invalid",
            "SOCKS5 speed test or proxy-side DNS failed");
    }

    private sealed class ProgressCapture(Action<DownloadProgress>? received = null) : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Values { get; } = [];
        public void Report(DownloadProgress value) { Values.Add(value); received?.Invoke(value); }
    }

    private sealed class ForbiddenProxy : IWebProxy
    {
        public int Calls { get; private set; }
        public ICredentials? Credentials { get; set; }
        public Uri GetProxy(Uri destination) { Calls++; throw new InvalidOperationException("System proxy used"); }
        public bool IsBypassed(Uri host) { Calls++; return false; }
    }
}
