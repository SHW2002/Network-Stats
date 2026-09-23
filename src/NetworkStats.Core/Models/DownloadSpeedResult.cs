namespace NetworkStats.Models;

public sealed record DownloadSpeedRequest(string Url, TimeSpan TimeLimit, long ByteLimit = 20_000_000);

public enum DownloadCompletion { EndOfFile, ByteLimit, TimeLimit, Failed }

public sealed record DownloadProgress(long BytesReceived, TimeSpan TransferTime, TimeSpan TotalTime)
{
    // 网络速率采用十进制单位；测量 HTTP 响应体吞吐，不计 TLS/HTTP 协议开销。
    public double MegabytesPerSecond => TransferTime.TotalSeconds > 0
        ? BytesReceived / 1_000_000.0 / TransferTime.TotalSeconds : 0;
    public double MegabitsPerSecond => MegabytesPerSecond * 8;
    public TimeSpan ResponseWaitTime => TimeSpan.FromTicks(Math.Max(0, TotalTime.Ticks - TransferTime.Ticks));
    public bool ShortSample => IsShortSample(BytesReceived, TransferTime.TotalMilliseconds);

    // 小响应或短时间采样容易受缓冲影响，不应当作持续下载能力。
    public static bool IsShortSample(long bytesReceived, double transferMilliseconds) =>
        bytesReceived > 0 && (bytesReceived < 1_000_000 || transferMilliseconds < 2000);
}

public sealed record DownloadSpeedResult(
    string Url, string RouteName, DownloadProgress Download, DownloadCompletion Completion,
    int? HttpStatus, string? Error, bool BrowserVerificationRequired = false, bool UsedBrowser = false)
{
    public bool Succeeded => Completion != DownloadCompletion.Failed && Download.BytesReceived > 0;
    public bool ShortSample => Download.ShortSample;
}
