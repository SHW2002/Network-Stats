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
            ? LoadExisting(File.ReadAllText(_path))
            : defaults);
    }

    public MonitorSettings Current => Volatile.Read(ref _current);

    private static MonitorSettings LoadExisting(string content)
    {
        var settings = JsonSerializer.Deserialize(content, Json.MonitorSettings)
            ?? throw new InvalidDataException("保存的配置为空");
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        // v1.4 及更早版本没有线路级开关；旧配置升级后保留“开启全局测速时所有线路可测”的行为。
        if (!root.TryGetProperty("directSpeedMeasurementEnabled", out _))
            settings = settings with { DirectSpeedMeasurementEnabled = true };
        if (root.TryGetProperty("proxies", out var proxies) && proxies.ValueKind == JsonValueKind.Array &&
            settings.Proxies is { } configuredProxies)
        {
            var normalized = configuredProxies.Select((proxy, index) =>
                proxy is null ? proxy! :
                index < proxies.GetArrayLength() && proxies[index].TryGetProperty("speedMeasurementEnabled", out _)
                    ? proxy
                    : proxy with { SpeedMeasurementEnabled = true }).ToArray();
            settings = settings with { Proxies = normalized };
        }
        return settings;
    }

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
