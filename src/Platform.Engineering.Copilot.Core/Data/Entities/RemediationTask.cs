using System.ComponentModel.DataAnnotations;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// A work item on the Kanban board derived from a compliance finding.
/// SLA: Critical 24h, High 7d, Medium 30d, Low 90d (FR-052).
/// State transitions: Backlog → ToDo → InProgress → InReview → Done | Blocked (from any except Done).
/// </summary>
public class RemediationTask
{
    [Key]
    public Guid TaskId { get; set; }

    [Required]
    public Guid BoardId { get; set; }

    [Required]
    public Guid FindingId { get; set; }

    /// <summary>Auto-generated display ID: REM-001, REM-002, etc.</summary>
    [Required]
    [MaxLength(10)]
    public string DisplayId { get; set; } = string.Empty;

    /// <summary>Derived from control title (FR-051).</summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Mirrors finding severity.</summary>
    [Required]
    public Severity Severity { get; set; }

    public Guid? AssigneeUserId { get; set; }

    [Required]
    public RemediationTaskStatus Status { get; set; } = RemediationTaskStatus.Backlog;

    /// <summary>SLA-based due date (FR-052).</summary>
    [Required]
    public DateTimeOffset DueDate { get; set; }

    /// <summary>SLA hours: Critical=24, High=168, Medium=720, Low=2160.</summary>
    [Required]
    public int SlaHours { get; set; }

    /// <summary>Computed: DueDate &lt; UtcNow &amp;&amp; Status != Done.</summary>
    public bool IsOverdue => DueDate < DateTimeOffset.UtcNow && Status != RemediationTaskStatus.Done;

    /// <summary>Required when Status = Blocked (FR-053).</summary>
    [MaxLength(1000)]
    public string? BlockedReason { get; set; }

    /// <summary>Assessment triggered on "Done" transition.</summary>
    public Guid? ValidationScanId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public RemediationBoard Board { get; set; } = null!;
    public ComplianceFinding Finding { get; set; } = null!;
    public User? Assignee { get; set; }
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();

    /// <summary>
    /// Returns the SLA hours for the given severity level.
    /// Critical: 24h, High: 168h (7d), Medium: 720h (30d), Low: 2160h (90d).
    /// </summary>
    public static int GetSlaHours(Severity severity) => severity switch
    {
        Severity.Critical => 24,
        Severity.High => 168,
        Severity.Medium => 720,
        Severity.Low => 2160,
        _ => 720
    };
}
