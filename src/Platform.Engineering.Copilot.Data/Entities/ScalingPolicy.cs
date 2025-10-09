using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Data.Entities;

/// <summary>
/// Scaling policy entity for auto-scaling configurations
/// </summary>
[Table("ScalingPolicies")]
public class ScalingPolicy
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid DeploymentId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string PolicyType { get; set; } = string.Empty; // cpu, memory, requests, custom
    
    public int MinReplicas { get; set; } = 1;
    public int MaxReplicas { get; set; } = 10;
    
    public int TargetCpuUtilization { get; set; } = 70;
    public int TargetMemoryUtilization { get; set; } = 80;
    
    [Required]
    [StringLength(20)]
    public string ScaleUpCooldown { get; set; } = "5m";
    
    [Required]
    [StringLength(20)]
    public string ScaleDownCooldown { get; set; } = "10m";
    
    public bool AutoScalingEnabled { get; set; } = false;
    public bool CostOptimizationEnabled { get; set; } = false;
    public bool TrafficBasedScalingEnabled { get; set; } = false;
    
    public string? CustomMetrics { get; set; } // JSON custom scaling metrics
    public string? Schedule { get; set; } // JSON scheduled scaling rules
    
    [Required]
    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    [ForeignKey("DeploymentId")]
    public virtual EnvironmentDeployment Deployment { get; set; } = null!;
    
    public virtual ICollection<ScalingEvent> ScalingEvents { get; set; } = new List<ScalingEvent>();
}

/// <summary>
/// Scaling event entity for tracking scaling operations
/// </summary>
[Table("ScalingEvents")]
public class ScalingEvent
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid PolicyId { get; set; }
    
    [Required]
    [StringLength(20)]
    public string EventType { get; set; } = string.Empty; // scale_up, scale_down
    
    public int PreviousReplicas { get; set; }
    public int NewReplicas { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Trigger { get; set; } = string.Empty; // cpu_threshold, memory_threshold, schedule, manual
    
    public string? TriggerDetails { get; set; } // JSON trigger metadata
    
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty; // pending, succeeded, failed
    
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public TimeSpan? Duration { get; set; }
    
    // Navigation properties
    [ForeignKey("PolicyId")]
    public virtual ScalingPolicy Policy { get; set; } = null!;
}