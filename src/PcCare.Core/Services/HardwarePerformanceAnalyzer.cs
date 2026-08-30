using PcCare.Core.Models;

namespace PcCare.Core.Services;

public static class HardwarePerformanceAnalyzer
{
    public static HardwareAssessment Analyze(HardwareProfile profile)
    {
        CpuClass cpuClass = ClassifyCpu(profile.CpuName, profile.CpuVendor, profile.CpuPhysicalCores, profile.CpuLogicalProcessors);
        MemoryClass memoryClass = ClassifyMemory(profile.MemoryTotalBytes);
        var upgrades = new List<HardwareRecommendationItem>();
        PrimaryBottleneck bottleneck = DetermineBottleneck(profile, memoryClass, cpuClass, upgrades);
        HardwarePerformanceLevel performance = DeterminePerformance(profile, memoryClass, cpuClass, bottleneck);
        return new HardwareAssessment
        {
            CpuClass = cpuClass,
            MemoryClass = memoryClass,
            PerformanceLevel = performance,
            PrimaryBottleneck = bottleneck,
            Summary = BuildSummary(profile, performance, bottleneck),
            UpgradeRecommendations = upgrades
        };
    }

    public static MemoryClass ClassifyMemory(ulong bytes)
    {
        double gigabytes = bytes / 1024d / 1024d / 1024d;
        return gigabytes <= 4 ? MemoryClass.VeryLow :
            gigabytes <= 8 ? MemoryClass.Low :
            gigabytes < 16 ? MemoryClass.Normal : MemoryClass.Good;
    }

    public static CpuClass ClassifyCpu(string name, string vendor, int physicalCores, int logicalProcessors)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "无法读取")
        {
            return CpuClass.Unknown;
        }

        if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            int generation = FindIntelGeneration(name);
            return generation switch
            {
                > 0 and <= 4 => CpuClass.Legacy,
                >= 5 and <= 7 => CpuClass.Old,
                >= 8 and <= 10 => CpuClass.Moderate,
                >= 11 => CpuClass.Modern,
                _ => CpuClass.Unknown
            };
        }

        if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || vendor.Contains("AMD", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
            {
                return CpuClass.Moderate;
            }

            return physicalCores <= 2 || logicalProcessors <= 2 ? CpuClass.Legacy : CpuClass.Unknown;
        }

        return CpuClass.Unknown;
    }

    private static int FindIntelGeneration(string name)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(name, @"i[3579]-([0-9]{4,5})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int model))
        {
            return 0;
        }

        return model / 1000;
    }

    private static PrimaryBottleneck DetermineBottleneck(HardwareProfile profile, MemoryClass memory, CpuClass cpu, ICollection<HardwareRecommendationItem> upgrades)
    {
        if (profile.DeviceType == DeviceType.VirtualMachine)
        {
            return PrimaryBottleneck.Unknown;
        }

        if (profile.SystemDiskType == DiskType.Hdd)
        {
            upgrades.Add(new HardwareRecommendationItem("高", "优先考虑升级系统盘 SSD", "当前系统盘为机械硬盘；升级 SSD 对启动、程序加载和办公响应通常更明显。"));
            if (memory == MemoryClass.VeryLow)
            {
                upgrades.Add(new HardwareRecommendationItem("高", "同时考虑升级内存", "当前物理内存约 4 GB 或更低；建议至少升级至 8 GB，多任务办公可优先考虑 16 GB。"));
            }

            return PrimaryBottleneck.Disk;
        }

        if (memory == MemoryClass.VeryLow)
        {
            upgrades.Add(new HardwareRecommendationItem("高", "优先考虑升级内存", "当前物理内存约 4 GB 或更低；建议至少升级至 8 GB，多任务办公可优先考虑 16 GB。"));
            return PrimaryBottleneck.Memory;
        }

        if (memory == MemoryClass.Low)
        {
            upgrades.Add(new HardwareRecommendationItem("中", "关注多任务内存压力", "8 GB 可满足基础办公，但同时运行浏览器、企业微信和 Office 时可能偏紧。"));
        }

        if (profile.SystemDiskTotalBytes > 0 && (profile.SystemDiskFreeBytes < 10L * 1024 * 1024 * 1024 || (double)profile.SystemDiskFreeBytes / profile.SystemDiskTotalBytes < 0.15))
        {
            upgrades.Add(new HardwareRecommendationItem("高", "释放或扩容系统盘空间", "系统盘可用空间偏低，可能影响更新、临时文件和应用响应。"));
            return PrimaryBottleneck.StorageSpace;
        }

        return cpu is CpuClass.Legacy or CpuClass.Old ? PrimaryBottleneck.Cpu : PrimaryBottleneck.None;
    }

    private static HardwarePerformanceLevel DeterminePerformance(HardwareProfile profile, MemoryClass memory, CpuClass cpu, PrimaryBottleneck bottleneck)
    {
        if (profile.DeviceType == DeviceType.VirtualMachine)
        {
            return HardwarePerformanceLevel.Unknown;
        }

        if ((bottleneck == PrimaryBottleneck.Disk && memory == MemoryClass.VeryLow) || (cpu == CpuClass.Legacy && memory == MemoryClass.VeryLow))
        {
            return HardwarePerformanceLevel.Legacy;
        }

        if (bottleneck is PrimaryBottleneck.Disk or PrimaryBottleneck.Memory || cpu is CpuClass.Legacy or CpuClass.Old)
        {
            return HardwarePerformanceLevel.Low;
        }

        if (memory == MemoryClass.Good && profile.SystemDiskType == DiskType.NvmeSsd && cpu == CpuClass.Modern)
        {
            return HardwarePerformanceLevel.High;
        }

        if (memory == MemoryClass.Good && profile.SystemDiskType is (DiskType.SataSsd or DiskType.NvmeSsd))
        {
            return HardwarePerformanceLevel.Good;
        }

        return HardwarePerformanceLevel.Standard;
    }

    private static string BuildSummary(HardwareProfile profile, HardwarePerformanceLevel level, PrimaryBottleneck bottleneck) =>
        profile.DeviceType == DeviceType.VirtualMachine
            ? "这是虚拟机；性能主要取决于宿主机资源和虚拟化平台配置。"
            : bottleneck switch
            {
                PrimaryBottleneck.Disk => "主要瓶颈是系统盘机械硬盘；硬件升级通常比继续调整 Windows 参数更有价值。",
                PrimaryBottleneck.Memory => "主要瓶颈是物理内存偏低；建议优先评估内存扩容。",
                PrimaryBottleneck.StorageSpace => "主要瓶颈是系统盘可用空间偏低；应优先处理空间问题。",
                PrimaryBottleneck.Cpu => "CPU 平台较早；可继续使用保守软件优化，但不建议用高风险电源调整掩盖硬件限制。",
                PrimaryBottleneck.None => level is HardwarePerformanceLevel.Good or HardwarePerformanceLevel.High ? "当前硬件状态适合办公，通常无需为了少量收益强制切换高性能模式。" : "当前硬件适合基础办公。",
                _ => "硬件信息不足，建议结合实际办公负载判断。"
            };
}
