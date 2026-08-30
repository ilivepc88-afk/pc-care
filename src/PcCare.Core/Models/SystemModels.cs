namespace PcCare.Core.Models;

public sealed class SystemSnapshot
{
    public string ComputerName { get; init; } = Environment.MachineName;

    public string WindowsEdition { get; init; } = "无法读取";

    public string WindowsVersion { get; init; } = "无法读取";

    public string WindowsBuild { get; init; } = "无法读取";

    public string CpuName { get; init; } = "无法读取";

    public ulong TotalMemoryBytes { get; init; }

    public ulong AvailableMemoryBytes { get; init; }

    public long SystemDriveTotalBytes { get; init; }

    public long SystemDriveFreeBytes { get; init; }

    public string DiskMediaType { get; init; } = "未识别";

    public TimeSpan Uptime { get; init; }

    public bool IsAdministrator { get; init; }

    public bool RebootPending { get; init; }

    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
