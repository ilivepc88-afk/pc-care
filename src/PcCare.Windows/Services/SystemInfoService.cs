using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
using PcCare.Core.Models;

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

        return new SystemSnapshot
        {
            ComputerName = Environment.MachineName,
            WindowsEdition = ReadWindowsValue("ProductName") ?? "无法读取",
            WindowsVersion = ReadWindowsValue("DisplayVersion") ?? ReadWindowsValue("ReleaseId") ?? "无法读取",
            WindowsBuild = BuildWindowsBuild(),
            CpuName = ReadCpuName(),
            TotalMemoryBytes = memoryStatus.Total,
            AvailableMemoryBytes = memoryStatus.Available,
            SystemDriveTotalBytes = systemDrive.IsReady ? systemDrive.TotalSize : 0,
            SystemDriveFreeBytes = systemDrive.IsReady ? systemDrive.AvailableFreeSpace : 0,
            DiskMediaType = ReadDiskMediaType(),
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

    private static string BuildWindowsBuild()
    {
        string? build = ReadWindowsValue("CurrentBuildNumber");
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

    private static string ReadDiskMediaType()
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT MediaType FROM MSFT_PhysicalDisk"));
            var mediaTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ManagementObject disk in searcher.Get().Cast<ManagementObject>())
            {
                uint mediaType = Convert.ToUInt32(disk["MediaType"] ?? 0, System.Globalization.CultureInfo.InvariantCulture);
                mediaTypes.Add(mediaType switch
                {
                    3 => "HDD",
                    4 => "SSD",
                    5 => "SCM",
                    _ => "未识别"
                });
                disk.Dispose();
            }

            return mediaTypes.Count == 0 ? "未识别" : string.Join(" / ", mediaTypes.Order());
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException or COMException)
        {
            return "未识别";
        }
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
}
