namespace NetworkStats.Models;

public enum ProbeStatus { Healthy, Slow, Unreachable }

public sealed record ProbeResult(
    DateTimeOffset CheckedAt,
    string SiteId,
    string RouteId,
    ProbeStatus Status,
    long LatencyMs,
    int? HttpStatus,
    string? Error,
    UrlTransfer? Transfer = null)
{
    public long Minute => CheckedAt.ToUnixTimeSeconds() / 60;
}

public sealed record WorkerStatus(
    bool Running,
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastCompletedAt,
    DateTimeOffset? NextRunAt,
    string? Error);
