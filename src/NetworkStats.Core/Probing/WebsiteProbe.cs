using NetworkStats.Models;

namespace NetworkStats.Probing;

public sealed class WebsiteProbe(DownloadSpeedProbe? downloads = null, WebsiteAvailabilityProbe? availability = null)
{
    public const long SampleByteLimit = SpeedMeasurementTraffic.MaximumBytesPerSample;
    private readonly DownloadSpeedProbe _downloads = downloads ?? new();
    private readonly WebsiteAvailabilityProbe _availability = availability ?? new();

    public async Task<ProbeResult> CheckAsync(
        SiteDefinition site, RouteDefinition route, MonitorSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.SpeedMeasurementEnabled || !route.SpeedMeasurementEnabled)
            return await _availability.CheckAsync(site, route, settings, cancellationToken).ConfigureAwait(false);

        var checkedAt = DateTimeOffset.UtcNow;
        // 同一个 GET 请求同时检查可访问性并计量该 URL 的响应体，避免测量其他服务器。
        var result = await _downloads.MeasureAsync(
            new(site.Url, TimeSpan.FromSeconds(settings.TimeoutSeconds), SampleByteLimit),
            route, cancellationToken: cancellationToken).ConfigureAwait(false);
        var elapsed = (long)result.Download.TotalTime.TotalMilliseconds;
        var accessible = result.HttpStatus is >= 200 and < 300 && result.Completion != DownloadCompletion.Failed;
        var transfer = new UrlTransfer(result.Download.BytesReceived, result.Download.TotalTime.TotalMilliseconds,
            result.Completion, result.Url, result.Download.TransferTime.TotalMilliseconds);
        return new(checkedAt, site.Id, route.Id,
            !accessible ? ProbeStatus.Unreachable : elapsed > settings.SlowThresholdMs ? ProbeStatus.Slow : ProbeStatus.Healthy,
            elapsed, result.HttpStatus, accessible ? null : result.Error, transfer);
    }
}
