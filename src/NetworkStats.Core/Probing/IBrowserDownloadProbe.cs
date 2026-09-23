using NetworkStats.Models;

namespace NetworkStats.Probing;

public interface IBrowserDownloadProbe
{
    Task<DownloadSpeedResult> MeasureAsync(DownloadSpeedRequest options, RouteDefinition route,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
