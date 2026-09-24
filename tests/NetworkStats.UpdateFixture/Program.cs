using System.Diagnostics;
using NetworkStats.App.Platforms.Windows.Updates;

if (args is ["--apply-update", var request]) return UpdateWorker.Run(request, TimeSpan.FromSeconds(3));
var directory = args is ["--updated", var installedRequest, ..] ? Path.GetDirectoryName(installedRequest)!
    : args is ["--hold", var hold] ? hold : throw new ArgumentException("Fixture arguments required.");
using var self = Process.GetCurrentProcess();
File.WriteAllText(Path.Combine(directory, $"run-{self.Id}"), self.StartTime.ToUniversalTime().Ticks.ToString());
if (args.Contains("--updated"))
{
    if (File.Exists(Path.Combine(directory, "crash-new"))) return 42;
    if (!File.Exists(Path.Combine(directory, "hang-new"))) File.WriteAllText(Path.Combine(directory, "started"), "");
}
while (!File.Exists(Path.Combine(directory, $"exit-{self.Id}"))) Thread.Sleep(100);
return 0;
