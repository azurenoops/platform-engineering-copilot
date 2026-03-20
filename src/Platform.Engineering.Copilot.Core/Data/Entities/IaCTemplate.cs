using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Generated IaC template (Bicep/Terraform) with 30-minute TTL.
/// Annotation coverage target: ≥80% of security properties (SC-009).
/// </summary>
public class IaCTemplate
{
    [Key]
    public Guid TemplateId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public TemplateMethod GenerationMethod { get; set; }

    /// <summary>e.g. "AKS Cluster", "Storage Account".</summary>
    [Required]
    [MaxLength(200)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>e.g. "usgovvirginia".</summary>
    [Required]
    [MaxLength(50)]
    public string Region { get; set; } = string.Empty;

    /// <summary>Compliance framework used for annotations (legacy string, nullable).</summary>
    [MaxLength(100)]
    public string? Framework { get; set; }

    /// <summary>Generated Bicep content.</summary>
    public string? ContentBicep { get; set; }

    /// <summary>Generated Terraform content.</summary>
    public string? ContentTerraform { get; set; }

    /// <summary>Control mappings JSON: [{ "property": "...", "controlId": "SC-8", "controlName": "..." }].</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ComplianceAnnotations { get; set; }

    /// <summary>Percentage of security properties annotated (SC-009: ≥80%).</summary>
    public double? AnnotationCoverage { get; set; }

    /// <summary>Default: CreatedAt + 30 minutes.</summary>
    [Required]
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Computed: ExpiresAt &lt; UtcNow.</summary>
    public bool IsExpired => ExpiresAt < DateTimeOffset.UtcNow;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
