using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// An Azure environment provisioned from a ServiceTemplate.
/// Tracks lifecycle, deployed resources, drift, and activity history.
/// </summary>
public class ProvisionedEnvironment
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid TemplateId { get; set; }

    [MaxLength(200)]
    public string? TemplateName { get; set; }

    [Required]
    [MaxLength(100)]
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ResourceGroup { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Location { get; set; } = "eastus";

    public EnvironmentStatus Status { get; set; } = EnvironmentStatus.Provisioning;

    [MaxLength(1000)]
    public string? StatusMessage { get; set; }

    [MaxLength(200)]
    public string? DeploymentId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ParameterValuesJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? DeployedResourcesJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? TagsJson { get; set; }

    public bool HasDrift { get; set; }

    public int DriftCount { get; set; }

    public decimal? EstimatedMonthlyCost { get; set; }

    [MaxLength(200)]
    public string? OwnerEmail { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public bool AutoDelete { get; set; }

    [MaxLength(50)]
    public string? DeploymentScope { get; set; }

    [MaxLength(200)]
    public string? RequestedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    [MaxLength(200)]
    public string? DeletedBy { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public ServiceTemplate Template { get; set; } = null!;
    public ICollection<DeployedResource> DeployedResources { get; set; } = new List<DeployedResource>();
    public ICollection<DriftItem> DriftItems { get; set; } = new List<DriftItem>();
    public ICollection<EnvironmentActivity> Activities { get; set; } = new List<EnvironmentActivity>();
}
