# PcCare

PcCare 是一个面向公司老旧 Windows 电脑的离线系统检查与视觉效果优化工具。它不依赖服务端、不安装服务、不常驻、不上传数据，也不包含杀毒、文件清理、系统精简或高风险“注册表加速”。

当前版本：`0.6.0`

## 功能

- 检查 Windows、CPU、内存、系统盘、磁盘介质、连续运行时间和待重启状态；磁盘介质使用 Windows 原生只读存储查询，不引入 WMI 依赖。
- 依据系统 Build 号区分 Windows 10/11，兼容企业版 LTSC 的注册表名称差异。
- 一键应用视觉效果性能模式：仅保留字体平滑，关闭其他动画、淡入淡出、阴影、Peek 和缩略图效果。
- 后台优化页按功能实际存在状态，保守关闭 Windows 提示/推荐、可用 Widgets/News and interests/Copilot、浏览器后台模式和少量个性化推荐；不卸载组件或浏览器。
- 安全扫描 HKCU/HKLM Run、RunOnce、WOW6432Node Run/RunOnce、用户/公共启动目录，以及登录/开机计划任务；默认隐藏 `\Microsoft\Windows\` 系统任务。
- 显示启用状态、文件元数据、签名可用性、来源、风险和保守建议；未知项绝不加入一键优化。
- 支持单项启用/禁用，以及仅对“低风险且建议优化”的项一键处理。注册表和启动文件夹仅写入任务管理器对应的状态层，不删除原值或文件；计划任务仅切换启用状态。
- 检查后显示当前用户的视觉效果配置，再由用户决定是否应用性能模式。
- “硬件与电源”页读取 CPU/内存/系统盘、设备类型、供电与 Windows 电源计划，按台式机、笔记本、虚拟机和 HDD/SSD/NVMe 给出自适应建议；一键仅执行低风险、推荐且适用的固定电源操作。

## 明确不做

- 不扫描或处理病毒。
- 不扫描或删除任何文件，不提供“可清理项目”。
- 不修改 Windows 服务、Windows Update、Defender、SysMain、搜索索引或遥测。
- 不修改防火墙、UAC、SmartScreen、BITS、WMI、通知、剪贴板、最近文件/Jump List 或全局后台应用开关。
- 不删除 AppX、Edge、WebView2、Windows 组件、浏览器或浏览器数据。
- 不清理注册表、Prefetch、事件日志、浏览器资料。
- 不访问桌面、文档、下载、收藏夹或网盘目录。
- 不从网络下载或执行脚本，不自动更新。
- 不删除任何启动项注册表值、文件、计划任务、UWP 项或驱动，不提供任意命令或静默批量模式。
- 不创建备份、还原点、持久化操作日志或电源计划副本。

详见 [安全设计](docs/SAFETY_DESIGN.md)。

## 开发环境

- Windows 10 1809 或更高版本 / Windows 11
- .NET 10 SDK
- Visual Studio 2026（可选）或 `dotnet` CLI

## 构建与测试

```powershell
dotnet restore PcCare.sln
dotnet build PcCare.sln -c Release
dotnet test tests/PcCare.Core.Tests/PcCare.Core.Tests.csproj -c Release
```

## 发布版本与体积

GitHub Actions 同时生成两个版本：

| 版本 | 目标电脑要求 | 特点 |
|---|---|---|
| `PcCare-win-x64-offline` | 无需预装 .NET | 内含 .NET 10、WPF 和原生运行时，文件较大，可完全离线独立运行 |
| `PcCare-win-x64-lite` | 已安装 .NET 10 Desktop Runtime x64 | 不内置运行时，文件明显更小 |

完整离线版：

```powershell
dotnet publish src/PcCare.App/PcCare.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishTrimmed=false `
  -o artifacts/offline
```

轻量版：

```powershell
dotnet publish src/PcCare.App/PcCare.App.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:PublishTrimmed=false `
  -p:EnableCompressionInSingleFile=false `
  -p:IncludeNativeLibrariesForSelfExtract=false `
  -o artifacts/lite
```

没有开启裁剪，因为 WPF 和反射相关代码不能在没有完整兼容验证时安全裁剪。完整离线版不会自动获得后续 .NET 安全补丁，发布新版本时应同步升级 SDK 并重新构建。

## GitHub Release

推送形如 `v0.6.0` 的版本标签后，GitHub Actions 会先完成构建和测试，再自动创建 Release，并上传两个带版本号的 EXE 与对应 SHA256 文件。

- `PcCare-vX.Y.Z-win-x64-offline.exe`：完整离线版。
- `PcCare-vX.Y.Z-win-x64-lite.exe`：需预装 .NET 10 Desktop Runtime x64 的轻量版。

示例：`git tag -a v0.6.0 -m "PcCare v0.6.0"`，然后执行 `git push origin v0.6.0`。

## 使用方法

1. 运行 `PcCare.exe`，默认无需管理员权限。
2. 点击“开始检查”查看系统信息、启动项、后台优化和当前视觉效果配置；也可在对应页面单独扫描。
3. 如需修改当前用户项目，确认后直接生效；修改 HKLM、公共启动文件夹或第三方计划任务时，程序仅为该次固定操作请求 UAC。
4. 在“性能优化”页查看检查到的当前配置，再点击“应用性能模式”。该操作只修改当前 Windows 用户的视觉效果设置，不创建备份。
5. 在“后台优化”页可一键应用低风险推荐项，或逐项恢复为未配置默认状态。已有企业策略会被锁定，不会覆盖。
6. 在“硬件与电源”页先点击“刷新”；查看硬件评估和电源建议后，可对单项确认或执行“一键推荐优化”。操作记录仅保留在本次运行界面中。

## 已知限制

- 当前界面只提供简体中文。
- UWP StartupTask 本期仅预留模型，尚未扫描或修改；服务、驱动和其它 Autoruns 持久化点不在范围内。
- `StartupApproved` 是任务管理器使用的未公开状态层；程序只识别常见状态字节、保留其余数据，并在操作后重新扫描验证。
- 视觉效果优化只作用于当前用户；部分任务栏和资源管理器效果需要重新登录后完全生效。
- 性能模式不创建备份，应用前请确认确实需要该设置。
- 电源模块不会调整电池 DC 策略、休眠、快速启动或硬件固件参数；检测到 OEM/组织管理时只读展示。
- Linux 开发环境不能构建 WPF；仓库使用 Windows GitHub Actions 完成编译、测试和发布。

## 项目结构

```text
src/PcCare.App       WPF 界面和交互逻辑
src/PcCare.Core      系统模型与通用规则
src/PcCare.Windows   Windows 信息、视觉效果和启动项读取
tests                核心测试
docs                 参考项目、安全设计和验收标准
```
