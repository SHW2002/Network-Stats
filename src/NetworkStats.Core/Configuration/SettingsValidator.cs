using NetworkStats.Models;

namespace NetworkStats.Configuration;

public sealed class SettingsValidationException(string[] errors) : Exception(string.Join("；", errors))
{
    public string[] Errors { get; } = errors;
}

public static class SettingsValidator
{
    public static MonitorSettings Normalize(MonitorSettings settings)
    {
        var errors = new List<string>();
        string? updateProxy = null;
        try { updateProxy = Updates.UpdateProxy.Normalize(settings.UpdateProxy); }
        catch (ArgumentException exception) { errors.Add(exception.Message); }
        if (!Enum.IsDefined(settings.Theme)) errors.Add("主题必须为跟随系统、浅色或深色");
        CheckRange(settings.IntervalSeconds, 10, 3600, "探测间隔（秒）", errors);
        CheckRange(settings.TimeoutSeconds, 1, 60, "超时（秒）", errors);
        CheckRange(settings.SlowThresholdMs, 1, 60000, "慢速阈值（毫秒）", errors);
        CheckRange(settings.MaxConcurrency, 1, 32, "并发数", errors);
        CheckRange(settings.RetentionHours, 1, 168, "历史保留时间（小时）", errors);
        if (settings.SlowThresholdMs >= settings.TimeoutSeconds * 1000)
            errors.Add("慢速阈值必须小于超时时间");
        if (settings.Sites is null || settings.Sites.Length is < 1 or > 16)
            errors.Add("请配置 1 至 16 个网站");
        if (settings.Proxies is null || settings.Proxies.Length > 8)
            errors.Add("最多可配置 8 个代理");

        var sites = new List<SiteDefinition>();
        foreach (var site in settings.Sites ?? [])
        {
            if (site is null || string.IsNullOrWhiteSpace(site.Name) || site.Name.Trim().Length > 64)
            {
                errors.Add("网站名称不能为空，且不能超过 64 个字符");
                continue;
            }
            if (site.Url is null || site.Url.Length > 2048 ||
                !Uri.TryCreate(site.Url.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            {
                errors.Add($"网站 {site.Name} 需要有效的 HTTP/HTTPS 地址，且不能包含账号密码或片段标识");
                continue;
            }
            sites.Add(new(site.Name.Trim(), uri.AbsoluteUri));
        }
        if (sites.Select(site => site.Id).Distinct().Count() != sites.Count)
            errors.Add("网站地址不能重复");

        var proxies = new List<ProxyDefinition>();
        foreach (var proxy in settings.Proxies ?? [])
        {
            if (proxy is null || string.IsNullOrWhiteSpace(proxy.Name) || proxy.Name.Trim().Length > 64)
            {
                errors.Add("代理名称不能为空，且不能超过 64 个字符");
                continue;
            }
            var protocol = proxy.Protocol?.Trim().ToLowerInvariant();
            var host = proxy.Host?.Trim().Trim('[', ']');
            if (protocol is not ("http" or "https" or "socks5"))
                errors.Add($"代理 {proxy.Name} 仅支持 HTTP、HTTPS 或 SOCKS5");
            else if (string.IsNullOrEmpty(host) || Uri.CheckHostName(host) == UriHostNameType.Unknown)
                errors.Add($"代理 {proxy.Name} 的主机名或 IP 地址无效");
            else if (proxy.Port is < 1 or > 65535)
                errors.Add($"代理 {proxy.Name} 的端口必须在 1 至 65535 之间");
            else
                proxies.Add(new(proxy.Name.Trim(), protocol, host.ToLowerInvariant(), proxy.Port, proxy.SpeedMeasurementEnabled));
        }
        if (proxies.Select(proxy => proxy.Id).Distinct().Count() != proxies.Count)
            errors.Add("代理地址不能重复");
        if (errors.Count > 0)
            throw new SettingsValidationException(errors.ToArray());
        return settings with { Sites = sites.ToArray(), Proxies = proxies.ToArray(), UpdateProxy = updateProxy };
    }

    private static void CheckRange(int value, int min, int max, string label, List<string> errors)
    {
        if (value < min || value > max)
            errors.Add($"{label}必须在 {min} 至 {max} 之间");
    }
}
