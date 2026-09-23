using Microsoft.Win32;
using NetworkStats.App.Startup;
using System.Runtime.Versioning;

namespace NetworkStats.App.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsStartupService(string? executable = null,
    string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run",
    string approvalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run") : IStartupService
{
    private const string ValueName = "NetworkStats";
    private string Command => $"\"{executable ?? Environment.ProcessPath ?? throw new IOException("无法确定程序路径。") }\" --startup";

    public StartupStatus Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKey);
        var value = key?.GetValue(ValueName) as string;
        using var approval = Registry.CurrentUser.OpenSubKey(approvalKey);
        var bytes = approval?.GetValue(ValueName) as byte[];
        var blocked = value is not null && bytes is { Length: > 0 } && bytes[0] is 3 or 7;
        var moved = value is not null && value != Command;
        return new(true, value is not null, blocked
            ? "已注册，但 Windows 启动应用设置中已禁用；请在系统设置中允许。"
            : moved ? "自启动项指向其他程序位置，保存后会更新为当前 EXE。"
            : "登录 Windows 后自动启动并最小化；移动 EXE 后请重新保存此设置。", blocked, moved);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(runKey, true);
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            key?.DeleteValue(ValueName, false);
        }
    }
}
