namespace NetworkStats.App;

internal static class AppStorage
{
    public static string DataDirectory
    {
        get
        {
#if WINDOWS
            // 保持首版的数据路径，程序集名称调整不能导致配置和历史丢失。
            if (Platforms.Windows.PackageVerifier.ReportPath is { } report)
                return Path.Combine(Path.GetDirectoryName(report)!, "data");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NetworkStats.App", "NetworkStats.App", "Data");
#else
            return FileSystem.AppDataDirectory;
#endif
        }
    }
}
