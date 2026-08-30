using Microsoft.Win32;
using PcCare.Core.Models;

namespace PcCare.Windows.Services;

internal sealed record BackgroundOptimizationRule(
    string Id,
    string Name,
    OptimizationCategory Category,
    string Description,
    OptimizationRecommendation Recommendation,
    string Reason,
    string Impact,
    IReadOnlyList<RegistryValueLocation> Targets,
    int OptimizedValue,
    bool IsPolicy,
    bool RequiresLogoff,
    bool RequiresExplorerRestart,
    Func<WindowsFeatureDetector, bool> IsSupported)
{
    public bool RequiresAdministrator => Targets.Any(target => target.Hive == RegistryHive.LocalMachine);

    public RegistryValueLocation PrimaryTarget => Targets[0];
}
