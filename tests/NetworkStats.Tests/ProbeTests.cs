using System.Net;
using NetworkStats.Models;
using NetworkStats.Probing;

namespace NetworkStats.Tests;

internal static class ProbeTests
{
    private static readonly WebsiteProbe Probe = new();
    private static readonly RouteDefinition Direct = new("direct", "直连", null);
    private static readonly MonitorSettings Settings = new()
        { SpeedMeasurementEnabled = true, TimeoutSeconds = 5, SlowThresholdMs = 4000 };

    public static async Task DirectBypassesSystemProxyAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var original = HttpClient.DefaultProxy;
        var forbidden = new ForbiddenProxy();
        HttpClient.DefaultProxy = forbidden;
        try
        {
            var result = await Probe.CheckAsync(new("local", server.Address + "/fast"), Direct, Settings, default);
            Check.That(result.Status == ProbeStatus.Healthy && result.HttpStatus == 204, "Direct request failed");
            Check.That(forbidden.Calls == 0, "Direct route consulted the system proxy");
        }
        finally { HttpClient.DefaultProxy = original; }
    }

    public static async Task ColorsRedirectAndBodyAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var slow = await Probe.CheckAsync(new("slow", server.Address + "/slow"), Direct, Settings with { SlowThresholdMs = 50 }, default);
        Check.That(slow.Status == ProbeStatus.Slow && slow.LatencyMs >= 300, "Slow response was not yellow");
        var denied = await Probe.CheckAsync(new("denied", server.Address + "/denied"), Direct, Settings, default);
        Check.That(denied.Status == ProbeStatus.Unreachable && denied.HttpStatus == 403 && denied.Error!.Contains("403"), "HTTP rejection was not red");
        var redirect = await Probe.CheckAsync(new("redirect", server.Address + "/redirect"), Direct, Settings, default);
        Check.That(redirect.HttpStatus == 204, "Redirect was not followed");
        var stream = await Probe.CheckAsync(new("stream", server.Address + "/stream"), Direct, Settings with { TimeoutSeconds = 2 }, default);
        Check.That(stream.Status == ProbeStatus.Unreachable && stream.Transfer?.BytesReceived == 0,
            "Headers without body data were incorrectly treated as a completed URL visit");
    }

    public static async Task TimeoutAndCancellationAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var timeout = await Probe.CheckAsync(new("timeout", server.Address + "/timeout"), Direct,
            Settings with { TimeoutSeconds = 1, SlowThresholdMs = 500 }, default);
        Check.That(timeout.Status == ProbeStatus.Unreachable && timeout.Error!.Contains("超时"), "Timeout was not reported");
        using var stop = new CancellationTokenSource(100);
        try
        {
            await Probe.CheckAsync(new("cancel", server.Address + "/timeout"), Direct, Settings, stop.Token);
            throw new InvalidOperationException("User cancellation was recorded as a probe result");
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    public static async Task HttpProxyAsync()
    {
        await using var proxy = await LocalHttpServer.StartAsync();
        var result = await Probe.CheckAsync(new("proxy target", "http://target.invalid/fast"),
            new("test-proxy", "HTTP", proxy.Address), Settings, default);
        Check.That(result.Status == ProbeStatus.Healthy, "HTTP proxy request failed");
        Check.That(proxy.LastRawTarget == "http://target.invalid/fast", "Request did not reach the configured HTTP proxy");
        Check.That(result.RouteId == "test-proxy", "Route attribution was lost");
    }

    public static async Task SocksProxyAsync()
    {
        await using var proxy = new SocksProxy();
        var result = await Probe.CheckAsync(new("SOCKS target", "http://socks-target.invalid/"),
            new("socks-test", "SOCKS5", $"socks5://127.0.0.1:{proxy.Port}"), Settings, default);
        Check.That(result.Status == ProbeStatus.Healthy, $"SOCKS request failed: {result.Error}");
        Check.That(proxy.RequestedHost == "socks-target.invalid" && proxy.RequestedPort == 80,
            "SOCKS5 did not pass the target hostname to the proxy");
    }

    private sealed class ForbiddenProxy : IWebProxy
    {
        public int Calls { get; private set; }
        public ICredentials? Credentials { get; set; }
        public Uri GetProxy(Uri destination) { Calls++; throw new InvalidOperationException("System proxy used"); }
        public bool IsBypassed(Uri host) { Calls++; return false; }
    }
}
