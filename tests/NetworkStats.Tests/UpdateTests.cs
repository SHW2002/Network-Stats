using System.Net;
using NetworkStats.Configuration;
using NetworkStats.Models;
using NetworkStats.Updates;

namespace NetworkStats.Tests;

internal static class UpdateTests
{
    public static async Task ReleasesAsync()
    {
        using var handler = new ReleaseTestHandler();
        using var client = new HttpClient(handler);
        var updates = new GitHubReleaseClient(client);
        var release = await updates.CheckAsync();
        Check.That(release.IsNewerThan(new Version(2, 9, 0)) && !release.IsNewerThan(new Version(2, 10, 0, 0)), "Numeric versions were not compared correctly");
        Check.That(!release.IsNewerThan(new Version(3, 0)), "Updater proposed a downgrade");
        Check.That(release.Package?.Sha256 == handler.Hash && release.Notes == "Release notes", "Release details were lost");
        foreach (var mode in new[] { "draft", "prerelease", "invalid-version", "wrong-repository", "url-fragment" })
        {
            using var invalid = new ReleaseTestHandler
            {
                Draft = mode == "draft", Prerelease = mode == "prerelease",
                Tag = mode == "invalid-version" ? "next-build" : "v2.10.0", WrongRepository = mode == "wrong-repository",
                UrlSuffix = mode == "url-fragment" ? "#untrusted-fragment" : ""
            };
            using var invalidClient = new HttpClient(invalid);
            await FailsAsync(() => new GitHubReleaseClient(invalidClient).CheckAsync(), mode);
        }
    }

    public static async Task DownloadsAsync()
    {
        using var directory = new TemporaryDirectory();
        using var handler = new ReleaseTestHandler();
        using var client = new HttpClient(handler);
        var updates = new GitHubReleaseClient(client);
        var release = await updates.CheckAsync();
        var progress = new ProgressCapture();
        var result = await updates.DownloadAsync(release, directory.Path, progress);
        Check.That((await File.ReadAllBytesAsync(result.FilePath)).SequenceEqual(handler.Payload) && result.Sha256 == handler.Hash,
            "Downloaded package differs from release bytes");
        Check.That(progress.Received > 1 && progress.Last?.Received == handler.Payload.Length && progress.Last.Fraction == 1,
            "Download did not expose streaming progress");
        foreach (var mode in new[] { "corrupt", "truncated", "conflicting-checksum", "no-checksum" })
        {
            using var failureDirectory = new TemporaryDirectory();
            using var invalid = new ReleaseTestHandler
            {
                Corrupt = mode == "corrupt", Truncate = mode == "truncated",
                WrongChecksum = mode == "conflicting-checksum", NoChecksum = mode == "no-checksum"
            };
            using var invalidClient = new HttpClient(invalid);
            var invalidUpdates = new GitHubReleaseClient(invalidClient);
            await FailsAsync(async () => await invalidUpdates.DownloadAsync(await invalidUpdates.CheckAsync(), failureDirectory.Path), mode);
            Check.That(!Directory.EnumerateFiles(failureDirectory.Path).Any(), "A rejected package was left ready to install");
            if (mode.Contains("checksum")) Check.That(invalid.Downloads == 0, "Package was downloaded without consistent checksum information");
        }
        using var cancelledDirectory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        try
        {
            await updates.DownloadAsync(release, cancelledDirectory.Path, new ProgressCapture(() => cancellation.Cancel()), cancellation.Token);
            throw new InvalidOperationException("Cancelled download returned an installable package");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        Check.That(!Directory.EnumerateFiles(cancelledDirectory.Path).Any(), "Cancelled package was not cleaned up");
    }

    public static async Task ProxyConfigurationAsync()
    {
        using var directory = new TemporaryDirectory();
        var store = new SettingsStore(directory.Path, new MonitorSettings());
        Check.That(store.Current.UpdateProxy is null, "Updates must default to a direct connection");
        foreach (var address in new[] { "http://127.0.0.1:7890", "https://localhost:443", "socks5://[::1]:1080" })
        {
            await store.SaveAsync(store.Current with { UpdateProxy = " " + address + "/ " }, default);
            var loaded = new SettingsStore(directory.Path, new MonitorSettings());
            Check.That(loaded.Current.UpdateProxy == UpdateProxy.Normalize(address) && loaded.Current.Proxies.Length == 0,
                "Update proxy did not persist independently of probe routes");
        }
        foreach (var invalid in new[] { "127.0.0.1:7890", "ftp://localhost:21", "socks5://localhost", "http://host:0", "http://user:pass@host:80", "http://host:80/path" })
        {
            try { SettingsValidator.Normalize(store.Current with { UpdateProxy = invalid }); }
            catch (SettingsValidationException) { continue; }
            throw new InvalidOperationException("Invalid update proxy accepted: " + invalid);
        }
        await store.SaveAsync(store.Current with { UpdateProxy = "  " }, default);
        Check.That(new SettingsStore(directory.Path, new MonitorSettings()).Current.UpdateProxy is null, "Cannot restore direct updates");
    }

    public static async Task ProxyTransportAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var original = HttpClient.DefaultProxy;
        HttpClient.DefaultProxy = new ForbiddenProxy();
        try
        {
            using var direct = UpdateProxy.CreateClient(null);
            Check.That((await direct.GetByteArrayAsync(server.Address + "/download")).Length == DownloadTestEndpoints.Size,
                "Direct update transport used the system proxy");
            using var http = UpdateProxy.CreateClient(server.Address);
            await http.GetByteArrayAsync("http://updates.invalid/download");
            Check.That(server.LastRawTarget == "http://updates.invalid/download", "HTTP update proxy was bypassed");
            var requestsBeforeFailure = server.Requests;
            using var unavailable = UpdateProxy.CreateClient("http://127.0.0.1:1");
            try { await unavailable.GetByteArrayAsync(server.Address + "/download"); throw new InvalidOperationException("Dead proxy fell back to a direct connection"); }
            catch (HttpRequestException) { }
            Check.That(server.Requests == requestsBeforeFailure, "Unavailable proxy sent a direct request");
            await using var socks = new SocksProxy(new byte[100]);
            using var proxied = UpdateProxy.CreateClient($"socks5://127.0.0.1:{socks.Port}");
            Check.That((await proxied.GetByteArrayAsync("http://updates.invalid/file")).Length == 100 && socks.RequestedHost == "updates.invalid",
                "SOCKS5 update proxy or remote DNS failed");
        }
        finally { HttpClient.DefaultProxy = original; }
    }

    private static async Task FailsAsync(Func<Task> action, string scenario)
    {
        try { await action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Unsafe update accepted: " + scenario);
    }

    private sealed class ProgressCapture(Action? action = null) : IProgress<UpdateDownloadProgress>
    {
        public int Received { get; private set; }
        public UpdateDownloadProgress? Last { get; private set; }
        public void Report(UpdateDownloadProgress value) { Received++; Last = value; action?.Invoke(); }
    }
    private sealed class ForbiddenProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }
        public Uri GetProxy(Uri destination) => throw new InvalidOperationException("System proxy used");
        public bool IsBypassed(Uri host) => throw new InvalidOperationException("System proxy used");
    }
}
