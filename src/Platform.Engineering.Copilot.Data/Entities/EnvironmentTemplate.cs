using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Data.Entities;

/// <summary>
/// Environment template entity for storing reusable infrastructure templates
/// </summary>
[Table("EnvironmentTemplates")]
public class EnvironmentTemplate
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
    public bool IsMultiFile => FilesCount > 1;
    
    [NotMapped]
    public TemplateFile? EntryPointFile => Files?.FirstOrDefault(f => f.IsEntryPoint);
    
    // Navigation properties
    public virtual ICollection<EnvironmentDeployment> Deployments { get; set; } = new List<EnvironmentDeployment>();
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
    public virtual EnvironmentTemplate Template { get; set; } = null!;
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
    public virtual EnvironmentTemplate Template { get; set; } = null!;
}