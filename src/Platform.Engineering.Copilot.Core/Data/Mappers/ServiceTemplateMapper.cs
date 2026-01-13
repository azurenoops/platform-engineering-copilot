using System.Text.Json;
using Platform.Engineering.Copilot.Core.Data.Entities;
using ServiceTemplateModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.ServiceTemplate;
using TemplateFileModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.TemplateFile;
using TemplateParameterModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.TemplateParameter;
using TemplateGuardrailModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.TemplateGuardrail;
using TemplateVersionInfoModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.TemplateVersionInfo;
using GitSourceInfoModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.GitSourceInfo;
using ApprovalInfoModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.ApprovalInfo;
using TemplateAuditEntryModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.TemplateAuditEntry;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Core.Data.Mappers;

/// <summary>
/// Maps between ServiceTemplateEntity (database) and ServiceTemplate (domain model).
/// Handles JSON serialization/deserialization and comma-separated string conversions.
/// </summary>
public static class ServiceTemplateMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region Entity to Domain Model

    /// <summary>
    /// Maps a database entity to a domain model
    /// </summary>
    public static ServiceTemplateModel ToModel(this ServiceTemplateEntity entity)
    {
        return new ServiceTemplateModel
        {
            Id = entity.Id.ToString(),
            Name = entity.Name,
            DisplayName = entity.DisplayName ?? entity.Name,
            Description = entity.Description ?? string.Empty,
            Version = entity.Version,
            Category = entity.Category,
            Format = ParseFormat(entity.Format),
            Status = ParseStatus(entity.Status),
            
            // Main template content (no separate file name in entity)
            MainTemplateContent = entity.MainTemplateContent ?? string.Empty,
            AdditionalFiles = DeserializeList<TemplateFileModel>(entity.AdditionalFilesJson),
            
            // Git source
            GitSource = !string.IsNullOrEmpty(entity.GitRepositoryUrl) ? new GitSourceInfoModel
            {
                RepositoryUrl = entity.GitRepositoryUrl,
                Branch = entity.GitBranch ?? "main",
                Path = entity.GitPath ?? "/",
                AutoSync = entity.GitAutoSync
            } : null,
            GitCommitSha = entity.GitCommitSha,
            LastSyncedFromGit = entity.LastSyncedFromGit,
            
            // Parameters and guardrails
            Parameters = DeserializeList<TemplateParameterModel>(entity.ParametersJson),
            Guardrails = DeserializeList<TemplateGuardrailModel>(entity.GuardrailsJson),
            
            // Metadata - comma-separated strings to lists
            DefaultTags = DeserializeDictionary(entity.DefaultTagsJson),
            ComplianceFrameworks = SplitCommaSeparated(entity.ComplianceFrameworks),
            Keywords = SplitCommaSeparated(entity.Keywords),
            UseCases = SplitCommaSeparated(entity.UseCases),
            EnforceCompliance = entity.EnforceCompliance,
            DefaultExpirationDays = entity.DefaultExpirationDays,
            
            // AI Support
            AiSelectionHint = entity.AiSelectionHint,
            
            // Approval
            RequiresApproval = entity.RequiresApproval,
            Approval = !string.IsNullOrEmpty(entity.ApprovedBy) ? new ApprovalInfoModel
            {
                Source = ParseApprovalSource(entity.ApprovalSource),
                ApprovedBy = entity.ApprovedBy,
                ApprovedAt = entity.ApprovedAt ?? DateTime.UtcNow,
                ApprovalComments = entity.ApprovalComments,
                ExternalApprovalId = entity.ExternalApprovalId,
                ExternalApprovalUrl = entity.ExternalApprovalUrl
            } : null,
            
            // Usage stats
            DeploymentCount = entity.DeploymentCount,
            LastDeployedAt = entity.LastDeployedAt,
            
            // Version history
            VersionHistory = DeserializeList<TemplateVersionInfoModel>(entity.VersionHistoryJson),
            
            // Audit trail
            CreatedBy = entity.CreatedBy ?? "system",
            CreatedAt = entity.CreatedAt,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
    }

    /// <summary>
    /// Maps a list of entities to domain models
    /// </summary>
    public static IReadOnlyList<ServiceTemplateModel> ToModels(this IEnumerable<ServiceTemplateEntity> entities)
    {
        return entities.Select(e => e.ToModel()).ToList();
    }

    #endregion

    #region Domain Model to Entity

    /// <summary>
    /// Maps a domain model to a database entity
    /// </summary>
    public static ServiceTemplateEntity ToEntity(this ServiceTemplateModel model)
    {
        var entity = new ServiceTemplateEntity
        {
            Id = Guid.TryParse(model.Id, out var id) ? id : Guid.NewGuid(),
            Name = model.Name,
            DisplayName = model.DisplayName,
            Description = model.Description,
            Version = model.Version,
            Category = model.Category,
            Format = model.Format.ToString(),
            Status = model.Status.ToString(),
            
            // Main template content
            MainTemplateContent = model.MainTemplateContent,
            AdditionalFilesJson = SerializeToJson(model.AdditionalFiles),
            
            // Git source
            GitRepositoryUrl = model.GitSource?.RepositoryUrl,
            GitBranch = model.GitSource?.Branch,
            GitPath = model.GitSource?.Path,
            GitCommitSha = model.GitCommitSha,
            LastSyncedFromGit = model.LastSyncedFromGit,
            GitAutoSync = model.GitSource?.AutoSync ?? true,
            
            // Parameters and guardrails
            ParametersJson = SerializeToJson(model.Parameters),
            GuardrailsJson = SerializeToJson(model.Guardrails),
            
            // Metadata - lists to comma-separated strings
            DefaultTagsJson = SerializeToJson(model.DefaultTags),
            ComplianceFrameworks = JoinToCommaSeparated(model.ComplianceFrameworks),
            Keywords = JoinToCommaSeparated(model.Keywords),
            UseCases = JoinToCommaSeparated(model.UseCases),
            EnforceCompliance = model.EnforceCompliance,
            DefaultExpirationDays = model.DefaultExpirationDays,
            
            // AI Support
            AiSelectionHint = model.AiSelectionHint,
            
            // Approval
            RequiresApproval = model.RequiresApproval,
            ApprovalSource = model.Approval?.Source.ToString(),
            ApprovedBy = model.Approval?.ApprovedBy,
            ApprovedAt = model.Approval?.ApprovedAt,
            ApprovalComments = model.Approval?.ApprovalComments,
            ExternalApprovalId = model.Approval?.ExternalApprovalId,
            ExternalApprovalUrl = model.Approval?.ExternalApprovalUrl,
            
            // Usage stats
            DeploymentCount = model.DeploymentCount,
            LastDeployedAt = model.LastDeployedAt,
            
            // Version history
            VersionHistoryJson = SerializeToJson(model.VersionHistory),
            
            // Audit trail
            CreatedBy = model.CreatedBy,
            CreatedAt = model.CreatedAt,
            UpdatedBy = model.UpdatedBy,
            UpdatedAt = model.UpdatedAt
        };

        return entity;
    }

    /// <summary>
    /// Updates an existing entity from a domain model (preserves entity tracking)
    /// </summary>
    public static void UpdateFromModel(this ServiceTemplateEntity entity, ServiceTemplateModel model)
    {
        entity.Name = model.Name;
        entity.DisplayName = model.DisplayName;
        entity.Description = model.Description;
        entity.Version = model.Version;
        entity.Category = model.Category;
        entity.Format = model.Format.ToString();
        entity.Status = model.Status.ToString();
        
        // Main template content
        entity.MainTemplateContent = model.MainTemplateContent;
        entity.AdditionalFilesJson = SerializeToJson(model.AdditionalFiles);
        
        // Git source
        entity.GitRepositoryUrl = model.GitSource?.RepositoryUrl;
        entity.GitBranch = model.GitSource?.Branch;
        entity.GitPath = model.GitSource?.Path;
        entity.GitCommitSha = model.GitCommitSha;
        entity.LastSyncedFromGit = model.LastSyncedFromGit;
        entity.GitAutoSync = model.GitSource?.AutoSync ?? true;
        
        // Parameters and guardrails
        entity.ParametersJson = SerializeToJson(model.Parameters);
        entity.GuardrailsJson = SerializeToJson(model.Guardrails);
        
        // Metadata
        entity.DefaultTagsJson = SerializeToJson(model.DefaultTags);
        entity.ComplianceFrameworks = JoinToCommaSeparated(model.ComplianceFrameworks);
        entity.Keywords = JoinToCommaSeparated(model.Keywords);
        entity.UseCases = JoinToCommaSeparated(model.UseCases);
        entity.EnforceCompliance = model.EnforceCompliance;
        entity.DefaultExpirationDays = model.DefaultExpirationDays;
        
        // AI Support
        entity.AiSelectionHint = model.AiSelectionHint;
        
        // Approval
        entity.RequiresApproval = model.RequiresApproval;
        entity.ApprovalSource = model.Approval?.Source.ToString();
        entity.ApprovedBy = model.Approval?.ApprovedBy;
        entity.ApprovedAt = model.Approval?.ApprovedAt;
        entity.ApprovalComments = model.Approval?.ApprovalComments;
        entity.ExternalApprovalId = model.Approval?.ExternalApprovalId;
        entity.ExternalApprovalUrl = model.Approval?.ExternalApprovalUrl;
        
        // Usage stats
        entity.DeploymentCount = model.DeploymentCount;
        entity.LastDeployedAt = model.LastDeployedAt;
        
        // Version history
        entity.VersionHistoryJson = SerializeToJson(model.VersionHistory);
        
        // Audit trail
        entity.UpdatedBy = model.UpdatedBy;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Audit Entry Mapping

    /// <summary>
    /// Creates an audit entity from parameters
    /// </summary>
    public static ServiceTemplateAuditEntity CreateAuditEntry(
        Guid entityId,
        string entityName,
        string action,
        string performedBy,
        string? details = null,
        string? oldValue = null,
        string? newValue = null)
    {
        return new ServiceTemplateAuditEntity
        {
            Id = Guid.NewGuid(),
            EntityType = "ServiceTemplate",
            EntityId = entityId,
            EntityName = entityName,
            Action = action,
            PerformedBy = performedBy,
            Details = details,
            OldValuesJson = oldValue,
            NewValuesJson = newValue,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps audit entity to TemplateAuditEntry model
    /// </summary>
    public static TemplateAuditEntryModel ToAuditModel(this ServiceTemplateAuditEntity entity)
    {
        return new TemplateAuditEntryModel
        {
            Id = entity.Id.ToString(),
            Timestamp = entity.Timestamp,
            EntityType = entity.EntityType,
            EntityId = entity.EntityId.ToString(),
            EntityName = entity.EntityName,
            Action = entity.Action,
            PerformedBy = entity.PerformedBy,
            Details = entity.Details
        };
    }

    #endregion

    #region Private Helpers

    private static TemplateFormat ParseFormat(string format)
    {
        return Enum.TryParse<TemplateFormat>(format, ignoreCase: true, out var result) 
            ? result 
            : TemplateFormat.Bicep;
    }

    private static TemplateStatus ParseStatus(string status)
    {
        return Enum.TryParse<TemplateStatus>(status, ignoreCase: true, out var result) 
            ? result 
            : TemplateStatus.Draft;
    }

    private static ApprovalSource ParseApprovalSource(string? source)
    {
        if (string.IsNullOrEmpty(source)) return ApprovalSource.Internal;
        return Enum.TryParse<ApprovalSource>(source, ignoreCase: true, out var result) 
            ? result 
            : ApprovalSource.Internal;
    }

    private static List<T> DeserializeList<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    private static List<string> SplitCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static string? JoinToCommaSeparated(List<string>? values)
    {
        if (values == null || values.Count == 0) return null;
        return string.Join(",", values);
    }

    private static Dictionary<string, string> DeserializeDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) 
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static string? SerializeToJson<T>(T? value)
    {
        if (value == null) return null;
        if (value is System.Collections.ICollection collection && collection.Count == 0) return null;
        try
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
