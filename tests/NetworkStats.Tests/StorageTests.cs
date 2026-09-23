using NetworkStats.Configuration;
using NetworkStats.Models;
using NetworkStats.Storage;

namespace NetworkStats.Tests;

internal static class StorageTests
{
    public static async Task ValidateAndPersistSettingsAsync()
    {
        using var temporary = new TemporaryDirectory();
        var settings = new MonitorSettings
        {
            Sites = [new("  Local  ", "HTTP://EXAMPLE.COM")],
            Proxies = [new(" 本地代理 ", "SOCKS5", "127.0.0.1", 7890)]
        };
        var store = new SettingsStore(temporary.Path, settings);
        Check.That(store.Current.Sites[0].Url == "http://example.com/", "URL was not normalized");
        var siteId = store.Current.Sites[0].Id;
        var proxyId = store.Current.Proxies[0].Id;
        await store.SaveAsync(store.Current with { SlowThresholdMs = 1200 }, default);
        var reloaded = new SettingsStore(temporary.Path, new MonitorSettings());
        Check.That(reloaded.Current.SlowThresholdMs == 1200 && reloaded.Current.Sites[0].Name == "Local", "Settings did not survive restart");
        Check.That(reloaded.Current.Sites[0].Id == siteId && reloaded.Current.Proxies[0].Id == proxyId, "Stable IDs changed after serialization");
        Check.That((reloaded.Current.Sites[0] with { Name = "renamed" }).Id == siteId, "Renaming a target lost history identity");
        Reject(settings with { Sites = null! });
        Reject(settings with { Sites = [new("wrong", "file:///tmp/data")] });
        Reject(settings with { Sites = [new("one", "https://example.com"), new("two", "https://example.com/")] });
        Reject(settings with { Proxies = [new("wrong", "socks5", "127.0.0.1", 70000)] });
        Reject(settings with { Proxies = [new("wrong", "ftp", "127.0.0.1", 7890)] });
        Reject(settings with { SlowThresholdMs = 12000, TimeoutSeconds = 10 });
    }

    public static async Task HistoryBucketsAndRecoveryAsync()
    {
        using var temporary = new TemporaryDirectory();
        var history = new HistoryStore(temporary.Path);
        await history.LoadAsync(1);
        var minute = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds() / 60 * 60);
        var early = new ProbeResult(minute.AddSeconds(1), "site", "direct", ProbeStatus.Healthy, 100, 200, null);
        var late = early with { CheckedAt = minute.AddSeconds(2), Status = ProbeStatus.Slow, LatencyMs = 2500 };
        var expired = early with { CheckedAt = minute.AddHours(-2) };
        await history.RecordAsync([late, early, expired], 1, default);
        var samples = history.Query(DateTimeOffset.UtcNow.AddHours(-3), DateTimeOffset.UtcNow);
        Check.That(samples.Length == 1 && samples[0] == late, "Minute buckets or retention are incorrect");
        var path = Path.Combine(temporary.Path, "history", minute.UtcDateTime.ToString("yyyy-MM-dd") + ".jsonl");
        await File.AppendAllTextAsync(path, "truncated-json");
        var next = late with { CheckedAt = minute.AddMinutes(1) };
        await history.RecordAsync([next], 1, default);
        var restored = new HistoryStore(temporary.Path);
        await restored.LoadAsync(1);
        var restoredSamples = restored.Query(minute, DateTimeOffset.UtcNow);
        Check.That(restoredSamples.Length == 2, "A torn last line swallowed the next valid sample");
        Check.That(restored.Warning is not null, "Corrupt history was not reported");
        Check.That(restored.Query(minute.AddMinutes(3), minute.AddMinutes(5)).Length == 0,
            "Missing minutes were filled with invented samples");
    }

    private static void Reject(MonitorSettings settings)
    {
        try { SettingsValidator.Normalize(settings); }
        catch (SettingsValidationException) { return; }
        throw new InvalidOperationException("Invalid settings were accepted");
    }
}
