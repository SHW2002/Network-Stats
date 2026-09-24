using NetworkStats.DesktopTests;

if (!OperatingSystem.IsWindows()) { Console.WriteLine("SKIP: Windows startup checks"); return 0; }
StartupTests.Verify();
return 0;
