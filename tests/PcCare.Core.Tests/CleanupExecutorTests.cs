using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class CleanupExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_DeletesOnlyMatchingOldFilesAndCountsBytes()
    {
        using var temporary = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string oldFile = temporary.CreateFile("old.tmp", "123456", now.AddDays(-10));
        string newFile = temporary.CreateFile("new.tmp", "keep", now.AddDays(-1));
        var rule = new CleanupRule(
            "test",
            "测试分类",
            temporary.Path,
            "*.tmp",
            TimeSpan.FromDays(7),
            DefaultSelected: true,
            RequiresAdministrator: false,
            "测试规则");

        CleanupExecutionResult result = await new CleanupExecutor(new CleanupScanner()).ExecuteAsync([rule]);

        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(newFile));
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(6, result.FreedBytes);
        Assert.Equal(0, result.FailedCount);
    }
}
