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
        var start = now.AddMinutes(-minutes);
        var retentionStart = now.AddHours(-settings.RetentionHours);
        if (start < retentionStart) start = retentionStart;
        // 横轴按已完成探测轮次排列；未完成的请求和没有发生探测的时间不会合成空格。
        var relevant = published.Where(sample => sample.CheckedAt <= now && sample.SampleTime >= start &&
            sample.SampleTime <= now && sites.Contains(sample.SiteId) && routes.Contains(sample.RouteId))
            .OrderBy(sample => sample.SampleTime).ThenBy(sample => sample.CheckedAt).ToArray();
        return new(start, relevant.Length == 0 ? null : relevant[^1].SampleTime, relevant);
    }
}
