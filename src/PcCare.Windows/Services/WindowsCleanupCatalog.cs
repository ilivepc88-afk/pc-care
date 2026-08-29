using PcCare.Core.Models;

namespace PcCare.Windows.Services;

public sealed class WindowsCleanupCatalog
{
    private readonly IReadOnlyList<CleanupRule> _rules;

    public WindowsCleanupCatalog()
    {
        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        _rules =
        [
            new CleanupRule(
                "user-temp",
                "用户临时文件",
                Path.GetTempPath(),
                "*",
                TimeSpan.FromDays(7),
                DefaultSelected: true,
                RequiresAdministrator: false,
                "仅清理当前用户临时目录中超过7天且未被占用的文件。"),
            new CleanupRule(
                "windows-temp",
                "Windows临时文件",
                Path.Combine(windowsDirectory, "Temp"),
                "*",
                TimeSpan.FromDays(7),
                DefaultSelected: true,
                RequiresAdministrator: true,
                "仅清理Windows临时目录中超过7天且未被占用的文件。"),
            new CleanupRule(
                "user-wer",
                "用户错误报告归档",
                Path.Combine(localApplicationData, "Microsoft", "Windows", "WER", "ReportArchive"),
                "*",
                TimeSpan.FromDays(7),
                DefaultSelected: true,
                RequiresAdministrator: false,
                "删除超过7天的Windows用户错误报告归档，不清除事件日志。"),
            new CleanupRule(
                "system-wer",
                "系统错误报告归档",
                Path.Combine(commonApplicationData, "Microsoft", "Windows", "WER", "ReportArchive"),
                "*",
                TimeSpan.FromDays(7),
                DefaultSelected: true,
                RequiresAdministrator: true,
                "删除超过7天的Windows系统错误报告归档，不清除事件日志。"),
            new CleanupRule(
                "thumbnail-cache",
                "缩略图缓存",
                Path.Combine(localApplicationData, "Microsoft", "Windows", "Explorer"),
                "thumbcache_*.db",
                TimeSpan.FromDays(7),
                DefaultSelected: false,
                RequiresAdministrator: false,
                "可重新生成，清理后首次打开图片目录可能暂时变慢。")
        ];
    }

    public IReadOnlyList<CleanupRule> GetAll() => _rules;

    public IReadOnlyList<CleanupRule> ResolveIds(IEnumerable<string> categoryIds)
    {
        ArgumentNullException.ThrowIfNull(categoryIds);
        var requested = new HashSet<string>(categoryIds, StringComparer.OrdinalIgnoreCase);
        return _rules.Where(rule => requested.Contains(rule.Id)).ToList();
    }
}
