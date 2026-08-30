using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Windows.Services;

internal sealed class StartupFolderScanner
{
    private const string StartupApprovedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public IReadOnlyList<StartupItem> Scan(CancellationToken cancellationToken)
    {
        var items = new List<StartupItem>();
        ScanFolder(items, Environment.SpecialFolder.Startup, StartupScope.CurrentUser, RegistryHive.CurrentUser, cancellationToken);
        ScanFolder(items, Environment.SpecialFolder.CommonStartup, StartupScope.AllUsers, RegistryHive.LocalMachine, cancellationToken);
        return items;
    }

    private static void ScanFolder(
        ICollection<StartupItem> items,
        Environment.SpecialFolder specialFolder,
        StartupScope scope,
        RegistryHive approvedHive,
        CancellationToken cancellationToken)
    {
        string folder = Environment.GetFolderPath(specialFolder);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(approvedHive, RegistryView.Registry64);
            using RegistryKey? approvedKey = baseKey.OpenSubKey(StartupApprovedPath, writable: false);
            foreach (string filePath in Directory.EnumerateFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = Path.GetFileName(filePath);
                StartupCommand command = ResolveCommand(filePath);
                byte[]? approvedValue = approvedKey?.GetValue(fileName) as byte[];
                bool enabled = StartupApprovedState.IsEnabled(approvedValue);
                bool knownState = StartupApprovedState.IsKnown(approvedValue);

                items.Add(new StartupItem
                {
                    Id = CreateId(filePath),
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    SourceType = StartupSourceType.StartupFolder,
                    SourcePath = filePath,
                    Command = command.ExecutablePath.Length == 0 ? filePath : $"{command.ExecutablePath} {command.Arguments}".TrimEnd(),
                    ExecutablePath = command.ExecutablePath,
                    Arguments = command.Arguments,
                    Enabled = enabled,
                    Scope = scope,
                    User = scope == StartupScope.CurrentUser ? Environment.UserName : string.Empty,
                    RequiresAdministrator = scope == StartupScope.AllUsers,
                    OperationIdentity = fileName,
                    Reason = knownState ? string.Empty : "任务管理器尚未记录该项状态，当前按已启用处理。"
                });
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException)
        {
            // Keep registry and task sources available if one folder is protected.
        }
    }

    private static StartupCommand ResolveCommand(string filePath)
    {
        if (!filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return StartupCommandParser.Parse(filePath);
        }

        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: false);
            if (shellType is null)
            {
                return new StartupCommand(filePath, string.Empty);
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(filePath);
            string target = shortcut.TargetPath?.ToString() ?? string.Empty;
            string arguments = shortcut.Arguments?.ToString() ?? string.Empty;
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
            return new StartupCommand(target, arguments);
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or IOException)
        {
            return new StartupCommand(filePath, string.Empty);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static string CreateId(string path)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return Convert.ToHexString(hash)[..20];
    }
}
