using System.ComponentModel;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using NetworkStats.Models;
using NetworkStats.Probing;

namespace NetworkStats.App.Platforms.Windows.Browser;

internal sealed class WindowsBrowserProbe : IBrowserDownloadProbe
{
    private readonly SemaphoreSlim _slots = new(2);

    public async Task<DownloadSpeedResult> MeasureAsync(DownloadSpeedRequest options, RouteDefinition route,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var total = Stopwatch.StartNew();
        var measurement = new BrowserPageMeasurement(options, route, total);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.TimeLimit);
        var acquired = false;
        DownloadSpeedResult result;
        try
        {
            await _slots.WaitAsync(timeout.Token);
            acquired = true;
            using var browser = new BrowserProcess(route);
            await using var connection = new BrowserConnection();
            try
            {
                await connection.ConnectAsync(await browser.GetPageEndpointAsync(timeout.Token), timeout.Token);
                result = await measurement.RunAsync(connection, timeout.Token);
            }
            finally { browser.Dispose(); } // 先结束浏览器，再断开拦截连接，避免释放待拦截的子资源请求。
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { result = measurement.Timeout(); }
        catch (Exception exception) when (exception is IOException or HttpRequestException or WebSocketException or
            Win32Exception or JsonException or InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = measurement.Failure("后台浏览器探测失败：" + exception.Message);
        }
        finally { if (acquired) _slots.Release(); }
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(result.Download);
        return result;
    }
}
