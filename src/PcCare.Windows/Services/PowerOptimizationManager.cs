using System.ComponentModel;
using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Windows.Services;

public sealed class PowerOptimizationManager
{
    private readonly HardwarePowerService _hardwarePowerService;
    private readonly PowerManager _powerManager;

    public PowerOptimizationManager()
        : this(new HardwarePowerService(), new PowerManager())
    {
    }

    internal PowerOptimizationManager(HardwarePowerService hardwarePowerService, PowerManager powerManager)
    {
        _hardwarePowerService = hardwarePowerService;
        _powerManager = powerManager;
    }

    public async Task<PowerOptimizationOperationResult> ApplyAsync(PowerOptimizationOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        HardwarePowerSnapshot snapshot = await _hardwarePowerService.ScanAsync(cancellationToken);
        PowerOptimizationItem? item = snapshot.OptimizationItems.FirstOrDefault(candidate => candidate.Id == operation.ItemId);
        if (item is null)
        {
            return Failure(operation, "优化项不在允许范围内。");
        }

        if (snapshot.Power.IsOrganizationManaged)
        {
            return Failure(operation, snapshot.Power.OrganizationManagementReason);
        }

        if (operation.Action == PowerOptimizationAction.RestoreDefault)
        {
            if (item.Id != "power.restore-built-in-defaults" || !item.CanRestore)
            {
                return Failure(operation, "该项目不提供安全的恢复默认操作。");
            }

            return Execute(operation, () => _powerManager.RestoreBuiltInSchemeDefaults(snapshot.Power.ActiveSchemeGuid), "已恢复当前 Windows 内置电源计划的官方默认参数。");
        }

        if (!item.CanApply)
        {
            return Failure(operation, string.IsNullOrWhiteSpace(item.Reason) ? "当前设备不适合执行该调整。" : item.Reason);
        }

        return item.Id switch
        {
            "power.recommended-scheme" => ApplyScheme(operation, snapshot),
            "power.processor-max-ac" => Execute(operation, () => _powerManager.SetAcValue(snapshot.Power.ActiveSchemeGuid, PowerGuids.ProcessorSubgroup, PowerGuids.ProcessorMaximum, 100), "已将 AC/台式机 CPU 最大状态设为 100%。"),
            "power.hdd-disk-idle-ac" => Execute(operation, () => _powerManager.SetAcValue(snapshot.Power.ActiveSchemeGuid, PowerGuids.DiskSubgroup, PowerGuids.DiskIdleTimeout, 1200), "已将台式机机械硬盘休眠时间设为 20 分钟。"),
            "power.pcie-link-state-ac" => Execute(operation, () => _powerManager.SetAcValue(snapshot.Power.ActiveSchemeGuid, PowerGuids.PcieSubgroup, PowerGuids.PcieLinkState, 0), "已关闭台式机 PCIe Link State 节能。"),
            "power.restore-built-in-defaults" => RestoreViaApply(operation, snapshot, item),
            _ => Failure(operation, "优化项不在允许范围内。")
        };
    }

    private PowerOptimizationOperationResult ApplyScheme(PowerOptimizationOperation operation, HardwarePowerSnapshot snapshot)
    {
        Guid target = snapshot.Hardware.DeviceType == DeviceType.Laptop ? PowerGuids.BalancedScheme : PowerGuids.HighPerformanceScheme;
        return Execute(operation, () => _powerManager.SetActiveScheme(target), $"已切换至{(target == PowerGuids.BalancedScheme ? "平衡" : "高性能")}电源计划。");
    }

    private PowerOptimizationOperationResult RestoreViaApply(PowerOptimizationOperation operation, HardwarePowerSnapshot snapshot, PowerOptimizationItem item) =>
        item.CanRestore
            ? Execute(operation, () => _powerManager.RestoreBuiltInSchemeDefaults(snapshot.Power.ActiveSchemeGuid), "已恢复当前 Windows 内置电源计划的官方默认参数。")
            : Failure(operation, "当前计划不提供安全的恢复默认操作。");

    private static PowerOptimizationOperationResult Execute(PowerOptimizationOperation operation, Action action, string message)
    {
        try
        {
            action();
            return new PowerOptimizationOperationResult(operation.ItemId, operation.Action, true, message);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or Win32Exception or IOException or InvalidOperationException)
        {
            return Failure(operation, $"操作未完成：{exception.Message}");
        }
    }

    private static PowerOptimizationOperationResult Failure(PowerOptimizationOperation operation, string message) =>
        new(operation.ItemId, operation.Action, false, message);
}
