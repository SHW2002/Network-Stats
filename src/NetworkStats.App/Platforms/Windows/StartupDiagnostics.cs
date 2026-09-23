using System.Diagnostics;

namespace NetworkStats.App.Platforms.Windows;

internal static class StartupDiagnostics
{
    private static readonly Stopwatch Timer = Stopwatch.StartNew();
    private static readonly object Gate = new();
    private static string? _path;

    public static void Initialize()
    {
        var directory = Environment.GetEnvironmentVariable("NETWORKSTATS_DIAGNOSTICS_DIRECTORY")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkStats", "logs");
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var file in new DirectoryInfo(directory).GetFiles("startup-*.log")
                         .OrderByDescending(file => file.LastWriteTimeUtc).Skip(4)) file.Delete();
            _path = Path.Combine(directory, $"startup-{Environment.ProcessId}.log");
        }
        catch { /* 诊断失败不能阻止启动。 */ }
        Write($"Starting; runtime={AppContext.BaseDirectory}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Write($"Unhandled: {args.ExceptionObject}");
    }

    public static void Write(string message)
    {
        if (_path is null) return;
        try
        {
            lock (Gate) File.AppendAllText(_path, $"{DateTimeOffset.Now:O} +{Timer.Elapsed.TotalMilliseconds:F0} ms {message}{Environment.NewLine}");
        }
        catch { /* 保留原始异常行为。 */ }
    }
}
