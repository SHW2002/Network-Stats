using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using NetworkStats.App.Updates;
using NetworkStats.Updates;

namespace NetworkStats.App.Platforms.Windows.Updates;

internal sealed class WindowsUpdateInstaller : IUpdateInstaller
{
#pragma warning disable IL3000 // 同时支持内存加载和 IncludeAllContentForSelfExtract 完整解包的单文件包。
    public bool IsSupported
    {
        get
        {
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64 || Environment.ProcessPath is not { } executable) return false;
            var assembly = Assembly.GetEntryAssembly()?.Location;
            if (assembly is null) return false;
            return assembly.Length == 0 || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(assembly)),
                Path.GetDirectoryName(Path.GetFullPath(executable)), StringComparison.OrdinalIgnoreCase);
        }
    }
#pragma warning restore IL3000
    public string Description => IsSupported
        ? "从 GitHub 下载最新版，校验后自动安装并重启；保留配置和历史记录。"
        : "自动安装适用于 Windows x64 单文件发布版；当前构建可检查版本并查看发布页。";

    public async Task InstallAsync(DownloadedUpdate update, CancellationToken cancellationToken)
    {
        if (!IsSupported) throw new InvalidOperationException(Description);
        if (PackageVerifier.ReportPath is not null) throw new InvalidOperationException("普通验包流程不允许替换正在测试的程序。");
        var target = Environment.ProcessPath ?? throw new IOException("无法定位当前 EXE。");
        var directory = Path.GetDirectoryName(Path.GetDirectoryName(update.FilePath)!)!;
        var requestPath = Path.Combine(directory, "install.json");
        using var self = Process.GetCurrentProcess();
        var request = new UpdateRequest(self.Id, self.StartTime.ToUniversalTime().Ticks, target,
            update.Sha256, update.Version.ToString(), []);
        UpdateSession.Write(requestPath, request);
        var session = new UpdateSession(requestPath);
        Process? helper = null;
        var handoff = false;
        try
        {
            var helperPath = Path.Combine(directory, "helper", "Network-Stats.exe");
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(helperPath)!);
                File.Copy(target, helperPath, overwrite: false);
            }, cancellationToken);
            var start = new ProcessStartInfo(helperPath)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = directory
            };
            start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(requestPath);
            helper = Process.Start(start) ?? throw new IOException("无法启动后台更新程序。");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            while (!File.Exists(session.Marker("ready")))
            {
                if (helper.HasExited || File.Exists(session.Marker("failed")))
                    throw new IOException(File.Exists(session.Marker("failed")) ? await File.ReadAllTextAsync(session.Marker("failed")) : "后台更新程序无法启动。");
                await Task.Delay(100, timeout.Token);
            }
            cancellationToken.ThrowIfCancellationRequested();
            await ((App)Application.Current!).ExitForUpdateAsync();
            handoff = true;
        }
        finally
        {
            if (!handoff && helper is not null) session.Mark("cancel");
            helper?.Dispose();
        }
    }
}
