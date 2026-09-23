using System.Text.Json.Serialization;

namespace NetworkStats.Models;

// 与分钟记录一起保存，速度包含连接、响应等待及响应体读取所花的全部时间。
public sealed record UrlTransfer(long BytesReceived, double TotalMilliseconds, DownloadCompletion Completion, string Url)
{
    [JsonIgnore]
    public double MegabytesPerSecond => TotalMilliseconds > 0 ? BytesReceived / 1000.0 / TotalMilliseconds : 0;

    [JsonIgnore]
    public double MegabitsPerSecond => MegabytesPerSecond * 8;
}
