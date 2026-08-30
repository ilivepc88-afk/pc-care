# 启动项优化设计

## 目标与范围

面向 Windows 10、Windows 11 和 LTSC 的离线工具，只优化登录与开机时启动的软件。它不做系统精简、杀毒、服务优化或注册表清理。

| 来源 | 扫描 | 修改方式 | 权限 |
|---|---:|---|---|
| HKCU Run / RunOnce | 是 | Run 可通过 `StartupApproved\\Run` 启停；RunOnce 只读 | 当前用户 |
| HKLM Run / RunOnce | 是 | Run 可通过 `StartupApproved\\Run`/`Run32` 启停；RunOnce 只读 | UAC |
| HKLM WOW6432Node Run / RunOnce | 是 | Run 可通过 `StartupApproved\\Run32` 启停；RunOnce 只读 | UAC |
| 用户/公共启动目录 | 是 | 只写 `StartupApproved\\StartupFolder`，不删除文件 | 公共目录需 UAC |
| 登录/开机计划任务 | 是 | 仅切换 Enabled，不删除任务 | UAC |
| UWP StartupTask | 本期未实现 | 不修改 | 不适用 |

`StartupApproved` 不是 Microsoft 公开支持的应用 API。实现只识别已广泛观察到的状态首字节：`02/06/08` 视为启用，`01/03/07/09` 视为禁用；未知值默认按启用显示且不声称状态可靠。写入时只变更首字节、保留其它数据，并通过重新扫描验证。

## 规则与一键优化

规则由 `StartupRule` 数据结构表达，先匹配保护规则，再匹配可选项和低风险项：

- Windows 核心、安全、登录、EDR、终端管控、VPN、资产管理和监控：保留且不可操作。
- 驱动、显卡、声卡、触控板、蓝牙、无线和硬件厂商组件：默认保留。
- Teams、企业微信、QQ、OneDrive、网盘、WPS、打印状态等：标记“可选”，不自动处理。
- 更新器、助手、启动器和明确的后台常驻项：才可能标记为“建议优化 / 低风险”。
- 未知项目：中风险、只允许用户逐项确认、绝不进入一键优化。

一键优化的固定条件是：`Enabled && RecommendDisable && Low && CanDisable`。

## 参考

- Microsoft Run / RunOnce 说明：<https://learn.microsoft.com/windows/win32/setupapi/run-and-runonce-registry-keys>
- Sysinternals Autoruns 说明：<https://learn.microsoft.com/sysinternals/downloads/autoruns>
- 计划任务只采用 Windows Task Scheduler 的启用状态，不删除任务。
