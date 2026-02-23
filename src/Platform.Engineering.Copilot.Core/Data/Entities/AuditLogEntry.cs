using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Immutable audit log record for every agent action.
/// APPEND-ONLY — no updates or deletes permitted (FR-073).
/// Retained minimum 7 years. Partitioned by year on Timestamp.
/// Production DB: DENY UPDATE, DELETE ON AuditLogs TO [app_role].
/// </summary>
public class AuditLogEntry
{
    [Key]
    public Guid AuditLogId { get; set; }

    /// <summary>User identity (may be redacted in logs per FR-078).</summary>
    [Required]
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string UserDisplayName { get; set; } = string.Empty;

    /// <summary>Action performed (FR-066).</summary>
    [Required]
    [MaxLength(200)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Which agent handled the action.</summary>
    [Required]
    [MaxLength(50)]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Tool that executed the action.</summary>
    [Required]
    [MaxLength(100)]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Distributed tracing ID (FR-077).</summary>
    [Required]
    public Guid CorrelationId { get; set; }

    /// <summary>List of Azure resource IDs affected (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? AffectedResources { get; set; }

    [Required]
    public AuditOutcome Outcome { get; set; }

    /// <summary>Business justification for PIM elevation (FR-070).</summary>
    [MaxLength(1000)]
    public string? PimJustification { get; set; }

    /// <summary>Additional context (JSON).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Details { get; set; }

    /// <summary>Plain-language error if failed.</summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [Required]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Default: Timestamp + 7 years (FR-073).</summary>
    [Required]
    public DateTimeOffset RetentionExpiresAt { get; set; }

    /// <summary>Cold storage transition flag.</summary>
    public bool IsArchived { get; set; }

    /// <summary>For rowversion-less concurrency (SQLite compatibility).</summary>
    [ConcurrencyCheck]
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}
