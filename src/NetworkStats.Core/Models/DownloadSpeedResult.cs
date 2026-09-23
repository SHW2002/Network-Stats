namespace NetworkStats.Models;

public sealed record DownloadSpeedRequest(string Url, TimeSpan TimeLimit, long ByteLimit = 20_000_000);

public enum DownloadCompletion { EndOfFile, ByteLimit, TimeLimit, Failed }

public sealed record DownloadProgress(long BytesReceived, TimeSpan TransferTime, TimeSpan TotalTime)
{
    // 网络速率采用十进制单位；测量 HTTP 响应体吞吐，不计 TLS/HTTP 协议开销。
    public double MegabytesPerSecond => TransferTime.TotalSeconds > 0
        ? BytesReceived / 1_000_000.0 / TransferTime.TotalSeconds : 0;
    public double MegabitsPerSecond => MegabytesPerSecond * 8;
    public double AccessMegabytesPerSecond => TotalTime.TotalSeconds > 0
        ? BytesReceived / 1_000_000.0 / TotalTime.TotalSeconds : 0;
    public double AccessMegabitsPerSecond => AccessMegabytesPerSecond * 8;
}

public sealed record DownloadSpeedResult(
    string Url, string RouteName, DownloadProgress Download, DownloadCompletion Completion,
    int? HttpStatus, string? Error)
{
    public bool Succeeded => Completion != DownloadCompletion.Failed && Download.BytesReceived > 0;
    public bool ShortSample => Download.BytesReceived < 1_000_000 || Download.TransferTime.TotalSeconds < 2;
}
