using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Windows.Services;

public sealed class HardwarePowerService
{
    private readonly HardwareProfileService _hardwareProfileService;
    private readonly PowerManager _powerManager;

    public HardwarePowerService()
        : this(new HardwareProfileService(), new PowerManager())
    {
    }

    internal HardwarePowerService(HardwareProfileService hardwareProfileService, PowerManager powerManager)
    {
        _hardwareProfileService = hardwareProfileService;
        _powerManager = powerManager;
    }

    public async Task<HardwarePowerSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        Task<HardwareProfile> hardwareTask = _hardwareProfileService.CaptureAsync(cancellationToken);
        Task<PowerProfile> powerTask = Task.Run(_powerManager.ReadProfile, cancellationToken);
        HardwareProfile hardware = await ReadHardwareSafelyAsync(hardwareTask);
        PowerProfile power = await ReadPowerSafelyAsync(powerTask);
        HardwareAssessment assessment = HardwarePerformanceAnalyzer.Analyze(hardware);
        return new HardwarePowerSnapshot(hardware, power, assessment, PowerRecommendationEngine.Create(hardware, power, assessment));
    }

    private static async Task<HardwareProfile> ReadHardwareSafelyAsync(Task<HardwareProfile> task)
    {
        try
        {
            return await task;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new HardwareProfile();
        }
    }

    private static async Task<PowerProfile> ReadPowerSafelyAsync(Task<PowerProfile> task)
    {
        try
        {
            return await task;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new PowerProfile { IsOrganizationManaged = true, OrganizationManagementReason = "无法安全读取电源策略，PcCare 不会执行电源写入。" };
        }
    }
}

public sealed record HardwarePowerSnapshot(
    HardwareProfile Hardware,
    PowerProfile Power,
    HardwareAssessment Assessment,
    List<PowerOptimizationItem> OptimizationItems);
