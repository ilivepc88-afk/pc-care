using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class CleanupScannerTests
{
    [Fact]
    public async Task ScanAsync_IncludesOnlyFilesOlderThanMinimumAge()
    {
        using var temporary = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string oldFile = temporary.CreateFile("old.tmp", "old", now.AddDays(-10));
        temporary.CreateFile("new.tmp", "new", now.AddDays(-1));
        CleanupRule rule = CreateRule(temporary.Path);

        List<CleanupCategoryScanResult> results = await new CleanupScanner().ScanAsync([rule], now);

        CleanupCandidate candidate = Assert.Single(Assert.Single(results).Candidates);
        Assert.Equal(oldFile, candidate.FullPath);
    }

    [Fact]
    public async Task ScanAsync_SkipsLockedFileWithoutFailingCategory()
    {
        using var temporary = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string lockedPath = temporary.CreateFile("locked.tmp", "locked", now.AddDays(-10));
        string availablePath = temporary.CreateFile("available.tmp", "available", now.AddDays(-10));
        await using FileStream locked = new(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        List<CleanupCategoryScanResult> results = await new CleanupScanner().ScanAsync(
            [CreateRule(temporary.Path)],
            now);

        CleanupCategoryScanResult result = Assert.Single(results);
        Assert.Equal(availablePath, Assert.Single(result.Candidates).FullPath);
        Assert.True(result.SkippedCount >= 1);
    }

    [Fact]
    public async Task ScanAsync_DoesNotFollowDirectorySymbolicLink()
    {
        using var root = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        outside.CreateFile("outside.tmp", "outside", now.AddDays(-10));
        string linkPath = System.IO.Path.Combine(root.Path, "linked");

        try
        {
            Directory.CreateSymbolicLink(linkPath, outside.Path);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return;
        }

        List<CleanupCategoryScanResult> results = await new CleanupScanner().ScanAsync(
            [CreateRule(root.Path)],
            now);

        Assert.Empty(Assert.Single(results).Candidates);
    }

    [Fact]
    public async Task ScanAsync_HonorsPreCanceledToken()
    {
        using var temporary = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CleanupScanner().ScanAsync(
                [CreateRule(temporary.Path)],
                DateTimeOffset.UtcNow,
                cancellation.Token));
    }

    private static CleanupRule CreateRule(string rootPath)
    {
        return new CleanupRule(
            "test",
            "测试分类",
            rootPath,
            "*.tmp",
            TimeSpan.FromDays(7),
            DefaultSelected: true,
            RequiresAdministrator: false,
            "测试规则");
    }
}
