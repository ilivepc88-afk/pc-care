using PcCare.Core.Models;

namespace PcCare.Core.Services;

public static class PowerRecommendationEngine
{
    public static List<PowerOptimizationItem> Create(HardwareProfile hardware, PowerProfile power, HardwareAssessment assessment)
    {
        bool protectedByPolicy = power.IsOrganizationManaged;
        bool desktopLowEnd = hardware.DeviceType == DeviceType.Desktop && assessment.PerformanceLevel is (HardwarePerformanceLevel.Legacy or HardwarePerformanceLevel.Low);
        bool laptop = hardware.DeviceType == DeviceType.Laptop;
        bool virtualMachine = hardware.DeviceType == DeviceType.VirtualMachine;
        bool oemManaged = hardware.HasOemPowerManager;

        return
        [
            PlanItem(power, desktopLowEnd, laptop, virtualMachine, oemManaged, protectedByPolicy),
            ProcessorMaxItem(power, hardware, virtualMachine, oemManaged, protectedByPolicy),
            DiskIdleItem(power, hardware, virtualMachine, protectedByPolicy),
            PcieItem(power, hardware, assessment, virtualMachine, oemManaged, protectedByPolicy),
            BuiltInDefaultsItem(power, protectedByPolicy)
        ];
    }

    private static PowerOptimizationItem PlanItem(PowerProfile power, bool desktopLowEnd, bool laptop, bool virtualMachine, bool oemManaged, bool managed)
    {
        bool desktopRecommend = desktopLowEnd && power.ActiveSchemeKind is not PowerPlanKind.HighPerformance and not PowerPlanKind.UltimatePerformance;
        bool laptopRecommend = laptop && power.ActiveSchemeKind is (PowerPlanKind.HighPerformance or PowerPlanKind.UltimatePerformance);
        PowerPlanKind target = laptopRecommend ? PowerPlanKind.Balanced : PowerPlanKind.HighPerformance;
        bool applicable = !virtualMachine && (desktopRecommend || laptopRecommend);
        string reason = managed ? power.OrganizationManagementReason : oemManaged ? "检测到厂商电源管理软件，PcCare 不会自动切换电源计划。" : desktopRecommend ? "老旧或低配台式机可按需使用高性能计划；不会修改 CPU 最小状态或节能以外的硬件参数。" : laptopRecommend ? "笔记本使用高性能/卓越性能可能增加发热、噪声和耗电，平衡模式通常更适合办公。" : "当前电源计划与设备类型相符，无需调整。";
        return new PowerOptimizationItem
        {
            Id = "power.recommended-scheme",
            Name = "电源计划",
            Description = "按设备类型和硬件瓶颈给出电源计划建议。",
            CurrentValue = power.ActiveSchemeName,
            RecommendedValue = ToDisplayName(target),
            RiskLevel = PowerRiskLevel.Low,
            Recommendation = applicable && !oemManaged && !managed ? PowerRecommendation.Recommended : PowerRecommendation.Keep,
            Applicable = applicable,
            CanApply = applicable && !oemManaged && !managed,
            Reason = reason
        };
    }

    private static PowerOptimizationItem ProcessorMaxItem(PowerProfile power, HardwareProfile hardware, bool virtualMachine, bool oemManaged, bool managed)
    {
        bool onAc = hardware.DeviceType == DeviceType.Desktop || hardware.AcPowerConnected == true;
        bool needsChange = power.ProcessorMaxAc is < 100;
        bool applicable = !virtualMachine && onAc && needsChange;
        return new PowerOptimizationItem
        {
            Id = "power.processor-max-ac",
            Name = "CPU 最大状态（插电/台式机）",
            Description = "仅确保插电或台式机需要性能时可达到最大性能；不修改电池最大状态和 CPU 最小状态。",
            CurrentValue = Percent(power.ProcessorMaxAc),
            RecommendedValue = "100%",
            AcValue = Percent(power.ProcessorMaxAc),
            DcValue = Percent(power.ProcessorMaxDc),
            RiskLevel = PowerRiskLevel.Low,
            Recommendation = applicable && !oemManaged && !managed ? PowerRecommendation.Recommended : PowerRecommendation.Keep,
            Applicable = applicable,
            CanApply = applicable && !oemManaged && !managed,
            Reason = managed ? power.OrganizationManagementReason : oemManaged ? "检测到厂商电源管理软件，保持当前参数。" : !onAc && hardware.DeviceType == DeviceType.Laptop ? "当前笔记本未连接 AC 电源；不会将插电策略套用到电池。" : needsChange ? "当前 AC 最大状态低于 100%，可能限制需要性能时的处理器上限。" : "当前已允许处理器在 AC/台式机模式下达到最大状态。"
        };
    }

    private static PowerOptimizationItem DiskIdleItem(PowerProfile power, HardwareProfile hardware, bool virtualMachine, bool managed)
    {
        bool needsChange = hardware.DeviceType == DeviceType.Desktop && hardware.SystemDiskType == DiskType.Hdd && power.DiskIdleTimeoutAcSeconds is > 0 and < 1200;
        return new PowerOptimizationItem
        {
            Id = "power.hdd-disk-idle-ac",
            Name = "机械硬盘休眠时间（台式机）",
            Description = "对机械硬盘台式机避免过短的磁盘休眠；SSD/NVMe 不宣传该项能显著提升性能。",
            CurrentValue = Minutes(power.DiskIdleTimeoutAcSeconds),
            RecommendedValue = "20 分钟",
            AcValue = Minutes(power.DiskIdleTimeoutAcSeconds),
            DcValue = Minutes(power.DiskIdleTimeoutDcSeconds),
            RiskLevel = PowerRiskLevel.Low,
            Recommendation = needsChange && !managed && !virtualMachine ? PowerRecommendation.Recommended : PowerRecommendation.Keep,
            Applicable = needsChange,
            CanApply = needsChange && !managed && !virtualMachine,
            Reason = managed ? power.OrganizationManagementReason : needsChange ? "当前机械硬盘休眠时间过短，可能造成频繁启停和重新响应等待。" : hardware.SystemDiskType == DiskType.Hdd ? "当前磁盘休眠时间没有明显偏短。" : "系统盘不是机械硬盘，不建议因性能目的调整此项。"
        };
    }

    private static PowerOptimizationItem PcieItem(PowerProfile power, HardwareProfile hardware, HardwareAssessment assessment, bool virtualMachine, bool oemManaged, bool managed)
    {
        bool applicable = hardware.DeviceType == DeviceType.Desktop && assessment.PerformanceLevel is (HardwarePerformanceLevel.Legacy or HardwarePerformanceLevel.Low) && power.PcieLinkStateAc is > 0;
        return new PowerOptimizationItem
        {
            Id = "power.pcie-link-state-ac",
            Name = "PCIe Link State 节能（台式机）",
            Description = "老旧台式机的可选兼容性调整，不属于一键优化。",
            CurrentValue = Pcie(power.PcieLinkStateAc),
            RecommendedValue = "关闭",
            AcValue = Pcie(power.PcieLinkStateAc),
            DcValue = Pcie(power.PcieLinkStateDc),
            RiskLevel = PowerRiskLevel.Low,
            Recommendation = applicable && !oemManaged && !managed && !virtualMachine ? PowerRecommendation.Optional : PowerRecommendation.Keep,
            Applicable = applicable,
            CanApply = applicable && !oemManaged && !managed && !virtualMachine,
            Reason = managed ? power.OrganizationManagementReason : oemManaged ? "检测到厂商电源管理软件，保持当前 PCIe 设置。" : applicable ? "仅作为老旧台式机的可选项；笔记本保持 Windows 节能策略。" : "当前设备不适合调整此项。"
        };
    }

    private static PowerOptimizationItem BuiltInDefaultsItem(PowerProfile power, bool managed)
    {
        bool builtIn = power.ActiveSchemeKind is PowerPlanKind.Balanced or PowerPlanKind.HighPerformance or PowerPlanKind.PowerSaver;
        return new PowerOptimizationItem
        {
            Id = "power.restore-built-in-defaults",
            Name = "恢复当前内置计划默认值",
            Description = "仅重置当前 Windows 内置电源计划的官方默认参数；不删除、重命名或覆盖自定义/OEM 电源计划。",
            CurrentValue = power.ActiveSchemeName,
            RecommendedValue = "Windows 官方默认值",
            RiskLevel = PowerRiskLevel.Medium,
            Recommendation = PowerRecommendation.Optional,
            Applicable = builtIn,
            CanApply = builtIn && !managed,
            CanRestore = builtIn && !managed,
            Reason = managed ? power.OrganizationManagementReason : builtIn ? "这是可选的广泛重置操作，不会加入一键优化。" : "当前是自定义或卓越性能计划，不提供恢复以避免影响 OEM/企业配置。"
        };
    }

    private static string ToDisplayName(PowerPlanKind kind) => kind switch
    {
        PowerPlanKind.Balanced => "平衡",
        PowerPlanKind.HighPerformance => "高性能",
        PowerPlanKind.PowerSaver => "节能",
        PowerPlanKind.UltimatePerformance => "卓越性能",
        _ => "保持当前"
    };

    private static string Percent(uint? value) => value is null ? "无法读取" : $"{value}%";
    private static string Minutes(uint? value) => value is null ? "无法读取" : value == 0 ? "从不" : $"{Math.Ceiling(value.Value / 60d):0} 分钟";
    private static string Pcie(uint? value) => value switch { 0 => "关闭", 1 => "中等节能", 2 => "最大节能", null => "无法读取", _ => $"未知（{value}）" };
}
