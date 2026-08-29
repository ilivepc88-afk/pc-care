namespace PcCare.Core.Models;

public sealed record CleanupRule(
    string Id,
    string DisplayName,
    string RootPath,
    string SearchPattern,
    TimeSpan MinimumAge,
    bool DefaultSelected,
    bool RequiresAdministrator,
    string RiskDescription);

public sealed record CleanupCandidate(
    string CategoryId,
    string FullPath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);

public sealed class CleanupCategoryScanResult
{
    public required CleanupRule Rule { get; init; }

    public List<CleanupCandidate> Candidates { get; init; } = [];

    public List<string> Errors { get; init; } = [];

    public int SkippedCount { get; set; }

    public long TotalBytes => Candidates.Sum(candidate => candidate.SizeBytes);
}

public sealed record CleanupProgress(
    string CategoryId,
    int ProcessedFiles,
    int TotalFiles,
    string CurrentPath);

public sealed class CleanupExecutionResult
{
    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; set; }

    public int DeletedCount { get; set; }

    public int SkippedCount { get; set; }

    public int FailedCount { get; set; }

    public long FreedBytes { get; set; }

    public List<string> Errors { get; init; } = [];
}
