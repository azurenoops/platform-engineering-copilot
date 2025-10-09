using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Data.Entities;

/// <summary>
/// Environment lifecycle management entity for automated environment scheduling
/// </summary>
[Table("EnvironmentLifecycles")]
public class EnvironmentLifecycle
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid EnvironmentId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LifecycleType { get; set; } = string.Empty; // scheduled, permanent, ephemeral, on-demand

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty; // active, suspended, expired, scheduled

    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ScheduledEndTime { get; set; }

    [StringLength(20)]
    public string? AutoDestroyPolicy { get; set; } // never, after_inactivity, scheduled, cost_threshold

    public int InactivityThresholdHours { get; set; } = 72; // Auto-destroy after 72 hours of inactivity
    public decimal CostThreshold { get; set; } = 0; // Auto-destroy if cost exceeds threshold

    [StringLength(100)]
    public string? OwnerTeam { get; set; }

    [StringLength(100)]
    public string? Project { get; set; }

    [StringLength(50)]
    public string? CostCenter { get; set; }

    public string? NotificationEmails { get; set; } // JSON array of email addresses

    public bool NotifyBeforeDestroy { get; set; } = true;
    public int NotificationHours { get; set; } = 24; // Notify 24 hours before destruction

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }

    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation properties
    [ForeignKey("EnvironmentId")]
    public virtual EnvironmentDeployment Environment { get; set; } = null!;

    public virtual ICollection<EnvironmentActivity> Activities { get; set; } = new List<EnvironmentActivity>();
    public virtual ICollection<EnvironmentCostTracking> CostTrackings { get; set; } = new List<EnvironmentCostTracking>();
}

/// <summary>
/// Environment activity tracking for lifecycle management
/// </summary>
[Table("EnvironmentActivities")]
public class EnvironmentActivity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid EnvironmentLifecycleId { get; set; }

    [Required]
    [StringLength(50)]
    public string ActivityType { get; set; } = string.Empty; // deployment, access, scaling, backup, clone

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [StringLength(100)]
    public string? UserId { get; set; }

    [StringLength(100)]
    public string? UserName { get; set; }

    public string? Metadata { get; set; } // JSON metadata about the activity

    public DateTime Timestamp { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = string.Empty; // success, failed, in_progress

    public string? ErrorMessage { get; set; }

    // Navigation properties
    [ForeignKey("EnvironmentLifecycleId")]
    public virtual EnvironmentLifecycle EnvironmentLifecycle { get; set; } = null!;
}

/// <summary>
/// Environment cost tracking per team/project
/// </summary>
[Table("EnvironmentCostTrackings")]
public class EnvironmentCostTracking
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid EnvironmentLifecycleId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyCost { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal CumulativeCost { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = "USD";

    public string? CostBreakdown { get; set; } // JSON breakdown by service

    [StringLength(50)]
    public string? BillingResourceGroup { get; set; }

    [StringLength(100)]
    public string? SubscriptionId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey("EnvironmentLifecycleId")]
    public virtual EnvironmentLifecycle EnvironmentLifecycle { get; set; } = null!;
}

/// <summary>
/// Environment cloning operations and relationships
/// </summary>
[Table("EnvironmentClones")]
public class EnvironmentClone
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid SourceEnvironmentId { get; set; }

    [Required]
    public Guid TargetEnvironmentId { get; set; }

    [Required]
    [StringLength(50)]
    public string CloneType { get; set; } = string.Empty; // full, infrastructure_only, data_only, template_only

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty; // pending, in_progress, completed, failed

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [StringLength(100)]
    public string InitiatedBy { get; set; } = string.Empty;

    public bool IncludeData { get; set; } = true;
    public bool MaskSensitiveData { get; set; } = true;
    public bool IncludeSecrets { get; set; } = false;

    public string? DataMaskingRules { get; set; } // JSON rules for data masking
    public string? ExcludedResources { get; set; } // JSON array of resources to exclude

    public string? CloneOperationLog { get; set; } // JSON log of clone operations
    public string? ErrorDetails { get; set; }

    public int Progress { get; set; } = 0; // 0-100 percentage

    // Navigation properties
    [ForeignKey("SourceEnvironmentId")]
    public virtual EnvironmentDeployment SourceEnvironment { get; set; } = null!;

    [ForeignKey("TargetEnvironmentId")]
    public virtual EnvironmentDeployment TargetEnvironment { get; set; } = null!;
}

/// <summary>
/// Environment synchronization settings and operations
/// </summary>
[Table("EnvironmentSynchronizations")]
public class EnvironmentSynchronization
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid SourceEnvironmentId { get; set; }

    [Required]
    public Guid TargetEnvironmentId { get; set; }

    [Required]
    [StringLength(50)]
    public string SyncType { get; set; } = string.Empty; // schema, data, configuration, secrets

    [Required]
    [StringLength(20)]
    public string SyncFrequency { get; set; } = string.Empty; // manual, hourly, daily, weekly

    public bool IsActive { get; set; } = true;
    public bool IsBidirectional { get; set; } = false;

    public DateTime? LastSyncAt { get; set; }
    public DateTime? NextSyncAt { get; set; }

    [StringLength(20)]
    public string? LastSyncStatus { get; set; } // success, failed, partial

    public string? SyncRules { get; set; } // JSON rules for what to sync
    public string? ConflictResolution { get; set; } // JSON rules for conflict resolution

    public string? LastSyncLog { get; set; } // JSON log of last sync operation

    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("SourceEnvironmentId")]
    public virtual EnvironmentDeployment SourceEnvironment { get; set; } = null!;

    [ForeignKey("TargetEnvironmentId")]
    public virtual EnvironmentDeployment TargetEnvironment { get; set; } = null!;
}