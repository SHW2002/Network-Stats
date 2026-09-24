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
    }
}
