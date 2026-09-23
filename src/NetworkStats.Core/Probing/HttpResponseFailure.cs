using System.Net;

namespace NetworkStats.Probing;

public static class HttpResponseFailure
{
    public static bool RequiresBrowser(HttpResponseMessage response) =>
        response.Headers.TryGetValues("cf-mitigated", out var values) &&
        values.Any(value => string.Equals(value.Trim(), "challenge", StringComparison.OrdinalIgnoreCase));

    public static string? Describe(HttpResponseMessage response)
    {
        var prefix = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        // Cloudflare 的显式标记说明响应来自验证页，不能把验证页当作目标正文测速。
        if (RequiresBrowser(response))
            return $"{prefix}：站点要求浏览器验证（Cloudflare）。自动探测无法完成该验证，无法获取目标页面并计算下载速度。" +
                "已收到 HTTP 响应，此结果不表示网络不通；可在浏览器中使用同一线路检查能否访问。";

        if (response.IsSuccessStatusCode) return null;
        return response.StatusCode switch
        {
            HttpStatusCode.Forbidden => $"{prefix}：已收到 HTTP 响应，但站点拒绝此请求，可能涉及登录、访问策略或网络出口限制。" +
                "此结果不表示网络不通，无法计算目标页面的下载速度；可在浏览器中使用同一线路检查能否访问。",
            HttpStatusCode.Unauthorized => $"{prefix}：该 URL 要求身份认证，无法获取目标页面并计算下载速度。",
            HttpStatusCode.TooManyRequests => $"{prefix}：站点限制了请求频率，无法计算目标页面的下载速度；请稍后重试或增大探测间隔。",
            _ => prefix
        };
    }
}
