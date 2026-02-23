using System.ComponentModel.DataAnnotations;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// A single compliance violation or observation from an assessment.
/// </summary>
public class ComplianceFinding
{
    [Key]
    public Guid FindingId { get; set; }

    [Required]
    public Guid AssessmentId { get; set; }

    /// <summary>NIST control ID, e.g. "AC-2", "SC-8".</summary>
    [Required]
    [MaxLength(20)]
    public string ControlId { get; set; } = string.Empty;

    /// <summary>e.g. "Access Control".</summary>
    [Required]
    [MaxLength(50)]
    public string ControlFamily { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string ControlTitle { get; set; } = string.Empty;

    /// <summary>Full Azure resource ID.</summary>
    [Required]
    [MaxLength(1000)]
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>e.g. "Microsoft.Storage/storageAccounts".</summary>
    [Required]
    [MaxLength(200)]
    public string ResourceType { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ResourceName { get; set; } = string.Empty;

    [Required]
    public Severity Severity { get; set; }

    [Required]
    public FindingStatus Status { get; set; }

    /// <summary>Plain-language finding description.</summary>
    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>How to fix the finding.</summary>
    [MaxLength(4000)]
    public string? RemediationGuidance { get; set; }

    /// <summary>Azure Policy definition ID if applicable.</summary>
    [MaxLength(500)]
    public string? PolicyDefinitionId { get; set; }

    /// <summary>Defender for Cloud reference.</summary>
    [MaxLength(500)]
    public string? DefenderRecommendationId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public ComplianceAssessment Assessment { get; set; } = null!;
    public RemediationTask? RemediationTask { get; set; }
}
