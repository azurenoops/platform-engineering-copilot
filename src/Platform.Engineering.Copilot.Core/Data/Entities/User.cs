using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Represents a platform user with identity derived from CAC certificate.
/// </summary>
public class User
{
    [Key]
    public Guid UserId { get; set; }

    /// <summary>Distinguished Name from CAC certificate — unique identifier.</summary>
    [Required]
    [MaxLength(500)]
    public string CacSubjectDN { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User roles stored as JSON column. Must have at least one role.
    /// Permissions are the union of all assigned roles (FR-017).
    /// </summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public UserRole[] Roles { get; set; } = [];

    /// <summary>Current CAC session expiration.</summary>
    public DateTimeOffset? CacSessionExpiry { get; set; }

    /// <summary>Current PIM elevation expiration.</summary>
    public DateTimeOffset? PimElevationExpiry { get; set; }

    /// <summary>Current PIM tier (None, Read, Write).</summary>
    public PimTier PimActiveTier { get; set; } = PimTier.None;

    public bool IsActive { get; set; } = true;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public Configuration? Configuration { get; set; }
    public ICollection<ComplianceAssessment> Assessments { get; set; } = new List<ComplianceAssessment>();
    public ICollection<EvidencePackage> EvidencePackages { get; set; } = new List<EvidencePackage>();
    public ICollection<ComplianceDocument> Documents { get; set; } = new List<ComplianceDocument>();
    public ICollection<IaCTemplate> IaCTemplates { get; set; } = new List<IaCTemplate>();
}
