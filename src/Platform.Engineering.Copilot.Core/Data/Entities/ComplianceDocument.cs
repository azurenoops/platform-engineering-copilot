using System.ComponentModel.DataAnnotations;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Generated compliance documentation (SSP, SAR, POA&amp;M).
/// Content max 5MB; IsTruncated indicates truncation.
/// Retained minimum 3 years (FR-072).
/// </summary>
public class ComplianceDocument
{
    [Key]
    public Guid DocumentId { get; set; }

    /// <summary>Source assessment if applicable.</summary>
    public Guid? AssessmentId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public DocumentType DocumentType { get; set; }

    [Required]
    public ComplianceFramework Framework { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Full Markdown content.</summary>
    [Required]
    public string ContentMarkdown { get; set; } = string.Empty;

    /// <summary>Document size in bytes (max 5MB per compliance-tools.md).</summary>
    [Required]
    public long ContentSizeBytes { get; set; }

    /// <summary>True if content was truncated due to 5MB limit.</summary>
    public bool IsTruncated { get; set; }

    [Required]
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Default: GeneratedAt + 3 years (FR-072).</summary>
    [Required]
    public DateTimeOffset RetentionExpiresAt { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public ComplianceAssessment? Assessment { get; set; }
    public User User { get; set; } = null!;
}
