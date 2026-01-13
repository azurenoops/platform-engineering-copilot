using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// SERVICE TEMPLATE (Environment Agent) - Pre-Approved, Permanent Catalog
/// ═══════════════════════════════════════════════════════════════════════════════
/// 
/// Purpose: Pre-approved infrastructure patterns managed by the Platform Team.
///          Developers provision environments from this curated catalog.
/// 
/// Lifecycle: PERMANENT - Versioned catalog with approval workflow.
///            Status: Draft → PendingApproval → Published → Deprecated → Archived
/// 
/// Service Chain: ServiceTemplateCatalogService → (future: IServiceTemplateRepository) → This Entity
/// 
/// Use Case: Platform team creates AKS template → Approval workflow → Published →
///           Developer provisions from catalog → ProvisionedEnvironmentEntity tracks instance
/// 
/// Features:
/// • Versioned (semantic versioning)
/// • Approval workflow (Draft → PendingApproval → Published)
/// • Git sync (pull from approved repo)
/// • Guardrails (enforce compliance policies)
/// • Drift detection (compare deployed vs template)
/// 
/// ─────────────────────────────────────────────────────────────────────────────
/// DO NOT CONFUSE WITH: InfrastructureTemplate (AI-generated, temporary, 30-min expiry)
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
[Table("ServiceTemplates")]
public class ServiceTemplateEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Version { get; set; } = "1.0.0";

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    // Template Content
    [Required]
    [StringLength(20)]
    public string Format { get; set; } = "Bicep"; // Bicep, ARM, Terraform, Pulumi

    [Required]
    public string MainTemplateContent { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized list of additional template files
    /// </summary>
    public string? AdditionalFilesJson { get; set; }

    // Git Source of Truth
    public string? GitRepositoryUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? GitPath { get; set; }
    public string? GitCommitSha { get; set; }
    public DateTime? LastSyncedFromGit { get; set; }
    public bool GitAutoSync { get; set; } = true;
    public int GitSyncIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// JSON-serialized list of template parameters
    /// </summary>
    public string? ParametersJson { get; set; }

    /// <summary>
    /// JSON-serialized list of guardrails
    /// </summary>
    public string? GuardrailsJson { get; set; }

    /// <summary>
    /// JSON-serialized dictionary of default tags
    /// </summary>
    public string? DefaultTagsJson { get; set; }

    // Lifecycle Status
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, PendingApproval, Published, Deprecated, Archived

    [Required]
    [StringLength(200)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // Approval
    public bool RequiresApproval { get; set; } = true;

    [StringLength(20)]
    public string? ApprovalSource { get; set; } // Internal, GitHub, AzureDevOps, Manual

    [StringLength(200)]
    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [StringLength(1000)]
    public string? ApprovalComments { get; set; }

    [StringLength(100)]
    public string? ExternalApprovalId { get; set; }

    [StringLength(500)]
    public string? ExternalApprovalUrl { get; set; }

    // Compliance
    /// <summary>
    /// Comma-separated list of compliance frameworks (NIST-800-53, FedRAMP-High, etc.)
    /// </summary>
    [StringLength(500)]
    public string? ComplianceFrameworks { get; set; }

    public bool EnforceCompliance { get; set; } = true;

    // Expiration Defaults
    public int? DefaultExpirationDays { get; set; }

    // AI Context
    /// <summary>
    /// Comma-separated keywords for AI matching
    /// </summary>
    [StringLength(1000)]
    public string? Keywords { get; set; }

    /// <summary>
    /// Comma-separated use cases
    /// </summary>
    [StringLength(2000)]
    public string? UseCases { get; set; }

    [StringLength(1000)]
    public string? AiSelectionHint { get; set; }

    // Metrics
    public int DeploymentCount { get; set; } = 0;

    public DateTime? LastDeployedAt { get; set; }

    /// <summary>
    /// JSON-serialized version history
    /// </summary>
    public string? VersionHistoryJson { get; set; }

    // Navigation Properties
    public virtual ICollection<ProvisionedEnvironmentEntity> ProvisionedEnvironments { get; set; } = new List<ProvisionedEnvironmentEntity>();
    public virtual ICollection<ServiceTemplateAuditEntity> AuditEntries { get; set; } = new List<ServiceTemplateAuditEntity>();
}

/// <summary>
/// Audit log entry for service template operations
/// </summary>
[Table("ServiceTemplateAuditLog")]
public class ServiceTemplateAuditEntity
{
    [Key]
    public Guid Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(50)]
    public string EntityType { get; set; } = string.Empty; // ServiceTemplate, ProvisionedEnvironment

    public Guid EntityId { get; set; }

    [Required]
    [StringLength(200)]
    public string EntityName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty; // Created, Updated, Published, Deployed, etc.

    [Required]
    [StringLength(200)]
    public string PerformedBy { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Details { get; set; }

    /// <summary>
    /// JSON-serialized old values
    /// </summary>
    public string? OldValuesJson { get; set; }

    /// <summary>
    /// JSON-serialized new values
    /// </summary>
    public string? NewValuesJson { get; set; }

    // Navigation
    public Guid? ServiceTemplateId { get; set; }

    [ForeignKey("ServiceTemplateId")]
    public virtual ServiceTemplateEntity? ServiceTemplate { get; set; }
}
