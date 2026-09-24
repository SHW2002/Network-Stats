using System.Globalization;
using System.Text.Json;
using NetworkStats.Models;
using NetworkStats.Monitoring;

namespace NetworkStats.Storage;

public sealed class HistoryStore(string dataDirectory)
{
    private readonly string _directory = Path.Combine(dataDirectory, "history");
    private readonly Dictionary<(long Minute, string Site, string Route), ProbeResult> _samples = [];
    private readonly object _gate = new();
    private readonly SemaphoreSlim _fileGate = new(1, 1);
    public string? Warning { get; private set; }

    public async Task LoadAsync(int retentionHours, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        var cutoff = DateTimeOffset.UtcNow.AddHours(-retentionHours);
        var invalidLines = 0;
        foreach (var path in Directory.EnumerateFiles(_directory, "*.jsonl").Order())
        {
            if (!TryGetFileDate(path, out var date) || date.Date < cutoff.UtcDateTime.Date)
                continue;
            using var reader = new StreamReader(path);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                try
                {
                    var sample = JsonSerializer.Deserialize(line, StorageJsonContext.Default.ProbeResult);
                    if (sample is not null && sample.CheckedAt >= cutoff && sample.CheckedAt <= DateTimeOffset.UtcNow &&
                        !string.IsNullOrEmpty(sample.SiteId) && !string.IsNullOrEmpty(sample.RouteId) &&
                        Enum.IsDefined(sample.Status))
                        Put(sample);
                }
                catch (JsonException) { invalidLines++; }
            }
        }
        Warning = invalidLines == 0 ? null : $"历史文件中有 {invalidLines} 条不完整记录，已跳过";
        Prune(retentionHours);
    }

    public ProbeResult[] Query(DateTimeOffset start, DateTimeOffset end)
    {
        var first = start.ToUnixTimeSeconds() / 60;
        var last = end.ToUnixTimeSeconds() / 60;
        lock (_gate)
            return _samples.Values.Where(sample => sample.Minute >= first && sample.Minute <= last).ToArray();
    }

    public async Task RecordAsync(IEnumerable<ProbeResult> samples, int retentionHours, CancellationToken cancellationToken)
    {
        var batch = samples.ToArray();
        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_directory);
            foreach (var day in batch.GroupBy(sample => sample.CheckedAt.UtcDateTime.Date))
            {
                var path = Path.Combine(_directory, day.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
                var content = string.Concat(day.Select(sample => JsonSerializer.Serialize(sample, StorageJsonContext.Default.ProbeResult) + "\n"));
                if (File.Exists(path))
                {
                    using var existing = File.OpenRead(path);
                    if (existing.Length > 0)
                    {
                        existing.Seek(-1, SeekOrigin.End);
                        if (existing.ReadByte() != '\n') content = "\n" + content;
                    }
                }
                await File.AppendAllTextAsync(path, content, cancellationToken);
            }
            Prune(retentionHours);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Warning = $"历史记录暂时无法保存：{exception.Message}";
        }
        finally
        {
            // 读者只会看到完整的一批；先持久化，再一次性公布，避免整轮中途闪现空格。
            lock (_gate)
                foreach (var sample in batch.Where(sample => sample.CheckedAt >= DateTimeOffset.UtcNow.AddHours(-retentionHours)))
                    Put(sample);
            _fileGate.Release();
        }
    }

    public TimelineWindow LatestWindow(MonitorSettings settings, int minutes, DateTimeOffset now)
    {
        lock (_gate) return TimelineWindow.Create(_samples.Values, settings, minutes, now);
    }

    private void Put(ProbeResult sample)
    {
        lock (_gate)
        {
            var key = (sample.Minute, sample.SiteId, sample.RouteId);
            if (!_samples.TryGetValue(key, out var existing) || existing.CheckedAt <= sample.CheckedAt)
                _samples[key] = sample;
        }
    }

    private void Prune(int retentionHours)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-retentionHours);
        lock (_gate)
        {
            foreach (var key in _samples.Where(pair => pair.Value.CheckedAt < cutoff).Select(pair => pair.Key).ToArray())
                _samples.Remove(key);
        }
        foreach (var path in Directory.EnumerateFiles(_directory, "*.jsonl"))
        {
            if (TryGetFileDate(path, out var date) && date.Date < cutoff.UtcDateTime.Date)
            {
                try { File.Delete(path); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                { Warning = $"旧历史文件暂时无法清理：{exception.Message}"; }
            }
        }
    }

    private static bool TryGetFileDate(string path, out DateTime date) =>
        DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);

}
