using NetworkStats.App.Platforms.Windows.Browser;
using NetworkStats.BrowserTests;
using NetworkStats.Models;
using NetworkStats.Probing;
using System.Text.Json;

if (!OperatingSystem.IsWindows()) { Console.WriteLine("SKIP: Windows/Edge browser checks"); return 0; }
var browser = new WindowsBrowserProbe();
var probe = new DownloadSpeedProbe(browser);
var direct = new RouteDefinition("direct", "直连", null);
if (args.Contains("--live"))
{
    var routes = new List<RouteDefinition> { direct };
    var settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetworkStats.App", "NetworkStats.App", "Data", "settings.json");
    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsFile));
    foreach (var proxy in document.RootElement.GetProperty("proxies").EnumerateArray())
    {
        var address = new UriBuilder(proxy.GetProperty("protocol").GetString()!, proxy.GetProperty("host").GetString()!,
            proxy.GetProperty("port").GetInt32()).Uri.AbsoluteUri;
        routes.Add(new("proxy-" + routes.Count, proxy.GetProperty("name").GetString()!, address));
    }
    foreach (var route in routes)
    {
        var sample = await probe.MeasureAsync(new("https://chatgpt.com/", TimeSpan.FromSeconds(10), 1_000_000), route);
        Console.WriteLine(JsonSerializer.Serialize(new { route = route.Name, sample.HttpStatus, sample.UsedBrowser, sample.Succeeded,
            sample.Download.BytesReceived, sample.Download.MegabytesPerSecond, TotalMs = sample.Download.TotalTime.TotalMilliseconds, sample.Error }));
    }
    return 0;
}

await using var server = new BrowserTestServer();
StartupTests.Verify();
await server.StartAsync();
var url = server.Address + "/page?exact=1%202";
var request = new DownloadSpeedRequest(url, TimeSpan.FromSeconds(10), 1_000_000);
var result = await probe.MeasureAsync(request, direct);
Check(result.Succeeded && result.UsedBrowser && result.Url == url && result.HttpStatus == 200,
    "HTTP challenge fallback did not load the configured URL: " + result.Error);
Check(result.Download.BytesReceived == server.Compressed.Length && result.Download.TransferTime.TotalMilliseconds >= 150,
    "Browser speed did not use compressed body bytes and actual response timing");
Console.WriteLine("PASS isolated browser fallback, exact URL, compressed body bytes and timing");
var httpCount = server.HttpRequests;
var repeated = await probe.MeasureAsync(request, direct);
Check(repeated.Succeeded && server.HttpRequests == httpCount, "Successful browser route was not remembered");
Console.WriteLine("PASS repeated URL measurement without repeated rejected HTTP requests");
var throughProxy = await browser.MeasureAsync(new("http://browser-target.invalid/page", TimeSpan.FromSeconds(10), 1_000_000),
    new("proxy", "HTTP", server.Address + "/"), null, default);
Check(throughProxy.Succeeded && throughProxy.Download.BytesReceived == server.Compressed.Length,
    "Browser did not use the configured proxy: " + throughProxy.Error);
Console.WriteLine("PASS explicit proxy route with a target that cannot resolve locally");
var denied = await browser.MeasureAsync(request with { Url = server.Address + "/denied" }, direct, null, default);
Check(!denied.Succeeded && denied.HttpStatus == 403 && denied.Download.BytesReceived == 0, "Browser counted a denied response as speed");
var limited = await browser.MeasureAsync(request with { ByteLimit = 10000 }, direct, null, default);
Check(!limited.Succeeded && limited.Error!.Contains("上限"), "Browser did not stop an oversized decoded response");
Console.WriteLine("PASS HTTP errors and oversized bodies do not produce false speed");
using (var stop = new CancellationTokenSource(200))
{
    try { await browser.MeasureAsync(request with { Url = server.Address + "/timeout" }, direct, null, stop.Token); throw new Exception("Cancellation was ignored"); }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
}
Console.WriteLine("PASS browser cancellation and process cleanup");
return 0;

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
