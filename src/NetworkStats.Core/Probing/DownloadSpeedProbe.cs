using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using NetworkStats.Models;

namespace NetworkStats.Probing;

public sealed class DownloadSpeedProbe
{
    public async Task<DownloadSpeedResult> MeasureAsync(DownloadSpeedRequest options, RouteDefinition route,
        IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("请填写有效的 HTTP 或 HTTPS 下载地址。", nameof(options));
        if (options.TimeLimit < TimeSpan.FromSeconds(1) || options.TimeLimit > TimeSpan.FromSeconds(60))
            throw new ArgumentOutOfRangeException(nameof(options), "测速时限必须在 1 至 60 秒之间。");
        if (options.ByteLimit is < 1 or > 100_000_000)
            throw new ArgumentOutOfRangeException(nameof(options), "下载上限必须在 1 至 100,000,000 字节之间。");
        cancellationToken.ThrowIfCancellationRequested();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.TimeLimit);
        using var handler = new SocketsHttpHandler
        {
            UseProxy = route.Address is not null,
            Proxy = route.Address is null ? null : new WebProxy(route.Address),
            ConnectTimeout = options.TimeLimit,
            AutomaticDecompression = DecompressionMethods.None,
            MaxResponseDrainSize = 0,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseCookies = false
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("NetworkStats/1.0");
        request.Headers.AcceptEncoding.ParseAdd("identity");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };

        var total = Stopwatch.StartNew();
        var transfer = new Stopwatch();
        long received = 0;
        int? status = null;
        var finalUrl = options.Url;
        var lastProgress = TimeSpan.Zero;
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            status = (int)response.StatusCode;
            finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? options.Url;
            if (!response.IsSuccessStatusCode) return Finish(DownloadCompletion.Failed, $"HTTP {status} {response.ReasonPhrase}");
            transfer.Start();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            while (received < options.ByteLimit)
            {
                var count = (int)Math.Min(buffer.Length, options.ByteLimit - received);
                var read = await stream.ReadAsync(buffer.AsMemory(0, count), timeout.Token).ConfigureAwait(false);
                if (read == 0)
                    return received == 0 ? Finish(DownloadCompletion.EndOfFile, "该 URL 的响应体为空，没有可计算的传输速度。")
                        : Finish(DownloadCompletion.EndOfFile);
                received += read;
                if (total.Elapsed - lastProgress >= TimeSpan.FromMilliseconds(200))
                {
                    progress?.Report(Snapshot());
                    lastProgress = total.Elapsed;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Finish(DownloadCompletion.ByteLimit);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return received > 0 ? Finish(DownloadCompletion.TimeLimit)
                : Finish(DownloadCompletion.Failed, "测速超时，尚未收到下载数据。");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Finish(DownloadCompletion.Failed, exception.InnerException?.Message ?? exception.Message);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }

        DownloadProgress Snapshot() => new(received, transfer.Elapsed, total.Elapsed);
        DownloadSpeedResult Finish(DownloadCompletion completion, string? error = null)
        {
            transfer.Stop();
            total.Stop();
            var download = Snapshot();
            progress?.Report(download);
            return new(finalUrl, route.Name, download, completion, status, error);
        }
    }
}
