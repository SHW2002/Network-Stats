namespace NetworkStats.Models;

public static class DownloadRate
{
    // 少量数据或瞬间读完的响应可能全在缓冲区内，无法据此推断网络传输速度。
    public const long MinimumBytes = 64_000;
    public const double MinimumMilliseconds = 200;

    public static double? MegabytesPerSecond(long bytesReceived, double transferMilliseconds) =>
        bytesReceived >= MinimumBytes && double.IsFinite(transferMilliseconds) &&
        transferMilliseconds >= MinimumMilliseconds
            ? bytesReceived / 1000.0 / transferMilliseconds : null;
}
