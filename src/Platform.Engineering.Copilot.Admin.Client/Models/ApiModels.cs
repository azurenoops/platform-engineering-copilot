namespace Platform.Engineering.Copilot.Admin.Client.Models;

#region Template Models

public class TemplateListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DeploymentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    
    // Git Sync properties
    public bool HasGitSource { get; set; }
    public string? GitRepositoryUrl { get; set; }
    public DateTime? LastSyncedFromGit { get; set; }
    public bool GitAutoSync { get; set; }
}

public class TemplateDetail
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string TemplateContent { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool RequiresApproval { get; set; }
    public int? DefaultExpirationDays { get; set; }
    public List<string> ComplianceFrameworks { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public List<string> UseCases { get; set; } = new();
    public string? AiSelectionHint { get; set; }
    public int DeploymentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public ApprovalInfo? Approval { get; set; }
    public List<TemplateParameter> Parameters { get; set; } = new();
    public List<TemplateGuardrail> Guardrails { get; set; } = new();
    
    // Git Sync properties
    public bool HasGitSource { get; set; }
    public string? GitRepositoryUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? GitPath { get; set; }
    public string? GitCommitSha { get; set; }
    public DateTime? LastSyncedFromGit { get; set; }
    public bool GitAutoSync { get; set; }
    public int GitSyncIntervalMinutes { get; set; }
}

public class TemplateParameter
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public List<object>? AllowedValues { get; set; }
    public int DisplayOrder { get; set; }
}

public class TemplateGuardrail
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Property { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class ApprovalInfo
{
    public string Source { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public string? Comments { get; set; }
}

public class CreateTemplateModel
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Category { get; set; }
    public string? Format { get; set; }
    public string? TemplateContent { get; set; }
    public string? CreatedBy { get; set; }
    public bool RequiresApproval { get; set; }
    public int? DefaultExpirationDays { get; set; }
    public List<string>? ComplianceFrameworks { get; set; }
    public List<string>? Keywords { get; set; }
    public List<string>? UseCases { get; set; }
    public string? AiSelectionHint { get; set; }
    public List<CreateParameterModel>? Parameters { get; set; }
    public List<CreateGuardrailModel>? Guardrails { get; set; }
}

public class CreateParameterModel
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public List<object>? AllowedValues { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateGuardrailModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Property { get; set; }
    public string? Operator { get; set; }
    public string? Value { get; set; }
    public string? Action { get; set; }
    public string? ErrorMessage { get; set; }
}

public class UpdateTemplateModel
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? TemplateContent { get; set; }
    public List<string>? Keywords { get; set; }
    public List<string>? UseCases { get; set; }
    public string? AiSelectionHint { get; set; }
    public int? DefaultExpirationDays { get; set; }
    public string? UpdatedBy { get; set; }
}

public class ApprovalModel
{
    public string? Source { get; set; }
    public string? ApprovedBy { get; set; }
    public string? Comments { get; set; }
    public string? ExternalApprovalId { get; set; }
    public string? ExternalApprovalUrl { get; set; }
}

public class ValidateTemplateModel
{
    public string? Name { get; set; }
    public string? TemplateContent { get; set; }
    public string? Format { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

#endregion

#region Environment Models

public class EnvironmentListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public decimal EstimatedMonthlyCost { get; set; }
    public string? OwnerEmail { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class EnvironmentDetail
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public decimal EstimatedMonthlyCost { get; set; }
    public string? OwnerEmail { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool AutoDelete { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public Dictionary<string, object>? ParameterValues { get; set; }
}

public class EnvironmentStatusSummary
{
    public int TotalEnvironments { get; set; }
    public int HealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public int UnhealthyCount { get; set; }
    public int RunningEnvironments { get; set; }
    public int ProvisioningEnvironments { get; set; }
    public int FailedEnvironments { get; set; }
    public int EnvironmentsWithDrift { get; set; }
    public int ExpiringWithin7Days { get; set; }
    public decimal TotalEstimatedMonthlyCost { get; set; }
    public Dictionary<string, int>? ByTemplate { get; set; }
    public Dictionary<string, int>? ByStatus { get; set; }
}

public class CreateEnvironmentModel
{
    public string TemplateId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string? Location { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public string? OwnerEmail { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool AutoDelete { get; set; }
}

public class CreateEnvironmentResult
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public string? EnvironmentName { get; set; }
    public string? DeploymentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public EnvironmentDetail? Environment { get; set; }
}

public class ScaleEnvironmentModel
{
    public int? NodeCount { get; set; }
    public int? ReplicaCount { get; set; }
    public string? Sku { get; set; }
    public string? Tier { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string? ScaledBy { get; set; }
}

public class ScaleResult
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public Dictionary<string, object>? OldValues { get; set; }
    public Dictionary<string, object>? NewValues { get; set; }
}

public class DriftDetectionResult
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public string? EnvironmentName { get; set; }
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public DateTime DetectedAt { get; set; }
    public List<DriftItem>? DriftItems { get; set; }
}

public class DriftItem
{
    public string Id { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string PropertyPath { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public string DriftType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool CanAutoRemediate { get; set; }
}

public class RemediateDriftResult
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public int ItemsRemediated { get; set; }
    public int ItemsFailed { get; set; }
    public int RemainingDriftCount { get; set; }
    public List<string>? Errors { get; set; }
}

public class EnvironmentHealth
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string OverallHealth { get; set; } = string.Empty;
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public decimal EstimatedMonthlyCost { get; set; }
    public DateTime LastChecked { get; set; }
    public List<string>? Issues { get; set; }
    public List<ResourceHealthItem>? ResourceHealth { get; set; }
}

public class ResourceHealthItem
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Health { get; set; } = string.Empty;
    public string? Message { get; set; }
}

#endregion
