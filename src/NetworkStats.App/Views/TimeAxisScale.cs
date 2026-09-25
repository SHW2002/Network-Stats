namespace NetworkStats.App.Views;

internal readonly record struct TimeAxisTick(DateTimeOffset Time, float X);

internal static class TimeAxisScale
{
    internal const string NowLabel = "现在";
    internal const float MinimumTickSpacing = 92;
    private const float PreferredTickLabelWidth = 100;
    private const float PreferredNowLabelWidth = 40;
    private static readonly int[] Intervals = [10, 20, 30, 60, 120, 180, 240, 360, 480, 720, 1440];

    internal static float TickLabelWidth(float width) => Math.Min(PreferredTickLabelWidth, Math.Max(0, width));
    internal static float NowLabelWidth(float width) => Math.Min(PreferredNowLabelWidth, Math.Max(0, width));
    internal static float NowPosition(float width) => Math.Max(0, width) - NowLabelWidth(width) / 2;

    internal static int SelectIntervalMinutes(int rangeMinutes, float width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rangeMinutes, 1);
        var availableWidth = Math.Max(0, NowPosition(width) - TickLabelWidth(width) / 2);
        var maximumSegments = Math.Max(1, (int)Math.Floor(availableWidth / MinimumTickSpacing));
        return Intervals.FirstOrDefault(interval => Math.Ceiling((double)rangeMinutes / interval) <= maximumSegments,
            Intervals[^1]);
    }

    internal static TimeAxisTick[] Create(DateTimeOffset now, int rangeMinutes, float width)
    {
        if (width <= 0) return [];
        var interval = SelectIntervalMinutes(rangeMinutes, width);
        var segments = Math.Max(1, (int)Math.Ceiling((double)rangeMinutes / interval));
        var firstX = TickLabelWidth(width) / 2;
        var nowX = NowPosition(width);
        var spacing = (nowX - firstX) / segments;
        var alignedEnd = AlignedTickAtOrAfter(now, interval);
        var ticks = new TimeAxisTick[segments];
        for (var index = 0; index < segments; index++)
        {
            var minutesBeforeEnd = (segments - index) * interval;
            ticks[index] = new(alignedEnd.AddMinutes(-minutesBeforeEnd), firstX + index * spacing);
        }
        return ticks;
    }

    private static DateTimeOffset AlignedTickAtOrAfter(DateTimeOffset time, int intervalMinutes)
    {
        var utc = time.ToUniversalTime();
        var candidate = new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
        if (candidate < utc) candidate = candidate.AddMinutes(1);
        while (true)
        {
            var local = TimeZoneInfo.ConvertTime(candidate, TimeZoneInfo.Local);
            var localMinutes = local.DateTime.Ticks / TimeSpan.TicksPerMinute;
            if (localMinutes % intervalMinutes == 0) return candidate;
            candidate = candidate.AddMinutes(1);
        }
    }

}
