namespace PcCare.Core.Models;

public enum DeviceType
{
    Desktop,
    Laptop,
    VirtualMachine,
    Unknown
}

public enum DiskType
{
    Hdd,
    SataSsd,
    NvmeSsd,
    SsdUnknown,
    Unknown
}

public enum CpuClass
{
    Legacy,
    Old,
    Moderate,
    Modern,
    Unknown
}

public enum MemoryClass
{
    VeryLow,
    Low,
    Normal,
    Good
}

public enum HardwarePerformanceLevel
{
    Legacy,
    Low,
    Standard,
    Good,
    High,
    Unknown
}

public enum PrimaryBottleneck
{
    Disk,
    Memory,
    Cpu,
    StorageSpace,
    None,
    Unknown
}

public enum PowerPlanKind
{
    Balanced,
    HighPerformance,
    PowerSaver,
    UltimatePerformance,
    Custom,
    Unknown
}

public enum PowerRecommendation
{
    Recommended,
    Optional,
    Keep,
    Unsupported
}

public enum PowerRiskLevel
{
    Low,
    Medium,
    High
}

public enum PowerOptimizationAction
{
    Apply,
    RestoreDefault
}

public sealed class HardwareProfile
{
    public string CpuName { get; init; } = "无法读取";
    public string CpuVendor { get; init; } = "无法读取";
    public int CpuPhysicalCores { get; init; }
    public int CpuLogicalProcessors { get; init; }
    public int? CpuMaxFrequencyMhz { get; init; }
    public int? CpuCurrentFrequencyMhz { get; init; }
    public ulong MemoryTotalBytes { get; init; }
    public ulong MemoryAvailableBytes { get; init; }
    public string SystemDrive { get; init; } = "无法读取";
    public string SystemDiskModel { get; init; } = "无法识别";
    public DiskType SystemDiskType { get; init; } = DiskType.Unknown;
    public string SystemDiskBusType { get; init; } = "无法识别";
    public long SystemDiskTotalBytes { get; init; }
    public long SystemDiskFreeBytes { get; init; }
    public DeviceType DeviceType { get; init; } = DeviceType.Unknown;
    public bool HasBattery { get; init; }
    public int? BatteryPercent { get; init; }
    public string BatteryStatus { get; init; } = "不适用";
    public bool? AcPowerConnected { get; init; }
    public bool IsVirtualMachine { get; init; }
    public string Manufacturer { get; init; } = "无法读取";
    public string Model { get; init; } = "无法读取";
    public bool HasOemPowerManager { get; init; }
    public string OemPowerManagerHint { get; init; } = string.Empty;
    public string WindowsVersion { get; init; } = "无法读取";
    public string WindowsBuild { get; init; } = "无法读取";
}

public sealed class PowerProfile
{
    public Guid ActiveSchemeGuid { get; init; }
    public PowerPlanKind ActiveSchemeKind { get; init; } = PowerPlanKind.Unknown;
    public string ActiveSchemeName { get; init; } = "无法读取";
    public uint? ProcessorMinAc { get; init; }
    public uint? ProcessorMinDc { get; init; }
    public uint? ProcessorMaxAc { get; init; }
    public uint? ProcessorMaxDc { get; init; }
    public uint? PcieLinkStateAc { get; init; }
    public uint? PcieLinkStateDc { get; init; }
    public uint? DiskIdleTimeoutAcSeconds { get; init; }
    public uint? DiskIdleTimeoutDcSeconds { get; init; }
    public uint? SleepTimeoutAcSeconds { get; init; }
    public uint? SleepTimeoutDcSeconds { get; init; }
    public bool? HibernateEnabled { get; init; }
    public bool? FastStartupEnabled { get; init; }
    public bool IsOrganizationManaged { get; init; }
    public string OrganizationManagementReason { get; init; } = string.Empty;
}

public sealed class HardwareAssessment
{
    public CpuClass CpuClass { get; init; } = CpuClass.Unknown;
    public MemoryClass MemoryClass { get; init; } = MemoryClass.Normal;
    public HardwarePerformanceLevel PerformanceLevel { get; init; } = HardwarePerformanceLevel.Unknown;
    public PrimaryBottleneck PrimaryBottleneck { get; init; } = PrimaryBottleneck.Unknown;
    public string Summary { get; init; } = "硬件信息不足，无法完成完整评估。";
    public List<HardwareRecommendationItem> UpgradeRecommendations { get; init; } = [];
}

public sealed record HardwareRecommendationItem(string Priority, string Name, string Reason);

public sealed class PowerOptimizationItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string CurrentValue { get; init; } = "无法读取";
    public string RecommendedValue { get; init; } = "保持当前";
    public PowerRiskLevel RiskLevel { get; init; } = PowerRiskLevel.Low;
    public PowerRecommendation Recommendation { get; init; } = PowerRecommendation.Keep;
    public bool Applicable { get; init; }
    public bool CanApply { get; init; }
    public bool CanRestore { get; init; }
    public bool RequiresAdministrator { get; init; }
    public bool RequiresRestart { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string AcValue { get; init; } = string.Empty;
    public string DcValue { get; init; } = string.Empty;
}

public sealed record PowerOptimizationOperation(string ItemId, PowerOptimizationAction Action);

public sealed record PowerOptimizationOperationResult(string ItemId, PowerOptimizationAction Action, bool Succeeded, string Message);

public sealed record PowerOptimizationLogEntry(
    DateTimeOffset Timestamp,
    string ItemName,
    PowerOptimizationAction Action,
    string PreviousValue,
    string TargetValue,
    bool Succeeded,
    string Message);
