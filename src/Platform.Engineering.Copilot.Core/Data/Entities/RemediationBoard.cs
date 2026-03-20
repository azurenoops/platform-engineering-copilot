using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// A Kanban board created from assessment findings.
/// </summary>
public class RemediationBoard
{
    [Key]
    public Guid BoardId { get; set; }

    [Required]
    public Guid AssessmentId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<RemediationTask> Tasks { get; set; } = new List<RemediationTask>();
}
