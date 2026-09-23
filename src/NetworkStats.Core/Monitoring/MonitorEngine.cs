using System.Collections.Concurrent;
using System.Threading.Channels;
using NetworkStats.Configuration;
using NetworkStats.Models;
using NetworkStats.Probing;
using NetworkStats.Storage;

namespace NetworkStats.Monitoring;

public sealed class MonitorEngine(SettingsStore settings, HistoryStore history, WebsiteProbe probe) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _gate = new();
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private CancellationTokenSource? _lifetime;
    private Task? _loop;
    private bool _initialized;
    private WorkerStatus _status = new(false, null, null, null, null);

    public event EventHandler? Updated;
    public MonitorSettings Settings => settings.Current;
    public string? HistoryWarning => history.Warning;
    public WorkerStatus Status { get { lock (_gate) return _status; } }
    public bool IsActive => _lifetime is { IsCancellationRequested: false };
    public bool UserPaused { get; private set; }

    public async Task SetPausedAsync(bool paused)
    {
        UserPaused = paused;
        if (paused) await StopAsync();
        else await StartAsync();
    }

    public async Task StartAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (_loop is { IsCompleted: false }) return;
            if (!_initialized)
            {
                await history.LoadAsync(Settings.RetentionHours);
                _initialized = true;
            }
            _lifetime?.Dispose();
            _lifetime = new();
            _loop = Task.Run(() => RunAsync(_lifetime.Token));
        }
        finally { _lifecycle.Release(); }
        Notify();
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (_lifetime is null) return;
            await _lifetime.CancelAsync();
            if (_loop is not null)
            {
                try { await _loop; }
                catch (OperationCanceledException) { }
            }
            SetStatus(Status with { Running = false, NextRunAt = null });
        }
        finally { _lifecycle.Release(); }
    }

    public bool RequestProbe()
    {
        if (!IsActive || Status.Running) return false;
        return _requests.Writer.TryWrite(true);
    }

    public async Task SaveSettingsAsync(MonitorSettings value, CancellationToken cancellationToken = default)
    {
        await settings.SaveAsync(value, cancellationToken);
        // 正在运行的一轮使用原配置，新配置在下一轮生效。
        _requests.Writer.TryWrite(true);
        Notify();
    }

    public MonitorSnapshot Snapshot(int minutes = 60)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minutes, 1440);
        var now = DateTimeOffset.UtcNow;
        var start = DateTimeOffset.FromUnixTimeSeconds((now.ToUnixTimeSeconds() / 60 - minutes + 1) * 60);
        var current = Settings;
        return new(now, start, minutes, current, Status, IsActive,
            history.Query(start, now), HistoryWarning);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            while (_requests.Reader.TryRead(out _)) { }
            var current = Settings;
            var started = DateTimeOffset.UtcNow;
            SetStatus(Status with { Running = true, LastStartedAt = started, NextRunAt = null, Error = null });
            var results = new ConcurrentBag<ProbeResult>();
            try
            {
                var tasks = current.GetRoutes().SelectMany(route => current.Sites.Select(site => (site, route)));
                await Parallel.ForEachAsync(tasks,
                    new ParallelOptions { MaxDegreeOfParallelism = current.MaxConcurrency, CancellationToken = cancellationToken },
                    async (task, token) => results.Add(await probe.CheckAsync(task.site, task.route, current, token)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                SetStatus(Status with { Error = $"本轮探测异常：{exception.Message}" });
            }
            // 暂停时只保存已经完成的请求，不把取消的请求记作网络故障。
            await history.RecordAsync(results, current.RetentionHours, CancellationToken.None);
            var now = DateTimeOffset.UtcNow;
            var next = DateTimeOffset.FromUnixTimeSeconds(
                (now.ToUnixTimeSeconds() / current.IntervalSeconds + 1) * current.IntervalSeconds);
            SetStatus(Status with { Running = false, LastCompletedAt = now,
                NextRunAt = cancellationToken.IsCancellationRequested ? null : next });
            if (cancellationToken.IsCancellationRequested) break;
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wait.CancelAfter(next - now);
            try { await _requests.Reader.ReadAsync(wait.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        }
    }

    private void SetStatus(WorkerStatus value)
    {
        lock (_gate) _status = value;
        Notify();
    }

    private void Notify() => Updated?.Invoke(this, EventArgs.Empty);

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifetime?.Dispose();
        _lifecycle.Dispose();
    }
}

public sealed record MonitorSnapshot(DateTimeOffset Now, DateTimeOffset WindowStart, int Minutes,
    MonitorSettings Settings, WorkerStatus Worker, bool Active, ProbeResult[] Samples, string? Warning);
