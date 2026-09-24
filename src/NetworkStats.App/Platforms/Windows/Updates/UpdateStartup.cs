using Microsoft.Extensions.DependencyInjection;
using NetworkStats.App.Updates;
using NetworkStats.App.Views;

namespace NetworkStats.App.Platforms.Windows.Updates;

internal static class UpdateStartup
{
    internal static async Task NotifyReadyAsync(IServiceProvider services)
    {
        var arguments = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(arguments, "--updated");
        if (index < 0 || index + 1 >= arguments.Length) return;
        try
        {
            var path = Path.GetFullPath(arguments[index + 1]);
            if (!path.StartsWith(Path.GetFullPath(UpdateServices.CacheDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
            var session = new UpdateSession(path);
            if (!string.Equals(session.Request.TargetPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) ||
                Version.Parse(session.Request.Version) != NetworkStats.Updates.UpdateRelease.Normalize(AppInfo.Current.Version)) return;
            var page = services.GetRequiredService<MainPage>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!page.IsLoaded || page.Width <= 0) await Task.Delay(50, timeout.Token);
            session.Mark("started");
        }
        catch (Exception exception) { StartupDiagnostics.Write("Update startup confirmation: " + exception.Message); }
    }
}
