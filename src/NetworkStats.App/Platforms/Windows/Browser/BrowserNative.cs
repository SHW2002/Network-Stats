using System.Runtime.InteropServices;
using System.Text;

namespace NetworkStats.App.Platforms.Windows.Browser;

internal static class BrowserNative
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2Size;
        public nint Reserved2, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInfo { public nint Process, Thread; public uint ProcessId, ThreadId; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint Flags;
        public nuint MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JobLimits
    {
        public BasicLimits Basic;
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemory, PeakJobMemory;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateDesktop(string name, nint device, nint mode, uint flags, uint access, nint attributes);
    [DllImport("user32.dll")]
    internal static extern bool CloseDesktop(nint desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateJobObject(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetInformationJobObject(nint job, int infoClass, ref JobLimits limits, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AssignProcessToJobObject(nint job, nint process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool CreateProcess(string application, StringBuilder command, nint processAttributes,
        nint threadAttributes, bool inheritHandles, uint flags, nint environment, string directory,
        ref StartupInfo startup, out ProcessInfo info);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint ResumeThread(nint thread);
    [DllImport("kernel32.dll")]
    internal static extern uint WaitForSingleObject(nint handle, uint milliseconds);
    [DllImport("kernel32.dll")]
    internal static extern bool TerminateProcess(nint process, uint code);
    [DllImport("kernel32.dll")]
    internal static extern bool CloseHandle(nint handle);
}
