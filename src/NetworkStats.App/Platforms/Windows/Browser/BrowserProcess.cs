using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using NetworkStats.Models;

namespace NetworkStats.App.Platforms.Windows.Browser;

internal sealed class BrowserProcess : IDisposable
{
    private nint _desktop, _job, _process;
    private readonly string _profile;

    public BrowserProcess(RouteDefinition route)
    {
        var executable = FindBrowser();
        var id = Guid.NewGuid().ToString("N");
        _profile = Path.Combine(Path.GetTempPath(), "NetworkStats-browser", id);
        Directory.CreateDirectory(_profile);
        try
        {
            var desktopName = "NetworkStats-browser-" + id;
            _desktop = BrowserNative.CreateDesktop(desktopName, 0, 0, 0, 0x01FF, 0);
            _job = BrowserNative.CreateJobObject(0, null);
            if (_desktop == 0 || _job == 0) throw new Win32Exception();
            var limits = new BrowserNative.JobLimits { Basic = new() { Flags = 0x2000 } }; // 子进程随 Job 一起退出。
            if (!BrowserNative.SetInformationJobObject(_job, 9, ref limits, (uint)Marshal.SizeOf<BrowserNative.JobLimits>()))
                throw new Win32Exception();
            var arguments = $"\"{executable}\" --no-first-run --no-default-browser-check --disable-background-networking " +
                "--disable-sync --disable-extensions --disable-component-update --remote-debugging-address=127.0.0.1 " +
                $"--remote-debugging-port=0 --user-data-dir=\"{_profile}\" " +
                (route.Address is null ? "--no-proxy-server " : $"--proxy-server=\"{route.Address.TrimEnd('/')}\" --proxy-bypass-list=\"<-loopback>\" ") +
                "about:blank";
            var startup = new BrowserNative.StartupInfo { Size = Marshal.SizeOf<BrowserNative.StartupInfo>(), Desktop = desktopName };
            if (!BrowserNative.CreateProcess(executable, new StringBuilder(arguments), 0, 0, false, 0x08000404,
                0, Path.GetDirectoryName(executable)!, ref startup, out var info)) throw new Win32Exception();
            _process = info.Process;
            try
            {
                if (!BrowserNative.AssignProcessToJobObject(_job, _process)) throw new Win32Exception();
                if (BrowserNative.ResumeThread(info.Thread) == uint.MaxValue) throw new Win32Exception();
            }
            finally { BrowserNative.CloseHandle(info.Thread); }
        }
        catch { Dispose(); throw; }
    }

    public async Task<Uri> GetPageEndpointAsync(CancellationToken token)
    {
        var portFile = Path.Combine(_profile, "DevToolsActivePort");
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (BrowserNative.WaitForSingleObject(_process, 0) == 0) throw new IOException("后台浏览器提前退出。");
            try
            {
                var lines = await File.ReadAllLinesAsync(portFile, token);
                if (lines.Length > 0 && int.TryParse(lines[0], out var port) && port is > 0 and <= 65535)
                {
                    using var response = await client.GetAsync($"http://127.0.0.1:{port}/json/list", token);
                    response.EnsureSuccessStatusCode();
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                    foreach (var page in document.RootElement.EnumerateArray())
                        if (page.GetProperty("type").GetString() == "page")
                        {
                            var endpoint = new Uri(page.GetProperty("webSocketDebuggerUrl").GetString()!);
                            if (!endpoint.IsLoopback || endpoint.Scheme != "ws") throw new IOException("浏览器调试地址无效。");
                            return endpoint;
                        }
                }
            }
            catch (IOException) { }
            catch (HttpRequestException) { }
            await Task.Delay(50, token);
        }
    }

    private static string FindBrowser()
    {
        foreach (var directory in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
        {
            var path = Path.Combine(directory, "Microsoft", "Edge", "Application", "msedge.exe");
            if (File.Exists(path)) return path;
        }
        throw new IOException("浏览器验证需要安装 Microsoft Edge。");
    }

    public void Dispose()
    {
        if (_process != 0 && BrowserNative.WaitForSingleObject(_process, 0) != 0) BrowserNative.TerminateProcess(_process, 1);
        if (_job != 0) { BrowserNative.CloseHandle(_job); _job = 0; }
        if (_process != 0) { BrowserNative.WaitForSingleObject(_process, 2000); BrowserNative.CloseHandle(_process); _process = 0; }
        if (_desktop != 0) { BrowserNative.CloseDesktop(_desktop); _desktop = 0; }
        // 只清理此实例创建的独立配置，不读取或修改用户的浏览器配置。
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try { Directory.Delete(_profile, true); return; }
            catch (DirectoryNotFoundException) { return; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { Thread.Sleep(50); }
        }
    }
}
