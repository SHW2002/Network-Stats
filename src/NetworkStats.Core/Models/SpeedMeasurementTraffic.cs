namespace NetworkStats.Models;

public static class SpeedMeasurementTraffic
{
    public const long MaximumBytesPerSample = 1_000_000;
    public const long EstimatedAvailabilityBytesPerSample = 20_000;

    public static double EstimateAvailabilityHourlyBytes(int intervalSeconds, int siteCount, int routeCount) =>
        EstimateHourlyBytes(EstimatedAvailabilityBytesPerSample, intervalSeconds, siteCount, routeCount);

    public static double EstimateMaximumHourlyBytes(int intervalSeconds, int siteCount, int routeCount)
        => EstimateHourlyBytes(MaximumBytesPerSample, intervalSeconds, siteCount, routeCount);

    private static double EstimateHourlyBytes(long bytesPerSample, int intervalSeconds, int siteCount, int routeCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalSeconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(siteCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(routeCount, 1);
        return bytesPerSample * (3600d / intervalSeconds) * siteCount * routeCount;
    }

    public static double EstimateMaximumHourlyBytes(MonitorSettings settings) =>
        EstimateMaximumHourlyBytes(settings.IntervalSeconds, settings.Sites.Length, settings.GetRoutes().Length);

    public static double EstimateEnabledHourlyBytes(MonitorSettings settings) =>
        !settings.SpeedMeasurementEnabled ? 0 : EnabledHourlyBytes(settings);

    private static double EnabledHourlyBytes(MonitorSettings settings)
    {
        var routeCount = settings.GetRoutes().Count(route => route.SpeedMeasurementEnabled);
        return routeCount == 0 ? 0 : EstimateMaximumHourlyBytes(settings.IntervalSeconds, settings.Sites.Length, routeCount);
    }
}
