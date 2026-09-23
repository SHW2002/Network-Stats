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
        Check.That(store.Current.Theme == ThemeMode.System && !store.Current.MinimizeOnClose,
            "Legacy preferences did not use system theme and normal close defaults");
        Check.That(store.Current.Sites is [{ Name: "Existing" }], "Loading preferences replaced existing site configuration");
        Check.That(new MonitorSettings().Sites.Any(site => site.Name == "ChatGPT" && site.Url == "https://chatgpt.com/"),
            "ChatGPT is missing from the default URL list");
        foreach (var theme in Enum.GetValues<ThemeMode>())
        {
            await store.SaveAsync(store.Current with { Theme = theme, MinimizeOnClose = true }, default);
            var restored = new SettingsStore(directory.Path, new MonitorSettings());
            Check.That(restored.Current.Theme == theme && restored.Current.MinimizeOnClose,
                "Theme/close preferences did not survive a settings-store restart");
        }
        await store.SaveAsync(store.Current with { MinimizeOnClose = false }, default);
        Check.That(!new SettingsStore(directory.Path, new MonitorSettings()).Current.MinimizeOnClose,
            "Close preference could not be disabled");
        try { SettingsValidator.Normalize(store.Current with { Theme = (ThemeMode)99 }); }
        catch (SettingsValidationException) { return; }
        throw new InvalidOperationException("Unknown theme value was accepted");
    }
}
