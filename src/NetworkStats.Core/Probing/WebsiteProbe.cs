using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using NetworkStats.Models;

namespace NetworkStats.Probing;

public sealed class WebsiteProbe
{
    public async Task<ProbeResult> CheckAsync(
        SiteDefinition site, RouteDefinition route, MonitorSettings settings, CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        // 每次建立新连接，使 DNS、连接、TLS、重定向及响应头等待都计入耗时。
        using var handler = new SocketsHttpHandler
        {
            UseProxy = route.Address is not null,
            Proxy = route.Address is null ? null : new WebProxy(route.Address),
            ConnectTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseCookies = false
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Get, site.Url);
        request.Headers.UserAgent.ParseAdd("NetworkStats/1.0");
        request.Headers.Accept.ParseAdd("*/*");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var elapsed = stopwatch.ElapsedMilliseconds;
            var code = (int)response.StatusCode;
            var accessible = code is >= 200 and < 400;
            return new(checkedAt, site.Id, route.Id,
                !accessible ? ProbeStatus.Unreachable : elapsed > settings.SlowThresholdMs ? ProbeStatus.Slow : ProbeStatus.Healthy,
                elapsed, code, accessible ? null : $"HTTP {code} {response.ReasonPhrase}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure($"请求超时（{settings.TimeoutSeconds} 秒）");
        }
        catch (HttpRequestException exception)
        {
            return Failure(exception.InnerException?.Message ?? exception.Message);
        }
        catch (IOException exception)
        {
            return Failure(exception.Message);
        }

        ProbeResult Failure(string error) => new(checkedAt, site.Id, route.Id,
            ProbeStatus.Unreachable, stopwatch.ElapsedMilliseconds, null, error);
    }
}
