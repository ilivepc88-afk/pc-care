using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using PcCare.Core.Models;

namespace PcCare.Windows.Services;

public sealed class StartupManager
{
    private const string StartupApprovedRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    public Task<StartupOperationResult> ApplyAsync(StartupOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Task.Run(() => Apply(operation, cancellationToken), cancellationToken);
    }

    private static StartupOperationResult Apply(StartupOperation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return operation.SourceType switch
            {
                StartupSourceType.RegistryRun => SetRegistryStartupApproved(operation),
                StartupSourceType.StartupFolder => SetFolderStartupApproved(operation),
                StartupSourceType.ScheduledTask => SetScheduledTaskEnabled(operation),
                _ => Failure(operation, "该启动项来源不支持修改。")
            };
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException or COMException)
        {
            return Failure(operation, $"操作未完成：{exception.Message}");
        }
    }

    private static StartupOperationResult SetRegistryStartupApproved(StartupOperation operation)
    {
        if (!TryGetRegistryLocation(operation, out RegistryHive hive, out string approvedLeaf) || !IsSafeValueName(operation.OperationIdentity))
        {
            return Failure(operation, "启动项注册表位置不在允许范围内。 ");
        }

        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using RegistryKey approvedKey = baseKey.CreateSubKey($"{StartupApprovedRoot}\\{approvedLeaf}", writable: true)
            ?? throw new IOException("无法创建启动项状态位置。");
        WriteApprovalState(approvedKey, operation.OperationIdentity, operation.Action);
        return Success(operation);
    }

    private static StartupOperationResult SetFolderStartupApproved(StartupOperation operation)
    {
        if (!TryGetStartupFolderLocation(operation, out RegistryHive hive) || !IsSafeFileName(operation.OperationIdentity))
        {
            return Failure(operation, "启动文件夹位置不在允许范围内。 ");
        }

        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using RegistryKey approvedKey = baseKey.CreateSubKey($"{StartupApprovedRoot}\\StartupFolder", writable: true)
            ?? throw new IOException("无法创建启动项状态位置。");
        WriteApprovalState(approvedKey, operation.OperationIdentity, operation.Action);
        return Success(operation);
    }

    private static StartupOperationResult SetScheduledTaskEnabled(StartupOperation operation)
    {
        string taskPath = operation.OperationIdentity;
        if (operation.Scope != StartupScope.System ||
            string.IsNullOrWhiteSpace(taskPath) ||
            !taskPath.StartsWith('\\') ||
            taskPath.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(operation, "计划任务不在允许修改的范围内。 ");
        }

        Type? schedulerType = Type.GetTypeFromProgID("Schedule.Service", throwOnError: false);
        if (schedulerType is null)
        {
            return Failure(operation, "无法连接 Windows 计划任务服务。 ");
        }

        object? scheduler = null;
        object? folder = null;
        object? task = null;
        try
        {
            dynamic dynamicScheduler = Activator.CreateInstance(schedulerType)!;
            scheduler = dynamicScheduler;
            dynamicScheduler.Connect();

            int split = taskPath.LastIndexOf('\\');
            if (split <= 0 || split >= taskPath.Length - 1)
            {
                return Failure(operation, "计划任务路径无效。 ");
            }

            string folderPath = taskPath[..split];
            string taskName = taskPath[(split + 1)..];
            dynamic dynamicFolder = dynamicScheduler.GetFolder(folderPath);
            folder = dynamicFolder;
            dynamic dynamicTask = dynamicFolder.GetTask(taskName);
            task = dynamicTask;
            dynamicTask.Enabled = operation.Action == StartupOperationAction.Enable;
            return Success(operation);
        }
        finally
        {
            ReleaseComObject(task);
            ReleaseComObject(folder);
            ReleaseComObject(scheduler);
        }
    }

    private static bool TryGetRegistryLocation(StartupOperation operation, out RegistryHive hive, out string approvedLeaf)
    {
        hive = operation.Scope == StartupScope.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
        approvedLeaf = operation.SourcePath.Contains("WOW6432Node", StringComparison.OrdinalIgnoreCase) ? "Run32" : "Run";
        string expectedPrefix = hive == RegistryHive.CurrentUser ? "HKCU\\" : "HKLM\\";
        if (!operation.SourcePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string relativePath = operation.SourcePath[expectedPrefix.Length..];
        return relativePath.Equals(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StringComparison.OrdinalIgnoreCase) ||
               (hive == RegistryHive.LocalMachine && relativePath.Equals(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetStartupFolderLocation(StartupOperation operation, out RegistryHive hive)
    {
        hive = operation.Scope == StartupScope.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
        string expectedFolder = Environment.GetFolderPath(
            operation.Scope == StartupScope.CurrentUser ? Environment.SpecialFolder.Startup : Environment.SpecialFolder.CommonStartup);
        if (string.IsNullOrWhiteSpace(expectedFolder) || string.IsNullOrWhiteSpace(operation.SourcePath))
        {
            return false;
        }

        string fullFolder = Path.GetFullPath(expectedFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullSource = Path.GetFullPath(operation.SourcePath);
        return string.Equals(Path.GetDirectoryName(fullSource), fullFolder, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetFileName(fullSource), operation.OperationIdentity, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeValueName(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(['\\', '/', '\0']) < 0;

    private static bool IsSafeFileName(string value) =>
        IsSafeValueName(value) && string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal);

    private static void WriteApprovalState(RegistryKey approvedKey, string valueName, StartupOperationAction action)
    {
        byte[] state = approvedKey.GetValue(valueName) as byte[] ?? new byte[12];
        if (state.Length == 0)
        {
            state = new byte[12];
        }

        state[0] = action == StartupOperationAction.Enable ? (byte)0x02 : (byte)0x03;
        approvedKey.SetValue(valueName, state, RegistryValueKind.Binary);
    }

    private static StartupOperationResult Success(StartupOperation operation) => new(
        operation.OperationIdentity,
        operation.Action,
        true,
        operation.Action == StartupOperationAction.Enable ? "已启用，原始启动项未被删除。" : "已禁用，原始启动项未被删除。");

    private static StartupOperationResult Failure(StartupOperation operation, string message) => new(
        operation.OperationIdentity,
        operation.Action,
        false,
        message.Trim());

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
