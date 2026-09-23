using System.Runtime.InteropServices;

namespace NetworkStats.App.Platforms.Windows;

internal sealed class TrayIcon : IDisposable
{
    private const uint TrayMessage = 0x8000 + 73;
    private readonly Microsoft.UI.Xaml.Window _window;
    private readonly nint _handle;
    private readonly Native.SubclassProc _callback;
    private readonly uint _taskbarCreated;
    private Native.NotifyIconData _data;
    private bool _installed;
    private bool _disposed;

    public TrayIcon(Microsoft.UI.Xaml.Window window)
    {
        _window = window;
        var ink = global::Windows.UI.Color.FromArgb(255, 23, 43, 69);
        var background = global::Windows.UI.Color.FromArgb(255, 245, 247, 251);
        window.AppWindow.TitleBar.ButtonForegroundColor = ink;
        window.AppWindow.TitleBar.ButtonInactiveForegroundColor = ink;
        window.AppWindow.TitleBar.ButtonBackgroundColor = background;
        window.AppWindow.TitleBar.ButtonInactiveBackgroundColor = background;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _callback = WindowMessage;
        _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
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
            _installed = Native.ShellNotifyIcon(0, ref _data);
    }

    private bool OwnsIcon { get; }

    private nint WindowMessage(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
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
            if (command == 2) _window.Close();
            Native.PostMessage(_handle, 0, 0, 0);
        }
        finally { Native.DestroyMenu(menu); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_installed) Native.ShellNotifyIcon(2, ref _data);
        Native.RemoveWindowSubclass(_handle, _callback, 1);
        if (OwnsIcon) Native.DestroyIcon(_data.Icon);
    }
}
