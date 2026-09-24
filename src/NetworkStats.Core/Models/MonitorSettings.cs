using System.Security.Cryptography;
using System.Text;

namespace NetworkStats.Models;

public sealed record MonitorSettings
{
    public ThemeMode Theme { get; init; } = ThemeMode.System;
    public bool MinimizeOnClose { get; init; }
    public bool LaunchOnStartup { get; init; }
    public int IntervalSeconds { get; init; } = 60;
    public int TimeoutSeconds { get; init; } = 10;
    public int SlowThresholdMs { get; init; } = 1500;
    public int MaxConcurrency { get; init; } = 12;
    public int RetentionHours { get; init; } = 168;
    public SiteDefinition[] Sites { get; init; } =
    [
        new("Baidu", "https://baidu.com/"),
        new("Google", "https://google.com/"),
        new("GitHub", "https://github.com/"),
        new("Pixiv", "https://pixiv.net/")
    ];
    public ProxyDefinition[] Proxies { get; init; } = [];

    public RouteDefinition[] GetRoutes() =>
        [new("direct", "直接连接", null), .. Proxies.Select(proxy =>
            new RouteDefinition(proxy.Id, proxy.Name, proxy.Address))];
}

public sealed record SiteDefinition(string Name, string Url)
{
    public string Id => StableId.For("site", Url);
}

public sealed record ProxyDefinition(string Name, string Protocol, string Host, int Port)
{
    public string Address => new UriBuilder(Protocol, Host, Port).Uri.AbsoluteUri.TrimEnd('/');
    public string Id => StableId.For("proxy", Address);
}

public sealed record RouteDefinition(string Id, string Name, string? Address);

internal static class StableId
{
    public static string For(string prefix, string value) =>
        $"{prefix}-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant()}";
}
