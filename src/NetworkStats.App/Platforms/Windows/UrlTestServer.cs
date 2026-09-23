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
    private readonly bool _requireBrowser;
    private readonly List<Task> _connections = [];
    private int _requests;
    private int _unexpectedTarget;
    public int Requests => Volatile.Read(ref _requests);
    public bool UnexpectedTarget => Volatile.Read(ref _unexpectedTarget) != 0;
    public string? UnexpectedRequest { get; private set; }
    public string Url { get; }

    public UrlTestServer(bool requireBrowser = false)
    {
        _requireBrowser = requireBrowser;
        _listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}{Target}";
        _serving = Task.Run(ServeAsync);
    }

    private async Task ServeAsync()
    {
        var token = _stop.Token;
        while (!token.IsCancellationRequested)
        {
            var connection = await _listener.AcceptTcpClientAsync(token);
            _connections.Add(RespondAsync(connection, token));
        }
    }

    private async Task RespondAsync(TcpClient connection, CancellationToken token)
    {
        using var client = connection;
        try
        {
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var request = await reader.ReadLineAsync(token);
            if (request is null) return; // 浏览器可能提前建立随后不使用的连接。
            if (request != $"GET {Target} HTTP/1.1")
            {
                UnexpectedRequest = request;
                Interlocked.Exchange(ref _unexpectedTarget, 1);
            }
            Interlocked.Increment(ref _requests);
            var isBrowser = false;
            while (await reader.ReadLineAsync(token) is { Length: > 0 } header)
                if (header.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase) && header.Contains("Edg/")) isBrowser = true;
            if (_requireBrowser && !isBrowser)
            {
                await stream.WriteAsync(Encoding.ASCII.GetBytes(
                    "HTTP/1.1 403 Forbidden\r\ncf-mitigated: challenge\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), token);
                return;
            }
            await Task.Delay(600, token); // 响应头之前的等待不应降低响应体下载速度。
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: {(_requireBrowser ? "text/html" : "application/octet-stream")}\r\nContent-Length: {BodySize}\r\nConnection: close\r\n\r\n"), token);
            var block = new byte[64 * 1024];
            if (_requireBrowser) Array.Fill(block, (byte)'a');
            for (var i = 0; i < 8; i++)
            {
                await Task.Delay(40, token);
                await stream.WriteAsync(block, token);
            }
        }
        catch (IOException) { } // 客户端取消或关闭预连接不应终止测试服务器。
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        try { await _serving; }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally { _listener.Stop(); await Task.WhenAll(_connections); _stop.Dispose(); }
    }
}
