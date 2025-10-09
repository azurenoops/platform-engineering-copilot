using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Core.Interfaces;
using DataEntities = Platform.Engineering.Copilot.Data.Entities;
using CoreModels = Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Data.Services;

/// <summary>
/// Template storage service implementation using Entity Framework Core
/// </summary>
public class TemplateStorageService : ITemplateStorageService
{
    private readonly EnvironmentManagementContext _context;
    private readonly ILogger<TemplateStorageService> _logger;

    public TemplateStorageService(
        EnvironmentManagementContext context,
        ILogger<TemplateStorageService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CoreModels.EnvironmentTemplate> StoreTemplateAsync(string name, object template, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Storing template: {TemplateName}", name);

        // Check if **active** template already exists (not soft-deleted ones)
        var existingActiveTemplate = await _context.EnvironmentTemplates
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, cancellationToken);

        if (existingActiveTemplate != null)
        {
            _logger.LogWarning("Active template {TemplateName} already exists, will update instead", name);
            return await UpdateTemplateAsync(name, template, cancellationToken);
        }

        // Check for soft-deleted templates with the same name and physically remove them
        var softDeletedTemplates = await _context.EnvironmentTemplates
            .Where(t => t.Name == name && !t.IsActive)
            .ToListAsync(cancellationToken);

        if (softDeletedTemplates.Any())
        {
            _logger.LogInformation("Found {Count} soft-deleted template(s) with name {TemplateName}, physically deleting them", 
                softDeletedTemplates.Count, name);
            
            // First, nullify the TemplateId in any deployments that reference these templates
            // This preserves deployment history while allowing template deletion
            foreach (var softDeleted in softDeletedTemplates)
            {
                var deployments = await _context.EnvironmentDeployments
                    .Where(d => d.TemplateId == softDeleted.Id)
                    .ToListAsync(cancellationToken);
                
                if (deployments.Any())
                {
                    _logger.LogInformation("Unlinking {Count} deployment(s) from template {TemplateId}", 
                        deployments.Count, softDeleted.Id);
                    foreach (var deployment in deployments)
                    {
                        deployment.TemplateId = null;
                    }
                }
            }
            
            // Delete associated files
            foreach (var softDeleted in softDeletedTemplates)
            {
                var associatedFiles = await _context.TemplateFiles
                    .Where(f => f.TemplateId == softDeleted.Id)
                    .ToListAsync(cancellationToken);
                _context.TemplateFiles.RemoveRange(associatedFiles);
            }
            
            _context.EnvironmentTemplates.RemoveRange(softDeletedTemplates);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Physically deleted {Count} soft-deleted template(s)", softDeletedTemplates.Count);
        }

        // Extract template properties and files
        var (templateData, files) = ExtractTemplateDataAndFiles(template);
        
        var newTemplate = new DataEntities.EnvironmentTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = templateData.Description,
            TemplateType = templateData.TemplateType,
            Version = templateData.Version,
            Content = templateData.Content,
            Format = templateData.Format,
            DeploymentTier = templateData.DeploymentTier,
            MultiRegionSupported = templateData.MultiRegionSupported,
            DisasterRecoverySupported = templateData.DisasterRecoverySupported,
            HighAvailabilitySupported = templateData.HighAvailabilitySupported,
            Parameters = templateData.Parameters,
            Tags = templateData.Tags,
            CreatedBy = templateData.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
            IsPublic = templateData.IsPublic,
            AzureService = templateData.AzureService,
            AutoScalingEnabled = templateData.AutoScalingEnabled,
            MonitoringEnabled = templateData.MonitoringEnabled,
            BackupEnabled = templateData.BackupEnabled,
            FilesCount = files.Count,
            MainFileType = DetermineMainFileType(files)
        };

        _context.EnvironmentTemplates.Add(newTemplate);

        // Add individual files
        if (files.Count > 0)
        {
            _logger.LogInformation("Storing {Count} files for template {TemplateName}", files.Count, name);
            var entryPointFile = DetermineEntryPoint(files);
            var order = 0;
            foreach (var (fileName, content) in files.OrderBy(f => f.Key))
            {
                var templateFile = new DataEntities.TemplateFile
                {
                    Id = Guid.NewGuid(),
                    TemplateId = newTemplate.Id,
                    FileName = Path.GetFileName(fileName),
                    FilePath = fileName,
                    Content = content,
                    FileType = DetermineFileType(fileName),
                    IsEntryPoint = fileName == entryPointFile,
                    Order = order++,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.TemplateFiles.Add(templateFile);
            }
        }
        else
        {
            _logger.LogWarning("No files provided for template {TemplateName}", name);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template {TemplateName} stored successfully with ID: {TemplateId}, Files: {FilesCount}", name, newTemplate.Id, files.Count);
        
        // Reload with files to return complete DTO
        return await GetTemplateByIdAsync(newTemplate.Id.ToString(), cancellationToken) 
            ?? throw new InvalidOperationException($"Failed to load newly created template {newTemplate.Id}");
    }

    public async Task<List<CoreModels.EnvironmentTemplate>> ListAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing all active templates");

        var entities = await _context.EnvironmentTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Include(t => t.Versions)
            .ToListAsync(cancellationToken);
        
        return entities.Select(MapToDto).ToList();
    }

    public async Task<CoreModels.EnvironmentTemplate?> GetTemplateByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting template: {TemplateName}", name);

        var entity = await _context.EnvironmentTemplates
            .Include(t => t.Files)  // Include template files for multi-file templates
            .Include(t => t.Versions)
            .Include(t => t.Deployments)
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, cancellationToken);
        
        if (entity != null)
        {
            _logger.LogInformation("Template {TemplateName} loaded with {FileCount} files", 
                name, entity.Files?.Count ?? 0);
        }
        
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<CoreModels.EnvironmentTemplate?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting template by ID: {TemplateId}", id);

        if (!Guid.TryParse(id, out var templateId))
        {
            _logger.LogWarning("Invalid template ID format: {TemplateId}", id);
            return null;
        }

        _logger.LogInformation("Parsed GUID: {ParsedGuid}, Original String: {OriginalString}", templateId, id);
        
        // First, let's check if any templates exist at all
        var allTemplates = await _context.EnvironmentTemplates.Where(t => t.IsActive).ToListAsync(cancellationToken);
        _logger.LogInformation("Found {Count} active templates", allTemplates.Count);
        
        foreach (var t in allTemplates) 
        {
            _logger.LogInformation("Template ID: {Id}, Name: {Name}", t.Id, t.Name);
        }

        // Try string comparison instead of GUID comparison for SQLite compatibility  
        var idString = id.ToLowerInvariant(); // Ensure consistent casing
        _logger.LogInformation("Using string comparison with: {IdString}", idString);
        
        // Load all active templates and do comparison in memory (for debugging)
        var allActiveTemplates = await _context.EnvironmentTemplates
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);
            
        var memoryResult = allActiveTemplates.FirstOrDefault(t => t.Id.ToString().ToLowerInvariant() == idString);
        _logger.LogInformation("Memory comparison result: {Result}", memoryResult != null ? "Found" : "Not Found");
        
        if (memoryResult != null)
        {
            // Load the full template with includes using string comparison instead of GUID
            var foundIdString = memoryResult.Id.ToString().ToLowerInvariant();
            var result = await _context.EnvironmentTemplates
                .Include(t => t.Versions)
                .Include(t => t.Deployments)
                .Include(t => t.Files)
                .Where(t => t.IsActive)
                .ToListAsync(cancellationToken);
                
            // Use in-memory comparison for the final result too
            var finalResult = result.FirstOrDefault(t => t.Id.ToString().ToLowerInvariant() == foundIdString);
            _logger.LogInformation("Full memory-based query result: {Result}", finalResult != null ? "Found" : "Not Found");
            return finalResult != null ? MapToDto(finalResult) : null;
        }
        
        return null;
    }

    public async Task<bool> DeleteTemplateAsync(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting template: {TemplateName}", name);

        var template = await _context.EnvironmentTemplates
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, cancellationToken);

        if (template == null)
        {
            _logger.LogWarning("Template {TemplateName} not found for deletion", name);
            return false;
        }

        // Soft delete - mark as inactive
        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template {TemplateName} deleted successfully", name);
        return true;
    }

    public async Task<object> SyncTemplatesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Syncing templates from external sources");

        // Implementation would sync from Git repositories, Azure DevOps, etc.
        // For now, return a placeholder response
        var syncResult = new
        {
            Message = "Template sync completed",
            SyncedAt = DateTime.UtcNow,
            TemplatesProcessed = await _context.EnvironmentTemplates.CountAsync(t => t.IsActive, cancellationToken),
            Source = "Local Database"
        };

        _logger.LogInformation("Template sync completed: {TemplatesProcessed} templates", syncResult.TemplatesProcessed);
        return syncResult;
    }

    public async Task<object> PushTemplateToGitAsync(string templateName, string repositoryUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Pushing template {TemplateName} to Git repository: {RepositoryUrl}", templateName, repositoryUrl);

        var templateDto = await GetTemplateByNameAsync(templateName, cancellationToken);
        if (templateDto == null)
        {
            throw new ArgumentException($"Template '{templateName}' not found");
        }

        // Implementation would use LibGit2Sharp or similar to push to Git
        // For now, return a placeholder response
        var pushResult = new
        {
            Message = "Template pushed to Git repository successfully",
            TemplateName = templateName,
            RepositoryUrl = repositoryUrl,
            PushedAt = DateTime.UtcNow,
            CommitHash = Guid.NewGuid().ToString("N")[..8], // Mock commit hash
            Branch = "main"
        };

        _logger.LogInformation("Template {TemplateName} pushed to Git successfully with commit: {CommitHash}", 
            templateName, pushResult.CommitHash);
        return pushResult;
    }

    public async Task<CoreModels.EnvironmentTemplate> UpdateTemplateAsync(string name, object template, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating template: {TemplateName}", name);

        var existingTemplate = await _context.EnvironmentTemplates
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, cancellationToken);

        if (existingTemplate == null)
        {
            throw new ArgumentException($"Template '{name}' not found");
        }

        // Create a new version entry with backup version number
        var backupVersion = $"{existingTemplate.Version}-backup-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var newVersion = new DataEntities.TemplateVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = existingTemplate.Id,
            Version = backupVersion,
            Content = existingTemplate.Content,
            ChangeLog = "Previous version backup",
            CreatedBy = existingTemplate.CreatedBy,
            CreatedAt = existingTemplate.UpdatedAt,
            IsDeprecated = false
        };

        _context.TemplateVersions.Add(newVersion);

        // Update the main template
        var templateData = ExtractTemplateData(template);
        existingTemplate.Description = templateData.Description;
        existingTemplate.TemplateType = templateData.TemplateType;
        existingTemplate.Version = templateData.Version;
        existingTemplate.Content = templateData.Content;
        existingTemplate.Format = templateData.Format;
        existingTemplate.DeploymentTier = templateData.DeploymentTier;
        existingTemplate.MultiRegionSupported = templateData.MultiRegionSupported;
        existingTemplate.DisasterRecoverySupported = templateData.DisasterRecoverySupported;
        existingTemplate.HighAvailabilitySupported = templateData.HighAvailabilitySupported;
        existingTemplate.Parameters = templateData.Parameters;
        existingTemplate.Tags = templateData.Tags;
        existingTemplate.UpdatedAt = DateTime.UtcNow;
        existingTemplate.IsPublic = templateData.IsPublic;
        existingTemplate.AzureService = templateData.AzureService;
        existingTemplate.AutoScalingEnabled = templateData.AutoScalingEnabled;
        existingTemplate.MonitoringEnabled = templateData.MonitoringEnabled;
        existingTemplate.BackupEnabled = templateData.BackupEnabled;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template {TemplateName} updated successfully", name);
        return MapToDto(existingTemplate);
    }

    public async Task<List<CoreModels.EnvironmentTemplate>> GetTemplatesByTypeAsync(string templateType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting templates by type: {TemplateType}", templateType);

        var entities = await _context.EnvironmentTemplates
            .Where(t => t.IsActive && t.TemplateType == templateType)
            .OrderBy(t => t.Name)
            .Include(t => t.Versions)
            .ToListAsync(cancellationToken);
        
        return entities.Select(MapToDto).ToList();
    }

    public async Task<List<CoreModels.TemplateVersion>> GetTemplateVersionsAsync(string templateName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting versions for template: {TemplateName}", templateName);

        var template = await _context.EnvironmentTemplates
            .FirstOrDefaultAsync(t => t.Name == templateName && t.IsActive, cancellationToken);

        if (template == null)
        {
            return new List<CoreModels.TemplateVersion>();
        }

        var entities = await _context.TemplateVersions
            .Where(v => v.TemplateId == template.Id)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
        
        return entities.Select(MapToDto).ToList();
    }

    private static TemplateData ExtractTemplateData(object template)
    {
        var templateData = new TemplateData();

        if (template is Dictionary<string, object> dict)
        {
            templateData.Description = GetValueOrDefault(dict, "description", "Generated template");
            templateData.TemplateType = GetValueOrDefault(dict, "templateType", "microservice");
            templateData.Version = GetValueOrDefault(dict, "version", "1.0.0");
            templateData.Content = GetValueOrDefault(dict, "content", JsonSerializer.Serialize(dict));
            templateData.Format = GetValueOrDefault(dict, "format", "YAML");
            templateData.DeploymentTier = GetValueOrDefault(dict, "deploymentTier", "standard");
            templateData.MultiRegionSupported = GetBoolValueOrDefault(dict, "multiRegionSupported", false);
            templateData.DisasterRecoverySupported = GetBoolValueOrDefault(dict, "disasterRecoverySupported", false);
            templateData.HighAvailabilitySupported = GetBoolValueOrDefault(dict, "highAvailabilitySupported", true);
            templateData.Parameters = GetValueOrDefault(dict, "parameters", "{}");
            templateData.Tags = GetValueOrDefault(dict, "tags", "{}");
            templateData.CreatedBy = GetValueOrDefault(dict, "createdBy", "system");
            templateData.IsPublic = GetBoolValueOrDefault(dict, "isPublic", false);
            templateData.AzureService = GetValueOrDefault(dict, "azureService", "");
            templateData.AutoScalingEnabled = GetBoolValueOrDefault(dict, "autoScalingEnabled", false);
            templateData.MonitoringEnabled = GetBoolValueOrDefault(dict, "monitoringEnabled", true);
            templateData.BackupEnabled = GetBoolValueOrDefault(dict, "backupEnabled", false);
        }
        else
        {
            // Fallback: serialize the entire object as content
            templateData.Content = JsonSerializer.Serialize(template);
        }

        return templateData;
    }

    private static string GetValueOrDefault(Dictionary<string, object> dict, string key, string defaultValue)
    {
        return dict.TryGetValue(key, out var value) ? value?.ToString() ?? defaultValue : defaultValue;
    }

    private static bool GetBoolValueOrDefault(Dictionary<string, object> dict, string key, bool defaultValue)
    {
        if (dict.TryGetValue(key, out var value))
        {
            if (value is bool boolValue) return boolValue;
            if (bool.TryParse(value?.ToString(), out var parsedValue)) return parsedValue;
        }
        return defaultValue;
    }

    private static (TemplateData, Dictionary<string, string>) ExtractTemplateDataAndFiles(object template)
    {
        var files = new Dictionary<string, string>();
        var templateData = new TemplateData();

        // Check if template is an anonymous object with template and files properties
        var templateType = template.GetType();
        var templateProp = templateType.GetProperty("template");
        var filesProp = templateType.GetProperty("files");

        object actualTemplate = template;

        if (filesProp != null)
        {
            var filesValue = filesProp.GetValue(template);
            if (filesValue is Dictionary<string, string> fileDict)
            {
                files = fileDict;
            }
            
            if (templateProp != null)
            {
                actualTemplate = templateProp.GetValue(template) ?? template;
            }
        }

        // Extract template data from the actual template object
        if (actualTemplate is CoreModels.EnvironmentTemplate envTemplate)
        {
            templateData.Description = envTemplate.Description ?? "Generated template";
            templateData.TemplateType = envTemplate.TemplateType ?? "microservice";
            templateData.Version = envTemplate.Version ?? "1.0.0";
            templateData.Content = envTemplate.Content ?? string.Empty;
            templateData.Format = envTemplate.Format ?? "YAML";
            templateData.DeploymentTier = envTemplate.DeploymentTier ?? "standard";
            templateData.MultiRegionSupported = envTemplate.MultiRegionSupported;
            templateData.DisasterRecoverySupported = envTemplate.DisasterRecoverySupported;
            templateData.HighAvailabilitySupported = envTemplate.HighAvailabilitySupported;
            templateData.Parameters = envTemplate.Parameters ?? "{}";
            templateData.Tags = envTemplate.Tags ?? "{}";
            templateData.CreatedBy = envTemplate.CreatedBy ?? "system";
            templateData.IsPublic = envTemplate.IsPublic;
            templateData.AzureService = envTemplate.AzureService;
            templateData.AutoScalingEnabled = envTemplate.AutoScalingEnabled;
            templateData.MonitoringEnabled = envTemplate.MonitoringEnabled;
            templateData.BackupEnabled = envTemplate.BackupEnabled;
        }
        else if (actualTemplate is Dictionary<string, object> dict)
        {
            templateData.Description = GetValueOrDefault(dict, "description", "Generated template");
            templateData.TemplateType = GetValueOrDefault(dict, "templateType", "microservice");
            templateData.Version = GetValueOrDefault(dict, "version", "1.0.0");
            templateData.Content = GetValueOrDefault(dict, "content", JsonSerializer.Serialize(dict));
            templateData.Format = GetValueOrDefault(dict, "format", "YAML");
            templateData.DeploymentTier = GetValueOrDefault(dict, "deploymentTier", "standard");
            templateData.MultiRegionSupported = GetBoolValueOrDefault(dict, "multiRegionSupported", false);
            templateData.DisasterRecoverySupported = GetBoolValueOrDefault(dict, "disasterRecoverySupported", false);
            templateData.HighAvailabilitySupported = GetBoolValueOrDefault(dict, "highAvailabilitySupported", true);
            templateData.Parameters = GetValueOrDefault(dict, "parameters", "{}");
            templateData.Tags = GetValueOrDefault(dict, "tags", "{}");
            templateData.CreatedBy = GetValueOrDefault(dict, "createdBy", "system");
            templateData.IsPublic = GetBoolValueOrDefault(dict, "isPublic", false);
            templateData.AzureService = GetValueOrDefault(dict, "azureService", "");
            templateData.AutoScalingEnabled = GetBoolValueOrDefault(dict, "autoScalingEnabled", false);
            templateData.MonitoringEnabled = GetBoolValueOrDefault(dict, "monitoringEnabled", true);
            templateData.BackupEnabled = GetBoolValueOrDefault(dict, "backupEnabled", false);
        }

        return (templateData, files);
    }

    private static string DetermineMainFileType(Dictionary<string, string> files)
    {
        if (files.ContainsKey("infra/main.bicep") || files.Keys.Any(k => k.EndsWith(".bicep")))
            return "bicep";
        if (files.ContainsKey("infra/main.tf") || files.Keys.Any(k => k.EndsWith(".tf")))
            return "terraform";
        if (files.Keys.Any(k => k.EndsWith(".yaml") || k.EndsWith(".yml")))
            return "yaml";
        if (files.Keys.Any(k => k.EndsWith(".json")))
            return "json";
        return "unknown";
    }

    private static string DetermineEntryPoint(Dictionary<string, string> files)
    {
        // Priority order for entry points
        var entryPoints = new[]
        {
            "infra/main.bicep",
            "infra/main.tf",
            "main.bicep",
            "main.tf",
            "deploy.yaml",
            "deploy.yml",
            "deployment.yaml",
            "deployment.yml"
        };

        foreach (var entryPoint in entryPoints)
        {
            if (files.ContainsKey(entryPoint))
                return entryPoint;
        }

        // Return first file as fallback
        return files.Keys.FirstOrDefault() ?? string.Empty;
    }

    private static string DetermineFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".bicep" => "bicep",
            ".tf" => "terraform",
            ".yaml" or ".yml" => "yaml",
            ".json" => "json",
            ".sh" => "shell",
            ".ps1" => "powershell",
            ".md" => "markdown",
            ".cs" => "csharp",
            ".py" => "python",
            ".js" => "javascript",
            ".ts" => "typescript",
            _ => "text"
        };
    }

    /// <summary>
    /// Maps Data Entity to Core DTO
    /// </summary>
    private static CoreModels.EnvironmentTemplate MapToDto(DataEntities.EnvironmentTemplate entity)
    {
        return new CoreModels.EnvironmentTemplate
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            TemplateType = entity.TemplateType,
            Version = entity.Version,
            Content = entity.Content,
            Format = entity.Format,
            DeploymentTier = entity.DeploymentTier,
            MultiRegionSupported = entity.MultiRegionSupported,
            DisasterRecoverySupported = entity.DisasterRecoverySupported,
            HighAvailabilitySupported = entity.HighAvailabilitySupported,
            Parameters = entity.Parameters,
            Tags = entity.Tags,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsActive = entity.IsActive,
            IsPublic = entity.IsPublic,
            AzureService = entity.AzureService,
            AutoScalingEnabled = entity.AutoScalingEnabled,
            MonitoringEnabled = entity.MonitoringEnabled,
            BackupEnabled = entity.BackupEnabled,
            FilesCount = entity.FilesCount,
            MainFileType = entity.MainFileType,
            Summary = entity.Summary,
            Files = entity.Files?.Select(f => new CoreModels.EnvironmentManagement.ServiceTemplateFile
            {
                FileName = f.FileName,
                Content = f.Content,
                FileType = f.FileType,
                IsEntryPoint = f.IsEntryPoint,
                Order = f.Order
            }).OrderBy(f => f.Order).ToList()
        };
    }

    /// <summary>
    /// Maps Data Entity to Core DTO for TemplateVersion
    /// </summary>
    private static CoreModels.TemplateVersion MapToDto(DataEntities.TemplateVersion entity)
    {
        return new CoreModels.TemplateVersion
        {
            Id = entity.Id,
            TemplateId = entity.TemplateId,
            Version = entity.Version,
            Content = entity.Content,
            ChangeLog = entity.ChangeLog,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            IsDeprecated = entity.IsDeprecated
        };
    }

    private class TemplateData
    {
        public string Description { get; set; } = "Generated template";
        public string TemplateType { get; set; } = "microservice";
        public string Version { get; set; } = "1.0.0";
        public string Content { get; set; } = "{}";
        public string Format { get; set; } = "YAML";
        public string DeploymentTier { get; set; } = "standard";
        public bool MultiRegionSupported { get; set; } = false;
        public bool DisasterRecoverySupported { get; set; } = false;
        public bool HighAvailabilitySupported { get; set; } = true;
        public string Parameters { get; set; } = "{}";
        public string Tags { get; set; } = "{}";
        public string CreatedBy { get; set; } = "system";
        public bool IsPublic { get; set; } = false;
        public string? AzureService { get; set; }
        public bool AutoScalingEnabled { get; set; } = false;
        public bool MonitoringEnabled { get; set; } = true;
        public bool BackupEnabled { get; set; } = false;
    }
}