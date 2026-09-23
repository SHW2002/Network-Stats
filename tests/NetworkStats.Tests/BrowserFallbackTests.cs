using NetworkStats.Models;
using NetworkStats.Probing;

namespace NetworkStats.Tests;

internal static class BrowserFallbackTests
{
    public static async Task RoutingAndBudgetAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var browser = new CapturingBrowser();
        var probe = new DownloadSpeedProbe(browser);
        var route = new RouteDefinition("direct", "直连", null);
        var request = new DownloadSpeedRequest(server.Address + "/challenge?exact=1%202", TimeSpan.FromSeconds(5));
        var first = await probe.MeasureAsync(request, route);
        Check.That(first.Succeeded && first.UsedBrowser && browser.Request?.Url == request.Url && browser.Route == route,
            "Fallback changed the configured URL or route");
        Check.That(browser.Request!.TimeLimit < request.TimeLimit && first.Download.TotalTime > browser.Elapsed,
            "Fallback reset the timeout budget or omitted initial HTTP waiting");
        var count = server.Requests;
        await probe.MeasureAsync(request, route);
        Check.That(server.Requests == count && browser.Calls == 2, "Known browser target repeated rejected HTTP requests");
        await probe.MeasureAsync(request with { Url = server.Address + "/denied" }, route);
        Check.That(browser.Calls == 2, "Generic access denial unexpectedly launched a browser");
        var otherRoute = new RouteDefinition("proxy", "HTTP", server.Address);
        await probe.MeasureAsync(request, otherRoute);
        Check.That(server.Requests == count + 2 && browser.Route == otherRoute,
            "Browser preference or route was shared across direct and proxy connections");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        try { await probe.MeasureAsync(request, route, cancellationToken: canceled.Token); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Canceled browser route was not canceled");
    }

    private sealed class CapturingBrowser : IBrowserDownloadProbe
    {
        public int Calls { get; private set; }
        public DownloadSpeedRequest? Request { get; private set; }
        public RouteDefinition? Route { get; private set; }
        public TimeSpan Elapsed { get; } = TimeSpan.FromMilliseconds(10);
        public Task<DownloadSpeedResult> MeasureAsync(DownloadSpeedRequest options, RouteDefinition route,
            IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            Calls++;
            Request = options;
            Route = route;
            return Task.FromResult(new DownloadSpeedResult(options.Url, route.Name, new(100, Elapsed, Elapsed),
                DownloadCompletion.EndOfFile, 200, null, UsedBrowser: true));
        }
    }
}
