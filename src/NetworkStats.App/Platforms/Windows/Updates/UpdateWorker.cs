using System.Diagnostics;

namespace NetworkStats.App.Platforms.Windows.Updates;

// 由单文件 EXE 的专用入口运行，不初始化 WinUI，也不创建窗口。
internal static class UpdateWorker
{
    internal static int Run(string requestPath, TimeSpan? startupTimeout = null)
    {
        UpdateSession? session = null;
        Process? previous = null, replacement = null;
        FileStream? updateLock = null;
        var replaced = false;
        try
        {
            session = new(requestPath);
            updateLock = new FileStream(Path.Combine(Path.GetDirectoryName(session.DirectoryPath)!, "install.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            previous = Process.GetProcessById(session.Request.ProcessId);
            if (previous.StartTime.ToUniversalTime().Ticks != session.Request.ProcessStartedUtcTicks ||
                !string.Equals(previous.MainModule?.FileName, session.Request.TargetPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("原程序进程与更新请求不匹配。");
            UpdateFiles.Validate(session.Payload, session.Request);
            // 在原进程退出前确认目标目录可写，并把新包放到同一卷以进行原子替换。
            File.Copy(session.Payload, session.Staging, overwrite: false);
            UpdateFiles.Validate(session.Staging, session.Request);
            session.Mark("ready");
            var waiting = Stopwatch.StartNew();
            while (!previous.WaitForExit(200))
            {
                if (File.Exists(session.Marker("cancel"))) throw new OperationCanceledException("更新已取消。");
                if (waiting.Elapsed > TimeSpan.FromSeconds(90)) throw new TimeoutException("原程序尚未退出，已保留原版本。");
            }
            if (File.Exists(session.Marker("cancel"))) throw new OperationCanceledException("更新已取消。");
            UpdateFiles.ReplaceWithRetry(session.Staging, session.Request.TargetPath, session.Backup);
            replaced = true;
            replacement = UpdateFiles.Start(session, updated: true);
            waiting.Restart();
            while (!File.Exists(session.Marker("started")))
            {
                if (replacement.WaitForExit(200)) throw new IOException($"新版本启动失败（退出代码 {replacement.ExitCode}）。");
                if (waiting.Elapsed > (startupTimeout ?? TimeSpan.FromSeconds(45))) throw new TimeoutException("新版本未能完成启动。");
            }
            if (replacement.HasExited) throw new IOException("新版本启动后意外退出。");
            session.Mark("complete", session.Request.Version);
            // 保留一次旧版本备份到更新缓存，程序所在目录仍只保留正常 EXE。
            TryCleanup(() => File.Move(session.Backup, session.Marker("previous.exe")));
            TryCleanup(() => File.Delete(session.Payload));
            return 0;
        }
        catch (Exception exception)
        {
            if (session is null) return 1;
            var message = exception.Message;
            try
            {
                if (replaced)
                {
                    if (replacement is { HasExited: false }) { replacement.Kill(); replacement.WaitForExit(5000); }
                    UpdateFiles.ReplaceWithRetry(session.Backup, session.Request.TargetPath, null);
                    message += " 已恢复原版本。";
                }
                if (previous is { HasExited: true })
                    using (UpdateFiles.Start(session, updated: false)) { }
            }
            catch (Exception rollback) { message += " 恢复失败：" + rollback.Message; }
            TryCleanup(() => session.Mark("failed", message));
            return 1;
        }
        finally
        {
            if (session is not null) TryCleanup(() => File.Delete(session.Staging));
            replacement?.Dispose(); previous?.Dispose(); updateLock?.Dispose();
        }
    }

    private static void TryCleanup(Action action) { try { action(); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
