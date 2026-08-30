using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class ReportWriterTests
{
    [Fact]
    public async Task WriteAsync_CreatesOfflineHtmlAndJsonWithEscapedValues()
    {
        using var temporary = new TemporaryDirectory();
        var report = new ScanReport
        {
            System = new SystemSnapshot
            {
                ComputerName = "PC-01",
                WindowsEdition = "Windows <测试>",
                WindowsVersion = "11",
                WindowsBuild = "26100",
                CpuName = "Test CPU"
            },
            StartupItems =
            [
                new StartupItem
                {
                    Id = "startup-1",
                    Name = "A&B",
                    SourceType = StartupSourceType.RegistryRun,
                    SourcePath = "HKCU\\Software\\Run",
                    Command = "app.exe",
                    Scope = StartupScope.CurrentUser,
                    Enabled = true,
                    Recommendation = StartupRecommendation.Unknown,
                    RiskLevel = StartupRiskLevel.Medium,
                    Reason = "无法可靠判断用途。"
                }
            ],
            BackgroundOptimizationItems =
            [
                new OptimizationItem
                {
                    Id = "edge.background-mode",
                    Name = "Edge <后台>",
                    Category = OptimizationCategory.BrowserBackground,
                    Description = "测试",
                    CurrentState = OptimizationState.Disabled,
                    RegistryPath = "HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge",
                    RegistryName = "BackgroundModeEnabled",
                    Reason = "<安全编码>",
                    Impact = "不会卸载 Edge"
                }
            ]
        };

        (string htmlPath, string jsonPath) = await new ReportWriter().WriteAsync(report, temporary.Path);

        Assert.True(File.Exists(htmlPath));
        Assert.True(File.Exists(jsonPath));
        string html = await File.ReadAllTextAsync(htmlPath);
        Assert.Contains("Windows &lt;测试&gt;", html, StringComparison.Ordinal);
        Assert.Contains("A&amp;B", html, StringComparison.Ordinal);
        Assert.Contains("Edge &lt;后台&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&lt;安全编码&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"computerName\": \"PC-01\"", await File.ReadAllTextAsync(jsonPath), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1073741824, "1 GB")]
    public void FormatBytes_ReturnsExpectedText(long bytes, string expected)
    {
        Assert.Equal(expected, ReportWriter.FormatBytes(bytes));
    }
}
