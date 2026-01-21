namespace Platform.Engineering.Copilot.Admin.API.Models;

#region Template DTOs

/// <summary>
/// Summary DTO for template list views
/// </summary>
public class ServiceTemplateSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DeploymentScope { get; set; } = "resourceGroup";
    public int DeploymentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    
    // Git Sync properties
    public bool HasGitSource { get; set; }
    public string? GitRepositoryUrl { get; set; }
    public DateTime? LastSyncedFromGit { get; set; }
    public bool GitAutoSync { get; set; }
}

/// <summary>
/// Full template DTO with all details
/// </summary>
public class ServiceTemplateDto
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
    
    /// <summary>
    /// Deployment scope: "resourceGroup" or "subscription"
    /// Subscription-scoped templates create their own resource groups.
    /// </summary>
    public string DeploymentScope { get; set; } = "resourceGroup";
    
    public bool RequiresApproval { get; set; }
    public bool EnforceCompliance { get; set; } = true;
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
    public ApprovalInfoDto? Approval { get; set; }
    public List<TemplateParameterDto> Parameters { get; set; } = new();
    public List<TemplateGuardrailDto> Guardrails { get; set; } = new();
    
    /// <summary>
    /// Additional template files (e.g., Bicep modules) synced from Git.
    /// </summary>
    public List<TemplateFileDto> AdditionalFiles { get; set; } = new();
    
    /// <summary>
    /// Indicates parameters were manually edited and will NOT be overwritten by Git sync.
    /// </summary>
    public bool ParametersOverridden { get; set; }
    
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

/// <summary>
/// Additional file DTO (e.g., Bicep module)
/// </summary>
public class TemplateFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
}

/// <summary>
/// Template parameter DTO
/// </summary>
public class TemplateParameterDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public List<object>? AllowedValues { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Template guardrail DTO
/// </summary>
public class TemplateGuardrailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Property { get; set; }
    public string? Operator { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Approval info DTO
/// </summary>
public class ApprovalInfoDto
{
    public string Source { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// Validation result DTO
/// </summary>
public class ValidationResultDto
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

#endregion

#region Environment DTOs

/// <summary>
/// Full environment DTO
/// </summary>
public class ProvisionedEnvironmentDto
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
    public List<DriftItemDto>? DriftItems { get; set; }
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

/// <summary>
/// Create environment result DTO
/// </summary>
public class CreateEnvironmentResultDto
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public string? EnvironmentName { get; set; }
    public string? DeploymentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public ProvisionedEnvironmentDto? Environment { get; set; }
}

/// <summary>
/// Scale result DTO
/// </summary>
public class ScaleResultDto
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public Dictionary<string, object>? OldValues { get; set; }
    public Dictionary<string, object>? NewValues { get; set; }
}

/// <summary>
/// Drift detection result DTO
/// </summary>
public class DriftDetectionResultDto
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public string? EnvironmentName { get; set; }
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public DateTime DetectedAt { get; set; }
    public List<DriftItemDto>? DriftItems { get; set; }
}

/// <summary>
/// Drift item DTO
/// </summary>
public class DriftItemDto
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

/// <summary>
/// Remediate drift result DTO
/// </summary>
public class RemediateDriftResultDto
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public int ItemsRemediated { get; set; }
    public int ItemsFailed { get; set; }
    public int RemainingDriftCount { get; set; }
    public List<string>? Errors { get; set; }
}

/// <summary>
/// Result of syncing resources from Azure
/// </summary>
public class SyncResourcesResultDto
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public int ResourcesFound { get; set; }
    public int ResourcesAdded { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Request to update environment status (admin only)
/// </summary>
public class UpdateEnvironmentStatusRequest
{
    public string? Status { get; set; }  // "Running", "Failed", "Provisioning", etc.
    public string? StatusMessage { get; set; }
    public string? DeploymentId { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Refresh deployment status result DTO
/// </summary>
public class RefreshDeploymentStatusResultDto
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string DeploymentId { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public bool StatusChanged { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Delete Azure resources result DTO
/// </summary>
public class DeleteResourcesResultDto
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public string? Message { get; set; }
    public List<string>? DeletedResources { get; set; }
    public List<string>? FailedResources { get; set; }
    public List<string>? Errors { get; set; }
    public int TotalResourcesDeleted { get; set; }
    public int TotalResourcesFailed { get; set; }
}

/// <summary>
/// Purge all result DTO
/// </summary>
public class PurgeAllResultDto
{
    public int PurgedCount { get; set; }
}

/// <summary>
/// Environment health DTO
/// </summary>
public class EnvironmentHealthDto
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string OverallHealth { get; set; } = string.Empty;
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public decimal EstimatedMonthlyCost { get; set; }
    public DateTime LastChecked { get; set; }
    public List<string>? Issues { get; set; }
    public List<ResourceHealthItemDto>? ResourceHealth { get; set; }
}

/// <summary>
/// Resource health item DTO
/// </summary>
public class ResourceHealthItemDto
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Health { get; set; } = string.Empty;
    public string? Message { get; set; }
}

/// <summary>
/// Environment status summary DTO
/// </summary>
public class EnvironmentStatusSummaryDto
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

#endregion

#region Natural Language Matching DTOs

/// <summary>
/// Request for natural language template matching
/// </summary>
public class NaturalLanguageMatchRequest
{
    /// <summary>
    /// Natural language description of what the user needs
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Minimum match score (0.0 to 1.0)
    /// </summary>
    public double? MinimumScore { get; set; }
    
    /// <summary>
    /// Maximum number of results
    /// </summary>
    public int? MaxResults { get; set; }
    
    /// <summary>
    /// Filter by category
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Required compliance framework
    /// </summary>
    public string? RequiredCompliance { get; set; }
}

/// <summary>
/// Result of template matching
/// </summary>
public class TemplateMatchResultDto
{
    public bool Success { get; set; }
    public string UserRequest { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool UsedLlm { get; set; }
    public List<TemplateMatchDto> Matches { get; set; } = new();
}

/// <summary>
/// A single template match
/// </summary>
public class TemplateMatchDto
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public Dictionary<string, object?> SuggestedParameters { get; set; } = new();
}

/// <summary>
/// Request for parameter extraction
/// </summary>
public class ExtractParametersRequest
{
    public string? UserRequest { get; set; }
}

/// <summary>
/// Result of parameter extraction
/// </summary>
public class ParameterExtractionResultDto
{
    public bool Success { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public Dictionary<string, ExtractedParameterDto> ExtractedParameters { get; set; } = new();
}

/// <summary>
/// An extracted parameter
/// </summary>
public class ExtractedParameterDto
{
    public string ParameterName { get; set; } = string.Empty;
    public object? SuggestedValue { get; set; }
    public double Confidence { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Reasoning { get; set; }
}

/// <summary>
/// Request for match explanation
/// </summary>
public class ExplainMatchRequest
{
    public string? UserRequest { get; set; }
}

/// <summary>
/// Result of match explanation
/// </summary>
public class ExplainMatchResultDto
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

#endregion

#region Git Sync DTOs

/// <summary>
/// Request to import template from Git
/// </summary>
public class GitImportRequest
{
    public string? RepositoryUrl { get; set; }
    public string? Branch { get; set; }
    public string? Path { get; set; }
    public string? ImportedBy { get; set; }
}

/// <summary>
/// Result of Git import
/// </summary>
public class GitImportResultDto
{
    public bool Success { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CommitSha { get; set; }
}

/// <summary>
/// Result of Git sync
/// </summary>
public class GitSyncResultDto
{
    public bool Success { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public bool WasUpdated { get; set; }
    public string? CommitSha { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of batch Git sync
/// </summary>
public class GitSyncBatchResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int UpdatedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<GitSyncFailureDto> Failures { get; set; } = new();
}

/// <summary>
/// Git sync failure details
/// </summary>
public class GitSyncFailureDto
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Result of Git diff check
/// </summary>
public class GitDiffResultDto
{
    public bool HasChanges { get; set; }
    public string? CurrentSha { get; set; }
    public string? LatestSha { get; set; }
    public DateTime? LastSynced { get; set; }
    public string Message { get; set; } = string.Empty;
}

#endregion

#region Environment Activity DTOs

/// <summary>
/// Environment activity entry DTO
/// </summary>
public class EnvironmentActivityDto
{
    public string Id { get; set; } = string.Empty;
    public string EnvironmentId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "Completed";
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Paged list of environment activities
/// </summary>
public class EnvironmentActivityListDto
{
    public List<EnvironmentActivityDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public bool HasMore => Skip + Items.Count < TotalCount;
}

#endregion

#region Deployed Resource DTOs

/// <summary>
/// Deployed resource DTO
/// </summary>
public class DeployedResourceDto
{
    public string Id { get; set; } = string.Empty;
    public string EnvironmentId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string ProvisioningState { get; set; } = string.Empty;
    public DateTime DeployedAt { get; set; }
    public string? AzurePortalUrl { get; set; }
}

/// <summary>
/// List of deployed resources for an environment
/// </summary>
public class DeployedResourceListDto
{
    public List<DeployedResourceDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
}

#endregion
