using System.ComponentModel;
using System.Diagnostics;
using PcCare.Core.Models;
using PcCare.Windows.Services;

namespace PcCare.App.Services;

public sealed class ElevatedBackgroundOptimizationRunner
{
    private readonly BackgroundOptimizationManager _manager;

    public ElevatedBackgroundOptimizationRunner(BackgroundOptimizationManager manager)
    {
        _manager = manager;
    }

    public async Task<OptimizationOperationResult> ApplyAsync(OptimizationItem item, CancellationToken cancellationToken)
    {
        OptimizationAction action = item.CurrentState == OptimizationState.Disabled
            ? OptimizationAction.RestoreDefault
            : OptimizationAction.Apply;
        var operation = new OptimizationOperation(item.Id, action);
        if (!item.RequiresAdministrator)
        {
            return await _manager.ApplyAsync(operation, cancellationToken);
        }

        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return new OptimizationOperationResult(item.Id, action, false, "无法定位当前程序，未请求管理员权限。");
        }

        try
        {
            using Process? child = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = StartupOperationCommandLine.CreateBackgroundOptimizationArguments(operation),
                UseShellExecute = true,
                Verb = "runas"
            });
            if (child is null)
            {
                return new OptimizationOperationResult(item.Id, action, false, "未能启动管理员操作。");
            }

            await child.WaitForExitAsync(cancellationToken);
            return child.ExitCode == 0
                ? new OptimizationOperationResult(item.Id, action, true, "已通过管理员权限完成操作。")
                : new OptimizationOperationResult(item.Id, action, false, "管理员操作未完成或被取消。");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new OptimizationOperationResult(item.Id, action, false, "用户取消了管理员权限请求。");
        }
    }
}
