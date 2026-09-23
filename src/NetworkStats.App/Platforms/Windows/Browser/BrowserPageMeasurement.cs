using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using NetworkStats.Models;
using NetworkStats.Probing;

namespace NetworkStats.App.Platforms.Windows.Browser;

internal sealed class BrowserPageMeasurement(DownloadSpeedRequest options, RouteDefinition route, Stopwatch total)
{
    private int? _status;
    private string _url = options.Url;
    private string? _error;
    private bool _challenge;

    public DownloadSpeedResult Failure(string message) => new(_url, route.Name, new(0, TimeSpan.Zero, total.Elapsed),
        DownloadCompletion.Failed, _status, message, _challenge, UsedBrowser: true);

    public DownloadSpeedResult Timeout() => Failure(_challenge
        ? "后台浏览器仍停留在站点验证页面，当前无法自动测速；请在同一线路的浏览器中检查站点。"
        : "后台浏览器探测超时，未完成目标正文下载。");

    public async Task<DownloadSpeedResult> RunAsync(BrowserConnection connection, CancellationToken token)
    {
        await connection.CallAsync("Network.enable", null, token);
        await connection.CallAsync("Page.enable", null, token);
        await connection.CallAsync("Network.setCacheDisabled", new() { ["cacheDisabled"] = true }, token);
        await connection.CallAsync("Network.setBypassServiceWorker", new() { ["bypass"] = true }, token);
        await connection.CallAsync("Fetch.enable", new() { ["patterns"] = new JsonArray(new JsonObject
            { ["urlPattern"] = "*", ["requestStage"] = "Request" }) }, token);
        var tree = await connection.CallAsync("Page.getFrameTree", null, token);
        var frame = tree.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString();
        var navigation = connection.CallAsync("Page.navigate", new() { ["url"] = options.Url }, token);
        // 导航期间仍需处理 Fetch 请求；观察任务异常，避免等待导航与请求放行互相阻塞。
        _ = navigation.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        string? requestId = null;
        var accepted = false;
        long decodedBytes = 0;
        var navigations = 0;
        await foreach (var (method, data) in connection.Events.ReadAllAsync(token))
        {
            if (navigation.IsFaulted) await navigation;
            if (method == "Fetch.requestPaused")
            {
                var type = data.GetProperty("resourceType").GetString();
                var block = type is "Image" or "Media" or "Font" || (accepted && type != "Document");
                if (type == "Document" && data.GetProperty("frameId").GetString() == frame && ++navigations > 6)
                    return Failure("浏览器导航次数过多，已停止探测。");
                var parameters = new JsonObject { ["requestId"] = data.GetProperty("requestId").GetString() };
                if (block) parameters["errorReason"] = "BlockedByClient";
                await connection.CallAsync(block ? "Fetch.failRequest" : "Fetch.continueRequest", parameters, token);
            }
            else if (method == "Network.requestWillBeSent" && IsMainDocument(data, frame))
            {
                requestId = data.GetProperty("requestId").GetString();
                accepted = false;
                decodedBytes = 0;
            }
            else if (method == "Network.responseReceived" && IsMainDocument(data, frame))
            {
                var response = data.GetProperty("response");
                _status = response.GetProperty("status").GetInt32();
                _url = response.GetProperty("url").GetString()!;
                using var http = new HttpResponseMessage((HttpStatusCode)_status);
                foreach (var header in response.GetProperty("headers").EnumerateObject())
                    if (header.Name.Equals("cf-mitigated", StringComparison.OrdinalIgnoreCase))
                        http.Headers.TryAddWithoutValidation(header.Name, header.Value.GetString());
                _challenge = HttpResponseFailure.RequiresBrowser(http);
                _error = HttpResponseFailure.Describe(http);
                if (_error is not null && !_challenge) return Failure(_error);
                accepted = _error is null;
                requestId = data.GetProperty("requestId").GetString();
            }
            else if (method == "Network.dataReceived" && data.GetProperty("requestId").GetString() == requestId)
            {
                decodedBytes += data.GetProperty("dataLength").GetInt64();
                if (decodedBytes > options.ByteLimit)
                    return Failure("浏览器正文超过采样上限，已停止下载；未完成的浏览器响应不计算速度。");
            }
            else if (method == "Network.loadingFailed" && data.GetProperty("requestId").GetString() == requestId)
                return Failure("浏览器请求失败：" + data.GetProperty("errorText").GetString());
            else if (method == "Network.loadingFinished" && accepted && data.GetProperty("requestId").GetString() == requestId)
            {
                var world = await connection.CallAsync("Page.createIsolatedWorld", new()
                    { ["frameId"] = frame, ["worldName"] = "NetworkStatsMeasurements" }, token);
                var result = await connection.CallAsync("Runtime.evaluate", new()
                {
                    ["contextId"] = world.GetProperty("executionContextId").GetInt32(), ["returnByValue"] = true,
                    ["expression"] = "(()=>{const n=performance.getEntriesByType('navigation')[0];return n&&{url:n.name,bytes:n.encodedBodySize,decoded:n.decodedBodySize,start:n.responseStart,end:n.responseEnd};})()"
                }, token);
                var measurement = result.GetProperty("result");
                if (!measurement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object ||
                    value.GetProperty("url").GetString() != _url)
                    return Failure("浏览器未返回该 URL 的完整性能计量。");
                var bytes = value.GetProperty("bytes").GetInt64();
                var duration = value.GetProperty("end").GetDouble() - value.GetProperty("start").GetDouble();
                if (bytes > options.ByteLimit || value.GetProperty("decoded").GetInt64() > options.ByteLimit)
                    return Failure("浏览器正文超过采样上限，未计算下载速度。");
                if (duration <= 0 || (bytes == 0 && decodedBytes > 0)) return Failure("浏览器未返回有效的正文传输计量。");
                return new(_url, route.Name, new(bytes, TimeSpan.FromMilliseconds(duration), total.Elapsed),
                    DownloadCompletion.EndOfFile, _status, bytes == 0 ? "该 URL 的响应体为空。" : null, UsedBrowser: true);
            }
        }
        return Failure("后台浏览器连接提前结束。");
    }

    private static bool IsMainDocument(JsonElement data, string? frame) =>
        data.TryGetProperty("type", out var type) && type.GetString() == "Document" &&
        data.TryGetProperty("frameId", out var frameId) && frameId.GetString() == frame;
}
