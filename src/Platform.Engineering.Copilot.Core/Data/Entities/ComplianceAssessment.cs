using System.ComponentModel.DataAnnotations;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// A compliance evaluation result containing scan results and summary scores.
/// State transitions: Running → Completed | Failed | Cancelled.
/// Retained minimum 3 years (FR-072).
/// </summary>
public class ComplianceAssessment
{
    [Key]
    public Guid AssessmentId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public ScanType ScanType { get; set; }

    [Required]
    public ComplianceFramework Framework { get; set; }

    [Required]
    [MaxLength(36)]
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    public int TotalControls { get; set; }

    [Required]
    public int Passing { get; set; }

    [Required]
    public int Failing { get; set; }

    [Required]
    public int NotApplicable { get; set; }

    [Required]
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Null while running.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Computed on completion.</summary>
    public double? DurationSeconds { get; set; }

    [Required]
    public int ResourceCount { get; set; }

    [Required]
    public AssessmentStatus Status { get; set; } = AssessmentStatus.Running;

    /// <summary>Default: CreatedAt + 3 years (FR-072).</summary>
    [Required]
    public DateTimeOffset RetentionExpiresAt { get; set; }

    /// <summary>Soft-delete for archival.</summary>
    public bool IsDeleted { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<ComplianceFinding> Findings { get; set; } = new List<ComplianceFinding>();
    public ICollection<EvidencePackage> EvidencePackages { get; set; } = new List<EvidencePackage>();
    public ICollection<ComplianceDocument> Documents { get; set; } = new List<ComplianceDocument>();
    public RemediationBoard? Board { get; set; }
}
