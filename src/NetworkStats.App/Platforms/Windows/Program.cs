namespace NetworkStats.App.WinUI;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["--apply-update", var request])
        {
            Environment.ExitCode = Platforms.Windows.Updates.UpdateWorker.Run(request);
            return;
        }
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(parameters =>
        {
            SynchronizationContext.SetSynchronizationContext(new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
        Platforms.Windows.StartupDiagnostics.Write("WinUI message loop returned");
        // 窗口关闭前已停止探测并写完历史。显式结束 CLR，避免 Main 返回后的
        // STA 清理与 WinRT 终结线程交错；保留失败退出码，不吞掉运行期间的异常。
        Environment.Exit(Environment.ExitCode);
    }
}
