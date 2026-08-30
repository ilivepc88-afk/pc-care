using Microsoft.Win32;
using PcCare.Core.Models;

namespace PcCare.Windows.Services;

internal static class BackgroundOptimizationCatalog
{
    private const string ContentDeliveryManager = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string CloudContentPolicy = @"SOFTWARE\Policies\Microsoft\Windows\CloudContent";

    public static readonly IReadOnlyList<BackgroundOptimizationRule> Rules =
    [
        new(
            "windows.tips",
            "Windows 使用建议",
            OptimizationCategory.WindowsContent,
            "减少 Windows 在系统界面中展示的提示、欢迎体验和功能建议。",
            OptimizationRecommendation.Recommended,
            "Windows Tips 和建议会在登录后展示推荐内容。",
            "关闭后不影响 Windows 设置或正常办公；部分提示将不再显示。",
            [
                UserSetting("SoftLandingEnabled"),
                UserSetting("SystemPaneSuggestionsEnabled")
            ],
            0,
            false,
            true,
            false,
            detector => detector.WindowsBuild >= 17763),
        new(
            "windows.settings-suggestions",
            "Windows 设置建议内容",
            OptimizationCategory.WindowsContent,
            "减少 Windows 设置页和系统界面中的建议内容。",
            OptimizationRecommendation.Recommended,
            "设置建议内容可能展示功能推荐或推广信息。",
            "关闭后不影响 Windows Settings 的正常配置功能。",
            [UserSetting("SubscribedContent-338393Enabled")],
            0,
            false,
            false,
            true,
            detector => detector.WindowsBuild >= 19041),
        new(
            "windows.consumer-experience",
            "Microsoft Consumer Experience",
            OptimizationCategory.WindowsContent,
            "阻止 Windows 推荐消费应用和开始菜单推广内容。",
            OptimizationRecommendation.Recommended,
            "减少自动推荐的消费应用和第三方应用推广。",
            "不会删除已安装应用；只阻止后续消费体验推荐。",
            [PolicyMachine(CloudContentPolicy, "DisableWindowsConsumerFeatures")],
            1,
            true,
            true,
            false,
            detector => detector.IsBusinessEdition),
        new(
            "windows.widgets",
            "Windows Widgets / News and interests",
            OptimizationCategory.Widgets,
            "关闭 Widgets 或 Windows 10 News and interests 的后台内容入口。",
            OptimizationRecommendation.Recommended,
            "Widgets 会加载新闻、天气和 Web Experience 相关内容。",
            "不会卸载 Windows Web Experience Pack 或 WebView2；关闭后普通桌面和浏览器不受影响。",
            [PolicyMachine(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests")],
            0,
            true,
            true,
            true,
            detector => detector.HasAppPackage("MicrosoftWindows.Client.WebExperience") ||
                        detector.HasWidgetsPolicyState()),
        new(
            "windows.news-and-interests",
            "Windows 10 News and interests",
            OptimizationCategory.Widgets,
            "关闭 Windows 10 任务栏 News and interests 内容入口。",
            OptimizationRecommendation.Recommended,
            "该功能会在任务栏展示新闻、天气和推荐内容。",
            "不会删除任何 Windows 组件；关闭后可通过恢复默认重新交给系统管理。",
            [PolicyMachine(@"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds")],
            0,
            true,
            true,
            true,
            detector => detector.WindowsBuild is >= 19041 and < 22000 && detector.HasWindowsFeedsState()),
        new(
            "windows.copilot",
            "Microsoft Copilot",
            OptimizationCategory.Copilot,
            "关闭当前用户可用的 Windows Copilot 入口。",
            OptimizationRecommendation.Recommended,
            "不使用 Copilot 时可减少相关入口和后台触发。",
            "不会卸载 Copilot、Edge 或 WebView2；若系统未安装 Copilot 则不显示为可操作。",
            [PolicyUser(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot")],
            1,
            true,
            true,
            true,
            detector => detector.HasAppPackage("Microsoft.Copilot") ||
                        detector.HasPolicyValue(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot")),
        new(
            "privacy.tailored-experiences",
            "个性化体验",
            OptimizationCategory.PrivacyAndRecommendations,
            "关闭基于诊断数据的个性化提示和推荐。",
            OptimizationRecommendation.Recommended,
            "可减少 Windows 针对用户展示的个性化提示和推荐。",
            "性能收益有限，但不会影响核心办公功能。",
            [PolicyUser(CloudContentPolicy, "DisableTailoredExperiencesWithDiagnosticData")],
            1,
            true,
            true,
            false,
            detector => detector.WindowsBuild >= 17763),
        new(
            "edge.startup-boost",
            "Edge Startup Boost",
            OptimizationCategory.BrowserBackground,
            "阻止 Microsoft Edge 在 Windows 登录后预加载后台进程。",
            OptimizationRecommendation.Recommended,
            "老旧电脑关闭后可减少登录后的 Edge 常驻进程和内存占用。",
            "Edge 仍可正常打开，但首次启动可能略慢。",
            [PolicyMachine(@"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled")],
            0,
            true,
            false,
            false,
            detector => detector.HasExecutableRegistration("msedge.exe")),
        new(
            "edge.background-mode",
            "Edge 后台运行",
            OptimizationCategory.BrowserBackground,
            "关闭所有 Edge 窗口后，不继续保留后台扩展和应用进程。",
            OptimizationRecommendation.Recommended,
            "减少 Edge 关闭窗口后的后台驻留。",
            "不会结束当前 Edge 进程，也不会禁止手动打开 Edge。",
            [PolicyMachine(@"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled")],
            0,
            true,
            false,
            false,
            detector => detector.HasExecutableRegistration("msedge.exe")),
        new(
            "chrome.background-mode",
            "Chrome 后台运行",
            OptimizationCategory.BrowserBackground,
            "关闭所有 Chrome 窗口后，不继续保留后台应用进程。",
            OptimizationRecommendation.Recommended,
            "减少 Chrome 关闭窗口后的后台驻留。",
            "不会结束当前 Chrome 进程，也不会禁止手动打开 Chrome。",
            [PolicyMachine(@"SOFTWARE\Policies\Google\Chrome", "BackgroundModeEnabled")],
            0,
            true,
            false,
            false,
            detector => detector.HasExecutableRegistration("chrome.exe")),
        new(
            "privacy.advertising-id",
            "Windows 广告 ID",
            OptimizationCategory.PrivacyAndRecommendations,
            "关闭 App 使用广告 ID 提供个性化广告。",
            OptimizationRecommendation.Optional,
            "减少跨 App 的个性化广告标识使用。",
            "性能收益有限，仅作为隐私偏好选项。",
            [new RegistryValueLocation(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled")],
            0,
            false,
            false,
            false,
            detector => detector.WindowsBuild >= 17763),
        new(
            "windows.spotlight",
            "Windows Spotlight 锁屏推荐",
            OptimizationCategory.InterfaceExperience,
            "关闭锁屏 Spotlight 推荐和相关提示内容。",
            OptimizationRecommendation.Optional,
            "锁屏 Spotlight 主要影响推荐内容，不是核心性能项。",
            "性能收益有限；关闭后锁屏不再显示 Spotlight 推荐。",
            [PolicyUser(CloudContentPolicy, "DisableWindowsSpotlightFeatures")],
            1,
            true,
            false,
            true,
            detector => detector.WindowsBuild >= 17763)
    ];

    public static BackgroundOptimizationRule? Find(string id) => Rules.FirstOrDefault(rule => string.Equals(rule.Id, id, StringComparison.Ordinal));

    private static RegistryValueLocation UserSetting(string valueName) => new(RegistryHive.CurrentUser, ContentDeliveryManager, valueName);

    private static RegistryValueLocation PolicyMachine(string subKeyPath, string valueName) => new(RegistryHive.LocalMachine, subKeyPath, valueName);

    private static RegistryValueLocation PolicyUser(string subKeyPath, string valueName) => new(RegistryHive.CurrentUser, subKeyPath, valueName);

}
