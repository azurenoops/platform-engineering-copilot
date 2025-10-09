using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Data.Entities;

/// <summary>
/// Environment metrics entity for performance tracking
/// </summary>
[Table("EnvironmentMetrics")]
public class EnvironmentMetrics
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid DeploymentId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string MetricType { get; set; } = string.Empty; // cpu, memory, requests, errors, latency
    
    [Required]
    [StringLength(50)]
    public string MetricName { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(18,4)")]
    public decimal Value { get; set; }
    
    [StringLength(20)]
    public string? Unit { get; set; } // %, MB, ms, count
    
    public DateTime Timestamp { get; set; }
    
    [StringLength(50)]
    public string? Source { get; set; } // azure-monitor, application-insights, custom
    
    public string? Labels { get; set; } // JSON key-value pairs for metric labels
    
    // Navigation properties
    [ForeignKey("DeploymentId")]
    public virtual EnvironmentDeployment Deployment { get; set; } = null!;
}

/// <summary>
/// Compliance scan entity for tracking security and compliance checks
/// </summary>
[Table("ComplianceScans")]
public class ComplianceScan
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid DeploymentId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string ScanType { get; set; } = string.Empty; // security, compliance, vulnerability
    
    [Required]
    [StringLength(50)]
    public string Standard { get; set; } = string.Empty; // nist-800-53, iso-27001, sox, hipaa, fedramp
    
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty; // running, completed, failed
    
    public int TotalChecks { get; set; }
    public int PassedChecks { get; set; }
    public int FailedChecks { get; set; }
    public int WarningChecks { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal ComplianceScore { get; set; } // 0.00 to 100.00
    
    public string? Results { get; set; } // JSON detailed scan results
    public string? Recommendations { get; set; } // JSON remediation recommendations
    
    [Required]
    [StringLength(100)]
    public string InitiatedBy { get; set; } = string.Empty;
    
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public TimeSpan? Duration { get; set; }
    
    // Navigation properties
    [ForeignKey("DeploymentId")]
    public virtual EnvironmentDeployment Deployment { get; set; } = null!;
    
    public virtual ICollection<ComplianceFinding> Findings { get; set; } = new List<ComplianceFinding>();
}

/// <summary>
/// Individual compliance finding entity
/// </summary>
[Table("ComplianceFindings")]
public class ComplianceFinding
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid ScanId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string RuleId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    [StringLength(20)]
    public string Severity { get; set; } = string.Empty; // critical, high, medium, low
    
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty; // passed, failed, warning, not_applicable
    
    [StringLength(200)]
    public string? ResourceName { get; set; }
    
    [StringLength(100)]
    public string? ControlId { get; set; } // NIST control ID, ISO control ID, etc.
    
    public string? Evidence { get; set; } // JSON evidence data
    public string? Remediation { get; set; } // Remediation guidance
    
    public bool IsRemediable { get; set; } = false;
    public bool IsAutomaticallyFixable { get; set; } = false;
    
    public DateTime DetectedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    
    // Navigation properties
    [ForeignKey("ScanId")]
    public virtual ComplianceScan Scan { get; set; } = null!;
}