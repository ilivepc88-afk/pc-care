using System.Runtime.InteropServices;
using Microsoft.Win32;
using PcCare.Core.Models;

namespace PcCare.Windows.Services;

public sealed class PowerManager
{
    public PowerProfile ReadProfile()
    {
        Guid activeScheme = GetActiveScheme();
        PowerPlanKind schemeKind = GetPlanKind(activeScheme);
        bool managed = HasOrganizationPowerPolicy();
        return new PowerProfile
        {
            ActiveSchemeGuid = activeScheme,
            ActiveSchemeKind = schemeKind,
            ActiveSchemeName = GetPlanName(schemeKind, activeScheme),
            ProcessorMinAc = ReadAc(activeScheme, PowerGuids.ProcessorSubgroup, PowerGuids.ProcessorMinimum),
            ProcessorMinDc = ReadDc(activeScheme, PowerGuids.ProcessorSubgroup, PowerGuids.ProcessorMinimum),
            ProcessorMaxAc = ReadAc(activeScheme, PowerGuids.ProcessorSubgroup, PowerGuids.ProcessorMaximum),
            ProcessorMaxDc = ReadDc(activeScheme, PowerGuids.ProcessorSubgroup, PowerGuids.ProcessorMaximum),
            PcieLinkStateAc = ReadAc(activeScheme, PowerGuids.PcieSubgroup, PowerGuids.PcieLinkState),
            PcieLinkStateDc = ReadDc(activeScheme, PowerGuids.PcieSubgroup, PowerGuids.PcieLinkState),
            DiskIdleTimeoutAcSeconds = ReadAc(activeScheme, PowerGuids.DiskSubgroup, PowerGuids.DiskIdleTimeout),
            DiskIdleTimeoutDcSeconds = ReadDc(activeScheme, PowerGuids.DiskSubgroup, PowerGuids.DiskIdleTimeout),
            SleepTimeoutAcSeconds = ReadAc(activeScheme, PowerGuids.SleepSubgroup, PowerGuids.SleepIdleTimeout),
            SleepTimeoutDcSeconds = ReadDc(activeScheme, PowerGuids.SleepSubgroup, PowerGuids.SleepIdleTimeout),
            HibernateEnabled = ReadDword(@"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled"),
            FastStartupEnabled = ReadDword(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled"),
            IsOrganizationManaged = managed,
            OrganizationManagementReason = managed ? "检测到组织电源策略，PcCare 不会覆盖。" : string.Empty
        };
    }

    public void SetActiveScheme(Guid schemeGuid)
    {
        ThrowOnError(PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid), "切换电源计划");
    }

    public void SetAcValue(Guid schemeGuid, Guid subgroup, Guid setting, uint value)
    {
        ThrowOnError(PowerWriteACValueIndex(IntPtr.Zero, ref schemeGuid, ref subgroup, ref setting, value), "写入 AC 电源参数");
        ThrowOnError(PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid), "应用 AC 电源参数");
    }

    public void RestoreBuiltInSchemeDefaults(Guid schemeGuid)
    {
        if (GetPlanKind(schemeGuid) is not (PowerPlanKind.Balanced or PowerPlanKind.HighPerformance or PowerPlanKind.PowerSaver))
        {
            throw new InvalidOperationException("当前不是可安全恢复的 Windows 内置电源计划。");
        }

        ThrowOnError(PowerRestoreIndividualDefaultPowerScheme(ref schemeGuid), "恢复内置电源计划默认值");
        ThrowOnError(PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid), "应用默认电源计划");
    }

    private static Guid GetActiveScheme()
    {
        uint status = PowerGetActiveScheme(IntPtr.Zero, out IntPtr pointer);
        ThrowOnError(status, "读取当前电源计划");
        try
        {
            return Marshal.PtrToStructure<Guid>(pointer);
        }
        finally
        {
            LocalFree(pointer);
        }
    }

    private static uint? ReadAc(Guid scheme, Guid subgroup, Guid setting) => Read(PowerReadACValueIndex, scheme, subgroup, setting);

    private static uint? ReadDc(Guid scheme, Guid subgroup, Guid setting) => Read(PowerReadDCValueIndex, scheme, subgroup, setting);

    private delegate uint ReadValueDelegate(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, out uint valueIndex);

    private static uint? Read(ReadValueDelegate read, Guid scheme, Guid subgroup, Guid setting)
    {
        uint status = read(IntPtr.Zero, ref scheme, ref subgroup, ref setting, out uint value);
        return status == 0 ? value : null;
    }

    private static bool? ReadDword(string path, string name)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
            return key?.GetValue(name) switch { int value => value != 0, _ => null };
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static bool HasOrganizationPowerPolicy()
    {
        try
        {
            using RegistryKey? policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Power", writable: false);
            using RegistryKey? mdmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PolicyManager\current\device\Power", writable: false);
            return policyKey is not null || mdmKey is not null;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return true;
        }
    }

    public static PowerPlanKind GetPlanKind(Guid schemeGuid)
    {
        if (schemeGuid == PowerGuids.BalancedScheme) return PowerPlanKind.Balanced;
        if (schemeGuid == PowerGuids.HighPerformanceScheme) return PowerPlanKind.HighPerformance;
        if (schemeGuid == PowerGuids.PowerSaverScheme) return PowerPlanKind.PowerSaver;
        if (schemeGuid == PowerGuids.UltimatePerformanceScheme) return PowerPlanKind.UltimatePerformance;
        return schemeGuid == Guid.Empty ? PowerPlanKind.Unknown : PowerPlanKind.Custom;
    }

    private static string GetPlanName(PowerPlanKind kind, Guid schemeGuid) => kind switch
    {
        PowerPlanKind.Balanced => "平衡",
        PowerPlanKind.HighPerformance => "高性能",
        PowerPlanKind.PowerSaver => "节能",
        PowerPlanKind.UltimatePerformance => "卓越性能",
        PowerPlanKind.Custom => $"自定义（{schemeGuid.ToString()[..8]}）",
        _ => "无法读取"
    };

    private static void ThrowOnError(uint status, string operation)
    {
        if (status != 0)
        {
            throw new System.ComponentModel.Win32Exception((int)status, $"无法{operation}。");
        }
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, out uint valueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, out uint valueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid settingGuid, uint valueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerRestoreIndividualDefaultPowerScheme(ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
