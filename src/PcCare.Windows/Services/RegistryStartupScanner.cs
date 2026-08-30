using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Windows.Services;

internal sealed class RegistryStartupScanner
{
    private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOncePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string Run32Path = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnce32Path = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce";

    private static readonly RegistryStartupLocation[] Locations =
    [
        new(RegistryHive.CurrentUser, RunPath, StartupSourceType.RegistryRun, StartupScope.CurrentUser),
        new(RegistryHive.CurrentUser, RunOncePath, StartupSourceType.RegistryRunOnce, StartupScope.CurrentUser),
        new(RegistryHive.LocalMachine, RunPath, StartupSourceType.RegistryRun, StartupScope.AllUsers),
        new(RegistryHive.LocalMachine, RunOncePath, StartupSourceType.RegistryRunOnce, StartupScope.AllUsers),
        new(RegistryHive.LocalMachine, Run32Path, StartupSourceType.RegistryRun, StartupScope.AllUsers),
        new(RegistryHive.LocalMachine, RunOnce32Path, StartupSourceType.RegistryRunOnce, StartupScope.AllUsers)
    ];

    public IReadOnlyList<StartupItem> Scan(CancellationToken cancellationToken)
    {
        var items = new List<StartupItem>();
        foreach (RegistryStartupLocation location in Locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanLocation(items, location);
        }

        return items;
    }

    private static void ScanLocation(ICollection<StartupItem> items, RegistryStartupLocation location)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Registry64);
            using RegistryKey? runKey = baseKey.OpenSubKey(location.RelativePath, writable: false);
            if (runKey is null)
            {
                return;
            }

            using RegistryKey? approvedKey = baseKey.OpenSubKey(GetStartupApprovedPath(location), writable: false);
            foreach (string valueName in runKey.GetValueNames())
            {
                string command = runKey.GetValue(valueName)?.ToString() ?? string.Empty;
                StartupCommand parsed = StartupCommandParser.Parse(command);
                byte[]? approvedValue = approvedKey?.GetValue(valueName) as byte[];
                bool enabled = StartupApprovedState.IsEnabled(approvedValue);
                bool knownState = StartupApprovedState.IsKnown(approvedValue);
                string sourcePath = $"{(location.Hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM")}\\{location.RelativePath}";
                bool runOnce = location.SourceType == StartupSourceType.RegistryRunOnce;

                items.Add(new StartupItem
                {
                    Id = CreateId(sourcePath, valueName),
                    Name = string.IsNullOrWhiteSpace(valueName) ? "(默认值)" : valueName,
                    SourceType = location.SourceType,
                    SourcePath = sourcePath,
                    Command = command,
                    ExecutablePath = parsed.ExecutablePath,
                    Arguments = parsed.Arguments,
                    Enabled = enabled,
                    Scope = location.Scope,
                    User = location.Scope == StartupScope.CurrentUser ? Environment.UserName : string.Empty,
                    RequiresAdministrator = location.Scope != StartupScope.CurrentUser,
                    OperationIdentity = valueName,
                    Reason = runOnce
                        ? "RunOnce 项将在下次成功登录后由 Windows 自动删除；为避免使用未验证的禁用方式，当前版本仅展示。"
                        : knownState ? string.Empty : "任务管理器尚未记录该项状态，当前按已启用处理。",
                    CanDisable = !runOnce,
                    CanEnable = !runOnce
                });
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException)
        {
            // One protected registry location must not hide the remaining locations.
        }
    }

    internal static string GetStartupApprovedPath(RegistryStartupLocation location)
    {
        string kind = location.RelativePath.Contains("WOW6432Node", StringComparison.OrdinalIgnoreCase) ? "Run32" : "Run";
        return $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\{kind}";
    }

    private static string CreateId(string sourcePath, string valueName)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{sourcePath}\0{valueName}"));
        return Convert.ToHexString(hash)[..20];
    }
}

internal sealed record RegistryStartupLocation(
    RegistryHive Hive,
    string RelativePath,
    StartupSourceType SourceType,
    StartupScope Scope);
