using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Data.Entities;

/// <summary>
/// Environment deployment entity for tracking all deployments
/// </summary>
[Table("EnvironmentDeployments")]
public class EnvironmentDeployment
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string EnvironmentType { get; set; } = string.Empty; // aks, webapp, function, containerapp
    
    [Required]
    [StringLength(100)]
    public string ResourceGroupName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Location { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string SubscriptionId { get; set; } = string.Empty;
    
    public Guid? TemplateId { get; set; }
    
    [Required]
    public DeploymentStatus Status { get; set; } = DeploymentStatus.InProgress;
    
    public string? Configuration { get; set; } // JSON deployment configuration
    public string? Parameters { get; set; } // JSON deployment parameters
    public string? Tags { get; set; } // JSON key-value pairs
    
    [Required]
    [StringLength(100)]
    public string DeployedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    public bool IsDeleted { get; set; } = false;
    
    // Polling tracking fields
    public bool IsPollingActive { get; set; } = false;
    public DateTime? LastPolledAt { get; set; }
    public int PollingAttempts { get; set; } = 0;
    public TimeSpan? CurrentPollingInterval { get; set; }
    public int ProgressPercentage { get; set; } = 0;
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    
    // Cost tracking
    [Column(TypeName = "decimal(10,2)")]
    public decimal? EstimatedMonthlyCost { get; set; }
    
    [Column(TypeName = "decimal(10,2)")]
    public decimal? ActualMonthlyCost { get; set; }
    
    // Navigation properties
    [ForeignKey("TemplateId")]
    public virtual EnvironmentTemplate? Template { get; set; }
    
    public virtual ICollection<DeploymentHistory> History { get; set; } = new List<DeploymentHistory>();
    public virtual ICollection<EnvironmentMetrics> Metrics { get; set; } = new List<EnvironmentMetrics>();
    public virtual ICollection<ScalingPolicy> ScalingPolicies { get; set; } = new List<ScalingPolicy>();
    public virtual ICollection<ComplianceScan> ComplianceScans { get; set; } = new List<ComplianceScan>();
}

/// <summary>
/// Deployment history entity for tracking all changes
/// </summary>
[Table("DeploymentHistory")]
public class DeploymentHistory
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid DeploymentId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty; // create, update, scale, migrate, delete
    
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty; // pending, running, succeeded, failed
    
    public string? Details { get; set; } // JSON operation details
    public string? ErrorMessage { get; set; }
    
    [Required]
    [StringLength(100)]
    public string InitiatedBy { get; set; } = string.Empty;
    
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public TimeSpan? Duration { get; set; }
    
    // Navigation properties
    [ForeignKey("DeploymentId")]
    public virtual EnvironmentDeployment Deployment { get; set; } = null!;
}