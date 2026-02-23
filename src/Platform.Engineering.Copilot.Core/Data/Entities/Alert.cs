using System.ComponentModel.DataAnnotations;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Compliance drift alert.
/// SLA: Critical 1h, High 4h, Medium 24h, Low 7d (FR-059).
/// Related alerts within 5-minute window grouped by GroupingKey (FR-060).
/// Auto-escalate if not acknowledged within SLA (FR-061).
/// State transitions: New → Acknowledged → InProgress → Resolved | Dismissed.
/// </summary>
public class Alert
{
    [Key]
    public Guid AlertId { get; set; }

    [Required]
    public Severity Severity { get; set; }

    [Required]
    public AlertState LifecycleState { get; set; } = AlertState.New;

    [Required]
    public DriftCategory Category { get; set; }

    /// <summary>Affected NIST control.</summary>
    [MaxLength(20)]
    public string? ControlId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string ResourceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Who made the drift-causing change.</summary>
    [MaxLength(200)]
    public string? ChangeAuthor { get; set; }

    /// <summary>What changed.</summary>
    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string RecommendedAction { get; set; } = string.Empty;

    /// <summary>For 5-minute grouping (FR-060).</summary>
    [Required]
    [MaxLength(200)]
    public string GroupingKey { get; set; } = string.Empty;

    /// <summary>Based on severity SLA (FR-059).</summary>
    [Required]
    public DateTimeOffset SlaDeadline { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    [MaxLength(200)]
    public string? AcknowledgedBy { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public int EscalationCount { get; set; }

    public bool IsArchived { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Returns the SLA hours for the given alert severity.
    /// Critical: 1h, High: 4h, Medium: 24h, Low: 168h (7d).
    /// </summary>
    public static int GetAlertSlaHours(Severity severity) => severity switch
    {
        Severity.Critical => 1,
        Severity.High => 4,
        Severity.Medium => 24,
        Severity.Low => 168,
        _ => 24
    };
}
