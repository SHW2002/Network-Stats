using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

// CreateDesktop isolates every window, including WinUI activation and crash dialogs.
// Deliberately never calls SwitchDesktop or changes the caller's desktop.
public sealed class WindowsDesktopProcess : IDisposable
{
    private IntPtr desktop;
    private IntPtr process;
    public uint Id { get; private set; }

    public WindowsDesktopProcess(string executable, string arguments, string directory, string environment)
    {
        var name = "NetworkStats-test-" + Guid.NewGuid().ToString("N");
        desktop = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x01FF, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception();
        var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)), Desktop = name };
        var block = Marshal.StringToHGlobalUni(environment);
        try
        {
            ProcessInfo info;
            if (!CreateProcess(executable, new StringBuilder("\"" + executable + "\" " + arguments),
                IntPtr.Zero, IntPtr.Zero, false, 0x08000400, block, directory, ref startup, out info))
                throw new Win32Exception();
            process = info.Process;
            Id = info.ProcessId;
            CloseHandle(info.Thread);
        }
        catch { Dispose(); throw; }
        finally { Marshal.FreeHGlobal(block); }
    }

    public bool WaitForExit(int milliseconds)
    {
        var result = WaitForSingleObject(process, (uint)milliseconds);
        if (result == 0xFFFFFFFF) throw new Win32Exception();
        return result == 0;
    }

    public uint ExitCode
    {
        get
        {
            uint code;
            if (!GetExitCodeProcess(process, out code)) throw new Win32Exception();
            return code;
        }
    }

    public void Dispose()
    {
        if (process != IntPtr.Zero)
        {
            if (!WaitForExit(0))
            {
                TerminateProcess(process, 1);
                WaitForExit(5000);
            }
            CloseHandle(process);
            process = IntPtr.Zero;
        }
        if (desktop != IntPtr.Zero) { CloseDesktop(desktop); desktop = IntPtr.Zero; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2Size;
        public IntPtr Reserved2, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr attributes);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string directory,
        ref StartupInfo startup, out ProcessInfo processInfo);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
