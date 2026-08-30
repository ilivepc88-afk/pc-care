using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Microsoft.Win32;
using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Windows.Services;

public sealed class SystemInfoService
{
    public Task<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(Capture, cancellationToken);
    }

    private static SystemSnapshot Capture()
    {
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string systemRoot = Path.GetPathRoot(systemDirectory) ?? "C:\\";
        var systemDrive = new DriveInfo(systemRoot);
        MemoryStatus memoryStatus = ReadMemoryStatus();
        string? currentBuildNumber = ReadWindowsValue("CurrentBuildNumber");

        return new SystemSnapshot
        {
            ComputerName = Environment.MachineName,
            WindowsEdition = WindowsProductNameResolver.Resolve(
                ReadWindowsValue("ProductName"),
                currentBuildNumber),
            WindowsVersion = ReadWindowsValue("DisplayVersion") ?? ReadWindowsValue("ReleaseId") ?? "无法读取",
            WindowsBuild = BuildWindowsBuild(currentBuildNumber),
            CpuName = ReadCpuName(),
            TotalMemoryBytes = memoryStatus.Total,
            AvailableMemoryBytes = memoryStatus.Available,
            SystemDriveTotalBytes = systemDrive.IsReady ? systemDrive.TotalSize : 0,
            SystemDriveFreeBytes = systemDrive.IsReady ? systemDrive.AvailableFreeSpace : 0,
            DiskMediaType = ReadDiskMediaType(systemRoot),
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
            IsAdministrator = IsAdministrator(),
            RebootPending = IsRebootPending(),
            CapturedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static string? ReadWindowsValue(string name)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                writable: false);
            return key?.GetValue(name)?.ToString();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string BuildWindowsBuild(string? build)
    {
        string? revision = ReadWindowsValue("UBR");
        return string.IsNullOrWhiteSpace(build)
            ? "无法读取"
            : string.IsNullOrWhiteSpace(revision) ? build : $"{build}.{revision}";
    }

    private static string ReadCpuName()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                writable: false);
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "无法读取";
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return "无法读取";
        }
    }

    private static string ReadDiskMediaType(string systemRoot)
    {
        string volumeName = @"\\.\" + systemRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        using SafeFileHandle volume = CreateFile(
            volumeName,
            desiredAccess: 0,
            shareMode: FileShareRead | FileShareWrite | FileShareDelete,
            securityAttributes: IntPtr.Zero,
            creationDisposition: OpenExisting,
            flagsAndAttributes: 0,
            templateFile: IntPtr.Zero);
        if (volume.IsInvalid)
        {
            return "未识别";
        }

        var query = new StoragePropertyQuery
        {
            PropertyId = StorageDeviceSeekPenaltyProperty,
            QueryType = PropertyStandardQuery,
            AdditionalParameters = 0
        };
        bool succeeded = DeviceIoControl(
            volume,
            IoctlStorageQueryProperty,
            ref query,
            (uint)Marshal.SizeOf<StoragePropertyQuery>(),
            out DeviceSeekPenaltyDescriptor descriptor,
            (uint)Marshal.SizeOf<DeviceSeekPenaltyDescriptor>(),
            out uint bytesReturned,
            IntPtr.Zero);

        if (!succeeded || bytesReturned < 9)
        {
            return "未识别";
        }

        return descriptor.IncursSeekPenalty ? "HDD" : "SSD";
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool IsRebootPending()
    {
        try
        {
            return RegistryKeyExists(
                       @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") ||
                   RegistryKeyExists(
                       @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") ||
                   HasPendingFileRenameOperations();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static bool RegistryKeyExists(string path)
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
        return key is not null;
    }

    private static bool HasPendingFileRenameOperations()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Session Manager",
            writable: false);
        return key?.GetValue("PendingFileRenameOperations") is not null;
    }

    private static MemoryStatus ReadMemoryStatus()
    {
        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(status)
            ? new MemoryStatus(status.TotalPhysical, status.AvailablePhysical)
            : new MemoryStatus(0, 0);
    }

    private readonly record struct MemoryStatus(ulong Total, ulong Available);

    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageDeviceSeekPenaltyProperty = 7;
    private const int PropertyStandardQuery = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct StoragePropertyQuery
    {
        public int PropertyId;
        public int QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceSeekPenaltyDescriptor
    {
        public uint Version;
        public uint Size;

        [MarshalAs(UnmanagedType.U1)]
        public bool IncursSeekPenalty;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad = 0;
        public ulong TotalPhysical = 0;
        public ulong AvailablePhysical = 0;
        public ulong TotalPageFile = 0;
        public ulong AvailablePageFile = 0;
        public ulong TotalVirtual = 0;
        public ulong AvailableVirtual = 0;
        public ulong AvailableExtendedVirtual = 0;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        ref StoragePropertyQuery inputBuffer,
        uint inputBufferSize,
        out DeviceSeekPenaltyDescriptor outputBuffer,
        uint outputBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);
}
