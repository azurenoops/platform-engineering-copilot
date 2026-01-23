using System.Text.Json;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using ProvisionedEnvironmentModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.ProvisionedEnvironment;
using DeployedResourceModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.DeployedResource;
using DriftItemModel = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.DriftItem;
using EnvironmentStatusEnum = Platform.Engineering.Copilot.Core.Models.ServiceTemplates.EnvironmentStatus;

namespace Platform.Engineering.Copilot.Core.Data.Mappers;

/// <summary>
/// Maps between ProvisionedEnvironmentEntity (database) and ProvisionedEnvironment (domain model).
/// Handles JSON serialization/deserialization for complex properties.
/// </summary>
public static class ProvisionedEnvironmentMapper
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
    public static ProvisionedEnvironmentModel ToModel(this ProvisionedEnvironmentEntity entity)
    {
        var parameterValues = DeserializeDictionary<object>(entity.ParameterValuesJson);
        var deployedResources = DeserializeList<DeployedResourceModel>(entity.DeployedResourcesJson);
        var driftItems = DeserializeList<DriftItemModel>(entity.DriftItemsJson);

        return new ProvisionedEnvironmentModel
        {
            Id = entity.Id.ToString(),
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            
            // Template Reference
            TemplateId = entity.TemplateId.ToString(),
            TemplateName = entity.TemplateName,
            TemplateVersion = entity.TemplateVersion,
            
            // Azure Location
            SubscriptionId = entity.SubscriptionId,
            ResourceGroup = entity.ResourceGroup,
            ResourceGroupName = entity.ResourceGroup,
            Location = entity.Location,
            
            // Parameters
            ParameterValues = parameterValues,
            Parameters = parameterValues,
            
            // Tags
            Tags = DeserializeDictionary<string>(entity.TagsJson),
            
            // Deployed Resources
            Resources = deployedResources,
            DeployedResources = deployedResources,
            
            // Status
            Status = ParseStatus(entity.Status),
            StatusMessage = entity.StatusMessage,
            DeploymentId = entity.DeploymentId,
            DeploymentDurationMinutes = entity.DeploymentDurationMinutes,
            
            // Owner
            OwnerEmail = entity.OwnerEmail,
            
            // Cloning
            ClonedFromId = entity.ClonedFromId?.ToString(),
            
            // Lifecycle
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt,
            DeletedBy = entity.DeletedBy,
            DeletedAt = entity.DeletedAt,
            
            // Drift Detection
            LastDriftCheck = entity.LastDriftCheck,
            HasDrift = entity.HasDrift,
            DriftCount = entity.DriftCount,
            DriftItems = driftItems,
            
            // Costs
            EstimatedMonthlyCost = entity.EstimatedMonthlyCost,
            ActualMonthlyCost = entity.ActualMonthlyCost,
            
            // Expiration
            ExpiresAt = entity.ExpiresAt,
            AutoDelete = entity.AutoDelete
        };
    }

    /// <summary>
    /// Maps a list of entities to domain models
    /// </summary>
    public static IReadOnlyList<ProvisionedEnvironmentModel> ToModels(this IEnumerable<ProvisionedEnvironmentEntity> entities)
    {
        return entities.Select(e => e.ToModel()).ToList();
    }

    #endregion

    #region Domain Model to Entity

    /// <summary>
    /// Maps a domain model to a database entity
    /// </summary>
    public static ProvisionedEnvironmentEntity ToEntity(this ProvisionedEnvironmentModel model)
    {
        // Use ParameterValues or Parameters
        var parameters = model.ParameterValues.Count > 0 ? model.ParameterValues : model.Parameters ?? new Dictionary<string, object>();
        
        // Use Resources or DeployedResources
        var resources = model.Resources.Count > 0 ? model.Resources : model.DeployedResources ?? new List<DeployedResourceModel>();

        var entity = new ProvisionedEnvironmentEntity
        {
            Id = Guid.TryParse(model.Id, out var id) ? id : Guid.NewGuid(),
            Name = model.Name,
            DisplayName = model.DisplayName,
            Description = model.Description,
            
            // Template Reference
            TemplateId = Guid.TryParse(model.TemplateId, out var templateId) ? templateId : Guid.Empty,
            TemplateName = model.TemplateName,
            TemplateVersion = model.TemplateVersion,
            
            // Azure Location - use ResourceGroupName or ResourceGroup
            SubscriptionId = model.SubscriptionId,
            ResourceGroup = !string.IsNullOrEmpty(model.ResourceGroupName) ? model.ResourceGroupName : model.ResourceGroup,
            Location = model.Location,
            
            // Parameters - JSON serialized
            ParameterValuesJson = SerializeToJson(parameters),
            
            // Tags - JSON serialized
            TagsJson = SerializeToJson(model.Tags),
            
            // Deployed Resources - JSON serialized
            DeployedResourcesJson = SerializeToJson(resources),
            
            // Status
            Status = model.Status.ToString(),
            StatusMessage = model.StatusMessage,
            DeploymentId = model.DeploymentId,
            DeploymentDurationMinutes = model.DeploymentDurationMinutes,
            
            // Owner
            OwnerEmail = model.OwnerEmail,
            
            // Cloning
            ClonedFromId = Guid.TryParse(model.ClonedFromId, out var clonedFromId) ? clonedFromId : null,
            
            // Lifecycle
            CreatedBy = model.CreatedBy,
            CreatedAt = model.CreatedAt,
            UpdatedBy = model.UpdatedBy,
            UpdatedAt = model.UpdatedAt,
            DeletedBy = model.DeletedBy,
            DeletedAt = model.DeletedAt,
            IsDeleted = model.Status == EnvironmentStatusEnum.Deleted,
            
            // Drift Detection
            LastDriftCheck = model.LastDriftCheck,
            HasDrift = model.HasDrift,
            DriftCount = model.DriftCount,
            DriftItemsJson = SerializeToJson(model.DriftItems ?? new List<DriftItemModel>()),
            
            // Costs
            EstimatedMonthlyCost = model.EstimatedMonthlyCost,
            ActualMonthlyCost = model.ActualMonthlyCost,
            
            // Expiration
            ExpiresAt = model.ExpiresAt,
            AutoDelete = model.AutoDelete
        };

        return entity;
    }

    /// <summary>
    /// Updates an existing entity from a domain model
    /// </summary>
    public static void UpdateFromModel(this ProvisionedEnvironmentEntity entity, ProvisionedEnvironmentModel model)
    {
        // Use ParameterValues or Parameters
        var parameters = model.ParameterValues.Count > 0 ? model.ParameterValues : model.Parameters ?? new Dictionary<string, object>();
        
        // Use Resources or DeployedResources
        var resources = model.Resources.Count > 0 ? model.Resources : model.DeployedResources ?? new List<DeployedResourceModel>();

        entity.Name = model.Name;
        entity.DisplayName = model.DisplayName;
        entity.Description = model.Description;
        
        // Template Reference
        entity.TemplateId = Guid.TryParse(model.TemplateId, out var templateId) ? templateId : entity.TemplateId;
        entity.TemplateName = model.TemplateName;
        entity.TemplateVersion = model.TemplateVersion;
        
        // Azure Location
        entity.SubscriptionId = model.SubscriptionId;
        entity.ResourceGroup = !string.IsNullOrEmpty(model.ResourceGroupName) ? model.ResourceGroupName : model.ResourceGroup;
        entity.Location = model.Location;
        
        // Parameters
        entity.ParameterValuesJson = SerializeToJson(parameters);
        
        // Tags
        entity.TagsJson = SerializeToJson(model.Tags);
        
        // Deployed Resources
        entity.DeployedResourcesJson = SerializeToJson(resources);
        
        // Status
        entity.Status = model.Status.ToString();
        entity.StatusMessage = model.StatusMessage;
        entity.DeploymentId = model.DeploymentId;
        entity.DeploymentDurationMinutes = model.DeploymentDurationMinutes;
        
        // Owner
        entity.OwnerEmail = model.OwnerEmail;
        
        // Lifecycle
        entity.UpdatedBy = model.UpdatedBy;
        entity.UpdatedAt = DateTime.UtcNow;
        
        // Drift Detection
        entity.LastDriftCheck = model.LastDriftCheck;
        entity.HasDrift = model.HasDrift;
        entity.DriftCount = model.DriftCount;
        entity.DriftItemsJson = SerializeToJson(model.DriftItems ?? new List<DriftItemModel>());
        
        // Costs
        entity.EstimatedMonthlyCost = model.EstimatedMonthlyCost;
        entity.ActualMonthlyCost = model.ActualMonthlyCost;
        
        // Expiration
        entity.ExpiresAt = model.ExpiresAt;
        entity.AutoDelete = model.AutoDelete;
    }

    #endregion

    #region Audit Entry Mapping

    /// <summary>
    /// Maps a domain audit entry to an entity
    /// </summary>
    public static EnvironmentAuditEntity ToEntity(this EnvironmentAuditEntry entry, Guid environmentId)
    {
        return new EnvironmentAuditEntity
        {
            Id = Guid.TryParse(entry.Id, out var id) ? id : Guid.NewGuid(),
            EnvironmentId = environmentId,
            Action = entry.Action,
            PerformedBy = entry.PerformedBy,
            Details = entry.Details,
            MetadataJson = entry.Metadata.Count > 0 ? SerializeToJson(entry.Metadata) : null,
            Timestamp = entry.Timestamp
        };
    }

    /// <summary>
    /// Maps an entity audit entry to a domain model
    /// </summary>
    public static EnvironmentAuditEntry ToModel(this EnvironmentAuditEntity entity)
    {
        return new EnvironmentAuditEntry
        {
            Id = entity.Id.ToString(),
            Timestamp = entity.Timestamp,
            EnvironmentId = entity.EnvironmentId.ToString(),
            Action = entity.Action,
            PerformedBy = entity.PerformedBy,
            Details = entity.Details,
            Metadata = DeserializeDictionary<object>(entity.MetadataJson)
        };
    }

    /// <summary>
    /// Maps a list of audit entities to domain models
    /// </summary>
    public static List<EnvironmentAuditEntry> ToModels(this IEnumerable<EnvironmentAuditEntity> entities)
    {
        return entities.Select(e => e.ToModel()).ToList();
    }

    #endregion

    #region Helper Methods

    private static EnvironmentStatusEnum ParseStatus(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return EnvironmentStatusEnum.Provisioning;

        return status.ToLowerInvariant() switch
        {
            "provisioning" => EnvironmentStatusEnum.Provisioning,
            "running" => EnvironmentStatusEnum.Running,
            "updating" => EnvironmentStatusEnum.Updating,
            "scaling" => EnvironmentStatusEnum.Scaling,
            "stopped" => EnvironmentStatusEnum.Stopped,
            "failed" => EnvironmentStatusEnum.Failed,
            "deleting" => EnvironmentStatusEnum.Deleting,
            "deleted" => EnvironmentStatusEnum.Deleted,
            _ => Enum.TryParse<EnvironmentStatusEnum>(status, true, out var parsed) ? parsed : EnvironmentStatusEnum.Provisioning
        };
    }

    private static Dictionary<string, T> DeserializeDictionary<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, T>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, T>>(json, JsonOptions) 
                ?? new Dictionary<string, T>();
        }
        catch
        {
            return new Dictionary<string, T>();
        }
    }

    private static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<T>();

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) 
                ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    private static string? SerializeToJson<T>(T obj)
    {
        if (obj == null)
            return null;

        try
        {
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            return json == "null" || json == "{}" || json == "[]" ? null : json;
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
