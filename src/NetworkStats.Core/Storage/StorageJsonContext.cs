using System.Text.Json.Serialization;
using NetworkStats.Models;

namespace NetworkStats.Storage;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MonitorSettings))]
[JsonSerializable(typeof(ProbeResult))]
internal partial class StorageJsonContext : JsonSerializerContext;
