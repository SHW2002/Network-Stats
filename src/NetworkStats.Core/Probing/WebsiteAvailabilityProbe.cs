using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using NetworkStats.Models;

namespace NetworkStats.Probing;

public sealed class WebsiteAvailabilityProbe
{
    public async Task<ProbeResult> CheckAsync(
        SiteDefinition site, RouteDefinition route, MonitorSettings settings, CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var handler = new SocketsHttpHandler
        {
            UseProxy = route.Address is not null,
            Proxy = route.Address is null ? null : new WebProxy(route.Address),
            ConnectTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            AutomaticDecompression = DecompressionMethods.None,
            MaxResponseDrainSize = 0,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseCookies = false
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Get, site.Url);
        request.Headers.UserAgent.ParseAdd("NetworkStats/1.0");
        request.Headers.AcceptEncoding.ParseAdd("identity");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        try
        {
            // 只等到响应头，不主动读取响应体，避免关闭周期网速检测后持续消耗下载流量。
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            var elapsed = stopwatch.ElapsedMilliseconds;
            var failure = HttpResponseFailure.Describe(response, measuringSpeed: false);
            return new(checkedAt, site.Id, route.Id,
                failure is not null ? ProbeStatus.Unreachable : elapsed > settings.SlowThresholdMs ? ProbeStatus.Slow : ProbeStatus.Healthy,
                elapsed, (int)response.StatusCode, failure);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure($"请求超时（{settings.TimeoutSeconds} 秒）");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Failure(exception.InnerException?.Message ?? exception.Message);
        }

        ProbeResult Failure(string error) => new(checkedAt, site.Id, route.Id,
            ProbeStatus.Unreachable, stopwatch.ElapsedMilliseconds, null, error);
    }
}
