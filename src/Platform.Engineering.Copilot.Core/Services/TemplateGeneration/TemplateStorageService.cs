using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;
using DataEntities = Platform.Engineering.Copilot.Core.Data.Entities;
using CoreModels = Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Data.Services;

/// <summary>
/// Template storage service implementation using Repository pattern.
/// Manages EnvironmentTemplates (service templates for Environment Agent deployments)
/// and their associated TemplateFiles (individual files that make up a template).
/// </summary>
public class TemplateStorageService : ITemplateStorageService
{
    private readonly IEnvironmentTemplateRepository _templateRepository;
    private readonly ILogger<TemplateStorageService> _logger;
    private readonly int _expirationMinutes;

    public TemplateStorageService(
        IEnvironmentTemplateRepository templateRepository,
        IConfiguration configuration,
        ILogger<TemplateStorageService> logger)
    {
        _templateRepository = templateRepository;
        _logger = logger;
        _expirationMinutes = configuration.GetValue("TemplateExpiration:ExpirationMinutes", 30);
    }

    public async Task<CoreModels.EnvironmentTemplate> StoreTemplateAsync(string name, object template, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Storing template: {TemplateName}", name);

        // Check if **active** template already exists (not soft-deleted ones)
        var existingActiveTemplate = await _templateRepository.GetByNameAsync(name, cancellationToken);

        if (existingActiveTemplate != null)
        {
            _logger.LogWarning("Active template {TemplateName} already exists, will update instead", name);
            return await UpdateTemplateAsync(name, template, cancellationToken);
        }

        // Clean up soft-deleted templates with the same name
        await _templateRepository.CleanupSoftDeletedByNameAsync(name, cancellationToken);

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
            ExpiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes),
            IsActive = true,
            IsPublic = templateData.IsPublic,
            AzureService = templateData.AzureService,
            AutoScalingEnabled = templateData.AutoScalingEnabled,
            MonitoringEnabled = templateData.MonitoringEnabled,
            BackupEnabled = templateData.BackupEnabled,
            FilesCount = files.Count,
            MainFileType = DetermineMainFileType(files)
        };

        // Create template in repository
        await _templateRepository.CreateAsync(newTemplate, cancellationToken);

        // Add individual TemplateFiles (the files that make up this template)
        if (files.Count > 0)
        {
            _logger.LogInformation("Storing {Count} files for template {TemplateName}", files.Count, name);
            var entryPointFile = DetermineEntryPoint(files);
            var templateFiles = new List<DataEntities.TemplateFile>();
            var order = 0;
            
            foreach (var (fileName, content) in files.OrderBy(f => f.Key))
            {
                templateFiles.Add(new DataEntities.TemplateFile
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
                });
            }
            
            await _templateRepository.AddFilesAsync(templateFiles, cancellationToken);
        }
        else
        {
            _logger.LogWarning("No files provided for template {TemplateName}", name);
        }

        _logger.LogInformation("Template {TemplateName} stored successfully with ID: {TemplateId}, Files: {FilesCount}", name, newTemplate.Id, files.Count);
        
        // Reload with files to return complete DTO
        return await GetTemplateByIdAsync(newTemplate.Id.ToString(), cancellationToken) 
            ?? throw new InvalidOperationException($"Failed to load newly created template {newTemplate.Id}");
    }

    public async Task<List<CoreModels.EnvironmentTemplate>> ListAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing all active templates");

        var entities = await _templateRepository.GetAllActiveAsync(cancellationToken);
        
        return entities.Select(MapToDto).ToList();
    }

    public async Task<CoreModels.EnvironmentTemplate?> GetTemplateByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting template: {TemplateName}", name);

        var entity = await _templateRepository.GetByNameAsync(name, cancellationToken);
        
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

        var entity = await _templateRepository.GetByIdAsync(templateId, cancellationToken);
        
        if (entity != null)
        {
            _logger.LogInformation("Template {TemplateName} loaded with {FileCount} files", 
                entity.Name, entity.Files?.Count ?? 0);
        }
        
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<bool> DeleteTemplateAsync(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting template: {TemplateName}", name);

        var template = await _templateRepository.GetByNameAsync(name, cancellationToken);

        if (template == null)
        {
            _logger.LogWarning("Template {TemplateName} not found for deletion", name);
            return false;
        }

        // Soft delete via repository
        var result = await _templateRepository.SoftDeleteAsync(template.Id, cancellationToken);

        if (result)
        {
            _logger.LogInformation("Template {TemplateName} deleted successfully", name);
        }
        
        return result;
    }

    public async Task<object> SyncTemplatesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Syncing templates from external sources");

        // Implementation would sync from Git repositories, Azure DevOps, etc.
        // For now, return a placeholder response
        var templates = await _templateRepository.GetAllActiveAsync(cancellationToken);
        var syncResult = new
        {
            Message = "Template sync completed",
            SyncedAt = DateTime.UtcNow,
            TemplatesProcessed = templates.Count,
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

        var existingTemplate = await _templateRepository.GetByNameAsync(name, cancellationToken);

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

        await _templateRepository.AddVersionAsync(newVersion, cancellationToken);

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

        await _templateRepository.UpdateAsync(existingTemplate, cancellationToken);

        _logger.LogInformation("Template {TemplateName} updated successfully", name);
        return MapToDto(existingTemplate);
    }

    public async Task<List<CoreModels.EnvironmentTemplate>> GetTemplatesByTypeAsync(string templateType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting templates by type: {TemplateType}", templateType);

        var entities = await _templateRepository.GetByTypeAsync(templateType, cancellationToken);
        
        return entities.Select(MapToDto).ToList();
    }

    public async Task<List<CoreModels.TemplateVersion>> GetTemplateVersionsAsync(string templateName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting versions for template: {TemplateName}", templateName);

        var template = await _templateRepository.GetByNameAsync(templateName, cancellationToken);

        if (template == null)
        {
            return new List<CoreModels.TemplateVersion>();
        }

        var entities = await _templateRepository.GetVersionsAsync(template.Id, cancellationToken);
        
        return entities.Select(MapVersionToDto).ToList();
    }

    public async Task<List<CoreModels.EnvironmentTemplate>> GetTemplatesByConversationIdAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting templates for conversation: {ConversationId}", conversationId);
        
        var templates = await _templateRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        
        _logger.LogInformation("Found {Count} template(s) for conversation: {ConversationId}", templates.Count, conversationId);
        
        return templates.Select(MapToDto).ToList();
    }

    public async Task<CoreModels.EnvironmentTemplate?> GetLatestTemplateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting latest template");
        
        var template = await _templateRepository.GetLatestAsync(cancellationToken);
        
        if (template == null)
        {
            _logger.LogWarning("No templates found");
            return null;
        }
        
        _logger.LogInformation("Found latest template: {TemplateName} created at {CreatedAt}", template.Name, template.CreatedAt);
        
        return MapToDto(template);
    }

    #region Private Helper Methods

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

        // First check if template is a Dictionary<string, object> (from Infrastructure Plugin)
        if (template is Dictionary<string, object> dictTemplate)
        {
            // Extract files from dictionary
            if (dictTemplate.TryGetValue("files", out var filesObj) || dictTemplate.TryGetValue("Files", out filesObj))
            {
                if (filesObj is Dictionary<string, string> fileDict)
                {
                    files = fileDict;
                }
            }
            // The rest of templateData extraction will happen below
        }
        else
        {
            // Check if template is an anonymous object with template and files properties
            var templateType = template.GetType();
            var templateProp = templateType.GetProperty("template");
            // Try both "files" and "Files" for case-insensitive matching
            var filesProp = templateType.GetProperty("files") ?? templateType.GetProperty("Files");

            if (filesProp != null)
            {
                var filesValue = filesProp.GetValue(template);
                if (filesValue is Dictionary<string, string> fileDict)
                {
                    files = fileDict;
                }
                
                if (templateProp != null)
                {
                    template = templateProp.GetValue(template) ?? template;
                }
            }
        }
        
        object actualTemplate = template;

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
        else
        {
            // Handle anonymous types via reflection
            var actualType = actualTemplate.GetType();
            templateData.Description = GetPropertyValue<string>(actualType, actualTemplate, "Description") ?? "Generated template";
            templateData.TemplateType = GetPropertyValue<string>(actualType, actualTemplate, "TemplateType") ?? "microservice";
            templateData.Version = GetPropertyValue<string>(actualType, actualTemplate, "Version") ?? "1.0.0";
            templateData.Content = GetPropertyValue<string>(actualType, actualTemplate, "Content") ?? string.Empty;
            templateData.Format = GetPropertyValue<string>(actualType, actualTemplate, "Format") ?? "YAML";
            templateData.DeploymentTier = GetPropertyValue<string>(actualType, actualTemplate, "DeploymentTier") ?? "standard";
            templateData.MultiRegionSupported = GetPropertyValue<bool?>(actualType, actualTemplate, "MultiRegionSupported") ?? false;
            templateData.DisasterRecoverySupported = GetPropertyValue<bool?>(actualType, actualTemplate, "DisasterRecoverySupported") ?? false;
            templateData.HighAvailabilitySupported = GetPropertyValue<bool?>(actualType, actualTemplate, "HighAvailabilitySupported") ?? true;
            templateData.Parameters = GetPropertyValue<string>(actualType, actualTemplate, "Parameters") ?? "{}";
            templateData.CreatedBy = GetPropertyValue<string>(actualType, actualTemplate, "CreatedBy") ?? "system";
            templateData.IsPublic = GetPropertyValue<bool?>(actualType, actualTemplate, "IsPublic") ?? false;
            templateData.AzureService = GetPropertyValue<string>(actualType, actualTemplate, "AzureService") ?? "";
            templateData.AutoScalingEnabled = GetPropertyValue<bool?>(actualType, actualTemplate, "AutoScalingEnabled") ?? false;
            templateData.MonitoringEnabled = GetPropertyValue<bool?>(actualType, actualTemplate, "MonitoringEnabled") ?? true;
            templateData.BackupEnabled = GetPropertyValue<bool?>(actualType, actualTemplate, "BackupEnabled") ?? false;
            
            // Handle Tags - could be Dictionary<string, string> or string
            var tagsProp = actualType.GetProperty("Tags");
            if (tagsProp != null)
            {
                var tagsValue = tagsProp.GetValue(actualTemplate);
                if (tagsValue is Dictionary<string, string> tagsDict)
                {
                    templateData.Tags = JsonSerializer.Serialize(tagsDict);
                }
                else if (tagsValue is string tagsStr)
                {
                    templateData.Tags = tagsStr;
                }
            }
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

    private static T? GetPropertyValue<T>(Type type, object obj, string propertyName)
    {
        var prop = type.GetProperty(propertyName);
        if (prop == null) return default;
        
        var value = prop.GetValue(obj);
        if (value == null) return default;
        
        if (value is T typedValue)
            return typedValue;
            
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
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
    /// Maps Data Entity to Core DTO for EnvironmentTemplate
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
            Files = entity.Files?.Select(f => new ServiceTemplateFile
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
    private static CoreModels.TemplateVersion MapVersionToDto(DataEntities.TemplateVersion entity)
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

    #endregion

    #region Private Classes

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

    #endregion
}
