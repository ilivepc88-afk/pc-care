using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using PcCare.Core.Models;
using PcCare.Core.Services;
using PcCare.Windows.Services;

namespace PcCare.App.Services;

public sealed class CleanupCoordinator(
    WindowsCleanupCatalog catalog,
    CleanupExecutor executor,
    CleanupJobStore jobStore)
{
    private readonly WindowsCleanupCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly CleanupExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    private readonly CleanupJobStore _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));

    public async Task<CleanupExecutionResult> ExecuteAsync(
        IReadOnlyCollection<string> categoryIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryIds);
        IReadOnlyList<CleanupRule> rules = _catalog.ResolveIds(categoryIds);
        if (rules.Count == 0 || rules.Count != categoryIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new InvalidOperationException("清理分类无效或不在白名单中。");
        }

        if (IsAdministrator())
        {
            return await _executor.ExecuteAsync(rules, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await ExecuteElevatedAsync(categoryIds, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CleanupExecutionResult> ExecuteElevatedAsync(
        IEnumerable<string> categoryIds,
        CancellationToken cancellationToken)
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("无法确定当前程序路径，不能申请管理员权限。");
        }

        Guid jobId = Guid.NewGuid();
        string resultPath = _jobStore.GetResultPath(jobId);
        if (File.Exists(resultPath))
        {
            File.Delete(resultPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add("--elevated-clean");
        startInfo.ArgumentList.Add("--job");
        startInfo.ArgumentList.Add(jobId.ToString("D"));
        startInfo.ArgumentList.Add("--categories");
        startInfo.ArgumentList.Add(string.Join(',', categoryIds));

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动管理员清理进程。");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("用户取消了管理员权限申请。", exception, cancellationToken);
        }

        using (process)
        {
            // Once an elevated cleanup starts, wait for its audited result instead of
            // abandoning a still-running administrator process when the UI cancel token fires.
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"管理员清理进程执行失败，退出码：{process.ExitCode}。");
            }
        }

        return await _jobStore.ReadAndDeleteResultAsync(jobId, CancellationToken.None).ConfigureAwait(false);
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
