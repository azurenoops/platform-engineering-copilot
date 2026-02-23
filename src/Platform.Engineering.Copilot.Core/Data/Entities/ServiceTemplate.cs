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

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>e.g. "Compute", "Networking", "Security".</summary>
    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Bicep template content.</summary>
    [Required]
    public string ContentBicep { get; set; } = string.Empty;

    /// <summary>Template parameter definitions (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Parameters { get; set; }

    /// <summary>Git sync source URL.</summary>
    [MaxLength(500)]
    public string? GitRepoUrl { get; set; }

    /// <summary>Default: "main".</summary>
    [MaxLength(200)]
    public string? GitBranch { get; set; }

    public GitSyncStatus GitSyncStatus { get; set; } = GitSyncStatus.NotConfigured;

    public DateTimeOffset? GitLastSyncAt { get; set; }

    /// <summary>Semantic version.</summary>
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    public bool IsApproved { get; set; }

    [MaxLength(200)]
    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
