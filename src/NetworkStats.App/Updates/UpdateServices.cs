namespace NetworkStats.App.Updates;

internal static class UpdateServices
{
    internal static string CacheDirectory => Path.Combine(AppStorage.DataDirectory, "updates");
    internal static string? LastFailure()
    {
        try
        {
            if (!Directory.Exists(CacheDirectory)) return null;
            var latest = new DirectoryInfo(CacheDirectory).EnumerateDirectories()
                .Where(directory => Guid.TryParseExact(directory.Name, "N", out _))
                .OrderByDescending(directory => directory.LastWriteTimeUtc).FirstOrDefault();
            var failed = latest is null ? null : Path.Combine(latest.FullName, "failed");
            return failed is not null && File.Exists(failed) ? "上次更新未完成：" + File.ReadAllText(failed) : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
    internal static IUpdateInstaller CreateInstaller()
    {
#if WINDOWS
        return new Platforms.Windows.Updates.WindowsUpdateInstaller();
#else
        return new ManualInstaller();
#endif
    }

#if !WINDOWS
    private sealed class ManualInstaller : IUpdateInstaller
    {
        public bool IsSupported => false;
        public string Description => "当前 GitHub Release 提供 Windows 安装包；此平台可检查版本并查看发布页。";
        public Task InstallAsync(NetworkStats.Updates.DownloadedUpdate update, CancellationToken cancellationToken) =>
            throw new PlatformNotSupportedException(Description);
    }
#endif
}
