using NetworkStats.Updates;

namespace NetworkStats.App.Updates;

internal interface IUpdateInstaller
{
    bool IsSupported { get; }
    string Description { get; }
    Task InstallAsync(DownloadedUpdate update, CancellationToken cancellationToken);
}
