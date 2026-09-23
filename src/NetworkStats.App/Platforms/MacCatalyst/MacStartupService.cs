using NetworkStats.App.Startup;
using ServiceManagement;

namespace NetworkStats.App.Platforms.MacCatalyst;

internal sealed class MacStartupService : IStartupService
{
    public StartupStatus Read()
    {
        if (!OperatingSystem.IsMacCatalystVersionAtLeast(16))
            return new(false, false, "开机自启动需要 macOS 13 或更新版本。");
        var status = SMAppService.MainApp.Status;
        return new(true, status is SMAppServiceStatus.Enabled or SMAppServiceStatus.RequiresApproval,
            status == SMAppServiceStatus.RequiresApproval
                ? "已注册，等待系统允许；请在系统设置 → 通用 → 登录项中启用。"
                : "登录 macOS 后自动启动；建议将应用放在“应用程序”目录，使用正式签名的应用包。",
            status == SMAppServiceStatus.RequiresApproval);
    }

    public void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsMacCatalystVersionAtLeast(16))
        {
            if (enabled) throw new InvalidOperationException("开机自启动需要 macOS 13 或更新版本。");
            return;
        }
        var service = SMAppService.MainApp;
        if (!enabled && service.Status == SMAppServiceStatus.NotRegistered) return;
        if (enabled && service.Status is SMAppServiceStatus.Enabled or SMAppServiceStatus.RequiresApproval) return;
        var succeeded = enabled ? service.Register(out var error) : service.Unregister(out error);
        if (!succeeded) throw new InvalidOperationException("系统未能更新登录启动项：" + error?.LocalizedDescription);
    }
}
