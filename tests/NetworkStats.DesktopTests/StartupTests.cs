using Microsoft.Win32;
using NetworkStats.App.Platforms.Windows;
using System.Runtime.Versioning;

namespace NetworkStats.DesktopTests;

[SupportedOSPlatform("windows")]
internal static class StartupTests
{
    public static void Verify()
    {
        // 使用独立注册表位置；不读写 Windows 的真实启动项或 StartupApproved。
        var root = @"Software\NetworkStats\Tests\" + Guid.NewGuid().ToString("N");
        var runKey = root + @"\Run";
        var approvalKey = root + @"\Approval";
        var executable = @"C:\Example App 中文\Network-Stats.exe";
        var startup = new WindowsStartupService(executable, runKey, approvalKey);
        try
        {
            Check(!startup.Read().Registered, "Fresh startup registration was enabled");
            startup.SetEnabled(true);
            using (var key = Registry.CurrentUser.OpenSubKey(runKey))
                Check((string?)key?.GetValue("NetworkStats") == $"\"{executable}\" --startup", "Startup command quoting is incorrect");
            Check(startup.Read().Registered && !startup.Read().NeedsUpdate, "Startup registration did not persist");
            var moved = new WindowsStartupService(@"D:\Moved\Network-Stats.exe", runKey, approvalKey);
            Check(moved.Read().NeedsUpdate, "Moved executable was not detected");
            moved.SetEnabled(true);
            Check(!moved.Read().NeedsUpdate, "Moved executable path was not repaired");
            using (var approval = Registry.CurrentUser.CreateSubKey(approvalKey))
                approval.SetValue("NetworkStats", new byte[] { 3, 0, 0, 0 }, RegistryValueKind.Binary);
            Check(moved.Read().RequiresApproval, "System-disabled startup was displayed as active");
            moved.SetEnabled(true);
            Check(moved.Read().RequiresApproval, "App overwrote the user's system startup restriction");
            moved.SetEnabled(false);
            Check(!moved.Read().Registered, "Startup registration could not be removed");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
        Console.WriteLine("PASS isolated startup registry, path quoting, relocation and system-disabled state");
    }

    private static void Check(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
}
