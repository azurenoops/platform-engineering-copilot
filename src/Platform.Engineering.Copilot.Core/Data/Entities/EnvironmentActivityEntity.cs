using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Entity for tracking environment activity history.
/// Records deployment events, drift detection results, configuration changes, etc.
/// </summary>
[Table("EnvironmentActivities")]
public class EnvironmentActivityEntity
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The environment this activity belongs to.
    /// Note: Uses ProvisionedEnvironment.Id, mapped as EnvironmentLifecycleId for backward compatibility with SQL schema.
    /// </summary>
    [Column("EnvironmentLifecycleId")]
    public Guid EnvironmentId { get; set; }

    /// <summary>
    /// Type of activity: Created, Updated, Scaled, DriftDetected, DriftRemediated, 
    /// Cloned, Deleted, ExpirationExtended, HealthCheckCompleted, etc.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what happened
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// User ID who performed the action (if applicable)
    /// </summary>
    [StringLength(100)]
    public string? UserId { get; set; }

    /// <summary>
    /// User display name who performed the action
    /// </summary>
    [StringLength(100)]
    public string? UserName { get; set; }

    /// <summary>
    /// JSON-serialized additional metadata about the activity
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When the activity occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Status of the activity: Started, InProgress, Completed, Failed
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Completed";

    /// <summary>
    /// Error message if the activity failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    // Navigation property
    [ForeignKey(nameof(EnvironmentId))]
    public virtual ProvisionedEnvironmentEntity? Environment { get; set; }
}

/// <summary>
/// Predefined activity types for environment tracking
/// </summary>
public static class EnvironmentActivityTypes
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Scaled = "Scaled";
    public const string Cloned = "Cloned";
    public const string Deleted = "Deleted";
    public const string DriftDetected = "DriftDetected";
    public const string DriftRemediated = "DriftRemediated";
    public const string ExpirationExtended = "ExpirationExtended";
    public const string HealthCheckCompleted = "HealthCheckCompleted";
    public const string ComplianceScanCompleted = "ComplianceScanCompleted";
    public const string StatusChanged = "StatusChanged";
    public const string TagsUpdated = "TagsUpdated";
    public const string ParametersUpdated = "ParametersUpdated";
}

/// <summary>
/// Predefined activity statuses
/// </summary>
public static class EnvironmentActivityStatuses
{
    public const string Started = "Started";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
