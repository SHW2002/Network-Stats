# Network Stats · 网络观测站

原生 **.NET 10 MAUI** 应用，每台设备独立探测自己的网络，用 GUI 时间图展示配置 URL 的可访问性、访问总耗时与响应体下载速度。
没有 WebView、JavaScript、HTTP 服务或外部图表依赖。

## 功能

- 默认检测 `https://baidu.com/`、`https://google.com/`、`https://github.com/`、`https://pixiv.net/`，可在设置中增删网站。
- 直连始终保留，显式设置 `SocketsHttpHandler.UseProxy = false`，不使用系统或环境变量中的 HTTP / SOCKS 代理。
- 可添加多个 **HTTP、HTTPS、SOCKS5** 代理，分别配置名称、主机和端口；每个代理单独显示一组时间图。
- 默认每 60 秒探测一次，每格固定代表 1 分钟，支持最近 1、3、6、24 小时视图；长时间图可以横向滚动。
- 最新一格在探测结果到齐后才显示；等待期间保留已有时间图，首次探测显示加载提示，避免新一分钟先闪出“无数据”色块。
- 绿色：成功且耗时 ≤ 1500 ms；黄色：成功但耗时 > 1500 ms；红色：请求失败、超时或 HTTP 4xx/5xx；浅灰空格：没有数据。
- 每个网站行分别显示最近一次响应体下载速度（KB/s / MB/s）、访问总耗时和小样本提示；点击色块查看响应等待、内容读取时间、流量、HTTP 状态、采样限制和失败原因。
- 默认保留 7 天历史，重启后恢复；同一分钟多次采样显示最后一次结果。
- 支持立即测速、暂停和继续；请求并发受限，各轮不会重叠；阈值、超时、采样间隔及历史保留时间可调整。
- 自动测速直接读取每个已配置 URL 的响应体；“单项测速”可选择同一网站和线路单独复测，支持随时取消。
- 支持浅色、深色和跟随系统三种外观，默认跟随系统；保存后立即生效，并在下次启动时恢复。
- 设置内可检查 GitHub 最新正式版本，Windows x64 单文件发布版支持一键下载、校验、安装并重启；更新代理独立配置，支持 HTTP、HTTPS、SOCKS5。
- 桌面端采用紧凑布局，减少标题、卡片和行间留白；默认窗口为 960 × 680，Windows 启动时按当前显示器的 DPI 和可用工作区缩小并居中，避免高缩放下超出屏幕。移动端保留触控尺寸。
- Windows 最小化后隐藏到托盘并继续探测，双击托盘恢复，右键选择显示或退出；可在设置中开启“关闭时改为缩放到托盘”，默认关闭。

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
主界面顶部显示版本号；启动后自动开始一轮 URL 测速，完成后网站名称下方显示下载速度和总耗时，也可以点击“立即测速”。
发布版只有一个 EXE，已经包含 .NET、Windows App SDK、图标和界面资源，无需安装 SDK 或另外复制 DLL。
可以将这个 EXE 单独复制到其他 Windows x64 电脑使用。
请保留 `Network-Stats.exe` 文件名；WinUI 的部分资源解析依赖编译时的名称，需要改名时重新编译并保持程序集与资源索引一致。

首次启动时，运行库会自动解包到用户临时目录中的 `.net/Network-Stats/` 缓存，后续启动复用缓存。
配置和历史依然保存在应用数据目录，不会写入 EXE 所在目录，也不会随运行库缓存清理而删除。
最小化后程序在托盘继续运行，双击托盘图标可以恢复窗口。
在 **设置 → 外观与窗口** 中选择深色模式（跟随系统 / 浅色 / 深色），或切换 Windows 的 **“关闭时改为缩放到托盘”**，然后点击“保存设置”。
开启关闭最小化后，关闭按钮和 Alt+F4 均改为最小化；托盘菜单的“退出”始终真正退出程序。托盘不可用时保留任务栏中的最小化窗口，避免无法恢复。
外观设置适用于四个平台，关闭最小化选项仅在 Windows 显示；移动端的后台限制不变。

在 **设置 → 启动行为** 中启用 **“开机自启动”**，点击“保存设置”后在登录系统时启动，默认关闭。
Windows 使用当前用户的启动项，无需管理员权限；自启动时最小化，托盘不可用时保留任务栏入口。移动 EXE 后重新保存设置即可更新启动路径。
macOS 使用系统 `SMAppService` 登录项，需要 **macOS 13 或更新版本**及有效的应用包；建议将签名应用放在“应用程序”目录。
系统禁止或要求批准时，页面会显示实际状态，并提供“系统启动设置”入口，由用户在系统设置中允许；应用不会覆盖系统对启动项的禁用。
关闭开关并保存会取消注册；Android / iOS 不显示自启动选项。自动测试使用隔离的配置和测试启动项，不修改电脑的真实登录启动设置。

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
# 已有旧版在运行时：验证新包后正常关闭旧版，并更新相同 EXE 路径。
.\scripts\run-windows.ps1 -Publish -CloseRunning
# 双击 artifacts\windows\Network-Stats.exe
```

发布使用 `WindowsPortable.pubxml`，启用依赖合并、压缩及资源自解包，关闭调试符号输出。
应用及 MAUI 使用 ReadyToRun 预编译，运行库复用自带预编译代码，较大的 Windows API 投影保留按需编译能力，减少首次解包内容；历史记录在后台线程加载。
脚本先在新目录构建，再将 EXE 单独复制到临时目录，在隔离的 Windows 桌面中执行完整窗口启动验证，通过后替换旧发布目录，避免遗留旧 DLL。
验证包含一次空解包缓存启动、两次复用缓存启动，以及真实控件模板加载、时间图绘制、设置页往返、配置 URL 自动采样与主界面速度显示、同一 URL 的单项复测和托盘初始化。
测试只访问回环地址，使用独立数据目录；报告（包含包的 SHA256 和启动耗时）保存到 `artifacts/windows-package-check.txt`。
测试桌面不会切换到前台，整个发布和验证流程都不会在当前桌面弹出或激活窗口。
使用 `-CloseRunning` 时，脚本只正常退出该发布路径对应的旧进程；新版通过与托盘“退出”相同的入口退出，不受关闭最小化设置影响，不强制结束、不显示或激活窗口；配置和历史保留。
未使用该参数且旧版占用发布目录时，新版会保留在 `artifacts/windows-update/`（已有同名目录时加唯一后缀），脚本输出实际路径；此时原目录中的 EXE 仍是旧版，需要退出旧版后重新发布才能更新原路径。

## 检查与安装更新

在 **设置 → 软件更新 → 检查更新** 中打开更新页面，点击“检查更新”读取本仓库的最新正式 GitHub Release。
发现新版本后，Windows x64 单文件发布版可点击 **“下载并安装”**，查看下载进度或取消；下载完成并通过 SHA256 校验后，程序自动退出、替换原路径中的 EXE 并重新启动。
配置和历史保留在原数据目录，自启动路径也保持不变。更新器等待新版完成窗口初始化；新版本无法启动时自动恢复旧文件并重启旧版。
程序安装目录需要当前用户可写；写入失败会在关闭旧实例前报错。上次安装失败的原因可在更新页面查看。

**更新代理** 支持填写 `http://127.0.0.1:7890`、`https://proxy.example:443` 或 `socks5://127.0.0.1:1080`，也可选用已有探测代理。
留空时显式直连，不使用系统代理；检查版本、读取校验文件和下载安装包都走同一条配置线路。点击检查更新时保存更新代理，该设置独立于探测线路。
下载损坏、大小不匹配、缺少校验信息、取消下载或无法连接 GitHub 时不会安装。只接受本仓库对应版本的发布附件，不安装草稿或预发布版本，也不降级已安装的版本。

目前 GitHub Release 提供 Windows x64 安装包；Android、iOS、macOS 和开发构建支持版本检查及发布页入口，尚不提供自动覆盖安装。
更新缓存与上次版本备份位于数据目录的 `updates` 子目录；更新成功后程序目录中的临时文件会移入缓存或清理。

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

## 对应 URL 的下载速度与访问耗时

在 **“设置”** 中配置网站 URL，主界面就会自动测量这个地址，无需打开额外测速页面。
启动时立即采样，默认之后每分钟一轮；每个网站名称下方分别显示最近一次 **下载 KB/s / MB/s**、**总耗时 ms** 和必要的小样本提示，点击分钟色块查看历史速度、时间明细、下载量和错误详情。
例如配置 `https://github.com/` 就实际 GET 该地址并读取它返回的响应体，跟随服务器重定向，不替换为外部测速服务器。

**响应体下载速度 = 实际读取的响应体字节数 ÷ 响应体读取时间**，从最终响应头返回开始计时，到内容读完或达到采样上限结束，包含读取期间的等待。
**访问总耗时**另包含 DNS、连接、TLS、重定向和服务器响应等待；详情将“连接与响应等待”和“响应体读取”分别显示，绿黄阈值仍按总耗时判定。
例如等待响应 800 ms、再用 100 ms 读取 500 KB 时，下载速度为 **5 MB/s**，访问总耗时为 **900 ms**；不会再用总耗时算成约 556 KB/s。
只下载配置 URL 返回的内容，不加载网页中的图片、脚本或视频；小网页的内容可能已经部分进入网络缓冲，短时读取速度会有波动，不能直接当作宽带最大带宽。

- 每个网站 / 线路 / 每轮最多读取 **1 MB**，整次请求使用设置中的超时时间（默认 **10 秒**）；实际网络流量还包含协议开销及缓冲。
- 使用十进制单位：`1 MB = 1,000,000 字节`，`8 Mbps = 1 MB/s`；请求声明不使用压缩，服务端仍压缩时按收到的压缩字节计量。
- 下载量不足 **1 MB** 或读取时间不足 **2 秒**时标注 **“样本较小 · 仅供参考”**；判断某个站点的持续下载能力需要它提供足够大的响应内容。
- 达到流量或时限上限且已有数据时，显示限量样本结果，详情标明原因；没有数据时超时、HTTP 失败或提前断流显示红色。
- 合法空响应仍标为可访问，速度显示“无响应体”；旧版历史保留，但没有响应体读取时间的记录显示“未记录下载速度”，不会把旧版总耗时平均速率当作下载速度。
- 直连和每个代理分别测量；直连显式禁用系统代理，代理失败不退回直连；自动结果随分钟历史保存。

点击 **“单项测速”** 可从已配置的网站和线路中选择并复测，URL 与主界面相同，使用相同流量上限、时限和计算口径。
单项结果显示在测速页，分钟历史由自动采样或“立即测速”更新；离开单项测速页或移动端进入后台会取消该测试，下载内容只计数、不保存。

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

每次探测使用新连接发送 GET，跟随最多 5 次重定向，计时到响应体读完或达到采样上限。
绿黄阈值按这次请求的总耗时判定；操作系统仍可能缓存 DNS，服务器或 CDN 也可能缓存内容。
HTTP 403、429 等会显示红色，网站名称下方分别显示“访问被拒绝 · 403”“请求受限 · 429”，详情保留状态码和原因；收到 HTTP 响应说明请求已得到回应，不能单凭红色推断整条网络不可用。
四个平台统一使用普通 HTTP 探测，不启动 Edge 或其他后台浏览器。Cloudflare 验证页及 HTTP 403 会如实显示失败，不把验证页算作目标正文下载速度。
默认列表已移除 ChatGPT，不再提供它的浏览器兼容探测；已保存的自定义网站仍可在设置中修改或移除。
v1.3.0 保存的浏览器测速历史仍可读取，并标注“旧版浏览器记录”，新探测只产生 HTTP 结果。

色块按整轮探测开始时的 UTC 分钟归档，同轮请求排队跨分钟也落在同一列；详情保留各请求的真实采样时间，界面按设备本地时间显示。
时间轴截止于最近已发布的采样分钟，标题显示具体时间；时钟跨分钟或请求仍在进行时不会先新增空格，整轮结果到齐后一起更新。
首次运行没有历史时显示“正在探测”，已有历史时先显示历史；暂停后图表停留在最后结果，恢复探测后保留暂停期间真正缺测的空格，不补造数据。
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
已有配置不会被新默认列表覆盖；升级前保存的网站可在设置中自行移除。
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

# Windows：隔离注册表验证自启动设置，不修改系统真实启动项。
dotnet run --project tests/NetworkStats.DesktopTests/NetworkStats.DesktopTests.csproj
# 或使用项目内独立 SDK：
.\scripts\test-windows-startup.ps1

# Windows：在独立目录验证真正的更新、取消、文件损坏、闪退和启动超时回退。
.\scripts\test-windows-updates.ps1
# 使用发布成品验证后台更新入口、原子替换和新版 GUI 启动确认，仍在隔离桌面运行。
.\scripts\test-windows-update-package.ps1 -ExecutablePath artifacts/windows/Network-Stats.exe
```

检查覆盖直连不读取默认代理、HTTP / SOCKS5 代理、远端 DNS、完整 URL 请求耗时与判色、重定向、超时、主动取消、配置校验、持久化、历史损坏恢复、并发上限、手动调度和多线路结果隔离。

已完成 Windows 构建、窗口及托盘检查、Android 构建与 APK 签名 / 内嵌程序集检查，以及核心集成检查。
下载测速检查覆盖真实流式字节计量、流量上限、响应头 / 响应体超时、取消、重定向、断流、压缩字节计量、HTTP / SOCKS5 代理和远端 DNS。
URL 自动测速检查还覆盖原始路径与查询参数、流量和时限上限、合法空响应、响应等待与下载计时分离、小样本提示，以及 v1.0 / v1.1 / 新版历史记录兼容。
时间图检查覆盖整分钟切换、等待整轮结果、跨分钟排队归档、真实缺测、配置切换和重启恢复；403 / 验证页不会触发另一种客户端重试。
单文件包额外验证真实 MAUI 窗口、WinUI 控件模板、Win2D 时间图绘制、设置页往返、配置 URL 的自动采样及主界面速度显示、单项复测、分别显示下载速度与耗时、小样本提示和托盘集成初始化。
更新检查覆盖正式版本比较、来源限制、下载大小与 SHA256、取消清理、代理持久化、HTTP / SOCKS5 传输、设置页下载与安装衔接，以及使用真实子进程完成文件替换和启动失败回退。
外观与窗口检查覆盖三种主题启动、在设置页切换并保存、主界面／设置页／测速页配色、图表重绘、关闭最小化继续探测，以及普通关闭与显式退出两条路径；隔离桌面没有 Explorer 托盘，托盘菜单交互仍需手动验收。
Android 尚未在真机运行验证；iOS 和 macOS 尚未在 Mac 上构建验证。
自动测试应在后台进行，不自动打开、激活或置前 GUI；启动脚本供用户手动打开应用使用。

## 开源许可

本项目采用 [MIT License](LICENSE)。
