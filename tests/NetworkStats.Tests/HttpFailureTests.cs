using Microsoft.AspNetCore.Http;
using NetworkStats.Models;
using NetworkStats.Probing;

namespace NetworkStats.Tests;

internal static class HttpFailureTests
{
    public static async Task<bool> HandleAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (path is not ("/challenge" or "/challenge-ok" or "/unauthorized" or "/rate-limited")) return false;
        context.Response.StatusCode = path switch
        {
            "/challenge" => 403,
            "/unauthorized" => 401,
            "/rate-limited" => 429,
            _ => 200
        };
        if (path is "/challenge" or "/challenge-ok") context.Response.Headers["cf-mitigated"] = "challenge";
        await context.Response.WriteAsync("<html>Verification or error page; not the requested content.</html>");
        return true;
    }

    public static async Task RejectionsAndChallengesAsync()
    {
        await using var server = await LocalHttpServer.StartAsync();
        var route = new RouteDefinition("direct", "直连", null);
        var settings = new MonitorSettings { TimeoutSeconds = 5 };
        var probe = new WebsiteProbe();
        var cases = new (string Path, int Code, string Hint)[]
        {
            ("/challenge", 403, "浏览器验证（Cloudflare）"),
            ("/challenge-ok", 200, "浏览器验证（Cloudflare）"),
            ("/denied", 403, "站点拒绝此请求"),
            ("/unauthorized", 401, "身份认证"),
            ("/rate-limited", 429, "请求频率")
        };
        foreach (var (path, code, hint) in cases)
        {
            var result = await probe.CheckAsync(new("restricted", server.Address + path), route, settings, default);
            Check.That(result.Status == ProbeStatus.Unreachable && result.HttpStatus == code &&
                result.Error?.Contains(hint) == true, $"Missing rejection diagnosis for {path}");
            Check.That(result.Transfer is { BytesReceived: 0, Completion: DownloadCompletion.Failed, MegabytesPerSecond: null },
                $"Verification/error page was measured as target download speed for {path}");
            if (code == 403) Check.That(result.Error!.Contains("不表示网络不通"), "HTTP rejection was confused with network failure");
        }
        Check.That(server.Requests == cases.Length, "Restricted requests were retried or replaced by another URL");
    }
}
