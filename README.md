# Network Stats · 网络观测站

原生 **.NET 10 MAUI** 应用，每台设备独立探测自己的网络，用 GUI 时间图展示网站可访问性与响应耗时。
没有 WebView、JavaScript、HTTP 服务或外部图表依赖。

## 功能

- 默认检测 `https://baidu.com/`、`https://google.com/`、`https://github.com/`、`https://pixiv.net/`，可在设置中增删网站。
- 直连始终保留，显式设置 `SocketsHttpHandler.UseProxy = false`，不使用系统或环境变量中的 HTTP / SOCKS 代理。
- 可添加多个 **HTTP、HTTPS、SOCKS5** 代理，分别配置名称、主机和端口；每个代理单独显示一组时间图。
- 默认每 60 秒探测一次，每格固定代表 1 分钟，支持最近 1、3、6、24 小时视图；长时间图可以横向滚动。
- 绿色：成功且耗时 ≤ 1500 ms；黄色：成功但耗时 > 1500 ms；红色：请求失败、超时或 HTTP 4xx/5xx；浅灰空格：没有数据。
- 点击色块查看时间、耗时、HTTP 状态和失败原因；阈值、超时、采样间隔及历史保留时间可调整。
- 默认保留 7 天历史，重启后恢复；同一分钟多次采样显示最后一次结果。
- 支持立即探测、暂停和继续；请求并发受限，各轮不会重叠。
- Windows 最小化后隐藏到托盘并继续探测，双击托盘恢复，右键选择显示或退出；窗口关闭按钮直接退出。

## 四端运行方式

| 平台 | 界面与独立探测 | 后台行为 | 构建条件 |
| --- | --- | --- | --- |
| Windows | 支持 | 最小化到托盘后继续 | Windows、.NET 10、MAUI Windows 工作负载 |
| Android | 支持 | 当前版本离开前台后暂停 | .NET 10、MAUI Android 工作负载、Android SDK、JDK 21 |
| iOS | 支持 | 离开前台后暂停 | Mac、匹配工作负载的 Xcode、真机安装所需签名 |
| macOS | 支持，使用 Mac Catalyst | 应用保持运行时持续探测 | Mac、匹配工作负载的 Xcode |

移动端返回应用会立即开始一轮探测，系统挂起、关闭或手动暂停期间不补造历史。
当前版本没有 Android 前台服务；iOS 普通应用也不能保证后台每分钟执行。

## Windows 启动

直接双击发布目录中的 **`artifacts/windows/Network-Stats.exe`** 即可启动。
发布版只有一个 EXE，已经包含 .NET、Windows App SDK、图标和界面资源，无需安装 SDK 或另外复制 DLL。
可以将这个 EXE 单独复制到其他 Windows x64 电脑使用。
请保留 `Network-Stats.exe` 文件名；WinUI 的部分资源解析依赖编译时的名称，需要改名时重新编译并保持程序集与资源索引一致。

首次启动时，运行库会自动解包到用户临时目录中的 `.net/Network-Stats/` 缓存，后续启动复用缓存。
配置和历史依然保存在应用数据目录，不会写入 EXE 所在目录，也不会随运行库缓存清理而删除。
最小化后程序在托盘继续运行，双击托盘图标可以恢复窗口。

从源码启动时，安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，然后执行：

```powershell
dotnet workload install maui-windows
.\scripts\run-windows.ps1
```

启动脚本优先打开已有的单文件发布版；需要编译并运行最新开发代码时，使用 `.\scripts\run-windows.ps1 -Development`。
需要编译时，脚本优先使用 `%LOCALAPPDATA%\NetworkStats\dotnet` 下的独立 SDK，否则使用 PATH 中的 `dotnet`。
Windows 数据固定保存在 `%LOCALAPPDATA%\NetworkStats.App\NetworkStats.App\Data`，沿用首版路径；其他平台使用 MAUI 的 `FileSystem.AppDataDirectory`，设置页底部可以查看具体路径。
Windows 启动和未处理异常日志保存在 `%LOCALAPPDATA%\NetworkStats\logs\startup-<进程号>.log`，保留最近 5 次启动记录。

重新生成 Windows 单文件发布版：

```powershell
.\scripts\run-windows.ps1 -Publish
# 双击 artifacts\windows\Network-Stats.exe
```

发布使用 `WindowsPortable.pubxml`，启用依赖合并、压缩及资源自解包，关闭调试符号输出。
应用及 MAUI 使用 ReadyToRun 预编译，运行库复用自带预编译代码，较大的 Windows API 投影保留按需编译能力，减少首次解包内容；历史记录在后台线程加载。
脚本先在新目录构建，再将 EXE 单独复制到临时目录，在隔离的 Windows 桌面中执行完整窗口启动验证，通过后替换旧发布目录，避免遗留旧 DLL。
验证包含一次空解包缓存启动、两次复用缓存启动，以及真实控件模板加载、时间图绘制、设置页往返和托盘初始化。
测试只访问回环地址，使用独立数据目录；报告（包含包的 SHA256 和启动耗时）保存到 `artifacts/windows-package-check.txt`。
测试桌面不会切换到前台，整个发布和验证流程都不会在当前桌面弹出或激活窗口。

## Android 构建

```powershell
dotnet workload install maui-android

# 已有 Android SDK / JDK 时，替换为实际路径。
dotnet build src/NetworkStats.App/NetworkStats.App.csproj `
  -p:NetworkStatsTargetFramework=net10.0-android -f net10.0-android `
  -p:AndroidSdkDirectory="C:\Android\Sdk" `
  -p:JavaSdkDirectory="C:\Java\jdk-21"
```

也可由 .NET 的 `InstallAndroidDependencies` 目标准备 SDK：

```powershell
dotnet build src/NetworkStats.App/NetworkStats.App.csproj `
  -t:InstallAndroidDependencies `
  -p:NetworkStatsTargetFramework=net10.0-android -f net10.0-android `
  -p:AndroidSdkDirectory="C:\Android\Sdk" `
  -p:JavaSdkDirectory="C:\Java\jdk-21" `
  -p:AcceptAndroidSDKLicenses=true
```

将生成的 `bin/Debug/net10.0-android/*-Signed.apk` 安装到设备进行调试；项目已启用程序集内嵌，可直接安装，不需要 IDE 的快速部署；商店发布需要自己的签名密钥。

## iOS / macOS 构建

在 Mac 上安装 .NET 10 和工作负载，并使用工作负载要求的 Xcode 版本：

```bash
dotnet workload install maui

# Apple Silicon Mac
dotnet build src/NetworkStats.App/NetworkStats.App.csproj \
  -p:NetworkStatsTargetFramework=net10.0-maccatalyst \
  -f net10.0-maccatalyst -r maccatalyst-arm64

# Apple Silicon Mac 上的 iOS 模拟器
dotnet build src/NetworkStats.App/NetworkStats.App.csproj \
  -p:NetworkStatsTargetFramework=net10.0-ios \
  -f net10.0-ios -r iossimulator-arm64
```

Intel Mac 分别使用 `maccatalyst-x64` 和 `iossimulator-x64`；iOS 真机使用 `ios-arm64` 并配置证书和描述文件。
Apple 平台的入口、权限声明及 Mac 网络沙盒权限已包含在项目中，仍需在 Mac / 真机上完成构建与运行验证。

## 代理与统计口径

在设置中添加代理，例如：

| 名称 | 协议 | 主机 | 端口 |
| --- | --- | --- | --- |
| 本机 HTTP | HTTP | 127.0.0.1 | 7890 |
| 本机 SOCKS | SOCKS5 | 127.0.0.1 | 7891 |
| 电脑上的代理 | HTTP | 192.168.1.20 | 7890 |

`127.0.0.1` 永远指**运行应用的那台设备**；手机要使用电脑代理，需要填写电脑局域网 IP，并让代理接受局域网连接。
当前设置支持无需账号密码的代理。
直连禁用的是 HTTP / SOCKS 代理，不能绕过 VPN、TUN 或操作系统的网络路由。

每次探测使用新连接发送 GET，跟随最多 5 次重定向，计时到收到最终响应头，不下载整页。
耗时包含 DNS、TCP、TLS、重定向和服务器响应等待，是响应速度指标，不是下载带宽测试；操作系统仍可能缓存 DNS。
HTTP 403、429 等会显示红色，详情中保留状态码；这可能是站点的机器人限制，不能单凭红色推断整条网络不可用。

色块按请求开始时的 UTC 分钟归档，界面按设备本地时间显示。
间隔小于 60 秒或手动重复探测时，同一分钟保留最后一次结果；大于 60 秒时，中间没有采样的分钟为空。
最新可访问数和平均耗时基于每个网站 / 线路的最新记录，窗口可用率仅计算实际存在的色块，不把空白分钟算成故障。

## 数据与配置

```text
应用数据目录/
├── settings.json          # 原子替换保存的设备配置
└── history/
    └── yyyy-MM-dd.jsonl    # 按 UTC 日期追加的历史记录
```

首次运行使用代码中的默认配置；设置在应用内保存后，下次启动自动读取。
网站及代理的标识由地址生成，改名称不会丢失历史，改地址不会混用旧目标的数据。
损坏的历史行会被跳过并提示；过期记录自动清理。
序列化使用源生成上下文，避免移动端裁剪 / AOT 对反射序列化的影响。

## 项目结构与验证

```text
src/NetworkStats.Core/    # 配置校验、HTTP 探测、循环调度、本地历史；无 MAUI 依赖
src/NetworkStats.App/     # MAUI 原生界面、GraphicsView 时间图、四端入口、Windows 托盘
tests/NetworkStats.Tests/ # 本机 HTTP / SOCKS5 服务器驱动的集成检查，无外部网络依赖
scripts/                 # Windows 启动、发布与测试脚本
```

```powershell
.\scripts\test.ps1
# 或在任意已安装 .NET 10 SDK 的系统上：
dotnet run --project tests/NetworkStats.Tests/NetworkStats.Tests.csproj

# 在隔离桌面验证完整 Windows 窗口启动，不打扰当前桌面：
.\scripts\test-windows-package.ps1 -ExecutablePath artifacts/windows/Network-Stats.exe
```

检查覆盖直连不读取默认代理、HTTP / SOCKS5 代理、远端 DNS、延迟判色、重定向、响应头计时、超时、主动取消、配置校验、持久化、历史损坏恢复、并发上限、手动调度和多线路结果隔离。

已完成 Windows 构建、窗口及托盘检查、Android 构建与 APK 签名 / 内嵌程序集检查，以及 10 组核心集成检查。
单文件包额外验证真实 MAUI 窗口、WinUI 控件模板、Win2D 时间图绘制、设置页往返及托盘集成初始化；隔离桌面没有 Explorer 托盘，托盘菜单交互仍需手动验收。
Android 尚未在真机运行验证；iOS 和 macOS 尚未在 Mac 上构建验证。
自动测试应在后台进行，不自动打开、激活或置前 GUI；启动脚本供用户手动打开应用使用。

## 开源许可

本项目采用 [MIT License](LICENSE)。
