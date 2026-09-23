using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetworkStats.App.Platforms.Windows;

// 仅用于隔离桌面包验证，响应主界面自动采样和单项复测。
internal sealed class UrlTestServer : IAsyncDisposable
{
    public const int BodySize = 524288;
    private const string Target = "/configured/page?check=exact%20url&source=settings";
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;
    private int _requests;
    private int _unexpectedTarget;
    public int Requests => Volatile.Read(ref _requests);
    public bool UnexpectedTarget => Volatile.Read(ref _unexpectedTarget) != 0;
    public string Url { get; }

    public UrlTestServer()
    {
        _listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}{Target}";
        _serving = Task.Run(ServeAsync);
    }

    private async Task ServeAsync()
    {
        var token = _stop.Token;
        while (!token.IsCancellationRequested)
        {
            using var connection = await _listener.AcceptTcpClientAsync(token);
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var request = await reader.ReadLineAsync(token);
            if (request != $"GET {Target} HTTP/1.1") Interlocked.Exchange(ref _unexpectedTarget, 1);
            Interlocked.Increment(ref _requests);
            while (await reader.ReadLineAsync(token) is { Length: > 0 }) { }
            await Task.Delay(600, token); // 响应头之前的等待不应降低响应体下载速度。
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: {BodySize}\r\nConnection: close\r\n\r\n"), token);
            var block = new byte[64 * 1024];
            for (var i = 0; i < 8; i++)
            {
                await Task.Delay(40, token);
                await stream.WriteAsync(block, token);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        try { await _serving; }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally { _listener.Stop(); _stop.Dispose(); }
    }
}
