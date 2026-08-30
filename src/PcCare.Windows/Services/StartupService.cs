using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Windows.Services;

public sealed class StartupService
{
    private readonly RegistryStartupScanner _registryScanner = new();
    private readonly StartupFolderScanner _startupFolderScanner = new();
    private readonly ScheduledTaskStartupScanner _scheduledTaskScanner = new();
    private readonly StartupItemMetadataReader _metadataReader = new();
    private readonly StartupAnalyzer _analyzer = new();

    public Task<List<StartupItem>> ScanAsync(
        bool includeMicrosoftSystemTasks = false,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Scan(includeMicrosoftSystemTasks, cancellationToken),
            cancellationToken);
    }

    private List<StartupItem> Scan(bool includeMicrosoftSystemTasks, CancellationToken cancellationToken)
    {
        var items = new List<StartupItem>();
        items.AddRange(_registryScanner.Scan(cancellationToken));
        items.AddRange(_startupFolderScanner.Scan(cancellationToken));
        items.AddRange(_scheduledTaskScanner.Scan(includeMicrosoftSystemTasks, cancellationToken));

        foreach (StartupItem item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _metadataReader.Populate(item);
            _analyzer.Analyze(item);
        }

        return items
            .OrderBy(item => item.IsSystemComponent)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
