using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetworkStats.Tests;

internal sealed class SocksProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(15));
    private readonly Task _server;
    private readonly byte[]? _body;
    public string? RequestedHost { get; private set; }
    public int RequestedPort { get; private set; }
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public SocksProxy(byte[]? body = null)
    {
        _body = body;
        _listener.Start();
        _server = ServeAsync();
    }

    private async Task ServeAsync()
    {
        using var connection = await _listener.AcceptTcpClientAsync(_lifetime.Token);
        await using var stream = connection.GetStream();
        var greeting = await ReadAsync(stream, 2);
        if (greeting[0] != 5) throw new InvalidOperationException("Expected SOCKS5 greeting");
        await ReadAsync(stream, greeting[1]);
        await stream.WriteAsync(new byte[] { 5, 0 }, _lifetime.Token);
        var request = await ReadAsync(stream, 4);
        if (request[0] != 5 || request[1] != 1 || request[3] != 3)
            throw new InvalidOperationException("Expected SOCKS5 CONNECT with remote DNS");
        var size = (await ReadAsync(stream, 1))[0];
        RequestedHost = Encoding.ASCII.GetString(await ReadAsync(stream, size));
        var port = await ReadAsync(stream, 2);
        RequestedPort = port[0] * 256 + port[1];
        await stream.WriteAsync(new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 0, 80 }, _lifetime.Token);
        var header = new StringBuilder();
        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (header.Length > 16384) throw new InvalidDataException("HTTP request too large");
            header.Append((char)(await ReadAsync(stream, 1))[0]);
        }
        var response = _body is null ? "HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n"
            : $"HTTP/1.1 200 OK\r\nContent-Length: {_body.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _lifetime.Token);
        if (_body is not null) await stream.WriteAsync(_body, _lifetime.Token);
    }

    private async Task<byte[]> ReadAsync(Stream stream, int size)
    {
        var bytes = new byte[size];
        await stream.ReadExactlyAsync(bytes, _lifetime.Token);
        return bytes;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        _listener.Stop();
        try { await _server; }
        catch (OperationCanceledException) { }
        _lifetime.Dispose();
    }
}
