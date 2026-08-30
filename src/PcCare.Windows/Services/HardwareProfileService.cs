using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using PcCare.Core.Models;

namespace PcCare.Windows.Services;

public sealed class HardwareProfileService
{
    public Task<HardwareProfile> CaptureAsync(CancellationToken cancellationToken = default) =>
        Task.Run(Capture, cancellationToken);

    private static HardwareProfile Capture()
    {
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string root = Path.GetPathRoot(systemDirectory) ?? "C:\\";
        var systemDrive = new DriveInfo(root);
        (ulong totalMemory, ulong availableMemory) = ReadMemory();
        string cpuName = ReadRegistry(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString")?.Trim() ?? "无法读取";
        string cpuVendor = ReadRegistry(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "VendorIdentifier")?.Trim() ?? "无法读取";
        (int? cpuMaxFrequency, int? cpuCurrentFrequency) = ReadCpuFrequencies();
        string manufacturer = ReadRegistry(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer")?.Trim() ?? "无法读取";
        string model = ReadRegistry(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName")?.Trim() ?? "无法读取";
        bool isVirtualMachine = IsVirtualMachine(manufacturer, model, cpuName);
        SystemPowerStatus powerStatus = ReadPowerStatus();
        bool hasBattery = powerStatus.HasBattery;
        DeviceType deviceType = DetectDeviceType(isVirtualMachine, hasBattery, manufacturer, model);
        DiskInformation disk = ReadSystemDisk(root);
        bool hasOemPowerManager = HasOemPowerManager(manufacturer, out string oemPowerManagerHint);

        return new HardwareProfile
        {
            CpuName = cpuName,
            CpuVendor = cpuVendor,
            CpuPhysicalCores = ReadPhysicalCoreCount(),
            CpuLogicalProcessors = Environment.ProcessorCount,
            CpuMaxFrequencyMhz = cpuMaxFrequency,
            CpuCurrentFrequencyMhz = cpuCurrentFrequency,
            MemoryTotalBytes = totalMemory,
            MemoryAvailableBytes = availableMemory,
            SystemDrive = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            SystemDiskModel = disk.Model,
            SystemDiskType = disk.Type,
            SystemDiskBusType = disk.BusType,
            SystemDiskTotalBytes = systemDrive.IsReady ? systemDrive.TotalSize : 0,
            SystemDiskFreeBytes = systemDrive.IsReady ? systemDrive.AvailableFreeSpace : 0,
            DeviceType = deviceType,
            HasBattery = hasBattery,
            BatteryPercent = powerStatus.BatteryPercent,
            BatteryStatus = powerStatus.BatteryStatus,
            AcPowerConnected = powerStatus.AcConnected,
            IsVirtualMachine = isVirtualMachine,
            Manufacturer = manufacturer,
            Model = model,
            HasOemPowerManager = hasOemPowerManager,
            OemPowerManagerHint = oemPowerManagerHint,
            WindowsVersion = ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion") ?? "无法读取",
            WindowsBuild = ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber") ?? "无法读取"
        };
    }

    private static DiskInformation ReadSystemDisk(string root)
    {
        try
        {
            uint? diskNumber = GetDiskNumber(root);
            if (diskNumber is null)
            {
                return new DiskInformation("无法识别", DiskType.Unknown, "无法识别");
            }

            using SafeFileHandle disk = CreateFile(
                $"\\\\.\\PhysicalDrive{diskNumber.Value}",
                0,
                FileShareRead | FileShareWrite | FileShareDelete,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);
            if (disk.IsInvalid)
            {
                return new DiskInformation("无法识别", DiskType.Unknown, "无法识别");
            }

            (string model, StorageBusType busType) = ReadStorageDescriptor(disk);
            bool? incursSeekPenalty = ReadSeekPenalty(disk);
            DiskType type = GetDiskType(incursSeekPenalty, busType);
            return new DiskInformation(string.IsNullOrWhiteSpace(model) ? "无法识别" : model, type, ToBusTypeName(busType));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new DiskInformation("无法识别", DiskType.Unknown, "无法识别");
        }
    }

    private static uint? GetDiskNumber(string root)
    {
        string volumeName = @"\\.\" + root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        using SafeFileHandle volume = CreateFile(volumeName, 0, FileShareRead | FileShareWrite | FileShareDelete, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (volume.IsInvalid)
        {
            return null;
        }

        var extents = new VolumeDiskExtents();
        return DeviceIoControl(volume, IoctlVolumeGetVolumeDiskExtents, IntPtr.Zero, 0, ref extents, (uint)Marshal.SizeOf<VolumeDiskExtents>(), out _, IntPtr.Zero)
            ? extents.FirstExtent.DiskNumber
            : null;
    }

    private static (string Model, StorageBusType BusType) ReadStorageDescriptor(SafeFileHandle disk)
    {
        StoragePropertyQuery query = new(StorageDeviceProperty);
        byte[] buffer = new byte[1024];
        if (!DeviceIoControl(disk, IoctlStorageQueryProperty, ref query, (uint)Marshal.SizeOf<StoragePropertyQuery>(), buffer, (uint)buffer.Length, out uint returned, IntPtr.Zero) || returned < 32)
        {
            return (string.Empty, StorageBusType.Unknown);
        }

        uint productOffset = BitConverter.ToUInt32(buffer, 16);
        int busOffset = 28;
        string model = productOffset > 0 && productOffset < returned ? ReadAsciiString(buffer, (int)productOffset) : string.Empty;
        StorageBusType bus = (StorageBusType)buffer[busOffset];
        return (model.Trim(), bus);
    }

    private static bool? ReadSeekPenalty(SafeFileHandle disk)
    {
        StoragePropertyQuery query = new(StorageDeviceSeekPenaltyProperty);
        var descriptor = new DeviceSeekPenaltyDescriptor();
        return DeviceIoControl(disk, IoctlStorageQueryProperty, ref query, (uint)Marshal.SizeOf<StoragePropertyQuery>(), ref descriptor, (uint)Marshal.SizeOf<DeviceSeekPenaltyDescriptor>(), out uint returned, IntPtr.Zero) && returned >= 9
            ? descriptor.IncursSeekPenalty
            : null;
    }

    private static DiskType GetDiskType(bool? incursSeekPenalty, StorageBusType bus) => incursSeekPenalty switch
    {
        true => DiskType.Hdd,
        false when bus == StorageBusType.Nvme => DiskType.NvmeSsd,
        false when bus is StorageBusType.Sata or StorageBusType.Ata => DiskType.SataSsd,
        false => DiskType.SsdUnknown,
        _ => DiskType.Unknown
    };

    private static string ToBusTypeName(StorageBusType bus) => bus switch
    {
        StorageBusType.Nvme => "NVMe",
        StorageBusType.Sata => "SATA",
        StorageBusType.Ata => "ATA",
        StorageBusType.Usb => "USB",
        StorageBusType.Scsi => "SCSI",
        StorageBusType.Raid => "RAID",
        StorageBusType.Virtual => "Virtual",
        _ => "无法识别"
    };

    private static string ReadAsciiString(byte[] buffer, int offset)
    {
        int end = Array.IndexOf(buffer, (byte)0, offset);
        return System.Text.Encoding.ASCII.GetString(buffer, offset, (end < 0 ? buffer.Length : end) - offset);
    }

    private static int ReadPhysicalCoreCount()
    {
        int length = 0;
        GetLogicalProcessorInformationEx(LogicalProcessorRelationship.RelationProcessorCore, IntPtr.Zero, ref length);
        if (length <= 0)
        {
            return 0;
        }

        IntPtr buffer = Marshal.AllocHGlobal(length);
        try
        {
            if (!GetLogicalProcessorInformationEx(LogicalProcessorRelationship.RelationProcessorCore, buffer, ref length))
            {
                return 0;
            }

            int count = 0;
            int offset = 0;
            while (offset < length)
            {
                var header = Marshal.PtrToStructure<ProcessorRelationshipHeader>(IntPtr.Add(buffer, offset));
                if (header.Relationship == LogicalProcessorRelationship.RelationProcessorCore)
                {
                    count++;
                }

                if (header.Size <= 0)
                {
                    break;
                }

                offset += header.Size;
            }

            return count;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static SystemPowerStatus ReadPowerStatus()
    {
        if (!GetSystemPowerStatus(out SystemPowerStatusNative status))
        {
            return new SystemPowerStatus(false, null, "无法读取", null);
        }

        bool hasBattery = (status.BatteryFlag & 128) == 0;
        bool? ac = status.AcLineStatus switch { 0 => false, 1 => true, _ => null };
        int? percent = status.BatteryLifePercent is <= 100 ? status.BatteryLifePercent : null;
        string batteryStatus = !hasBattery ? "不适用" : (status.BatteryFlag & 8) != 0 ? "正在充电" : (status.BatteryFlag & 1) != 0 ? "电量高" : (status.BatteryFlag & 2) != 0 ? "电量低" : (status.BatteryFlag & 4) != 0 ? "电量严重不足" : "未充电";
        return new SystemPowerStatus(hasBattery, percent, batteryStatus, ac);
    }

    private static DeviceType DetectDeviceType(bool virtualMachine, bool hasBattery, string manufacturer, string model)
    {
        if (virtualMachine)
        {
            return DeviceType.VirtualMachine;
        }

        string identity = $"{manufacturer} {model}";
        if (hasBattery || identity.Contains("Laptop", StringComparison.OrdinalIgnoreCase) || identity.Contains("Notebook", StringComparison.OrdinalIgnoreCase) || identity.Contains("ThinkPad", StringComparison.OrdinalIgnoreCase) || identity.Contains("Latitude", StringComparison.OrdinalIgnoreCase) || identity.Contains("EliteBook", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceType.Laptop;
        }

        return string.IsNullOrWhiteSpace(identity) || identity.Contains("无法读取", StringComparison.Ordinal) ? DeviceType.Unknown : DeviceType.Desktop;
    }

    private static bool IsVirtualMachine(string manufacturer, string model, string cpu) =>
        new[] { manufacturer, model, cpu }.Any(value =>
            value.Contains("VMware", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Virtual Machine", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("KVM", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("QEMU", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase));

    private static bool HasOemPowerManager(string manufacturer, out string hint)
    {
        string[] executables = manufacturer switch
        {
            var value when value.Contains("Lenovo", StringComparison.OrdinalIgnoreCase) => ["LenovoVantageService.exe", "ImControllerService.exe"],
            var value when value.Contains("Dell", StringComparison.OrdinalIgnoreCase) => ["DellPowerManagerSvc.exe", "DellTechHub.exe"],
            var value when value.Contains("HP", StringComparison.OrdinalIgnoreCase) => ["HPSystemEventUtilityHost.exe", "HPHotkeyMonitor.exe"],
            _ => []
        };

        foreach (string executable in executables)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executable}", writable: false);
                if (key is not null)
                {
                    hint = $"检测到 {manufacturer} 的电源/硬件管理组件，PcCare 不会自动覆盖其电源参数。";
                    return true;
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                hint = "无法确认厂商电源管理组件，已保守地不建议自动覆盖。";
                return true;
            }
        }

        hint = string.Empty;
        return false;
    }

    private static string? ReadRegistry(string path, string name)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
            return key?.GetValue(name)?.ToString();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static int? ReadRegistryDword(string path, string name) =>
        int.TryParse(ReadRegistry(path, name), out int value) ? value : null;

    private static (int? MaxMhz, int? CurrentMhz) ReadCpuFrequencies()
    {
        int count = Math.Max(Environment.ProcessorCount, 1);
        int itemSize = Marshal.SizeOf<ProcessorPowerInformation>();
        IntPtr buffer = Marshal.AllocHGlobal(itemSize * count);
        try
        {
            if (CallNtPowerInformation(PowerInformationLevel.ProcessorInformation, IntPtr.Zero, 0, buffer, (uint)(itemSize * count)) != 0)
            {
                int? nominal = ReadRegistryDword(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz");
                return (nominal, null);
            }

            int max = 0;
            int current = 0;
            for (int index = 0; index < count; index++)
            {
                var info = Marshal.PtrToStructure<ProcessorPowerInformation>(IntPtr.Add(buffer, index * itemSize));
                max = Math.Max(max, (int)info.MaxMhz);
                current = Math.Max(current, (int)info.CurrentMhz);
            }

            return (max > 0 ? max : null, current > 0 ? current : null);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or ExternalException)
        {
            return (ReadRegistryDword(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz"), null);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static (ulong Total, ulong Available) ReadMemory()
    {
        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(status) ? (status.TotalPhysical, status.AvailablePhysical) : (0, 0);
    }

    private readonly record struct DiskInformation(string Model, DiskType Type, string BusType);
    private readonly record struct SystemPowerStatus(bool HasBattery, int? BatteryPercent, string BatteryStatus, bool? AcConnected);

    private const uint FileShareRead = 1;
    private const uint FileShareWrite = 2;
    private const uint FileShareDelete = 4;
    private const uint OpenExisting = 3;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint IoctlVolumeGetVolumeDiskExtents = 0x00560000;
    private const int StorageDeviceProperty = 0;
    private const int StorageDeviceSeekPenaltyProperty = 7;

    private enum StorageBusType : byte { Unknown, Scsi, Atapi, Ata, Ieee1394, Ssa, Fibre, Usb, Raid, ISCSI, Sas, Sata, Sd, Mmc, Virtual, FileBackedVirtual, Spaces, Nvme }
    private enum LogicalProcessorRelationship { RelationProcessorCore = 0 }
    private enum PowerInformationLevel { ProcessorInformation = 11 }

    [StructLayout(LayoutKind.Sequential)]
    private struct StoragePropertyQuery
    {
        public StoragePropertyQuery(int propertyId) { PropertyId = propertyId; QueryType = 0; AdditionalParameters = 0; }
        public int PropertyId;
        public int QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceSeekPenaltyDescriptor
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.U1)] public bool IncursSeekPenalty;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DiskExtent { public uint DiskNumber; public long StartingOffset; public long ExtentLength; }

    [StructLayout(LayoutKind.Sequential)]
    private struct VolumeDiskExtents { public uint NumberOfDiskExtents; public uint Padding; public DiskExtent FirstExtent; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorRelationshipHeader { public LogicalProcessorRelationship Relationship; public int Size; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatusNative
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatusNative status);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(LogicalProcessorRelationship relationshipType, IntPtr buffer, ref int returnedLength);

    [DllImport("powrprof.dll")]
    private static extern uint CallNtPowerInformation(PowerInformationLevel informationLevel, IntPtr inputBuffer, uint inputBufferLength, IntPtr outputBuffer, uint outputBufferLength);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, IntPtr inputBuffer, uint inputBufferSize, ref VolumeDiskExtents outputBuffer, uint outputBufferSize, out uint bytesReturned, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, ref StoragePropertyQuery inputBuffer, uint inputBufferSize, byte[] outputBuffer, uint outputBufferSize, out uint bytesReturned, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, ref StoragePropertyQuery inputBuffer, uint inputBufferSize, ref DeviceSeekPenaltyDescriptor outputBuffer, uint outputBufferSize, out uint bytesReturned, IntPtr overlapped);
}
