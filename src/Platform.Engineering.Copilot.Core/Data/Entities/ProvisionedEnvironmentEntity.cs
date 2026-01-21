using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Provisioned Environment entity for database persistence.
/// Represents an Azure environment created from a Service Template.
/// </summary>
[Table("ProvisionedEnvironments")]
public class ProvisionedEnvironmentEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    // Template Reference
    public Guid TemplateId { get; set; }

    [Required]
    [StringLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string TemplateVersion { get; set; } = string.Empty;

    // Azure Location
    [Required]
    [StringLength(50)]
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ResourceGroup { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized parameter values used for deployment
    /// </summary>
    public string? ParameterValuesJson { get; set; }

    /// <summary>
    /// JSON-serialized tags applied to resources
    /// </summary>
    public string? TagsJson { get; set; }

    /// <summary>
    /// JSON-serialized list of deployed resources
    /// </summary>
    public string? DeployedResourcesJson { get; set; }

    // Status
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Provisioning"; // Provisioning, Running, Updating, Scaling, Stopped, Failed, Deleting, Deleted

    [StringLength(4000)]
    public string? StatusMessage { get; set; }

    [StringLength(200)]
    public string? DeploymentId { get; set; } // ARM deployment ID

    public int? DeploymentDurationMinutes { get; set; }

    // Owner
    [StringLength(200)]
    public string? OwnerEmail { get; set; }

    // Cloning
    public Guid? ClonedFromId { get; set; }

    // Lifecycle
    [Required]
    [StringLength(200)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [StringLength(200)]
    public string? DeletedBy { get; set; }

    public DateTime? DeletedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    // Drift Detection
    public DateTime? LastDriftCheck { get; set; }

    public bool HasDrift { get; set; } = false;

    public int DriftCount { get; set; } = 0;

    /// <summary>
    /// JSON-serialized list of drift items
    /// </summary>
    public string? DriftItemsJson { get; set; }

    // Costs
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedMonthlyCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualMonthlyCost { get; set; }

    // Expiration
    public DateTime? ExpiresAt { get; set; }

    public bool AutoDelete { get; set; } = false;

    // Navigation Properties
    [ForeignKey("TemplateId")]
    public virtual ServiceTemplateEntity? Template { get; set; }

    [ForeignKey("ClonedFromId")]
    public virtual ProvisionedEnvironmentEntity? ClonedFrom { get; set; }

    public virtual ICollection<ProvisionedEnvironmentEntity> ClonedEnvironments { get; set; } = new List<ProvisionedEnvironmentEntity>();
}

/// <summary>
/// Deployed resource within a provisioned environment
/// </summary>
[Table("DeployedResources")]
public class DeployedResourceEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProvisionedEnvironmentId { get; set; }

    [Required]
    [StringLength(500)]
    public string ResourceId { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ResourceType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Location { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Sku { get; set; }

    [Required]
    [StringLength(50)]
    public string ProvisioningState { get; set; } = string.Empty;

    public DateTime DeployedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("ProvisionedEnvironmentId")]
    public virtual ProvisionedEnvironmentEntity ProvisionedEnvironment { get; set; } = null!;
}

/// <summary>
/// Drift detection item for a provisioned environment
/// </summary>
[Table("EnvironmentDriftItems")]
public class DriftItemEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProvisionedEnvironmentId { get; set; }

    [Required]
    [StringLength(500)]
    public string ResourceId { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ResourceName { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string PropertyPath { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string ExpectedValue { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string ActualValue { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string DriftType { get; set; } = "Configuration"; // Configuration, Missing, Extra

    [Required]
    [StringLength(20)]
    public string Severity { get; set; } = "Warning"; // Critical, Warning, Info

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public bool CanAutoRemediate { get; set; } = false;

    public bool IsRemediated { get; set; } = false;

    public DateTime? RemediatedAt { get; set; }

    [StringLength(200)]
    public string? RemediatedBy { get; set; }

    // Navigation
    [ForeignKey("ProvisionedEnvironmentId")]
    public virtual ProvisionedEnvironmentEntity ProvisionedEnvironment { get; set; } = null!;
}

/// <summary>
/// Audit entry for environment operations
/// </summary>
[Table("EnvironmentAuditEntries")]
public class EnvironmentAuditEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid EnvironmentId { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string PerformedBy { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Details { get; set; }

    /// <summary>
    /// JSON-serialized metadata dictionary
    /// </summary>
    public string? MetadataJson { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("EnvironmentId")]
    public virtual ProvisionedEnvironmentEntity Environment { get; set; } = null!;
}

