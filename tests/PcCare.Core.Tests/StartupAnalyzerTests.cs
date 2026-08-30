using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class StartupAnalyzerTests
{
    private readonly StartupAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_SecurityComponent_IsProtectedAndCannotBeDisabled()
    {
        StartupItem item = CreateItem("Windows Security Health", StartupSourceType.RegistryRun, "C:\\Windows\\System32\\SecurityHealthSystray.exe");

        _analyzer.Analyze(item);

        Assert.Equal(StartupRecommendation.Keep, item.Recommendation);
        Assert.Equal(StartupRiskLevel.Critical, item.RiskLevel);
        Assert.True(item.IsSystemComponent);
        Assert.False(item.CanDisable);
    }

    [Fact]
    public void Analyze_Updater_IsLowRiskRecommendation()
    {
        StartupItem item = CreateItem("Contoso Updater", StartupSourceType.RegistryRun, "C:\\Apps\\ContosoUpdater.exe");

        _analyzer.Analyze(item);

        Assert.Equal(StartupRecommendation.RecommendDisable, item.Recommendation);
        Assert.Equal(StartupRiskLevel.Low, item.RiskLevel);
        Assert.True(item.CanDisable);
    }

    [Fact]
    public void Analyze_UnknownItem_IsNeverRecommendedForOneClickOptimization()
    {
        StartupItem item = CreateItem("Unfamiliar Agent", StartupSourceType.RegistryRun, "C:\\Apps\\unfamiliar.exe");

        _analyzer.Analyze(item);

        Assert.Equal(StartupRecommendation.Unknown, item.Recommendation);
        Assert.NotEqual(StartupRiskLevel.Low, item.RiskLevel);
    }

    [Fact]
    public void Analyze_RunOnce_IsReadOnlyEvenWhenRuleMatches()
    {
        StartupItem item = CreateItem("Contoso Updater", StartupSourceType.RegistryRunOnce, "C:\\Apps\\ContosoUpdater.exe");

        _analyzer.Analyze(item);

        Assert.False(item.CanDisable);
        Assert.False(item.CanEnable);
    }

    [Fact]
    public void Analyze_MicrosoftSystemTask_IsAlwaysImmutable()
    {
        StartupItem item = CreateItem("System Task", StartupSourceType.ScheduledTask, "C:\\Windows\\System32\\task.exe", @"\Microsoft\Windows\Example\Task");

        _analyzer.Analyze(item);

        Assert.Equal(StartupRecommendation.Keep, item.Recommendation);
        Assert.True(item.IsSystemComponent);
        Assert.False(item.CanDisable);
    }

    private static StartupItem CreateItem(string name, StartupSourceType sourceType, string executablePath, string? sourcePath = null) => new()
    {
        Id = name,
        Name = name,
        SourceType = sourceType,
        SourcePath = sourcePath ?? (sourceType == StartupSourceType.ScheduledTask ? @"\Contoso\Task" : @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
        Command = executablePath,
        ExecutablePath = executablePath,
        Enabled = true,
        Scope = StartupScope.CurrentUser
    };
}
