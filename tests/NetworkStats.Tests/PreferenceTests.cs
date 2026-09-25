using NetworkStats.Configuration;
using NetworkStats.Models;

namespace NetworkStats.Tests;

internal static class PreferenceTests
{
    public static async Task DefaultsAndPersistenceAsync()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "settings.json"),
            """{"intervalSeconds":60,"timeoutSeconds":10,"slowThresholdMs":1500,"maxConcurrency":12,"retentionHours":168,"sites":[{"name":"Existing","url":"https://example.com/"}],"proxies":[]}""");
        var store = new SettingsStore(directory.Path, new MonitorSettings());
        Check.That(store.Current.Theme == ThemeMode.System && !store.Current.MinimizeOnClose && !store.Current.LaunchOnStartup &&
            !store.Current.SpeedMeasurementEnabled && store.Current.DirectSpeedMeasurementEnabled,
            "Legacy preferences did not use system theme and normal close defaults");
        Check.That(store.Current.Sites is [{ Name: "Existing" }], "Loading preferences replaced existing site configuration");
        Check.That(new MonitorSettings().Sites.Select(site => site.Url).SequenceEqual(new[]
            { "https://baidu.com/", "https://google.com/", "https://github.com/", "https://pixiv.net/" }),
            "Default sites must contain exactly the four supported targets");
        foreach (var theme in Enum.GetValues<ThemeMode>())
        {
            await store.SaveAsync(store.Current with
                { Theme = theme, MinimizeOnClose = true, LaunchOnStartup = true, SpeedMeasurementEnabled = true,
                    DirectSpeedMeasurementEnabled = true,
                    Proxies = [new("Proxy", "http", "127.0.0.1", 7890, false)] }, default);
            var restored = new SettingsStore(directory.Path, new MonitorSettings());
            Check.That(restored.Current.Theme == theme && restored.Current.MinimizeOnClose && restored.Current.LaunchOnStartup &&
                restored.Current.SpeedMeasurementEnabled && restored.Current.DirectSpeedMeasurementEnabled &&
                !restored.Current.Proxies[0].SpeedMeasurementEnabled,
                "Theme/close preferences did not survive a settings-store restart");
        }
        await store.SaveAsync(store.Current with
            { MinimizeOnClose = false, LaunchOnStartup = false, SpeedMeasurementEnabled = false }, default);
        Check.That(!new SettingsStore(directory.Path, new MonitorSettings()).Current.MinimizeOnClose,
            "Close preference could not be disabled");
        Check.That(!new SettingsStore(directory.Path, new MonitorSettings()).Current.LaunchOnStartup,
            "Startup preference could not be disabled");
        Check.That(!new SettingsStore(directory.Path, new MonitorSettings()).Current.SpeedMeasurementEnabled,
            "Speed measurement preference could not be disabled");
        Check.That(new SettingsStore(directory.Path, new MonitorSettings()).Current.DirectSpeedMeasurementEnabled,
            "Direct speed measurement preference could not be enabled independently");
        var defaults = new MonitorSettings();
        Check.That(SpeedMeasurementTraffic.EstimateAvailabilityHourlyBytes(defaults.IntervalSeconds,
                defaults.Sites.Length, defaults.GetRoutes().Length) == 4_800_000 &&
            SpeedMeasurementTraffic.EstimateMaximumHourlyBytes(defaults) == 240_000_000,
            "Hourly ping/speed traffic estimates do not match the default schedule");
        Check.That(SpeedMeasurementTraffic.EstimateEnabledHourlyBytes(defaults with
                { SpeedMeasurementEnabled = true, DirectSpeedMeasurementEnabled = false }) == 0,
            "Disabling every speed route should produce a zero hourly estimate");
        try { SettingsValidator.Normalize(store.Current with { Theme = (ThemeMode)99 }); }
        catch (SettingsValidationException) { return; }
        throw new InvalidOperationException("Unknown theme value was accepted");
    }
}
