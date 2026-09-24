using NetworkStats.DesktopTests;

if (!OperatingSystem.IsWindows()) { Console.WriteLine("SKIP: Windows startup checks"); return 0; }
StartupTests.Verify();
if (args is ["--test-updater", var oldPackage, var newPackage]) await UpdateInstallTests.VerifyAsync(oldPackage, newPackage);
return 0;
