using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Comments on remediation tasks.
/// Owners can delete own; ComplianceOfficers can delete any (FR-054).
/// </summary>
public class TaskComment
{
    [Key]
    public Guid CommentId { get; set; }

    [Required]
    public Guid TaskId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public RemediationTask Task { get; set; } = null!;
    public User User { get; set; } = null!;
}
