using System.Security.Principal;
using PcCare.Core.Models;
using PcCare.Core.Services;
using PcCare.Windows.Services;

namespace PcCare.App.Services;

public static class ElevatedCleanupRunner
{
    public static async Task<int> RunAsync(
        CommandLineOptions options,
        WindowsCleanupCatalog catalog,
        CleanupExecutor executor,
        CleanupJobStore jobStore)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(jobStore);

        if (!IsAdministrator() || options.JobId == Guid.Empty || options.CategoryIds.Count == 0)
        {
            return 2;
        }

        IReadOnlyList<CleanupRule> rules = catalog.ResolveIds(options.CategoryIds);
        if (rules.Count == 0 || rules.Count != options.CategoryIds.Count)
        {
            return 3;
        }

        try
        {
            CleanupExecutionResult result = await executor.ExecuteAsync(rules).ConfigureAwait(false);
            await jobStore.WriteResultAsync(options.JobId, result).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            return 4;
        }
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
