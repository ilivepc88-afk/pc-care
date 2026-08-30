using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class HardwarePowerRecommendationTests
{
    [Theory]
    [InlineData(4, MemoryClass.VeryLow)]
    [InlineData(8, MemoryClass.Low)]
    [InlineData(16, MemoryClass.Good)]
    public void ClassifyMemory_UsesOfficeFriendlyTiers(int gigabytes, MemoryClass expected)
    {
        Assert.Equal(expected, HardwarePerformanceAnalyzer.ClassifyMemory((ulong)gigabytes * 1024 * 1024 * 1024));
    }

    [Fact]
    public void Analyze_HddAndFourGigabytes_RecommendsBothDiskAndMemory()
    {
        HardwareAssessment assessment = HardwarePerformanceAnalyzer.Analyze(Profile(DeviceType.Desktop, DiskType.Hdd, 4));

        Assert.Equal(PrimaryBottleneck.Disk, assessment.PrimaryBottleneck);
        Assert.Equal(HardwarePerformanceLevel.Legacy, assessment.PerformanceLevel);
        Assert.Contains(assessment.UpgradeRecommendations, item => item.Name.Contains("SSD", StringComparison.Ordinal));
        Assert.Contains(assessment.UpgradeRecommendations, item => item.Name.Contains("内存", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_VirtualMachine_DoesNotRecommendPhysicalHardwareChanges()
    {
        HardwareAssessment assessment = HardwarePerformanceAnalyzer.Analyze(Profile(DeviceType.VirtualMachine, DiskType.Hdd, 4));

        Assert.Equal(PrimaryBottleneck.Unknown, assessment.PrimaryBottleneck);
        Assert.Empty(assessment.UpgradeRecommendations);
    }

    [Fact]
    public void Create_DesktopHddWithLowPerformance_OffersOnlyExpectedLowRiskRecommendations()
    {
        HardwareProfile hardware = Profile(DeviceType.Desktop, DiskType.Hdd, 4);
        HardwareAssessment assessment = HardwarePerformanceAnalyzer.Analyze(hardware);
        List<PowerOptimizationItem> items = PowerRecommendationEngine.Create(hardware, new PowerProfile
        {
            ActiveSchemeKind = PowerPlanKind.Balanced,
            ActiveSchemeName = "平衡",
            ProcessorMaxAc = 80,
            DiskIdleTimeoutAcSeconds = 300,
            PcieLinkStateAc = 1
        }, assessment);

        Assert.True(Item(items, "power.recommended-scheme").CanApply);
        Assert.True(Item(items, "power.processor-max-ac").CanApply);
        Assert.True(Item(items, "power.hdd-disk-idle-ac").CanApply);
        Assert.Equal(PowerRecommendation.Optional, Item(items, "power.pcie-link-state-ac").Recommendation);
    }

    [Fact]
    public void Create_LaptopOnBattery_ProtectsBatterySpecificSettings()
    {
        HardwareProfile hardware = Profile(DeviceType.Laptop, DiskType.NvmeSsd, 16, acConnected: false);
        HardwareAssessment assessment = HardwarePerformanceAnalyzer.Analyze(hardware);
        List<PowerOptimizationItem> items = PowerRecommendationEngine.Create(hardware, new PowerProfile
        {
            ActiveSchemeKind = PowerPlanKind.HighPerformance,
            ActiveSchemeName = "高性能",
            ProcessorMaxAc = 70,
            ProcessorMaxDc = 70,
            PcieLinkStateAc = 2,
            PcieLinkStateDc = 2
        }, assessment);

        Assert.True(Item(items, "power.recommended-scheme").CanApply);
        Assert.Equal("平衡", Item(items, "power.recommended-scheme").RecommendedValue);
        Assert.False(Item(items, "power.processor-max-ac").CanApply);
        Assert.False(Item(items, "power.pcie-link-state-ac").CanApply);
    }

    [Fact]
    public void Create_VirtualMachine_DoesNotOfferAutomaticPowerWrites()
    {
        HardwareProfile hardware = Profile(DeviceType.VirtualMachine, DiskType.SsdUnknown, 8);
        HardwareAssessment assessment = HardwarePerformanceAnalyzer.Analyze(hardware);
        List<PowerOptimizationItem> items = PowerRecommendationEngine.Create(hardware, new PowerProfile
        {
            ActiveSchemeKind = PowerPlanKind.Balanced,
            ActiveSchemeName = "平衡",
            ProcessorMaxAc = 50,
            DiskIdleTimeoutAcSeconds = 60,
            PcieLinkStateAc = 2
        }, assessment);

        Assert.DoesNotContain(items, item => item.CanApply && item.Recommendation == PowerRecommendation.Recommended);
    }

    private static PowerOptimizationItem Item(IEnumerable<PowerOptimizationItem> items, string id) =>
        Assert.Single(items.Where(item => item.Id == id));

    private static HardwareProfile Profile(DeviceType deviceType, DiskType diskType, int memoryGb, bool? acConnected = true) => new()
    {
        DeviceType = deviceType,
        IsVirtualMachine = deviceType == DeviceType.VirtualMachine,
        HasBattery = deviceType == DeviceType.Laptop,
        AcPowerConnected = acConnected,
        CpuName = "Intel(R) Core(TM) i5-6500 CPU",
        CpuVendor = "GenuineIntel",
        CpuPhysicalCores = 4,
        CpuLogicalProcessors = 4,
        MemoryTotalBytes = (ulong)memoryGb * 1024 * 1024 * 1024,
        SystemDiskType = diskType,
        SystemDiskTotalBytes = 512L * 1024 * 1024 * 1024,
        SystemDiskFreeBytes = 256L * 1024 * 1024 * 1024
    };
}
