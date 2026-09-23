using System.Text.Json;
using NetworkStats.Models;
using NetworkStats.Storage;

namespace NetworkStats.Configuration;

public sealed class SettingsStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private static readonly StorageJsonContext Json = new(new JsonSerializerOptions(StorageJsonContext.Default.Options) { WriteIndented = true });
    private MonitorSettings _current;

    public SettingsStore(string dataDirectory, MonitorSettings defaults)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "settings.json");
        _current = SettingsValidator.Normalize(File.Exists(_path)
            ? JsonSerializer.Deserialize(File.ReadAllText(_path), Json.MonitorSettings)
                ?? throw new InvalidDataException("保存的配置为空")
            : defaults);
    }

    public MonitorSettings Current => Volatile.Read(ref _current);

    public async Task<MonitorSettings> SaveAsync(MonitorSettings settings, CancellationToken cancellationToken)
    {
        var normalized = SettingsValidator.Normalize(settings);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var temporaryPath = _path + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(normalized, Json.MonitorSettings), cancellationToken);
            File.Move(temporaryPath, _path, overwrite: true);
            Volatile.Write(ref _current, normalized);
            return normalized;
        }
        finally { _writeLock.Release(); }
    }
}
