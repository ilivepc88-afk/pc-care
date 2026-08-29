using System.IO.Enumeration;
using PcCare.Core.Models;
using PcCare.Core.Safety;

namespace PcCare.Core.Services;

public sealed class CleanupScanner
{
    public Task<List<CleanupCategoryScanResult>> ScanAsync(
        IEnumerable<CleanupRule> rules,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return Task.Run(
            () => rules.Select(rule => ScanRule(rule, nowUtc, cancellationToken)).ToList(),
            cancellationToken);
    }

    private static CleanupCategoryScanResult ScanRule(
        CleanupRule rule,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var result = new CleanupCategoryScanResult { Rule = rule };

        if (string.IsNullOrWhiteSpace(rule.RootPath) || !Directory.Exists(rule.RootPath))
        {
            return result;
        }

        string normalizedRoot;
        try
        {
            normalizedRoot = PathSafety.NormalizeRoot(rule.RootPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            result.Errors.Add($"无法解析目录：{exception.Message}");
            return result;
        }

        var root = new DirectoryInfo(rule.RootPath);
        if (PathSafety.IsReparsePoint(root))
        {
            result.Errors.Add("清理根目录是重解析点，已拒绝扫描。");
            return result;
        }

        DateTimeOffset cutoffUtc = nowUtc.Subtract(rule.MinimumAge);
        var pendingDirectories = new Stack<DirectoryInfo>();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo currentDirectory = pendingDirectories.Pop();

            FileSystemInfo[] children;
            try
            {
                children = currentDirectory.GetFileSystemInfos();
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                result.SkippedCount++;
                result.Errors.Add($"无法读取目录：{currentDirectory.FullName}");
                continue;
            }

            foreach (FileSystemInfo child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!PathSafety.IsWithinRoot(child.FullName, normalizedRoot) || PathSafety.IsReparsePoint(child))
                {
                    result.SkippedCount++;
                    continue;
                }

                if (child is DirectoryInfo directory)
                {
                    pendingDirectories.Push(directory);
                    continue;
                }

                if (child is not FileInfo file ||
                    !FileSystemName.MatchesSimpleExpression(rule.SearchPattern, file.Name, ignoreCase: true))
                {
                    continue;
                }

                try
                {
                    if (file.LastWriteTimeUtc > cutoffUtc.UtcDateTime || !CanOpenExclusively(file.FullName))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    result.Candidates.Add(new CleanupCandidate(
                        rule.Id,
                        file.FullName,
                        file.Length,
                        new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)));
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
                {
                    result.SkippedCount++;
                }
            }
        }

        return result;
    }

    private static bool CanOpenExclusively(string filePath)
    {
        try
        {
            using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
