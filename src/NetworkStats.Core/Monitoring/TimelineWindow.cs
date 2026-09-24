using NetworkStats.Models;

namespace NetworkStats.Monitoring;

public sealed record TimelineWindow(DateTimeOffset Start, DateTimeOffset? End, ProbeResult[] Samples)
{
    public static TimelineWindow Create(IEnumerable<ProbeResult> published, MonitorSettings settings,
        int minutes, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minutes, 1440);
        var sites = settings.Sites.Select(site => site.Id).ToHashSet();
        var routes = settings.GetRoutes().Select(route => route.Id).ToHashSet();
        var cutoff = now.AddHours(-settings.RetentionHours);
        var relevant = published.Where(sample => sample.CheckedAt >= cutoff && sample.CheckedAt <= now &&
            sample.Minute <= now.ToUnixTimeSeconds() / 60 && sites.Contains(sample.SiteId) && routes.Contains(sample.RouteId)).ToArray();
        // 只有已发布的结果才能推进时间轴；时钟过整分钟、排队或等待响应不会新增空色块。
        var last = relevant.Length == 0 ? (long?)null : relevant.Max(sample => sample.Minute);
        var first = (last ?? now.ToUnixTimeSeconds() / 60) - minutes + 1;
        return new(DateTimeOffset.FromUnixTimeSeconds(first * 60),
            last is { } minute ? DateTimeOffset.FromUnixTimeSeconds(minute * 60) : null,
            relevant.Where(sample => sample.Minute >= first).ToArray());
    }
}
