using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// IAC TEMPLATE (Infrastructure Agent) - AI-Generated, Temporary
/// ═══════════════════════════════════════════════════════════════════════════════
/// 
/// Purpose: Stores AI-generated Bicep/ARM/Terraform templates created on-demand.
/// 
/// Lifecycle: TEMPORARY - Default 30-minute expiry, auto-cleaned by TemplateCleanupBackgroundService.
/// 
/// Service Chain: TemplateStorageService → IInfrastructureTemplateRepository → This Entity
/// 
/// Use Case: User says "Create a storage account with private endpoints" → 
///           AI generates custom IaC → Stored here → User deploys → Template expires
/// 
/// ─────────────────────────────────────────────────────────────────────────────
/// DO NOT CONFUSE WITH: ServiceTemplateEntity (pre-approved, permanent catalog)
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
[Table("InfrastructureTemplates")]
public class InfrastructureTemplate
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string TemplateType { get; set; } = string.Empty; // microservice, web-app, api, data-platform, ml-platform
    
    [Required]
    [StringLength(20)]
    public string Version { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty; // JSON/YAML template content
    
    [Required]
    [StringLength(50)]
    public string Format { get; set; } = string.Empty; // Bicep, ARM, Terraform
    
    [Required]
    [StringLength(20)]
    public string DeploymentTier { get; set; } = string.Empty; // basic, standard, premium, enterprise
    
    public bool MultiRegionSupported { get; set; }
    public bool DisasterRecoverySupported { get; set; }
    public bool HighAvailabilitySupported { get; set; }
    
    public string? Parameters { get; set; } // JSON parameters schema
    public string? Tags { get; set; } // JSON key-value pairs
    
    [Required]
    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// When the template expires and should be auto-deleted. Null means no expiration.
    /// Default is 30 minutes from creation for generated templates.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = false;
    
    // Additional properties for Azure service integration
    [StringLength(50)]
    public string? AzureService { get; set; } // aks, webapp, function, storage, etc.
    
    public bool AutoScalingEnabled { get; set; } = false;
    public bool MonitoringEnabled { get; set; } = true;
    public bool BackupEnabled { get; set; } = false;
    
    // Multi-file template support
    public int FilesCount { get; set; } = 0;
    
    [StringLength(50)]
    public string? MainFileType { get; set; } // bicep, yaml, terraform, etc.
    
    public string? Summary { get; set; } // Brief description of what files are included
    
    // Computed properties for compatibility
    [NotMapped]
    public string TemplateContent => Content; // Kept for backward compatibility
    
    [NotMapped]
    public bool IsDeleted => !IsActive;
    
    [NotMapped]
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    
    [NotMapped]
    public int? ExpiresInMinutes => ExpiresAt.HasValue 
        ? (int)Math.Max(0, (ExpiresAt.Value - DateTime.UtcNow).TotalMinutes) 
        : null;
    
    [NotMapped]
    public bool IsMultiFile => FilesCount > 1;
    
    [NotMapped]
    public TemplateFile? EntryPointFile => Files?.FirstOrDefault(f => f.IsEntryPoint);
    
    // Navigation properties
    public virtual ICollection<InfrastructureDeployment> Deployments { get; set; } = new List<InfrastructureDeployment>();
    public virtual ICollection<TemplateVersion> Versions { get; set; } = new List<TemplateVersion>();
    public virtual ICollection<TemplateFile> Files { get; set; } = new List<TemplateFile>();
}

/// <summary>
/// Template version history entity
/// </summary>
[Table("TemplateVersions")]
public class TemplateVersion
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid TemplateId { get; set; }
    
    [Required]
    [StringLength(20)]
    public string Version { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public string? ChangeLog { get; set; }
    
    [Required]
    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    public bool IsDeprecated { get; set; } = false;
    
    // Navigation properties
    [ForeignKey("TemplateId")]
    public virtual InfrastructureTemplate Template { get; set; } = null!;
}

/// <summary>
/// Template file entity for storing individual files within a template
/// </summary>
[Table("TemplateFiles")]
public class TemplateFile
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid TemplateId { get; set; }
    
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string FileType { get; set; } = string.Empty; // bicep, yaml, json, dockerfile, markdown, etc.
    
    public bool IsEntryPoint { get; set; } = false; // true for main template files
    
    public int Order { get; set; } = 0; // deployment order
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    [ForeignKey("TemplateId")]
    public virtual InfrastructureTemplate Template { get; set; } = null!;
}