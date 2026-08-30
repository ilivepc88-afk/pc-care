using System.Net;
using System.Text;
using System.Text.Json;
using PcCare.Core.Models;

namespace PcCare.Core.Services;

public sealed class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<(string HtmlPath, string JsonPath)> WriteAsync(
        ScanReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        string safeComputerName = SanitizeFileName(report.System.ComputerName);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string baseName = $"{safeComputerName}_{timestamp}";
        string htmlPath = Path.Combine(outputDirectory, baseName + ".html");
        string jsonPath = Path.Combine(outputDirectory, baseName + ".json");

        await File.WriteAllTextAsync(htmlPath, BuildHtml(report), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(report, JsonOptions),
            Encoding.UTF8,
            cancellationToken).ConfigureAwait(false);

        return (htmlPath, jsonPath);
    }

    private static string BuildHtml(ScanReport report)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>PcCare 系统体检报告</title><style>");
        html.AppendLine("body{font-family:'Microsoft YaHei',sans-serif;margin:32px;color:#1f2937}h1,h2{color:#0f4c81}.meta{color:#64748b}table{width:100%;border-collapse:collapse;margin:12px 0 24px}th,td{border:1px solid #dbe3ea;padding:8px;text-align:left}th{background:#eef5fb}.num{text-align:right}.ok{color:#18794e}.warn{color:#a15c00}</style></head><body>");
        html.AppendLine("<h1>PcCare 系统体检报告</h1>");
        html.AppendLine($"<p class=\"meta\">生成时间：{E(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))}　应用版本：{E(report.ApplicationVersion)}</p>");
        html.AppendLine("<h2>系统概览</h2><table>");
        AddRow(html, "计算机名", report.System.ComputerName);
        AddRow(html, "Windows", $"{report.System.WindowsEdition} {report.System.WindowsVersion} ({report.System.WindowsBuild})");
        AddRow(html, "CPU", report.System.CpuName);
        AddRow(html, "内存", $"{FormatBytes((long)report.System.AvailableMemoryBytes)} 可用 / {FormatBytes((long)report.System.TotalMemoryBytes)}");
        AddRow(html, "系统盘", $"{FormatBytes(report.System.SystemDriveFreeBytes)} 可用 / {FormatBytes(report.System.SystemDriveTotalBytes)}");
        AddRow(html, "磁盘介质", report.System.DiskMediaType);
        AddRow(html, "连续运行", $"{report.System.Uptime.Days}天 {report.System.Uptime.Hours}小时");
        AddRow(html, "待重启", report.System.RebootPending ? "是" : "否");
        html.AppendLine("</table>");

        html.AppendLine("<h2>启动项</h2><table><thead><tr><th>名称</th><th>命令</th><th>来源</th><th>范围</th><th>状态</th><th>建议</th><th>风险</th><th>原因</th></tr></thead><tbody>");
        foreach (StartupItem item in report.StartupItems)
        {
            html.AppendLine($"<tr><td>{E(item.Name)}</td><td>{E(item.Command)}</td><td>{E(item.SourcePath)}</td><td>{E(item.Scope.ToString())}</td><td>{(item.Enabled ? "已启用" : "已禁用")}</td><td>{E(item.Recommendation.ToString())}</td><td>{E(item.RiskLevel.ToString())}</td><td>{E(item.Reason)}</td></tr>");
        }

        html.AppendLine("</tbody></table><h2>启动项操作记录</h2><table><thead><tr><th>时间（UTC）</th><th>项目</th><th>来源</th><th>操作</th><th>旧状态</th><th>新状态</th><th>结果</th><th>说明</th></tr></thead><tbody>");
        foreach (StartupOperationLogEntry entry in report.StartupOperationLog)
        {
            html.AppendLine($"<tr><td>{E(entry.TimeUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture))}</td><td>{E(entry.ItemName)}</td><td>{E(entry.SourceType.ToString())}</td><td>{E(entry.Action.ToString())}</td><td>{(entry.PreviousEnabled ? "已启用" : "已禁用")}</td><td>{(entry.CurrentEnabled ? "已启用" : "已禁用")}</td><td>{(entry.Succeeded ? "成功" : "失败")}</td><td>{E(entry.Message)}</td></tr>");
        }

        html.AppendLine("</tbody></table><h2>后台优化</h2><table><thead><tr><th>名称</th><th>分类</th><th>当前状态</th><th>建议</th><th>风险</th><th>注册表位置</th><th>值名称</th><th>原因</th><th>影响说明</th></tr></thead><tbody>");
        foreach (OptimizationItem item in report.BackgroundOptimizationItems)
        {
            html.AppendLine($"<tr><td>{E(item.Name)}</td><td>{E(item.Category.ToString())}</td><td>{E(item.CurrentState.ToString())}</td><td>{E(item.Recommendation.ToString())}</td><td>{E(item.RiskLevel.ToString())}</td><td>{E(item.RegistryPath)}</td><td>{E(item.RegistryName)}</td><td>{E(item.Reason)}</td><td>{E(item.Impact)}</td></tr>");
        }

        html.AppendLine("</tbody></table><h2>后台优化操作记录</h2><table><thead><tr><th>时间（UTC）</th><th>ID</th><th>项目</th><th>旧状态</th><th>新状态</th><th>注册表位置</th><th>值名称</th><th>操作</th><th>结果</th><th>说明</th></tr></thead><tbody>");
        foreach (BackgroundOptimizationLogEntry entry in report.BackgroundOptimizationLog)
        {
            html.AppendLine($"<tr><td>{E(entry.TimeUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture))}</td><td>{E(entry.ItemId)}</td><td>{E(entry.ItemName)}</td><td>{E(entry.PreviousState.ToString())}</td><td>{E(entry.CurrentState.ToString())}</td><td>{E(entry.RegistryPath)}</td><td>{E(entry.RegistryName)}</td><td>{E(entry.Action.ToString())}</td><td>{(entry.Succeeded ? "成功" : "失败")}</td><td>{E(entry.Message)}</td></tr>");
        }

        html.AppendLine("</tbody></table><p class=\"meta\">本报告完全离线生成，不包含远程资源。</p></body></html>");
        return html.ToString();
    }

    private static void AddRow(StringBuilder html, string label, string value)
    {
        html.AppendLine($"<tr><th>{WebUtility.HtmlEncode(label)}</th><td>{WebUtility.HtmlEncode(value)}</td></tr>");
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        string sanitized = new(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown-PC" : sanitized;
    }
}
