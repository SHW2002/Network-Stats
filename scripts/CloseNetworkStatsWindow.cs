using System;
using System.Runtime.InteropServices;
using System.Text;

// 发送正常关闭消息，兼容已隐藏到托盘的窗口；不显示、激活或强制结束进程。
public static class CloseNetworkStatsWindow
{
    private delegate bool EnumWindowProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProp(IntPtr window, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);

    public static bool RequestClose(int processId)
    {
        var sent = false;
        EnumWindows((window, data) =>
        {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != (uint)processId) return true;
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            if (title.ToString().StartsWith("Network Stats", StringComparison.Ordinal))
            {
                // 新版的 WM_CLOSE 可能只最小化；使用与托盘“退出”相同的正常退出入口。
                var message = GetProp(window, "NetworkStats.ExplicitExit") != IntPtr.Zero
                    ? RegisterWindowMessage("NetworkStats.ExitForUpdate") : 0x0010u;
                sent |= PostMessage(window, message, IntPtr.Zero, IntPtr.Zero);
            }
            return true;
        }, IntPtr.Zero);
        return sent;
    }
}
