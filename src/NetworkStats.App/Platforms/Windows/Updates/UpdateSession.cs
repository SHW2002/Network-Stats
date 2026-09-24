using System.Text.Json;

namespace NetworkStats.App.Platforms.Windows.Updates;

internal sealed record UpdateRequest(int ProcessId, long ProcessStartedUtcTicks, string TargetPath,
    string Sha256, string Version, string[] RestartArguments);

internal sealed class UpdateSession
{
    internal UpdateSession(string requestPath)
    {
        RequestPath = Path.GetFullPath(requestPath);
        DirectoryPath = Path.GetDirectoryName(RequestPath)!;
        Id = Path.GetFileName(DirectoryPath);
        if (Path.GetFileName(RequestPath) != "install.json" || !Guid.TryParseExact(Id, "N", out _) ||
            (File.GetAttributes(DirectoryPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("无效的更新会话目录。");
        Request = JsonSerializer.Deserialize<UpdateRequest>(File.ReadAllText(RequestPath))
            ?? throw new InvalidDataException("更新请求为空。");
        var target = Path.GetFullPath(Request.TargetPath);
        if (target != Request.TargetPath || !Path.GetFileName(target).Equals("Network-Stats.exe", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith(DirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            Request.Sha256.Length != 64 || !Request.Sha256.All(Uri.IsHexDigit) || !System.Version.TryParse(Request.Version, out _))
            throw new InvalidDataException("更新目标或校验信息无效。");
        TargetDirectory = Path.GetDirectoryName(target)!;
        if ((File.GetAttributes(TargetDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("暂不支持更新位于目录链接中的程序。");
    }

    internal string RequestPath { get; }
    internal string DirectoryPath { get; }
    internal string Id { get; }
    internal UpdateRequest Request { get; }
    internal string TargetDirectory { get; }
    internal string Payload => Path.Combine(DirectoryPath, "payload", "Network-Stats.exe");
    internal string Staging => Path.Combine(TargetDirectory, $".Network-Stats.update-{Id}.exe");
    internal string Backup => Path.Combine(TargetDirectory, $".Network-Stats.previous-{Id}.exe");
    internal string Marker(string name) => Path.Combine(DirectoryPath, name);
    internal void Mark(string name, string text = "") => File.WriteAllText(Marker(name), text);

    internal static void Write(string path, UpdateRequest request) => File.WriteAllText(path, JsonSerializer.Serialize(request));
}
