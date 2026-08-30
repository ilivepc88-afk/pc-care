using System.ComponentModel;
using System.Diagnostics;
using PcCare.Core.Models;
using PcCare.Windows.Services;

namespace PcCare.App.Services;

public sealed class ElevatedStartupOperationRunner
{
    private readonly StartupManager _startupManager;

    public ElevatedStartupOperationRunner(StartupManager startupManager)
    {
        _startupManager = startupManager;
    }

    public async Task<StartupOperationResult> ApplyAsync(StartupItem item, CancellationToken cancellationToken)
    {
        var operation = new StartupOperation(
            item.Enabled ? StartupOperationAction.Disable : StartupOperationAction.Enable,
            item.SourceType,
            item.Scope,
            item.SourcePath,
            item.OperationIdentity);

        if (!item.RequiresAdministrator)
        {
            return await _startupManager.ApplyAsync(operation, cancellationToken);
        }

        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return new StartupOperationResult(item.Id, operation.Action, false, "无法定位当前程序，未请求管理员权限。");
        }

        try
        {
            using Process? child = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = StartupOperationCommandLine.CreateArguments(operation),
                UseShellExecute = true,
                Verb = "runas"
            });
            if (child is null)
            {
                return new StartupOperationResult(item.Id, operation.Action, false, "未能启动管理员操作。 ");
            }

            await child.WaitForExitAsync(cancellationToken);
            return child.ExitCode == 0
                ? new StartupOperationResult(item.Id, operation.Action, true, "已通过管理员权限完成操作。")
                : new StartupOperationResult(item.Id, operation.Action, false, "管理员操作未完成或被取消。 ");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new StartupOperationResult(item.Id, operation.Action, false, "用户取消了管理员权限请求。 ");
        }
    }
}
