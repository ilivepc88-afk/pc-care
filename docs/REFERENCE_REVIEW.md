# GitHub参考项目评估

评估日期：2026-08-29。

## FluentCleaner

- 地址：https://github.com/builtbybel/FluentCleaner
- 许可证：MIT。
- 技术：C#；现代版使用 .NET 10 + WinUI 3，Classic版使用 .NET Framework 4.8；共享`FluentCleaner.Core`扫描/清理逻辑。
- 可借鉴：扫描与清理分阶段、声明式清理规则、轻量Classic发行方式。
- 本项目处理：仅参考架构，没有复制源码，也没有引入Winapp2规则库。

## SystemManager

- 地址：https://github.com/laurentiu021/SystemManager
- 许可证：MIT。
- 技术：C#、.NET 10、WPF、MVVM。
- 可借鉴：WPF桌面架构、Windows系统信息和启动项展示思路。
- 不作为基础项目：功能面过宽，包含网络、进程、游戏和在线更新等本项目不需要的能力，删减成本高于构建小型MVP。
- 本项目处理：仅参考公开文档和架构，没有复制源码。

## BitBroom

- 地址：https://github.com/pwnapplehat/BitBroom
- 许可证：MIT。
- 技术：.NET 10、WPF，强调安全清理与测试。
- 可借鉴：重解析点防护、清理规则测试、安全门禁。
- 本项目处理：独立实现路径校验和测试，没有复制源码。

## 未采用项目

- Winhance：https://github.com/memstechtips/Winhance
  - 使用 PolyForm Shield 1.0.0，不适合Fork、改名并作为同类工具重新分发。
- Optimizer：https://github.com/hellzerg/optimizer
  - 旧项目已声明废弃，GPL许可，并包含禁用更新、服务和Defender等不符合本项目边界的操作。
- Winapp2：https://github.com/MoscaDotTo/Winapp2
  - CC BY-SA 4.0，规则范围非常大；第一阶段为降低误删和许可证复杂度不引入。

## 结论

选择独立开发小型 C# WPF 工具，不整体 Fork 任何现有优化工具。当前版本已移除文件扫描与清理，只保留系统体检、启动项只读展示、本地报告和视觉效果性能模式。
