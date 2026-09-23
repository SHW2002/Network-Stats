namespace NetworkStats.App.Startup;

internal sealed record StartupStatus(bool Supported, bool Registered, string Detail, bool RequiresApproval = false, bool NeedsUpdate = false);

internal interface IStartupService
{
    StartupStatus Read();
    void SetEnabled(bool enabled);
}
