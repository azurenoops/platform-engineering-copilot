using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Timestamped, immutable evidence collection for a specific control.
/// Default behavior is append (new records per collection).
/// Replace mode: deletes prior evidence for same ControlId+SubscriptionId.
/// Retained minimum 3 years (FR-072).
/// </summary>
public class EvidencePackage
{
    [Key]
    public Guid PackageId { get; set; }

    /// <summary>Linked assessment if applicable.</summary>
    public Guid? AssessmentId { get; set; }

    /// <summary>Target NIST control.</summary>
    [Required]
    [MaxLength(20)]
    public string ControlId { get; set; } = string.Empty;

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(36)]
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>Configuration export data (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ConfigExports { get; set; }

    /// <summary>Policy assignment snapshots (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? PolicySnapshots { get; set; }

    /// <summary>Defender for Cloud recommendations (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? DefenderRecommendations { get; set; }

    /// <summary>Azure Activity Log excerpts (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ActivityLogs { get; set; }

    /// <summary>Resource inventory data (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ResourceInventory { get; set; }

    /// <summary>Total size of all artifacts in bytes.</summary>
    [Required]
    public long ContentSizeBytes { get; set; }

    [Required]
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>Default: CollectedAt + 3 years (FR-072).</summary>
    [Required]
    public DateTimeOffset RetentionExpiresAt { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public ComplianceAssessment? Assessment { get; set; }
    public User User { get; set; } = null!;
}
