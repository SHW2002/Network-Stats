using NetworkStats.Configuration;
using NetworkStats.Models;
using NetworkStats.Monitoring;
using NetworkStats.Probing;
using NetworkStats.Storage;

namespace NetworkStats.Tests;

internal static class TimelineTests
{
    public static Task AdvanceOnlyWithPublishedResultsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var minute = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() / 60 * 60);
        var settings = new MonitorSettings { Sites = [new("site", "https://example.com/")] };
        var site = settings.Sites[0];
        var previous = new ProbeResult(minute.AddMinutes(-1), site.Id, "direct", ProbeStatus.Healthy, 10, 200, null);
        var before = TimelineWindow.Create([previous], settings, 60, minute.AddSeconds(-1));
        var pending = TimelineWindow.Create([previous], settings, 60, minute.AddSeconds(20));
        Check.That(before.End == pending.End && pending.Samples.SequenceEqual([previous]) &&
            before.Start < pending.Start, "A clock boundary should move the range without inventing a probe cell");
        // 完成跨分钟的一轮，按整轮开始时间排列，而不是按分钟归档。
        var current = previous with { CheckedAt = minute.AddMinutes(1).AddSeconds(5), RoundStartedAt = minute.AddSeconds(50) };
        var completed = TimelineWindow.Create([previous, current], settings, 60, minute.AddMinutes(1).AddSeconds(10));
        Check.That(completed.End == current.SampleTime && completed.Samples.SequenceEqual([previous, current]),
            "A round spanning a minute did not create one cell for the completed probe");
        var later = current with { CheckedAt = minute.AddMinutes(4), RoundStartedAt = minute.AddMinutes(4) };
        var resumed = TimelineWindow.Create([previous, current, later], settings, 5, minute.AddMinutes(4).AddSeconds(2));
        Check.That(resumed.End == later.SampleTime && resumed.Samples.SequenceEqual([current, later]),
            "Unmeasured time did not remain empty between probe cells");
        var removed = later with { SiteId = "removed-site", CheckedAt = minute.AddMinutes(5), RoundStartedAt = null };
        var unrelated = TimelineWindow.Create([previous, removed], settings, 60, minute.AddMinutes(5));
        Check.That(unrelated.End == before.End, "Removed configuration advanced the visible timeline");
        Check.That(TimelineWindow.Create([], settings, 60, now).End is null, "First run displayed unmeasured cells");
        Check.That(TimelineWindow.Create([previous], settings, 60, now.AddHours(169)).End is null,
            "Expired historical data remained displayed");
        return Task.CompletedTask;
    }

    public static async Task PublishWholeRoundAsync()
    {
        using var directory = new TemporaryDirectory();
        await using var server = await LocalHttpServer.StartAsync();
        var settings = new MonitorSettings { Sites = [new("fast", server.Address + "/fast"), new("held", server.Address + "/controlled")] };
        var history = new HistoryStore(directory.Path);
        var older = DateTimeOffset.UtcNow.AddMinutes(-3);
        var seeds = settings.Sites.Select(site => new ProbeResult(older, site.Id, "direct", ProbeStatus.Healthy, 10, 200, null)).ToArray();
        await history.RecordAsync(seeds, 168, default);
        await using var engine = new MonitorEngine(new SettingsStore(directory.Path, settings), history, new WebsiteProbe());
        await engine.StartAsync();
        await Check.EventuallyAsync(() => server.Requests == 2, "Round did not reach both targets");
        var pending = engine.Snapshot();
        Check.That(pending.Worker.Running && pending.WindowEnd == seeds[0].SampleTime &&
            pending.Samples.Length == 2,
            $"Partially completed round shifted the timeline or exposed partial results: running={pending.Worker.Running}, end={pending.WindowEnd:O}, seed={seeds[0].SampleTime:O}, count={pending.Samples.Length}");
        server.ReleaseResponse.TrySetResult();
        await Check.EventuallyAsync(() => engine.Status.LastCompletedAt is not null, "Round did not publish");
        var completed = engine.Snapshot();
        var latest = completed.Samples.Where(sample => sample.RoundStartedAt == engine.Status.LastStartedAt).ToArray();
        Check.That(latest.Length == 2 && latest.All(sample => sample.SampleTime == completed.WindowEnd),
            "Completed round did not create one aligned cell per configured row");
        Check.That(completed.Samples.Length == 4, "Publishing a round replaced historical samples");
        var loaded = new HistoryStore(directory.Path);
        await loaded.LoadAsync(168);
        var restored = loaded.LatestWindow(settings, 60, DateTimeOffset.UtcNow);
        Check.That(restored.End == completed.WindowEnd && latest.All(sample => restored.Samples.Contains(sample)),
            "Round grouping or last visible minute did not survive restart");
    }
}
