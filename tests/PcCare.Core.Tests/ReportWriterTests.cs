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
            StartupEntries = [new StartupEntry("A&B", "app.exe", "HKCU", "当前用户")]
        };

        (string htmlPath, string jsonPath) = await new ReportWriter().WriteAsync(report, temporary.Path);

        Assert.True(File.Exists(htmlPath));
        Assert.True(File.Exists(jsonPath));
        string html = await File.ReadAllTextAsync(htmlPath);
        Assert.Contains("Windows &lt;测试&gt;", html, StringComparison.Ordinal);
        Assert.Contains("A&amp;B", html, StringComparison.Ordinal);
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
