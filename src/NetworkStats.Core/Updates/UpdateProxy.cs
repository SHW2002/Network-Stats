using System.Net;

namespace NetworkStats.Updates;

public static class UpdateProxy
{
    public static string? Normalize(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https" or "socks5") || string.IsNullOrEmpty(uri.Host) ||
            uri.Port is < 1 or > 65535 || uri.AbsolutePath is not ("" or "/") ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("更新代理需要 HTTP、HTTPS 或 SOCKS5 地址及有效端口，例如 http://127.0.0.1:7890；不支持账号密码、路径或查询参数。");
        return uri.AbsoluteUri.TrimEnd('/');
    }

    public static HttpClient CreateClient(string? address)
    {
        address = Normalize(address);
        var handler = new SocketsHttpHandler
        {
            UseProxy = address is not null,
            Proxy = address is null ? null : new WebProxy(address),
            ConnectTimeout = TimeSpan.FromSeconds(15),
            AllowAutoRedirect = true, MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.None, UseCookies = false
        };
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NetworkStats-Updater/1.0");
        return client;
    }
}
