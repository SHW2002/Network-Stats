using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace NetworkStats.App.Platforms.Windows.Browser;

internal sealed class BrowserConnection : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _sending = new(1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly Channel<(string Method, JsonElement Data)> _events = Channel.CreateUnbounded<(string, JsonElement)>();
    private Task? _reading;
    private int _nextId;
    public ChannelReader<(string Method, JsonElement Data)> Events => _events.Reader;

    public async Task ConnectAsync(Uri endpoint, CancellationToken token)
    {
        _socket.Options.Proxy = null; // 调试连接仅在本机回环地址上通信。
        await _socket.ConnectAsync(endpoint, token);
        _reading = ReadAsync();
    }

    public async Task<JsonElement> CallAsync(string method, JsonObject? parameters, CancellationToken token)
    {
        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            var message = new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new() };
            var bytes = System.Text.Encoding.UTF8.GetBytes(message.ToJsonString());
            await _sending.WaitAsync(token);
            try { await _socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, token); }
            finally { _sending.Release(); }
            return await completion.Task.WaitAsync(token);
        }
        finally { _pending.TryRemove(id, out _); }
    }

    private async Task ReadAsync()
    {
        Exception? failure = null;
        try
        {
            var buffer = new byte[32768];
            while (!_stop.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                ValueWebSocketReceiveResult received;
                do
                {
                    received = await _socket.ReceiveAsync(buffer.AsMemory(), _stop.Token);
                    if (received.MessageType == WebSocketMessageType.Close) throw new IOException("后台浏览器连接已关闭。");
                    message.Write(buffer, 0, received.Count);
                    if (message.Length > 8_000_000) throw new IOException("浏览器返回了过大的调试消息。");
                } while (!received.EndOfMessage);
                using var document = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var id))
                {
                    if (!_pending.TryGetValue(id.GetInt32(), out var completion)) continue;
                    if (root.TryGetProperty("error", out var error))
                        completion.TrySetException(new IOException(error.GetProperty("message").GetString()));
                    else completion.TrySetResult(root.GetProperty("result").Clone());
                }
                else if (root.TryGetProperty("method", out var method) && root.TryGetProperty("params", out var data))
                    _events.Writer.TryWrite((method.GetString()!, data.Clone()));
            }
        }
        catch (Exception exception) { failure = exception; }
        finally
        {
            _events.Writer.TryComplete(failure);
            foreach (var completion in _pending.Values) completion.TrySetException(failure ?? new IOException("浏览器连接已关闭。"));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _socket.Abort();
        if (_reading is not null) await _reading;
        _socket.Dispose();
        _stop.Dispose();
        _sending.Dispose();
    }
}
