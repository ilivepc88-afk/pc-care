using System.Security;
using Microsoft.Win32;
using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Windows.Services;

public sealed class BackgroundOptimizationService
{
    private readonly RegistryManager _registry;
    private readonly WindowsFeatureDetector _featureDetector;
    private readonly BackgroundOptimizationOwnershipStore _ownershipStore;

    public BackgroundOptimizationService()
        : this(new RegistryManager())
    {
    }

    internal BackgroundOptimizationService(RegistryManager registry)
    {
        _registry = registry;
        _featureDetector = new WindowsFeatureDetector(registry);
        _ownershipStore = new BackgroundOptimizationOwnershipStore(registry);
    }

    public Task<List<OptimizationItem>> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    internal List<OptimizationItem> Scan(CancellationToken cancellationToken)
    {
        var items = new List<OptimizationItem>();
        foreach (BackgroundOptimizationRule rule in BackgroundOptimizationCatalog.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(EvaluateSafely(rule));
        }

        return items
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    internal OptimizationItem Evaluate(BackgroundOptimizationRule rule)
    {
        bool supported = rule.IsSupported(_featureDetector);
        var item = CreateItem(rule, supported);
        if (!supported)
        {
            item.CurrentState = OptimizationState.Unsupported;
            item.Reason = "当前系统不支持、未安装该组件，或没有检测到对应功能。";
            return item;
        }

        IReadOnlyList<RegistryValueState> values = rule.Targets.Select(_registry.Read).ToList();
        bool anyConfigured = values.Any(value => value.Exists);
        bool optimized = values.All(value => value.Exists && value.DwordValue == rule.OptimizedValue);
        bool owned = _ownershipStore.IsOwned(rule.Id);
        bool anyPolicyConfigured = rule.IsPolicy && GetPolicyLocations(rule).Select(_registry.Read).Any(value => value.Exists);
        bool ltscAlreadyOptimized = IsLtscConsumerFeatureAlreadyOptimized(rule, anyConfigured);
        OptimizationEvaluation evaluation = OptimizationStateEvaluator.Evaluate(
            supported,
            rule.IsPolicy,
            rule.IsPolicy ? anyPolicyConfigured : anyConfigured,
            optimized,
            owned,
            ltscAlreadyOptimized);

        item.CurrentState = evaluation.State;
        item.CanOptimize = evaluation.CanOptimize;
        item.CanRestore = evaluation.CanRestore;
        item.IsOrganizationManaged = evaluation.IsOrganizationManaged;
        if (item.IsOrganizationManaged)
        {
            item.Reason = "检测到现有组织策略或企业管理配置，PcCare 不会覆盖。";
        }
        else if (ltscAlreadyOptimized)
        {
            item.Reason = "LTSC 通常不包含 Microsoft Consumer Experience 推广内容，当前按已优化显示。";
        }
        else if (optimized)
        {
            item.Reason = owned ? "已由 PcCare 优化，可恢复为 Windows 默认的未配置状态。" : "当前已关闭；该值并非由 PcCare 创建，不会擅自恢复。";
        }

        return item;
    }

    internal BackgroundOptimizationOwnershipStore OwnershipStore => _ownershipStore;

    internal RegistryManager Registry => _registry;

    private OptimizationItem EvaluateSafely(BackgroundOptimizationRule rule)
    {
        try
        {
            return Evaluate(rule);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException or ArgumentException)
        {
            OptimizationItem item = CreateItem(rule, supported: true);
            item.CurrentState = OptimizationState.Unknown;
            item.Reason = $"无法读取当前状态：{exception.Message}";
            return item;
        }
    }

    private OptimizationItem CreateItem(BackgroundOptimizationRule rule, bool supported) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Category = rule.Category,
        Description = rule.Description,
        Supported = supported,
        Recommendation = rule.Recommendation,
        RiskLevel = OptimizationRiskLevel.Low,
        RequiresAdministrator = rule.RequiresAdministrator,
        RequiresLogoff = rule.RequiresLogoff,
        RequiresExplorerRestart = rule.RequiresExplorerRestart,
        RegistryPath = $"{(rule.PrimaryTarget.Hive == Microsoft.Win32.RegistryHive.CurrentUser ? "HKCU" : "HKLM")}\\{rule.PrimaryTarget.SubKeyPath}",
        RegistryName = rule.PrimaryTarget.ValueName,
        PolicyPath = rule.IsPolicy ? rule.PrimaryTarget.SubKeyPath : string.Empty,
        Reason = rule.Reason,
        Impact = rule.Impact
    };

    private bool IsLtscConsumerFeatureAlreadyOptimized(BackgroundOptimizationRule rule, bool anyConfigured)
    {
        if (rule.Id != "windows.consumer-experience" || anyConfigured)
        {
            return false;
        }

        RegistryValueState productName = _registry.Read(new RegistryValueLocation(
            Microsoft.Win32.RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "ProductName"));
        return (productName.StringValue ?? string.Empty).Contains("LTSC", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<RegistryValueLocation> GetPolicyLocations(BackgroundOptimizationRule rule)
    {
        foreach (RegistryValueLocation target in rule.Targets)
        {
            yield return target;
            RegistryHive alternateHive = target.Hive == RegistryHive.CurrentUser
                ? RegistryHive.LocalMachine
                : RegistryHive.CurrentUser;
            yield return target with { Hive = alternateHive };
        }
    }
}
