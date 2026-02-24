namespace Platform.Engineering.Copilot.Admin.API.Models;

/// <summary>Template summary for list responses.</summary>
public class TemplateSummaryDto
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DeploymentScope { get; set; }
    public bool HasGitSource { get; set; }
    public string? GitRepositoryUrl { get; set; }
    public DateTimeOffset? LastSyncedFromGit { get; set; }
    public bool GitAutoSync { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Full template detail for single-item responses.</summary>
public class TemplateDetailDto
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? DeploymentScope { get; set; }
    public string? ParametersJson { get; set; }
    public string? GuardrailsJson { get; set; }
    public string? ComplianceFrameworks { get; set; }
    public string? Keywords { get; set; }
    public string? UseCases { get; set; }
    public string? AiSelectionHints { get; set; }
    public string? AdditionalFilesJson { get; set; }
    public bool ParametersOverridden { get; set; }
    public bool RequiresApproval { get; set; }
    public string? ApprovalSource { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovalComments { get; set; }
    public string? ExternalApprovalId { get; set; }
    public string? ExternalApprovalUrl { get; set; }
    public string? DeprecatedBy { get; set; }
    public DateTimeOffset? DeprecatedAt { get; set; }
    public string? DeprecationReason { get; set; }
    public string? GitRepoUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? GitPath { get; set; }
    public string? GitCommitSha { get; set; }
    public bool GitAutoSync { get; set; }
    public int GitSyncIntervalMinutes { get; set; }
    public string GitSyncStatus { get; set; } = string.Empty;
    public DateTimeOffset? GitLastSyncAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Template parameter definition.</summary>
public class TemplateParameterDto
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public List<string> AllowedValues { get; set; } = new();
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Template guardrail rule.</summary>
public class TemplateGuardrailDto
{
    public string Type { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>Template validation result.</summary>
public class TemplateValidationResultDto
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>Template match result from NL matching.</summary>
public class TemplateMatchResultDto
{
    public List<TemplateMatchDto> Matches { get; set; } = new();
    public bool UsedLlm { get; set; }
    public long ProcessingTimeMs { get; set; }
}

public class TemplateMatchDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public Dictionary<string, string> SuggestedParameters { get; set; } = new();
}

/// <summary>Explanation of why a template matches.</summary>
public class TemplateExplanationDto
{
    public string Explanation { get; set; } = string.Empty;
    public List<MatchingFactorDto> MatchingFactors { get; set; } = new();
}

public class MatchingFactorDto
{
    public string Factor { get; set; } = string.Empty;
    public double Weight { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>Extracted parameters from NL input.</summary>
public class ExtractedParametersDto
{
    public List<ExtractedParameterDto> Parameters { get; set; } = new();
}

public class ExtractedParameterDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>Git sync status for a template.</summary>
public class GitStatusDto
{
    public bool HasChanges { get; set; }
    public string? CurrentCommitSha { get; set; }
    public string? LatestCommitSha { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
}

/// <summary>Environment summary for list responses.</summary>
public class EnvironmentSummaryDto
{
    public int TotalCount { get; set; }
    public int HealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public int UnhealthyCount { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public int DriftCount { get; set; }
    public int ExpiringWithin7Days { get; set; }
    public decimal TotalEstimatedMonthlyCost { get; set; }
    public List<TemplateCountDto> ByTemplate { get; set; } = new();
}

public class TemplateCountDto
{
    public string TemplateName { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Full environment detail.</summary>
public class EnvironmentDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public Guid TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public string? DeploymentId { get; set; }
    public string? ParameterValuesJson { get; set; }
    public string? DeployedResourcesJson { get; set; }
    public string? TagsJson { get; set; }
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public decimal? EstimatedMonthlyCost { get; set; }
    public string? OwnerEmail { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool AutoDelete { get; set; }
    public string? DeploymentScope { get; set; }
    public string? RequestedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Environment health status.</summary>
public class EnvironmentHealthDto
{
    public Guid EnvironmentId { get; set; }
    public string OverallStatus { get; set; } = string.Empty;
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public decimal? EstimatedMonthlyCost { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<ResourceHealthDto> ResourceHealth { get; set; } = new();
}

public class ResourceHealthDto
{
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = new();
}

/// <summary>Scale operation result.</summary>
public class ScaleResultDto
{
    public Guid EnvironmentId { get; set; }
    public Dictionary<string, object> PreviousValues { get; set; } = new();
    public Dictionary<string, object> NewValues { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}

/// <summary>Drift detection result.</summary>
public class DriftDetectionResultDto
{
    public Guid EnvironmentId { get; set; }
    public List<DriftItemDto> DriftItems { get; set; } = new();
    public int TotalDriftCount { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
}

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

/// <summary>Drift remediation result.</summary>
public class RemediateDriftResultDto
{
    public int RemediatedCount { get; set; }
    public int FailedCount { get; set; }
    public int RemainingCount { get; set; }
    public List<DriftFailureDto> Failures { get; set; } = new();
}

public class DriftFailureDto
{
    public Guid DriftItemId { get; set; }
    public string Error { get; set; } = string.Empty;
}

/// <summary>Deployment status refresh result.</summary>
public class RefreshDeploymentStatusResultDto
{
    public Guid EnvironmentId { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public bool StatusChanged { get; set; }
}

/// <summary>Resource cleanup result.</summary>
public class DeleteResourcesResultDto
{
    public List<string> DeletedResources { get; set; } = new();
    public List<ResourceFailureDto> FailedResources { get; set; } = new();
    public int DeletedCount { get; set; }
    public int FailedCount { get; set; }
}

public class ResourceFailureDto
{
    public string ResourceId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

/// <summary>Deployed resource detail.</summary>
public class ResourceDto
{
    public Guid Id { get; set; }
    public string AzureResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Sku { get; set; }
    public string? ProvisioningState { get; set; }
    public DateTimeOffset? DeployedAt { get; set; }
    public string? PortalUrl { get; set; }
    public string? ResourceGroupName { get; set; }
}

/// <summary>Activity history entry.</summary>
public class ActivityDto
{
    public Guid Id { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Paginated activity list.</summary>
public class ActivityListDto
{
    public List<ActivityDto> Activities { get; set; } = new();
    public bool HasMore { get; set; }
}
