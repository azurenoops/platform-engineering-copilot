using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;

namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Environment template DTO for storing reusable infrastructure templates
/// </summary>
public class EnvironmentTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty; // microservice, web-app, api, data-platform, ml-platform
    public string Version { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // JSON/YAML template content
    public string Format { get; set; } = string.Empty; // Bicep, ARM, Terraform
    public string DeploymentTier { get; set; } = string.Empty; // basic, standard, premium, enterprise
    public bool MultiRegionSupported { get; set; }
    public bool DisasterRecoverySupported { get; set; }
    public bool HighAvailabilitySupported { get; set; }
    public string? Parameters { get; set; } // JSON parameters schema
    public string? Tags { get; set; } // JSON key-value pairs
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = false;
    public string? AzureService { get; set; } // aks, webapp, function, storage, etc.
    public bool AutoScalingEnabled { get; set; } = false;
    public bool MonitoringEnabled { get; set; } = true;
    public bool BackupEnabled { get; set; } = false;
    public int FilesCount { get; set; } = 0;
    public string? MainFileType { get; set; } // bicep, yaml, terraform, etc.
    public string? Summary { get; set; } // Brief description of what files are included
    
    // Computed properties for compatibility
    public string TemplateContent => Content; // Kept for backward compatibility
    public bool IsDeleted => !IsActive;

    public IEnumerable<ServiceTemplateFile>? Files { get; set; }
    public string? CloudProvider { get; set; }
    public string? InfrastructureType { get; set; }
}

/// <summary>
/// Template version DTO for versioning support
/// </summary>
public class TemplateVersion
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ChangeLog { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsDeprecated { get; set; } = false;
}
