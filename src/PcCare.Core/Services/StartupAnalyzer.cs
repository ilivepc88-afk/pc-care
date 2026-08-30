using System.Text.RegularExpressions;
using PcCare.Core.Models;

namespace PcCare.Core.Services;

public sealed record StartupRule(
    string NamePattern,
    string PublisherPattern,
    string PathPattern,
    StartupRecommendation Recommendation,
    StartupRiskLevel RiskLevel,
    string Reason,
    bool IsProtected = false);

public sealed class StartupAnalyzer
{
    private static readonly StartupRule[] Rules =
    [
        new(@"(securityhealth|windows security|windows defender|microsoft defender|credential guard|bitlocker|windows hello|windows logon|windows audio|windows shell)",
            @".*", @".*", StartupRecommendation.Keep, StartupRiskLevel.Critical,
            "Windows 核心安全或登录组件，禁止建议关闭。", true),
        new(@"(crowdstrike|sentinelone|carbon black|sophos|trend micro|symantec|mcafee|kaspersky|eset|bitdefender|deep instinct|edr|dlp|zero trust|zerotrust|vpn|堡垒机|终端管控|准入|资产管理|监控 agent)",
            @".*", @".*", StartupRecommendation.Keep, StartupRiskLevel.Critical,
            "疑似企业安全、准入、终端管控或监控组件，禁止建议关闭。", true),
        new(@".*", @"(intel|amd|nvidia|realtek|synaptics|elan|dell|lenovo|hewlett.packard|h3c)",
            @".*", StartupRecommendation.Keep, StartupRiskLevel.High,
            "驱动或硬件厂商组件，默认保留。", true),
        new(@"(touchpad|bluetooth|audio|graphics|display|hotkey|wireless|wifi)",
            @".*", @".*", StartupRecommendation.Keep, StartupRiskLevel.High,
            "疑似驱动、输入设备或硬件控制组件，默认保留。", true),
        new(@"(teams|wechat|weixin|企业微信|qq|onedrive|dropbox|google drive|wps|printer.*(status|monitor)|nvidia.*tray)",
            @".*", @".*", StartupRecommendation.Optional, StartupRiskLevel.Medium,
            "该程序是否需要随登录启动取决于用户或企业使用要求。"),
        new(@"(updater|update|assistant|launcher|background|browser.*assistant|edge.*background|adobe.*(gc|invoker))",
            @".*", @".*", StartupRecommendation.RecommendDisable, StartupRiskLevel.Low,
            "软件更新、助手或后台启动程序通常不影响主程序手动启动，可减少登录后的后台进程。"),
        new(@"(discord|steam|spotify|thunder|迅雷|baidu.*(netdisk|wangpan)|百度网盘)",
            @".*", @".*", StartupRecommendation.RecommendDisable, StartupRiskLevel.Low,
            "非必要的消费、娱乐或同步程序无需随 Windows 登录启动。")
    ];

    public void Analyze(StartupItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.IsMicrosoft = Matches(@"microsoft|windows", item.Publisher, item.CompanyName, item.ProductName) ||
                           Matches(@"\\windows\\|\\microsoft\\", item.ExecutablePath, item.SourcePath);

        StartupRule? rule = Rules.FirstOrDefault(candidate => MatchesRule(candidate, item));
        if (rule is null)
        {
            item.Recommendation = StartupRecommendation.Unknown;
            item.RiskLevel = StartupRiskLevel.Medium;
            item.Reason = "无法可靠判断用途；不会进入一键优化。";
            item.IsSystemComponent = false;
        }
        else
        {
            item.Recommendation = rule.Recommendation;
            item.RiskLevel = rule.RiskLevel;
            item.Reason = rule.Reason;
            item.IsSystemComponent = rule.IsProtected;
        }

        if (item.SourceType == StartupSourceType.ScheduledTask &&
            item.SourcePath.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase))
        {
            item.Recommendation = StartupRecommendation.Keep;
            item.RiskLevel = StartupRiskLevel.Critical;
            item.IsSystemComponent = true;
            item.Reason = "Microsoft Windows 系统计划任务，当前版本不允许修改。";
        }

        bool supportsToggle = item.SourceType is StartupSourceType.RegistryRun or StartupSourceType.StartupFolder or StartupSourceType.ScheduledTask;
        item.CanDisable = item.Enabled && !item.IsSystemComponent && supportsToggle;
        item.CanEnable = !item.Enabled && !item.IsSystemComponent && supportsToggle;

        if (item.SourceType == StartupSourceType.RegistryRunOnce)
        {
            item.CanDisable = false;
            item.CanEnable = false;
            item.Reason = "RunOnce 项将在下次成功登录后由 Windows 自动删除；为避免使用未验证的禁用方式，当前版本仅展示。";
        }
    }

    private static bool MatchesRule(StartupRule rule, StartupItem item)
    {
        return Matches(rule.NamePattern, item.Name, item.Description, item.ProductName) &&
               Matches(rule.PublisherPattern, item.Publisher, item.CompanyName) &&
               Matches(rule.PathPattern, item.ExecutablePath, item.SourcePath);
    }

    private static bool Matches(string pattern, params string[] values)
    {
        if (pattern == ".*")
        {
            return true;
        }

        return values.Any(value => !string.IsNullOrWhiteSpace(value) &&
                                   Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));
    }
}
