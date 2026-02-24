using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Predefined IaC configurations for common Azure Government workloads.
/// Managed via Admin Dashboard with Git sync and approval workflow.
/// </summary>
public class ServiceTemplate
{
    [Key]
    public Guid TemplateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Semantic version.</summary>
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    /// <summary>e.g. "Compute", "Networking", "Security".</summary>
    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = "General";

    /// <summary>IaC format: Bicep, ARM, or Terraform.</summary>
    public TemplateFormat Format { get; set; } = TemplateFormat.Bicep;

    /// <summary>Template content (format-agnostic).</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Lifecycle status in the approval workflow.</summary>
    public TemplateStatus Status { get; set; } = TemplateStatus.Draft;

    /// <summary>"resourceGroup" or "subscription".</summary>
    [MaxLength(50)]
    public string? DeploymentScope { get; set; }

    /// <summary>Template parameter definitions (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ParametersJson { get; set; }

    /// <summary>Template guardrail rules (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? GuardrailsJson { get; set; }

    /// <summary>Comma-separated compliance frameworks: "NIST 800-53,FedRAMP High".</summary>
    [MaxLength(1000)]
    public string? ComplianceFrameworks { get; set; }

    /// <summary>Comma-separated keywords for NL matching.</summary>
    [MaxLength(1000)]
    public string? Keywords { get; set; }

    /// <summary>Comma-separated use case descriptions.</summary>
    [MaxLength(2000)]
    public string? UseCases { get; set; }

    /// <summary>Hints for AI template selection.</summary>
    [MaxLength(2000)]
    public string? AiSelectionHints { get; set; }

    /// <summary>Bicep modules synced from Git (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalFilesJson { get; set; }

    /// <summary>True when manually edited; prevents Git sync of parameters.</summary>
    public bool ParametersOverridden { get; set; }

    /// <summary>Whether approval workflow is required for this template.</summary>
    public bool RequiresApproval { get; set; } = true;

    /// <summary>"Internal" or "External".</summary>
    [MaxLength(50)]
    public string? ApprovalSource { get; set; }

    [MaxLength(200)]
    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Reviewer comments from approval.</summary>
    [MaxLength(2000)]
    public string? ApprovalComments { get; set; }

    /// <summary>External system reference for external approvals.</summary>
    [MaxLength(200)]
    public string? ExternalApprovalId { get; set; }

    /// <summary>Link to external approval system.</summary>
    [MaxLength(500)]
    public string? ExternalApprovalUrl { get; set; }

    /// <summary>Who deprecated this template.</summary>
    [MaxLength(200)]
    public string? DeprecatedBy { get; set; }

    public DateTimeOffset? DeprecatedAt { get; set; }

    /// <summary>Reason for deprecation.</summary>
    [MaxLength(1000)]
    public string? DeprecationReason { get; set; }

    /// <summary>Git sync source URL.</summary>
    [MaxLength(500)]
    public string? GitRepoUrl { get; set; }

    /// <summary>Default: "main".</summary>
    [MaxLength(200)]
    public string? GitBranch { get; set; }

    /// <summary>File path within the Git repository.</summary>
    [MaxLength(500)]
    public string? GitPath { get; set; }

    /// <summary>Last synced commit SHA.</summary>
    [MaxLength(40)]
    public string? GitCommitSha { get; set; }

    /// <summary>Enable automatic Git sync.</summary>
    public bool GitAutoSync { get; set; }

    /// <summary>Sync frequency in minutes (5-1440).</summary>
    public int GitSyncIntervalMinutes { get; set; } = 60;

    public GitSyncStatus GitSyncStatus { get; set; } = GitSyncStatus.NotConfigured;

    public DateTimeOffset? GitLastSyncAt { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    [MaxLength(200)]
    public string? DeletedBy { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [MaxLength(200)]
    public string? CreatedBy { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
