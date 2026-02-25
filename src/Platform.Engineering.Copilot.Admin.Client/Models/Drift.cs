namespace Platform.Engineering.Copilot.Admin.Client.Models;

/// <summary>Result of drift detection for an environment.</summary>
public class DriftDetectionResultDto
{
    public Guid EnvironmentId { get; set; }
    public List<DriftItemDto> DriftItems { get; set; } = new();
    public int TotalDriftCount { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
}

/// <summary>Individual drift item.</summary>
public class DriftItemDto
{
    public Guid Id { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string? ResourceName { get; set; }
    public string? ResourceType { get; set; }
    public string PropertyPath { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public string? DriftType { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool CanAutoRemediate { get; set; }
}

/// <summary>Result of drift remediation.</summary>
public class RemediateDriftResultDto
{
    public int RemediatedCount { get; set; }
    public int FailedCount { get; set; }
    public int RemainingCount { get; set; }
    public List<DriftFailureDto> Failures { get; set; } = new();
}

/// <summary>Details of a failed drift remediation.</summary>
public class DriftFailureDto
{
    public Guid DriftItemId { get; set; }
    public string Error { get; set; } = string.Empty;
}
