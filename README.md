![Network Stats main window](docs/images/main-window.png)

# Network Stats

English | [简体中文](README_CN.md)

Track website availability, response time, and response-body download speed on a timeline, with continuous monitoring of direct and proxy connections.

**Useful for users who access websites through proxies**: monitor multiple websites and compare direct, HTTP, HTTPS, and SOCKS5 connections to identify connectivity or stability problems, using your existing proxy service.

## Getting started

1. Download your platform's package from [GitHub Releases](https://github.com/SHW2002/Network-Stats/releases/latest): **Network-Stats.exe** for Windows x64, APK for Android arm64, or ZIP for Apple Silicon / Intel macOS. iOS packages currently target the Xcode Simulator.
2. Open **Settings**, add websites and proxy addresses, and save to start monitoring. Direct connections are always included. Local proxies usually use `127.0.0.1` and the port provided by your proxy software.
3. Read the timeline: green means healthy, yellow means slow, red means unavailable, and an empty cell means no data. Select the last 1, 3, 6, or 24 hours and click a cell for details.
4. Use **Test now** to refresh all results or **Single-site test** to retest one website. Check for and install updates in **Settings → Software updates**, with a separate proxy setting for updates.

On Windows, monitoring continues in the system tray when minimized. Double-click the tray icon to restore the window or right-click to exit. Settings include light/dark themes, launch at login, and window-close behavior.

Speed measurements describe transfers from the target URL, rather than your connection's maximum bandwidth. Small samples are explicitly indicated. Settings and history are stored locally.

## Development

- **.NET 10 + .NET MAUI** provides native interfaces for Windows, Android, iOS, and macOS (Mac Catalyst).
- **GraphicsView** draws per-minute network status timelines; Windows uses WinUI 3.
- **NetworkStats.Core** contains HTTP probes, proxies, scheduling, history storage, and update logic. **NetworkStats.App** contains the UI and platform integrations.
- Current releases include Windows x64, Android arm64, macOS arm64 / x64, and iOS Simulator arm64 / x64. iOS device packages require Apple developer signing. macOS packages are ad-hoc signed and are not notarized.

Install the .NET 10 SDK, then run on Windows:

```powershell
dotnet workload install maui-windows
.\scripts\run-windows.ps1 -Development
```

Run local tests and create the Windows single-file package:

```powershell
.\scripts\test.ps1
.\scripts\publish-windows.ps1
```

Each `dotnet publish` also archives the publish directory under `bin/Release-Archives/`, named `application-platform-package-type-yymmdd-hhmmss.zip`.

Shared icon SVGs live in `src/NetworkStats.App/Resources/AppIcon/`. After editing them, run `./scripts/icons/update-icons.ps1` on Windows to regenerate the Windows ICO and documentation PNG. The rounded tile fills the canvas without outer padding; Android and iOS use a matching navy background for platform icon compatibility.

[Changelog](CHANGELOG.md) · [MIT License](LICENSE)

<p align="center">
  <img src="docs/images/app-icon.png" alt="Network Stats rounded icon" width="20%" />
</p>
