using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// IAC DEPLOYMENT (Infrastructure Agent) - Tracks AI-generated template deployments
/// ═══════════════════════════════════════════════════════════════════════════════
/// 
/// Purpose: Tracks deployments of AI-generated IaC templates to Azure.
/// 
/// Related: InfrastructureTemplate (the source template for this deployment)
/// 
/// ─────────────────────────────────────────────────────────────────────────────
/// DO NOT CONFUSE WITH: ProvisionedEnvironmentEntity (tracks Service Template deployments)
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
[Table("InfrastructureDeployments")]
public class InfrastructureDeployment
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
    public virtual InfrastructureTemplate? Template { get; set; }
    
    public virtual ICollection<DeploymentHistory> History { get; set; } = new List<DeploymentHistory>();
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
    public virtual InfrastructureDeployment Deployment { get; set; } = null!;
}