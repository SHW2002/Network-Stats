using System.Text.Json.Serialization;

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
    // 保留分钟值供旧版历史和兼容调用使用；新版时间图按 SampleTime 逐次排列。
    public long Minute => (RoundStartedAt ?? CheckedAt).ToUnixTimeSeconds() / 60;

    [JsonIgnore]
    public DateTimeOffset SampleTime => RoundStartedAt ?? CheckedAt;
}

public sealed record WorkerStatus(
    bool Running,
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastCompletedAt,
    DateTimeOffset? NextRunAt,
    string? Error);
