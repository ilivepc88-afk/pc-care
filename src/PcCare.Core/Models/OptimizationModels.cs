namespace PcCare.Core.Models;

public enum OptimizationCategory
{
    WindowsContent,
    Widgets,
    Copilot,
    BrowserBackground,
    PrivacyAndRecommendations,
    InterfaceExperience
}

public enum OptimizationState
{
    Enabled,
    Disabled,
    Default,
    NotConfigured,
    Unsupported,
    Unknown,
    OrganizationManaged
}

public enum OptimizationRecommendation
{
    Recommended,
    Optional,
    KeepDefault,
    Unsupported
}

public enum OptimizationRiskLevel
{
    Low,
    Medium,
    High
}

public enum OptimizationAction
{
    Apply,
    RestoreDefault
}

public sealed class OptimizationItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required OptimizationCategory Category { get; init; }

    public required string Description { get; init; }

    public OptimizationState CurrentState { get; set; } = OptimizationState.Unknown;

    public OptimizationState RecommendedState { get; init; } = OptimizationState.Disabled;

    public OptimizationState DefaultState { get; init; } = OptimizationState.NotConfigured;

    public bool Supported { get; set; }

    public OptimizationRiskLevel RiskLevel { get; init; } = OptimizationRiskLevel.Low;

    public OptimizationRecommendation Recommendation { get; init; } = OptimizationRecommendation.Recommended;

    public bool RequiresAdministrator { get; init; }

    public bool RequiresRestart { get; init; }

    public bool RequiresLogoff { get; init; }

    public bool RequiresExplorerRestart { get; init; }

    public string RegistryPath { get; init; } = string.Empty;

    public string RegistryName { get; init; } = string.Empty;

    public string PolicyPath { get; init; } = string.Empty;

    public bool CanOptimize { get; set; }

    public bool CanRestore { get; set; }

    public bool IsOrganizationManaged { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Impact { get; init; } = string.Empty;

    public int? WindowsMinBuild { get; init; }

    public int? WindowsMaxBuild { get; init; }
}

public sealed record OptimizationOperation(string ItemId, OptimizationAction Action);

public sealed record OptimizationOperationResult(
    string ItemId,
    OptimizationAction Action,
    bool Succeeded,
    string Message);
