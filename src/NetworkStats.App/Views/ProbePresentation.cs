using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal static class ProbePresentation
{
    public static string Speed(ProbeResult? sample) => sample switch
    {
        null => "等待 URL 测速",
        { Status: ProbeStatus.Unreachable } => "不可访问",
        { Transfer: null } => "未记录下载速度",
        { Transfer.BytesReceived: 0 } => "无响应体",
        { Transfer.MegabytesPerSecond: { } speed } => Rate(speed),
        _ => "未记录下载速度"
    };

    public static string Rate(double megabytesPerSecond) => megabytesPerSecond >= 1
        ? $"{megabytesPerSecond:N2} MB/s" : $"{megabytesPerSecond * 1000:N1} KB/s";

    public static string Describe(CellSelection selection)
    {
        var prefix = $"{selection.Site.Name} / {selection.Route.Name} · {selection.Minute.ToLocalTime():MM-dd HH:mm}\nURL：{selection.Site.Url}";
        if (selection.Sample is not { } sample) return $"{prefix}\n无数据：这一分钟没有完成的探测。";
        var timingLabel = sample.Transfer is null ? "响应头耗时（旧版）" : "访问总耗时";
        var text = $"{prefix}\n{Ui.StatusText(sample.Status)} · {timingLabel} {sample.LatencyMs:N0} ms" +
            (sample.HttpStatus is { } code ? $" · HTTP {code}" : "") +
            $" · 采样于 {sample.CheckedAt.ToLocalTime():HH:mm:ss}";
        if (sample.Transfer is { } transfer)
        {
            text += $"\n响应体下载速度：{Speed(sample)} · 已读取 {transfer.BytesReceived / 1000.0:N1} KB";
            if (sample.Status != ProbeStatus.Unreachable && transfer.MegabitsPerSecond is not null)
                text += $"（{transfer.MegabitsPerSecond:N2} Mbps）";
            if (transfer.TransferMilliseconds is { } bodyMs)
                text += $"\n连接与响应等待：{transfer.ResponseWaitMilliseconds:N0} ms · 响应体读取：{bodyMs:N0} ms";
            else text += "\n旧版未保存响应体读取时间，无法还原下载速度。";
            if (sample.Status != ProbeStatus.Unreachable && transfer.ShortSample)
                text += "\n样本较小：下载量不足 1 MB 或读取不足 2 秒，速率容易受缓冲影响，仅供参考。";
            if (transfer.Url != selection.Site.Url) text += $"\n重定向后地址：{transfer.Url}";
            text += transfer.Completion switch
            {
                DownloadCompletion.ByteLimit => "\n已达到每个 URL 1 MB 的采样上限。",
                DownloadCompletion.TimeLimit => "\n已达到请求时限，速度根据这段时间内已收到的数据计算。",
                _ => ""
            };
            text += "\n下载速度 = 响应体字节数 / 响应体读取时间；访问总耗时另含连接和响应等待。";
        }
        else text += "\n旧版记录只包含响应头耗时，没有下载速度数据。";
        return text + (sample.Error is { } error ? $"\n{error}" : "");
    }
}
