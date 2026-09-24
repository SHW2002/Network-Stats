using System.Diagnostics;
using System.Security.Cryptography;
using NetworkStats.Updates;

namespace NetworkStats.App.Platforms.Windows.Updates;

internal static class UpdateFiles
{
    internal static void Validate(string path, UpdateRequest request)
    {
        using var input = File.OpenRead(path);
        if (!Convert.ToHexString(SHA256.HashData(input)).Equals(request.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("待安装文件的 SHA256 已发生变化。");
        var versionText = FileVersionInfo.GetVersionInfo(path).FileVersion;
        if (!Version.TryParse(versionText, out var version) || UpdateRelease.Normalize(version) != Version.Parse(request.Version))
            throw new InvalidDataException("安装包的实际版本与发布版本不一致。");
    }

    internal static void ReplaceWithRetry(string source, string target, string? backup)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try { File.Replace(source, target, backup, ignoreMetadataErrors: true); return; }
            catch (IOException) when (timer.Elapsed < TimeSpan.FromSeconds(10)) { Thread.Sleep(200); }
        }
    }

    internal static Process Start(UpdateSession session, bool updated)
    {
        var start = new ProcessStartInfo(session.Request.TargetPath)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = session.TargetDirectory
        };
        if (updated) { start.ArgumentList.Add("--updated"); start.ArgumentList.Add(session.RequestPath); }
        foreach (var argument in session.Request.RestartArguments) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new IOException("无法重新启动程序。");
    }
}
