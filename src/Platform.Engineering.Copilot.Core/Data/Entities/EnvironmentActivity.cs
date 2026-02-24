using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// An activity log entry for a provisioned environment lifecycle event.
/// </summary>
public class EnvironmentActivity
{
    [Key]
    public Guid Id { get; set; }

    public Guid EnvironmentId { get; set; }

    /// <summary>e.g., "Created", "Scaled", "DriftDetected", "Deleted".</summary>
    [Required]
    [MaxLength(100)]
    public string ActivityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? UserId { get; set; }

    [MaxLength(200)]
    public string? UserName { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? MetadataJson { get; set; }

    [Required]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>"Success", "Failed", "InProgress".</summary>
    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    // Navigation
    public ProvisionedEnvironment Environment { get; set; } = null!;
}
