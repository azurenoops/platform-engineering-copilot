using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

#region Enums

/// <summary>
/// Format of the infrastructure template
/// </summary>
public enum TemplateFormat
{
    Bicep,
    Terraform,
    ARM,
    Pulumi
}

/// <summary>
/// Lifecycle status of a service template
/// </summary>
public enum TemplateStatus
{
    Draft,
    PendingApproval,
    Published,
    Deprecated,
    Archived
}

/// <summary>
/// Type of template parameter
/// </summary>
public enum ParameterType
{
    String,
    Number,
    Boolean,
    Choice,
    Secret,
    ResourceId,
    Location,
    ResourceGroup
}

/// <summary>
/// Type of guardrail enforcement
/// </summary>
public enum GuardrailType
{
    Limit,      // Maximum/minimum value
    Require,    // Must have this value/property
    Deny,       // Cannot have this value
    Recommend   // Suggest but don't enforce
}

/// <summary>
/// Enforcement action for guardrails
/// </summary>
public enum GuardrailAction
{
    Deny,       // Block the operation
    Audit,      // Log but allow
    Modify,     // Auto-correct to compliant value
    Warn        // Warn user but allow
}

/// <summary>
/// Approval source for templates
/// </summary>
public enum ApprovalSource
{
    Internal,       // Platform admin UI
    GitHub,         // GitHub PR approval
    AzureDevOps,    // Azure DevOps PR approval
    Manual          // Manual/offline approval
}

#endregion

#region Core Entities

/// <summary>
/// Service Template - Platform team-defined infrastructure pattern
/// </summary>
public class ServiceTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Category { get; set; } = string.Empty;  // Compute, Data, Integration, etc.
    
    // Template Content
    public TemplateFormat Format { get; set; } = TemplateFormat.Bicep;
    public string MainTemplateContent { get; set; } = string.Empty;
    public List<TemplateFile> AdditionalFiles { get; set; } = new();
    
    /// <summary>
    /// Deployment scope of the template.
    /// "resourceGroup" - Deploy into an existing resource group (user must specify RG name)
    /// "subscription" - Deploy at subscription level, template creates its own resource groups
    /// </summary>
    public string DeploymentScope { get; set; } = "resourceGroup";
    
    // Git Source of Truth
    public GitSourceInfo? GitSource { get; set; }
    public string? GitCommitSha { get; set; }
    public DateTime? LastSyncedFromGit { get; set; }
    
    // Parameters & Guardrails
    public List<TemplateParameter> Parameters { get; set; } = new();
    public List<TemplateGuardrail> Guardrails { get; set; } = new();
    
    /// <summary>
    /// When true, parameters were manually edited and should NOT be overwritten by Git sync.
    /// </summary>
    public bool ParametersOverridden { get; set; } = false;
    
    // Default Tags (applied to all resources)
    public Dictionary<string, string> DefaultTags { get; set; } = new();
    
    // Lifecycle
    public TemplateStatus Status { get; set; } = TemplateStatus.Draft;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Approval
    public bool RequiresApproval { get; set; } = true;
    public ApprovalInfo? Approval { get; set; }
    
    // Compliance
    public List<string> ComplianceFrameworks { get; set; } = new(); // ["NIST-800-53", "FedRAMP-High"]
    public bool EnforceCompliance { get; set; } = true;
    
    // Expiration Defaults (for dev/test environments)
    public int? DefaultExpirationDays { get; set; }  // Number of days until environment auto-expires
    
    // AI Context (for LLM template selection)
    public List<string> Keywords { get; set; } = new();
    public List<string> UseCases { get; set; } = new();
    public string? AiSelectionHint { get; set; }  // Help LLM understand when to use this
    
    // Metrics
    public int DeploymentCount { get; set; }
    public DateTime? LastDeployedAt { get; set; }
    
    // Version History
    public List<TemplateVersionInfo> VersionHistory { get; set; } = new();
}

/// <summary>
/// Additional file in a multi-file template
/// </summary>
public class TemplateFile
{
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;  // bicep, json, yaml
    public bool IsEntryPoint { get; set; } = false;
    public int Order { get; set; } = 0;
}

/// <summary>
/// Git repository source information
/// </summary>
public class GitSourceInfo
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string Path { get; set; } = string.Empty;  // Path within repo
    public string? PersonalAccessToken { get; set; }  // For private repos (encrypted)
    public bool AutoSync { get; set; } = true;
    public int SyncIntervalMinutes { get; set; } = 15;
}

/// <summary>
/// Template parameter definition
/// </summary>
public class TemplateParameter
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ParameterType Type { get; set; } = ParameterType.String;
    
    // Default and validation
    public object? DefaultValue { get; set; }
    public bool Required { get; set; } = false;
    public List<string>? AllowedValues { get; set; }
    public string? ValidationRegex { get; set; }
    public int? MinValue { get; set; }
    public int? MaxValue { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    
    // UI Hints
    public string? Placeholder { get; set; }
    public string? HelpText { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public string? GroupName { get; set; }  // For grouping related parameters
    
    // AI Hints
    public string? AiPromptHint { get; set; }  // Help LLM collect this value
    public bool AiCanInfer { get; set; } = false;  // LLM can infer from context
}

/// <summary>
/// Guardrail/policy for template deployments
/// </summary>
public class TemplateGuardrail
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public GuardrailType Type { get; set; }
    public string Property { get; set; } = string.Empty;  // Parameter or resource property
    public string Operator { get; set; } = string.Empty;  // <=, >=, ==, in, notIn, matches
    public object Value { get; set; } = string.Empty;
    public GuardrailAction Action { get; set; } = GuardrailAction.Deny;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Approval information
/// </summary>
public class ApprovalInfo
{
    public ApprovalSource Source { get; set; } = ApprovalSource.Internal;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public string? ApprovalComments { get; set; }
    public string? ExternalApprovalId { get; set; }  // PR number, etc.
    public string? ExternalApprovalUrl { get; set; }
}

/// <summary>
/// Version history entry
/// </summary>
public class TemplateVersionInfo
{
    public string Version { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string ChangeDescription { get; set; } = string.Empty;
    public string? GitCommitSha { get; set; }
}

#endregion

#region Provisioned Environments

/// <summary>
/// A provisioned environment created from a service template
/// </summary>
public class ProvisionedEnvironment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Template Reference
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    
    // Azure Location
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string? ResourceGroupName { get; set; }  // Alias for ResourceGroup
    public string Location { get; set; } = string.Empty;
    
    // Parameter Values Used
    public Dictionary<string, object> ParameterValues { get; set; } = new();
    public Dictionary<string, object>? Parameters { get; set; }  // Alias for ParameterValues
    
    // Applied Tags
    public Dictionary<string, string> Tags { get; set; } = new();
    
    // Deployed Resources
    public List<DeployedResource> Resources { get; set; } = new();
    public List<DeployedResource>? DeployedResources { get; set; }  // Alias for Resources
    
    // Status
    public EnvironmentStatus Status { get; set; } = EnvironmentStatus.Provisioning;
    public string? StatusMessage { get; set; }
    public string? DeploymentId { get; set; }  // ARM deployment ID
    public int? DeploymentDurationMinutes { get; set; }  // How long deployment took
    
    // Owner
    public string? OwnerEmail { get; set; }
    
    // Cloning
    public string? ClonedFromId { get; set; }  // Source environment ID if cloned
    
    // Lifecycle
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Drift Detection
    public DateTime? LastDriftCheck { get; set; }
    public bool HasDrift { get; set; } = false;
    public int DriftCount { get; set; } = 0;
    public List<DriftItem>? DriftItems { get; set; }
    
    // Costs
    public decimal? EstimatedMonthlyCost { get; set; }
    public decimal? ActualMonthlyCost { get; set; }
    
    // Expiration (for dev/test environments)
    public DateTime? ExpiresAt { get; set; }
    public bool AutoDelete { get; set; } = false;
}

/// <summary>
/// Status of a provisioned environment
/// </summary>
public enum EnvironmentStatus
{
    Provisioning,
    Running,
    Updating,
    Scaling,
    Stopped,
    Failed,
    Deleting,
    Deleted
}

/// <summary>
/// A deployed Azure resource within an environment
/// </summary>
public class DeployedResource
{
    public string ResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string ProvisioningState { get; set; } = string.Empty;
    public DateTime DeployedAt { get; set; }
}

/// <summary>
/// A configuration drift item
/// </summary>
public class DriftItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;  // Alias for PropertyPath
    public string PropertyPath { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public string DriftType { get; set; } = "Configuration";  // Configuration, Missing, Extra
    public string Severity { get; set; } = "Warning";  // Critical, Warning, Info
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool CanAutoRemediate { get; set; } = false;
}

#endregion

#region Request/Response Models

/// <summary>
/// Request to create an environment from a template
/// </summary>
public class CreateEnvironmentFromTemplateRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string ResourceGroup { get; set; } = string.Empty;
    public string? ResourceGroupName { get; set; }  // Alias for ResourceGroup
    public string? SubscriptionId { get; set; }
    public string Location { get; set; } = "eastus";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, string>? Tags { get; set; }  // Additional tags
    public Dictionary<string, string>? AdditionalTags { get; set; }  // Alias for Tags
    public string? OwnerEmail { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool AutoDelete { get; set; } = false;
}

/// <summary>
/// Result of environment creation
/// </summary>
public class CreateEnvironmentResult
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public string? EnvironmentName { get; set; }
    public string? DeploymentId { get; set; }
    public ProvisionedEnvironment? Environment { get; set; }
    public EnvironmentStatus Status { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? Errors { get; set; }
    public List<string>? ValidationErrors { get; set; }
    public List<GuardrailViolation>? GuardrailViolations { get; set; }
    public List<DeployedResource>? DeployedResources { get; set; }
}

/// <summary>
/// A guardrail violation
/// </summary>
public class GuardrailViolation
{
    public string GuardrailId { get; set; } = string.Empty;
    public string GuardrailName { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public object ProvidedValue { get; set; } = string.Empty;
    public object RequiredValue { get; set; } = string.Empty;
    public GuardrailAction Action { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Template search/filter criteria
/// </summary>
public class TemplateSearchCriteria
{
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public TemplateStatus? Status { get; set; }
    public string? ComplianceFramework { get; set; }
    public TemplateFormat? Format { get; set; }
    public bool IncludeDeprecated { get; set; } = false;
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 20;
}

/// <summary>
/// Environment search/filter criteria
/// </summary>
public class EnvironmentSearchCriteria
{
    public string? TemplateId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? ResourceGroup { get; set; }
    public string? ResourceGroupName { get; set; }  // Alias for ResourceGroup
    public string? Keyword { get; set; }
    public string? OwnerEmail { get; set; }
    public Dictionary<string, string>? TagFilters { get; set; }
    public EnvironmentStatus? Status { get; set; }
    public bool? HasDrift { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public bool IncludeDeleted { get; set; } = false;
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
}

#endregion

#region Scaling Models

/// <summary>
/// Request to scale an environment
/// </summary>
public class ScaleEnvironmentRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string ScaledBy { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }  // Alias for ScaledBy
    public string? Reason { get; set; }
    public int? NodeCount { get; set; }
    public int? ReplicaCount { get; set; }
    public string? Sku { get; set; }
    public string? Tier { get; set; }
    public Dictionary<string, object>? ScalingParameters { get; set; }
    public Dictionary<string, object>? AdditionalParameters { get; set; }
}

/// <summary>
/// Result of scaling operation
/// </summary>
public class ScaleEnvironmentResult
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public ProvisionedEnvironment? Environment { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? Errors { get; set; }
    public Dictionary<string, object>? OldValues { get; set; }
    public Dictionary<string, object>? NewValues { get; set; }
    public List<GuardrailViolation>? GuardrailViolations { get; set; }
}

/// <summary>
/// Result of template version upgrade
/// </summary>
public class UpgradeEnvironmentResult
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public ProvisionedEnvironment? Environment { get; set; }
    public string? PreviousVersion { get; set; }
    public string? NewVersion { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? Errors { get; set; }
    public List<string>? Changes { get; set; }
}

/// <summary>
/// Result of deleting Azure resources for an environment
/// </summary>
public class DeleteResourcesResult
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

#endregion

#region Drift Detection Models

/// <summary>
/// Result of drift detection for a single environment
/// </summary>
public class DriftDetectionResult
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public string? EnvironmentName { get; set; }
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public List<DriftItem>? DriftItems { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public List<string>? Errors { get; set; }
}

/// <summary>
/// Summary of drift for an environment (used in list views)
/// </summary>
public class EnvironmentDriftSummary
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public bool HasDrift { get; set; }
    public int DriftItemCount { get; set; }  // Total drift items
    public int CriticalDriftCount { get; set; }
    public int WarningDriftCount { get; set; }
    public int InfoDriftCount { get; set; }
    public DateTime? LastChecked { get; set; }  // Alias for LastCheckAt
    public DateTime? LastCheckAt { get; set; }
}

/// <summary>
/// Result of drift remediation
/// </summary>
public class RemediateDriftResult
{
    public bool Success { get; set; }
    public string? EnvironmentId { get; set; }
    public int ItemsRemediated { get; set; }
    public int ItemsFailed { get; set; }
    public int RemainingDriftCount { get; set; }
    public List<DriftRemediationItem>? RemediatedItems { get; set; }
    public List<DriftRemediationItem>? FailedItems { get; set; }
    public List<string>? Errors { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Individual drift remediation item result
/// </summary>
public class DriftRemediationItem
{
    public string ResourceId { get; set; } = string.Empty;
    public string PropertyPath { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion

#region Health & Status Models

/// <summary>
/// Health status for an environment
/// </summary>
public class EnvironmentHealthStatus
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string OverallHealth { get; set; } = "Unknown"; // Healthy, Degraded, Unhealthy, Unknown
    public List<ResourceHealthItem>? ResourceHealth { get; set; }
    public List<string>? Issues { get; set; }
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public decimal? EstimatedMonthlyCost { get; set; }
    public DateTime? CheckedAt { get; set; }  // When health was checked
    public DateTime? LastChecked { get; set; }  // Alias
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Health status for an individual resource
/// </summary>
public class ResourceHealthItem
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Health { get; set; } = "Unknown";  // Healthy, Degraded, Unhealthy, Unknown
    public string HealthStatus { get; set; } = "Unknown"; // Alias for Health
    public string? Message { get; set; }
    public string? StatusMessage { get; set; }  // Alias for Message
    public DateTime? LastChecked { get; set; }
}

/// <summary>
/// Summary of all environments status
/// </summary>
public class EnvironmentStatusSummary
{
    public int TotalEnvironments { get; set; }
    public int HealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public int UnhealthyCount { get; set; }
    public int RunningEnvironments { get; set; }  // Alias for Running status count
    public int ProvisioningEnvironments { get; set; }  // Alias for ProvisioningCount
    public int UpdatingEnvironments { get; set; }  // Count of updating environments
    public int ProvisioningCount { get; set; }
    public int FailedEnvironments { get; set; }  // Count of failed environments
    public int EnvironmentsWithDrift { get; set; }  // Alias for WithDriftCount
    public int WithDriftCount { get; set; }
    public int ExpiringWithin7Days { get; set; }
    public decimal TotalEstimatedMonthlyCost { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, int>? EnvironmentsByTemplate { get; set; }  // Alias for ByTemplate
    public Dictionary<string, int>? EnvironmentsByStatus { get; set; }  // Alias for ByStatus
    public Dictionary<string, int>? ByTemplate { get; set; }
    public Dictionary<string, int>? ByStatus { get; set; }
}

#endregion

#region Audit Models

/// <summary>
/// Audit log entry for template and environment operations
/// </summary>
public class TemplateAuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string EntityType { get; set; } = string.Empty;  // ServiceTemplate, ProvisionedEnvironment
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;  // Created, Updated, Published, Deployed, etc.
    public string PerformedBy { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Dictionary<string, object>? OldValues { get; set; }
    public Dictionary<string, object>? NewValues { get; set; }
}

#endregion
