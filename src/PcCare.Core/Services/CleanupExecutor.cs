using PcCare.Core.Models;
using PcCare.Core.Safety;

namespace PcCare.Core.Services;

public sealed class CleanupExecutor(CleanupScanner scanner)
{
    private readonly CleanupScanner _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));

    public async Task<CleanupExecutionResult> ExecuteAsync(
        IReadOnlyCollection<CleanupRule> selectedRules,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedRules);

        var result = new CleanupExecutionResult { StartedAtUtc = DateTimeOffset.UtcNow };
        List<CleanupCategoryScanResult> rescanned = await _scanner.ScanAsync(
            selectedRules,
            result.StartedAtUtc,
            cancellationToken).ConfigureAwait(false);

        int totalFiles = rescanned.Sum(category => category.Candidates.Count);
        int processedFiles = 0;

        foreach (CleanupCategoryScanResult category in rescanned)
        {
            foreach (CleanupCandidate candidate in category.Candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processedFiles++;
                progress?.Report(new CleanupProgress(
                    category.Rule.Id,
                    processedFiles,
                    totalFiles,
                    candidate.FullPath));

                if (!PathSafety.IsWithinRoot(candidate.FullPath, category.Rule.RootPath))
                {
                    result.SkippedCount++;
                    result.Errors.Add($"安全校验拒绝路径：{candidate.FullPath}");
                    continue;
                }

                try
                {
                    var file = new FileInfo(candidate.FullPath);
                    if (!file.Exists || PathSafety.IsReparsePoint(file))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    long size = file.Length;
                    file.Delete();
                    result.DeletedCount++;
                    result.FreedBytes += size;
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
                {
                    result.FailedCount++;
                    result.Errors.Add($"删除失败：{candidate.FullPath}；{exception.Message}");
                }
            }
        }

        result.FinishedAtUtc = DateTimeOffset.UtcNow;
        return result;
    }
}
