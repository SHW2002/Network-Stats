using System.Runtime.InteropServices;

namespace NetworkStats.App.Platforms.Windows;

internal sealed class TrayIcon : IDisposable
{
    private const uint TrayMessage = 0x8000 + 73;
    private readonly Microsoft.UI.Xaml.Window _window;
    private readonly nint _handle;
    private readonly Native.SubclassProc _callback;
    private readonly uint _taskbarCreated;
    internal const string ExitMessageName = "NetworkStats.ExitForUpdate";
    internal const string ExitPropertyName = "NetworkStats.ExplicitExit";
    private readonly uint _exitMessage;
    private readonly Func<bool> _minimizeOnClose;
    private Native.NotifyIconData _data;
    private bool _installed;
    private bool _disposed;
    private bool _exiting;

    public TrayIcon(Microsoft.UI.Xaml.Window window, Func<bool> minimizeOnClose)
    {
        _window = window;
        _minimizeOnClose = minimizeOnClose;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _callback = WindowMessage;
        _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        _exitMessage = Native.RegisterWindowMessage(ExitMessageName);
        var icon = Native.LoadImage(0, Path.Combine(AppContext.BaseDirectory, "app.ico"), 1, 32, 32, 0x10);
        _data = new()
        {
            Size = (uint)Marshal.SizeOf<Native.NotifyIconData>(), Window = _handle, Id = 1,
            Flags = 1 | 2 | 4, CallbackMessage = TrayMessage,
            Icon = icon != 0 ? icon : Native.LoadIcon(0, 32512),
            Tip = "Network Stats · 网络观测站\n双击恢复窗口，右键显示菜单", Info = "", InfoTitle = ""
        };
        OwnsIcon = icon != 0;
        if (Native.SetWindowSubclass(_handle, _callback, 1, 0))
        {
            _installed = Native.ShellNotifyIcon(0, ref _data);
            Native.SetProp(_handle, ExitPropertyName, 1);
        }
    }

    private bool OwnsIcon { get; }
    internal void Minimize() => Native.ShowWindow(_handle, 6);

    private nint WindowMessage(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == _exitMessage) { Exit(); return 0; }
        if (message == 0x0010 && !_exiting && _minimizeOnClose()) // WM_CLOSE
        {
            Native.ShowWindow(_handle, 6); // SW_MINIMIZE；托盘可用时由 WM_SIZE 隐藏，否则保留任务栏入口。
            return 0;
        }
        if (message == _taskbarCreated)
            _installed = Native.ShellNotifyIcon(0, ref _data);
        if (message == 0x0005 && wParam == 1 && _installed) // WM_SIZE / SIZE_MINIMIZED
            Native.ShowWindow(_handle, 0);
        if (message == TrayMessage)
        {
            if ((uint)lParam == 0x0203) Restore(); // WM_LBUTTONDBLCLK
            else if ((uint)lParam == 0x0205) ShowMenu(); // WM_RBUTTONUP
            return 0;
        }
        return Native.DefSubclassProc(window, message, wParam, lParam);
    }

    private void Restore()
    {
        Native.ShowWindow(_handle, 9);
        Native.SetForegroundWindow(_handle);
        _window.Activate();
    }

    private void ShowMenu()
    {
        var menu = Native.CreatePopupMenu();
        try
        {
            Native.AppendMenu(menu, 0, 1, "显示窗口");
            Native.AppendMenu(menu, 0x0800, 0, null);
            Native.AppendMenu(menu, 0, 2, "退出 Network Stats");
            Native.GetCursorPos(out var position);
            Native.SetForegroundWindow(_handle);
            var command = Native.TrackPopupMenu(menu, 0x0100 | 0x0002, position.X, position.Y, 0, _handle, 0);
            if (command == 1) Restore();
            if (command == 2) Exit();
            Native.PostMessage(_handle, 0, 0, 0);
        }
        finally { Native.DestroyMenu(menu); }
    }

    private void Exit()
    {
        _exiting = true;
        _window.Close();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_installed) Native.ShellNotifyIcon(2, ref _data);
        Native.RemoveProp(_handle, ExitPropertyName);
        Native.RemoveWindowSubclass(_handle, _callback, 1);
        if (OwnsIcon) Native.DestroyIcon(_data.Icon);
    }
}
