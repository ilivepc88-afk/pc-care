# PcCare

PcCare 是一个面向公司老旧 Windows 电脑的离线系统体检与安全清理工具。它不依赖服务端、不安装服务、不常驻、不上传数据，也不包含杀毒、系统精简或高风险“注册表加速”。

当前版本：`0.1.0`（第一阶段 MVP）

## 功能

- Windows、CPU、内存、系统盘、磁盘介质、连续运行时间和待重启状态体检。
- 扫描并预览超过 7 天的固定白名单临时文件。
- 用户确认后重新扫描并执行清理；需要时通过 UAC 按需提权。
- 读取 HKCU/HKLM Run 和用户/公共启动目录，仅展示，不修改。
- 导出完全离线的 HTML 与 JSON 报告。
- 在程序目录不可写时，将报告保存到“文档/PcCare/Reports”。

## 清理范围

第一阶段只包含以下固定分类：

1. 当前用户临时目录。
2. Windows 临时目录。
3. 当前用户 Windows 错误报告归档。
4. 系统 Windows 错误报告归档。
5. 缩略图缓存（默认不勾选）。

每个候选文件必须同时满足：位于白名单根目录内、超过 7 天、不是重解析点、未被其他进程占用。

## 明确不做

- 不扫描或处理病毒。
- 不修改 Windows 服务、Windows Update、Defender、SysMain、搜索索引或遥测。
- 不清理注册表、Prefetch、事件日志、浏览器资料。
- 不访问桌面、文档、下载、收藏夹或网盘目录。
- 不从网络下载或执行脚本，不自动更新。
- 不提供任意命令、任意路径清理或静默批量模式。

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

测试只使用随机创建的临时测试目录，不会扫描或清理开发电脑的真实临时目录。

## 发布单文件 EXE

```powershell
dotnet publish src/PcCare.App/PcCare.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=false `
  -o artifacts/publish
```

输出文件：`artifacts/publish/PcCare.exe`

自包含程序内置 .NET 运行时，不要求目标电脑预装 .NET。它不会自动获得后续 .NET 安全补丁，发布新版本时应同步升级 SDK 并重新构建。

## 使用方法

1. 运行 `PcCare.exe`，无需管理员权限即可开始只读体检。
2. 检查“可清理项目”的文件数量、空间和风险说明。
3. 勾选分类并点击“执行清理”。
4. Windows 弹出 UAC 时确认授权；程序只把分类 ID 传给提权进程，提权进程会重新按内置白名单扫描。
5. 点击“导出报告”生成 HTML 和 JSON 文件。

## 已知限制

- 当前界面只提供简体中文。
- 第一阶段不支持禁用或恢复启动项。
- 不删除空目录。
- 普通权限扫描 Windows 系统目录时，部分不可访问文件不会进入预览；提权清理仍会重新校验。
- 文件扫描与删除之间存在很短的时间窗口；执行前会再次检查完整路径和重解析点，但无法替代基于 Windows 文件句柄的内核级防竞态方案。
- Linux 开发环境不能构建 WPF；仓库使用 Windows GitHub Actions完成编译、测试和单文件发布。

## 项目结构

```text
src/PcCare.App       WPF 界面、UAC 协调和提权清理入口
src/PcCare.Core      清理模型、扫描器、执行器、路径安全和报告
src/PcCare.Windows   Windows 信息、白名单目录和启动项读取
tests                核心安全测试
docs                 参考项目、安全设计和验收标准
```
