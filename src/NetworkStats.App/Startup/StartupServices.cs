namespace NetworkStats.App.Startup;

internal static class StartupServices
{
    public static IStartupService Current { get; } = Create();

    private static IStartupService Create()
    {
#if WINDOWS
        // 隔离桌面验包也必须隔离自启动设置，不能注册测试目录中的临时 EXE。
        return Platforms.Windows.PackageVerifier.ReportPath is not null
            ? new VerificationStartupService() : new Platforms.Windows.WindowsStartupService();
#elif MACCATALYST
        return new Platforms.MacCatalyst.MacStartupService();
#else
        return new UnsupportedStartupService();
#endif
    }

    public static async Task OpenSystemSettingsAsync()
    {
#if WINDOWS
        await Launcher.Default.OpenAsync("ms-settings:startupapps");
#elif MACCATALYST
        if (OperatingSystem.IsMacCatalystVersionAtLeast(16)) ServiceManagement.SMAppService.OpenSystemSettingsLoginItems();
        await Task.CompletedTask;
#else
        await Task.CompletedTask;
#endif
    }

    private sealed class UnsupportedStartupService : IStartupService
    {
        public StartupStatus Read() => new(false, false, "此平台不支持开机自启动。");
        public void SetEnabled(bool enabled) { if (enabled) throw new InvalidOperationException("此平台不支持开机自启动。"); }
    }

    private sealed class VerificationStartupService : IStartupService
    {
        private bool _enabled;
        public StartupStatus Read() => new(true, _enabled, "隔离测试自启动设置。");
        public void SetEnabled(bool enabled) => _enabled = enabled;
    }
}
