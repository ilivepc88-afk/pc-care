# 后台优化设计

## 目标与边界

本模块面向老旧 Windows 10、Windows 11 与 LTSC 电脑，减少推荐内容、浏览器关窗后的后台驻留和少量个性化推荐。它是离线本地工具，不执行脚本、不联网、不卸载组件，也不处理病毒。

明确不在范围内：Windows Defender、防火墙、Windows Update、UAC、SmartScreen、BITS、SysMain、Windows Search、WMI、通知、剪贴板、最近文件/Jump List、全局 Background Apps、服务、AppX、Edge 或 WebView2 删除。

## 实现规则

| 优化项 | 注册表目标 | 条件 | 恢复默认 |
|---|---|---|---|
| Windows 使用建议 / 设置建议 | HKCU ContentDeliveryManager | Windows Build 支持 | 删除 PcCare 创建的值 |
| Consumer Experience | HKLM CloudContent 策略 | 企业/教育/专业版；LTSC 无该内容时显示已优化 | 删除 PcCare 创建的策略值 |
| Windows Widgets | HKLM Dsh 策略 | 检测到 Web Experience Pack 或既有 Dsh 状态 | 删除 PcCare 创建的策略值 |
| Windows 10 News and interests | HKLM Windows Feeds 策略 | Windows 10 且检测到 Feeds 状态 | 删除 PcCare 创建的策略值 |
| Copilot | HKCU WindowsCopilot 策略 | 检测到 Copilot 包或既有策略 | 删除 PcCare 创建的策略值 |
| 个性化体验 / Spotlight | HKCU CloudContent 策略 | Windows Build 支持 | 删除 PcCare 创建的策略值 |
| Edge / Chrome 后台模式 | HKLM 浏览器策略 | 通过 App Paths 检测浏览器 | 删除 PcCare 创建的策略值 |
| 广告 ID | HKCU AdvertisingInfo | Windows Build 支持 | 删除 PcCare 创建的值 |

所有规则固定在 `BackgroundOptimizationCatalog`。提权子进程只接收固定 `itemId + action`，并再次查找规则表；外部输入不能提供注册表路径、值名、命令或脚本。

## 企业策略保护

读取策略时同时检查 HKCU 和 HKLM 的同名 Policies 值。若发现值而当前项目没有 PcCare 所有权标记，项目会显示“组织策略”，不允许一键或单项覆盖。所有 HKLM 写入沿用现有的单次 UAC 子进程通道；HKCU 操作不提权。

PcCare 的所有权标记只用于判断能否“恢复默认”，保存于 `HKCU\Software\PcCare\BackgroundOptimization\Managed`。它不保存旧值、不是备份，也不创建系统还原点。恢复默认只删除由 PcCare 标记创建的目标值和标记，绝不删除未标记的现有组织策略。

## 版本与功能检测

规则不只依赖 Windows 10/11 名称：Widgets/Copilot 检查已安装 App 包或策略状态，Edge/Chrome 检查 32/64 位 `App Paths`，News and interests 限制为 Windows 10 Build 范围并检查 Feeds 状态。无法确认存在的功能显示“系统不支持”，不写入预期外的功能键。

Windows 11 的“隐藏整个开始菜单推荐区”尚未加入本期。该类策略可能同时影响最近文件、最近添加应用或管理员既有布局，和本工具“不干扰办公最近文件/Jump List”的边界冲突；Consumer Experience 已覆盖明确的 Microsoft 消费推广内容。

## 参考与差异

- Microsoft Edge Startup Boost 策略：<https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/startupboostenabled>
- Microsoft Edge 策略目录：<https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies>
- Chrome `BackgroundModeEnabled` 策略：<https://chromeenterprise.google/policies/background-mode-enabled/>
- Windows Experience 策略 CSP：<https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience>
- Sophia Script、Win11Debloat 和 winutil 仅用于比较功能边界；没有复制其脚本、备份、App 删除、服务调整或远程下载行为。
