using System.Text.Json.Serialization;

namespace NetworkStats.Models;

// UsedBrowser 仅用于读取 v1.3.0 历史；新探测不再启动浏览器。
// 下载计时独立保存；旧记录缺失该字段时不能用总耗时冒充响应体传输时间。
public sealed record UrlTransfer(long BytesReceived, double TotalMilliseconds, DownloadCompletion Completion, string Url,
    double? TransferMilliseconds = null, bool UsedBrowser = false)
{
    [JsonIgnore]
    public double? MegabytesPerSecond => TransferMilliseconds is > 0 && BytesReceived > 0
        ? BytesReceived / 1000.0 / TransferMilliseconds.Value : null;

    [JsonIgnore]
    public double? MegabitsPerSecond => MegabytesPerSecond * 8;

    [JsonIgnore]
    public double? ResponseWaitMilliseconds => TransferMilliseconds is { } elapsed
        ? Math.Max(0, TotalMilliseconds - elapsed) : null;

    [JsonIgnore]
    public bool ShortSample => TransferMilliseconds is { } elapsed && DownloadProgress.IsShortSample(BytesReceived, elapsed);
}
