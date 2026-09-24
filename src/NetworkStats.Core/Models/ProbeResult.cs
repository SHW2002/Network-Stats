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
    UrlTransfer? Transfer = null,
    DateTimeOffset? RoundStartedAt = null)
{
    // 同轮请求可能排队跨分钟；按整轮起始分钟归档，保留各请求真实的 CheckedAt。
    public long Minute => (RoundStartedAt ?? CheckedAt).ToUnixTimeSeconds() / 60;
}

public sealed record WorkerStatus(
    bool Running,
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastCompletedAt,
    DateTimeOffset? NextRunAt,
    string? Error);
