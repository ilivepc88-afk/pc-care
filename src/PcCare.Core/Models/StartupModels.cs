namespace PcCare.Core.Models;

public enum StartupSourceType
{
    RegistryRun,
    RegistryRunOnce,
    StartupFolder,
    ScheduledTask,
    UwpStartupTask,
    Other
}

public enum StartupScope
{
    CurrentUser,
    AllUsers,
    System
}

public enum StartupRecommendation
{
    Keep,
    Optional,
    RecommendDisable,
    Unknown
}

public enum StartupRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum DigitalSignatureStatus
{
    Unknown,
    Signed,
    Unsigned,
    Invalid
}

public enum StartupOperationAction
{
    Enable,
    Disable
}

public sealed class StartupItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required StartupSourceType SourceType { get; init; }

    public required string SourcePath { get; init; }

    public required string Command { get; init; }

    public string ExecutablePath { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public StartupScope Scope { get; init; }

    public string User { get; init; } = string.Empty;

    public StartupRiskLevel RiskLevel { get; set; } = StartupRiskLevel.Medium;

    public StartupRecommendation Recommendation { get; set; } = StartupRecommendation.Unknown;

    public bool FileExists { get; set; }

    public long FileSize { get; set; }

    public string FileVersion { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public DigitalSignatureStatus SignatureStatus { get; set; } = DigitalSignatureStatus.Unknown;

    public bool IsMicrosoft { get; set; }

    public bool IsSystemComponent { get; set; }

    public string StartupImpact { get; set; } = "未测量";

    public bool CanDisable { get; set; }

    public bool CanEnable { get; set; }

    public bool RequiresAdministrator { get; init; }

    public string Reason { get; set; } = string.Empty;

    public string OperationIdentity { get; init; } = string.Empty;
}

public sealed record StartupOperation(
    StartupOperationAction Action,
    StartupSourceType SourceType,
    StartupScope Scope,
    string SourcePath,
    string OperationIdentity);

public sealed record StartupOperationResult(
    string ItemId,
    StartupOperationAction Action,
    bool Succeeded,
    string Message);
