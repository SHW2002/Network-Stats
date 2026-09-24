using System.Diagnostics;
using System.Security.Cryptography;
using NetworkStats.App.Platforms.Windows.Updates;

namespace NetworkStats.DesktopTests;

internal static class UpdateInstallTests
{
    internal static async Task VerifyAsync(string oldPackage, string newPackage)
    {
        foreach (var scenario in new[] { "success", "corrupt", "cancel", "crash-new", "hang-new" })
        {
            using var test = new Fixture(oldPackage, newPackage);
            if (scenario is "crash-new" or "hang-new") File.WriteAllText(test.Session.Marker(scenario), "");
            if (scenario == "corrupt") await File.AppendAllTextAsync(test.Session.Payload, "damage");
            using var helper = test.StartHelper();
            if (scenario == "corrupt")
            {
                await WaitAsync(() => helper.HasExited);
                Require(helper.ExitCode != 0 && !test.Original.HasExited && !File.Exists(test.Session.Marker("ready")), "Corrupt package stopped the original app");
            }
            else
            {
                await WaitAsync(() => File.Exists(test.Session.Marker("ready")) || helper.HasExited);
                Require(!helper.HasExited, "Worker failed before handoff: " + test.Failure);
                if (scenario == "cancel") test.Session.Mark("cancel");
                else test.Session.Mark($"exit-{test.Original.Id}");
                await WaitAsync(() => helper.HasExited);
                Require(helper.ExitCode == (scenario == "success" ? 0 : 1), "Unexpected worker result: " + test.Failure);
            }
            var expected = scenario == "success" ? test.NewHash : test.OldHash;
            Require(Hash(test.Target) == expected, "Wrong executable survived: " + scenario);
            Require(File.ReadAllText(Path.Combine(test.Root, "settings.json")) == "preserve settings", "Updater modified configuration");
            Require(!File.Exists(test.Session.Staging) && !File.Exists(test.Session.Backup), "Updater left temporary files next to the EXE");
            if (scenario == "success")
                Require(File.Exists(test.Session.Marker("complete")) && Hash(test.Session.Marker("previous.exe")) == test.OldHash,
                    "Successful update lost its backup or completion marker");
            else if (scenario.EndsWith("new"))
            {
                await WaitAsync(() => Directory.GetFiles(test.Session.DirectoryPath, "run-*").Length >= 3);
                Require(test.Failure.Contains("已恢复原版本"), "Failed startup did not report rollback");
            }
            Console.WriteLine("PASS update installation: " + scenario);
        }
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!condition()) await Task.Delay(50, timeout.Token);
    }
    private static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    private static void Require(bool condition, string detail) { if (!condition) throw new InvalidOperationException(detail); }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "NetworkStats update 测试 " + Guid.NewGuid().ToString("N"));
        internal string Target => Path.Combine(Root, "Network-Stats.exe");
        internal Process Original { get; }
        internal UpdateSession Session { get; }
        internal string OldHash { get; }
        internal string NewHash { get; }
        internal string Failure => File.Exists(Session.Marker("failed")) ? File.ReadAllText(Session.Marker("failed")) : "";
        private readonly string _helper;

        internal Fixture(string oldPackage, string newPackage)
        {
            var directory = Path.Combine(Root, "updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory, "payload"));
            Directory.CreateDirectory(Path.Combine(directory, "helper"));
            File.Copy(oldPackage, Target);
            _helper = Path.Combine(directory, "helper", "Network-Stats.exe");
            File.Copy(oldPackage, _helper);
            File.Copy(newPackage, Path.Combine(directory, "payload", "Network-Stats.exe"));
            File.WriteAllText(Path.Combine(Root, "settings.json"), "preserve settings");
            OldHash = Hash(oldPackage); NewHash = Hash(newPackage);
            Original = Start(Target, "--hold", directory);
            var requestPath = Path.Combine(directory, "install.json");
            UpdateSession.Write(requestPath, new(Original.Id, Original.StartTime.ToUniversalTime().Ticks, Target, NewHash, "2.0.0.0", ["--hold", directory]));
            Session = new(requestPath);
        }

        internal Process StartHelper() => Start(_helper, "--apply-update", Session.RequestPath);
        private static Process Start(string executable, params string[] arguments)
        {
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            return Process.Start(start)!;
        }

        public void Dispose()
        {
            if (!Original.HasExited) { Original.Kill(); Original.WaitForExit(5000); }
            foreach (var record in Directory.EnumerateFiles(Session.DirectoryPath, "run-*"))
            {
                if (!int.TryParse(Path.GetFileName(record)[4..], out var id)) continue;
                try
                {
                    using var process = Process.GetProcessById(id);
                    if (process.StartTime.ToUniversalTime().Ticks.ToString() != File.ReadAllText(record)) continue;
                    if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
                }
                catch (ArgumentException) { }
            }
            Original.Dispose();
            var resolved = Path.GetFullPath(Root);
            if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("NetworkStats update 测试 ", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test cleanup directory");
            Directory.Delete(resolved, recursive: true);
        }
    }
}
