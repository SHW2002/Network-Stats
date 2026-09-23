using NetworkStats.Configuration;
using NetworkStats.Models;
using NetworkStats.Monitoring;
using NetworkStats.Probing;
using NetworkStats.Storage;

namespace NetworkStats.Tests;

internal static class EngineTests
{
    public static async Task RoutesRemainIndependentAsync()
    {
        using var directory = new TemporaryDirectory();
        await using var target = await LocalHttpServer.StartAsync();
        await using var proxy = await LocalHttpServer.StartAsync();
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var unusedPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var settings = new MonitorSettings
        {
            Sites = [new("target", target.Address + "/fast")],
            Proxies = [new("working", "http", "127.0.0.1", new Uri(proxy.Address).Port),
                new("unavailable", "http", "127.0.0.1", unusedPort)]
        };
        await using var engine = new MonitorEngine(new SettingsStore(directory.Path, settings), new HistoryStore(directory.Path), new WebsiteProbe());
        await engine.StartAsync();
        await Check.EventuallyAsync(() => engine.Status.LastCompletedAt is not null, "Multi-route round did not finish");
        var samples = engine.Snapshot().Samples;
        Check.That(samples.Length == 3, "Did not probe all direct and proxy combinations");
        Check.That(samples.Single(sample => sample.RouteId == "direct").Status != ProbeStatus.Unreachable, "Failed proxy contaminated direct route");
        Check.That(samples.Single(sample => sample.RouteId == settings.Proxies[0].Id).Status != ProbeStatus.Unreachable, "Working proxy failed");
        Check.That(samples.Single(sample => sample.RouteId == settings.Proxies[1].Id).Status == ProbeStatus.Unreachable, "Unavailable proxy was not red");
    }

    public static async Task SchedulingAndConcurrencyAsync()
    {
        using var directory = new TemporaryDirectory();
        await using var server = await LocalHttpServer.StartAsync();
        var settings = new MonitorSettings
        {
            Sites = Enumerable.Range(0, 4).Select(index => new SiteDefinition($"site-{index}", $"{server.Address}/slow?site={index}")).ToArray(),
            MaxConcurrency = 2
        };
        var store = new SettingsStore(directory.Path, settings);
        await using var engine = new MonitorEngine(store, new HistoryStore(directory.Path), new WebsiteProbe());
        await engine.StartAsync();
        await Check.EventuallyAsync(() => engine.Status.Running, "Engine did not start immediately");
        Check.That(!engine.RequestProbe(), "Manual probe overlapped a running round");
        await Check.EventuallyAsync(() => engine.Status.LastCompletedAt is not null && !engine.Status.Running, "First round did not finish");
        Check.That(engine.Snapshot().Samples.Length == 4, "Some targets were not probed");
        Check.That(server.MaximumConcurrency == 2, "Concurrency limit was not respected");
        var completed = engine.Status.LastCompletedAt;
        Check.That(engine.RequestProbe(), "Manual probe was rejected while idle");
        await Check.EventuallyAsync(() => engine.Status.LastCompletedAt > completed, "Manual probe did not run");
        Check.That(engine.Snapshot().Samples.Length is >= 4 and <= 8, "Minute buckets duplicated results");
        await engine.SetPausedAsync(true);
        Check.That(!engine.IsActive && !engine.Status.Running && engine.Status.NextRunAt is null, "Pause left work running");
        Check.That(!engine.RequestProbe(), "Paused engine accepted a request");
        completed = engine.Status.LastCompletedAt;
        await engine.SetPausedAsync(false);
        await Check.EventuallyAsync(() => engine.Status.LastCompletedAt > completed, "Resume did not probe immediately");
        var renamed = settings with { Sites = [new("new target", server.Address + "/fast")] };
        await engine.SaveSettingsAsync(renamed);
        await Check.EventuallyAsync(() => engine.Snapshot().Samples.Any(sample => sample.SiteId == renamed.Sites[0].Id),
            "Saved settings did not trigger a new round");
    }

    public static async Task PauseDoesNotCreateRedSamplesAsync()
    {
        using var directory = new TemporaryDirectory();
        await using var server = await LocalHttpServer.StartAsync();
        var settings = new MonitorSettings { Sites = [new("pending", server.Address + "/timeout")] };
        await using var engine = new MonitorEngine(new SettingsStore(directory.Path, settings), new HistoryStore(directory.Path), new WebsiteProbe());
        await engine.StartAsync();
        await Check.EventuallyAsync(() => server.Requests > 0, "Pending request was not started");
        await engine.StopAsync();
        Check.That(engine.Snapshot().Samples.Length == 0, "Pause recorded an unfinished request as a network failure");
    }
}
