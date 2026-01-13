using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Mappers;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Agents.Environments.Services;

/// <summary>
/// Database-backed implementation of the Provisioned Environment Service.
/// Manages environment lifecycle, scaling, cloning, and drift detection.
/// Uses EF Core repository for persistence.
/// </summary>
public class ProvisionedEnvironmentService : IProvisionedEnvironmentService
{
    private readonly ILogger<ProvisionedEnvironmentService> _logger;
    private readonly IServiceTemplateCatalogService _templateCatalog;
    private readonly IProvisionedEnvironmentRepository _repository;

    public ProvisionedEnvironmentService(
        ILogger<ProvisionedEnvironmentService> logger,
        IServiceTemplateCatalogService templateCatalog,
        IProvisionedEnvironmentRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateCatalog = templateCatalog ?? throw new ArgumentNullException(nameof(templateCatalog));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    #region Environment Lifecycle

    public async Task<CreateEnvironmentResult> CreateFromTemplateAsync(
        CreateEnvironmentFromTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new CreateEnvironmentResult();

        // Get template
        var template = await _templateCatalog.GetTemplateAsync(request.TemplateId, cancellationToken);
        if (template == null)
        {
            result.Errors = new List<string> { $"Template {request.TemplateId} not found" };
            return result;
        }

        if (template.Status != TemplateStatus.Published)
        {
            result.Errors = new List<string> { $"Template {template.Name} is not published (status: {template.Status})" };
            return result;
        }

        // Validate parameters
        var validationErrors = await _templateCatalog.ValidateParametersAsync(request.TemplateId, request.Parameters, cancellationToken);
        if (validationErrors.Any())
        {
            result.Errors = validationErrors.ToList();
            return result;
        }

        // Check guardrails
        var guardrailViolations = await _templateCatalog.CheckGuardrailsAsync(request.TemplateId, request.Parameters, cancellationToken);
        var denyViolations = guardrailViolations.Where(v => v.Action == GuardrailAction.Deny).ToList();
        if (denyViolations.Any())
        {
            result.Errors = denyViolations.Select(v => v.Message).ToList();
            result.GuardrailViolations = denyViolations;
            return result;
        }

        // Warnings for non-blocking violations
        result.GuardrailViolations = guardrailViolations.Where(v => v.Action != GuardrailAction.Deny).ToList();

        // Check name uniqueness
        var isUnique = await _repository.IsNameUniqueAsync(request.EnvironmentName, cancellationToken: cancellationToken);
        if (!isUnique)
        {
            result.Errors = new List<string> { $"Environment name '{request.EnvironmentName}' is already in use" };
            return result;
        }

        // Create environment
        var environment = new ProvisionedEnvironment
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.EnvironmentName,
            DisplayName = request.DisplayName ?? request.EnvironmentName,
            Description = request.Description,
            TemplateId = request.TemplateId,
            TemplateName = template.Name,
            TemplateVersion = template.Version,
            SubscriptionId = request.SubscriptionId ?? string.Empty,
            ResourceGroup = request.ResourceGroup ?? request.ResourceGroupName ?? string.Empty,
            ResourceGroupName = request.ResourceGroupName ?? request.ResourceGroup ?? string.Empty,
            Location = request.Location,
            Status = EnvironmentStatus.Provisioning,
            CreatedBy = request.RequestedBy ?? "system",
            CreatedAt = DateTime.UtcNow,
            ParameterValues = request.Parameters,
            Parameters = request.Parameters,
            Tags = MergeTags(template.DefaultTags, request.Tags ?? request.AdditionalTags, request.EnvironmentName, template.Id),
            ExpiresAt = request.ExpiresAt ?? DateTime.UtcNow.AddDays(template.DefaultExpirationDays ?? 365),
            AutoDelete = request.AutoDelete,
            OwnerEmail = request.OwnerEmail
        };

        // TODO: Actually deploy resources using ARM/Bicep
        // For now, simulate deployment
        await SimulateDeploymentAsync(environment, template, cancellationToken);

        // Save to database
        var entity = environment.ToEntity();
        await _repository.CreateAsync(entity, cancellationToken);

        // Add audit entry
        await AddAuditEntryAsync(Guid.Parse(environment.Id), "Created", request.RequestedBy ?? "system", 
            $"Created from template {template.Name}", cancellationToken);

        // Increment template deployment count
        template.DeploymentCount++;

        result.Success = true;
        result.Environment = environment;
        result.EnvironmentId = environment.Id;
        result.EnvironmentName = environment.Name;
        result.DeploymentId = Guid.NewGuid().ToString();
        result.Status = environment.Status;

        _logger.LogInformation("🚀 Created environment {Name} (ID: {Id}) from template {Template} by {CreatedBy}",
            environment.Name, environment.Id, template.Name, request.RequestedBy);

        return result;
    }

    public async Task<ProvisionedEnvironment?> GetEnvironmentAsync(string environmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(environmentId, cancellationToken);
        return entity?.ToModel();
    }

    public async Task<ProvisionedEnvironment?> GetEnvironmentByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByNameAsync(name, cancellationToken);
        return entity?.ToModel();
    }

    public async Task<List<ProvisionedEnvironment>> SearchEnvironmentsAsync(EnvironmentSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        Guid? templateId = null;
        if (!string.IsNullOrEmpty(criteria.TemplateId) && Guid.TryParse(criteria.TemplateId, out var tid))
            templateId = tid;

        var entities = await _repository.SearchAsync(
            keyword: criteria.Keyword,
            templateId: templateId,
            subscriptionId: criteria.SubscriptionId,
            status: criteria.Status?.ToString(),
            ownerEmail: criteria.OwnerEmail,
            includeDeleted: false,
            skip: 0,
            take: 100,
            cancellationToken: cancellationToken);

        var results = entities.ToModels().ToList();

        // Additional in-memory filtering for criteria not handled by repository
        if (!string.IsNullOrEmpty(criteria.ResourceGroupName))
            results = results.Where(e => e.ResourceGroupName?.Equals(criteria.ResourceGroupName, StringComparison.OrdinalIgnoreCase) == true).ToList();

        if (criteria.HasDrift.HasValue)
            results = results.Where(e => e.HasDrift == criteria.HasDrift.Value).ToList();

        if (criteria.TagFilters != null && criteria.TagFilters.Count > 0)
        {
            results = results.Where(e =>
            {
                foreach (var tag in criteria.TagFilters)
                {
                    if (!e.Tags.TryGetValue(tag.Key, out var value) || value != tag.Value)
                        return false;
                }
                return true;
            }).ToList();
        }

        if (criteria.CreatedAfter.HasValue)
            results = results.Where(e => e.CreatedAt >= criteria.CreatedAfter.Value).ToList();

        if (criteria.CreatedBefore.HasValue)
            results = results.Where(e => e.CreatedAt <= criteria.CreatedBefore.Value).ToList();

        return results;
    }

    public async Task<List<ProvisionedEnvironment>> ListEnvironmentsAsync(string? subscriptionId = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Platform.Engineering.Copilot.Core.Data.Entities.ProvisionedEnvironmentEntity> entities;

        if (!string.IsNullOrEmpty(subscriptionId))
            entities = await _repository.GetBySubscriptionAsync(subscriptionId, cancellationToken);
        else
            entities = await _repository.GetAllAsync(cancellationToken);

        return entities.ToModels().ToList();
    }

    public async Task<ProvisionedEnvironment> UpdateEnvironmentAsync(
        ProvisionedEnvironment environment,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(environment.Id, out var envId))
            throw new ArgumentException("Invalid environment ID");

        var entity = await _repository.GetByIdAsync(envId, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Environment {environment.Id} not found");

        environment.UpdatedBy = updatedBy;
        environment.UpdatedAt = DateTime.UtcNow;

        entity.UpdateFromModel(environment);
        await _repository.UpdateAsync(entity, cancellationToken);

        await AddAuditEntryAsync(envId, "Updated", updatedBy, null, cancellationToken);

        _logger.LogInformation("📝 Updated environment {Name} by {UpdatedBy}", environment.Name, updatedBy);

        return environment;
    }

    public async Task<bool> DeleteEnvironmentAsync(
        string environmentId,
        string deletedBy,
        bool forceDelete = false,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(environmentId, out var envId))
            return false;

        var entity = await _repository.GetByIdAsync(envId, cancellationToken);
        if (entity == null)
            return false;

        // TODO: Actually delete Azure resources
        // For now, just soft delete

        await AddAuditEntryAsync(envId, "Deleted", deletedBy, forceDelete ? "Force deleted" : "Soft deleted", cancellationToken);

        if (forceDelete)
        {
            await _repository.HardDeleteAsync(envId, cancellationToken);
            _logger.LogWarning("🗑️ Hard deleted environment {Name} by {DeletedBy}", entity.Name, deletedBy);
        }
        else
        {
            await _repository.SoftDeleteAsync(envId, deletedBy, cancellationToken);
            _logger.LogInformation("🗑️ Soft deleted environment {Name} by {DeletedBy}", entity.Name, deletedBy);
        }

        return true;
    }

    #endregion

    #region Environment Operations

    public async Task<ScaleEnvironmentResult> ScaleEnvironmentAsync(
        ScaleEnvironmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new ScaleEnvironmentResult();

        if (!Guid.TryParse(request.EnvironmentId, out var envId))
        {
            result.Errors = new List<string> { "Invalid environment ID" };
            return result;
        }

        var entity = await _repository.GetByIdAsync(envId, cancellationToken);
        if (entity == null)
        {
            result.Errors = new List<string> { $"Environment {request.EnvironmentId} not found" };
            return result;
        }

        var environment = entity.ToModel();

        if (environment.Status != EnvironmentStatus.Running)
        {
            result.Errors = new List<string> { $"Cannot scale environment in status {environment.Status}" };
            return result;
        }

        // Check guardrails for new parameters
        var scaleParams = request.ScalingParameters ?? request.AdditionalParameters ?? new Dictionary<string, object>();
        var guardrailViolations = await _templateCatalog.CheckGuardrailsAsync(
            environment.TemplateId, scaleParams, cancellationToken);
        var denyViolations = guardrailViolations.Where(v => v.Action == GuardrailAction.Deny).ToList();
        if (denyViolations.Any())
        {
            result.Errors = denyViolations.Select(v => v.Message).ToList();
            result.GuardrailViolations = denyViolations;
            return result;
        }

        // Update environment
        var previousStatus = environment.Status;
        environment.Status = EnvironmentStatus.Scaling;

        // Merge scale parameters
        foreach (var param in scaleParams)
        {
            environment.ParameterValues[param.Key] = param.Value;
            if (environment.Parameters != null)
                environment.Parameters[param.Key] = param.Value;
        }

        // TODO: Actually scale resources
        await Task.Delay(100, cancellationToken); // Simulate scaling

        environment.Status = EnvironmentStatus.Running;
        environment.UpdatedBy = request.ScaledBy;
        environment.UpdatedAt = DateTime.UtcNow;

        entity.UpdateFromModel(environment);
        await _repository.UpdateAsync(entity, cancellationToken);

        await AddAuditEntryAsync(envId, "Scaled", request.ScaledBy ?? "system", 
            $"Scaled with parameters: {string.Join(", ", scaleParams.Select(p => $"{p.Key}={p.Value}"))}", 
            cancellationToken);

        result.Success = true;
        result.Environment = environment;
        result.Message = $"Environment scaled successfully";

        _logger.LogInformation("📈 Scaled environment {Name} by {ScaledBy}", environment.Name, request.ScaledBy);

        return result;
    }

    public async Task<CreateEnvironmentResult> CloneEnvironmentAsync(
        string sourceEnvironmentId,
        string newName,
        string clonedBy,
        CancellationToken cancellationToken = default)
    {
        var result = new CreateEnvironmentResult();

        if (!Guid.TryParse(sourceEnvironmentId, out var sourceId))
        {
            result.Errors = new List<string> { "Invalid source environment ID" };
            return result;
        }

        var sourceEntity = await _repository.GetByIdAsync(sourceId, cancellationToken);
        if (sourceEntity == null)
        {
            result.Errors = new List<string> { $"Source environment {sourceEnvironmentId} not found" };
            return result;
        }

        var source = sourceEntity.ToModel();

        // Check name uniqueness
        var isUnique = await _repository.IsNameUniqueAsync(newName, cancellationToken: cancellationToken);
        if (!isUnique)
        {
            result.Errors = new List<string> { $"Environment name '{newName}' is already in use" };
            return result;
        }

        // Create cloned environment
        var cloned = new ProvisionedEnvironment
        {
            Id = Guid.NewGuid().ToString(),
            Name = newName,
            DisplayName = $"{source.DisplayName} (Clone)",
            Description = $"Cloned from {source.Name}",
            TemplateId = source.TemplateId,
            TemplateName = source.TemplateName,
            TemplateVersion = source.TemplateVersion,
            SubscriptionId = source.SubscriptionId,
            ResourceGroup = source.ResourceGroup,
            ResourceGroupName = source.ResourceGroupName,
            Location = source.Location,
            ParameterValues = new Dictionary<string, object>(source.ParameterValues),
            Parameters = source.Parameters != null ? new Dictionary<string, object>(source.Parameters) : null,
            Tags = new Dictionary<string, string>(source.Tags),
            Status = EnvironmentStatus.Provisioning,
            ClonedFromId = source.Id,
            CreatedBy = clonedBy,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            OwnerEmail = source.OwnerEmail
        };

        // Get template for simulation
        var template = await _templateCatalog.GetTemplateAsync(source.TemplateId, cancellationToken);
        if (template != null)
        {
            await SimulateDeploymentAsync(cloned, template, cancellationToken);
        }
        else
        {
            cloned.Status = EnvironmentStatus.Running;
            cloned.DeployedResources = source.DeployedResources?.Select(r => new DeployedResource
            {
                ResourceId = r.ResourceId.Replace(source.Name, newName),
                Name = r.Name.Replace(source.Name, newName),
                Type = r.Type,
                Location = r.Location,
                Sku = r.Sku,
                ProvisioningState = r.ProvisioningState,
                DeployedAt = DateTime.UtcNow
            }).ToList();
        }

        // Save to database
        var entity = cloned.ToEntity();
        await _repository.CreateAsync(entity, cancellationToken);

        await AddAuditEntryAsync(Guid.Parse(cloned.Id), "Cloned", clonedBy, 
            $"Cloned from environment {source.Name}", cancellationToken);

        result.Success = true;
        result.Environment = cloned;
        result.EnvironmentId = cloned.Id;
        result.EnvironmentName = cloned.Name;
        result.Message = $"Environment cloned successfully from {source.Name}";

        _logger.LogInformation("🔄 Cloned environment {Source} to {Target} by {ClonedBy}", 
            source.Name, newName, clonedBy);

        return result;
    }

    public async Task<UpgradeEnvironmentResult> UpgradeToTemplateVersionAsync(
        string environmentId,
        string newVersion,
        string upgradedBy,
        CancellationToken cancellationToken = default)
    {
        var result = new UpgradeEnvironmentResult();

        if (!Guid.TryParse(environmentId, out var envId))
        {
            result.Errors = new List<string> { "Invalid environment ID" };
            return result;
        }

        var entity = await _repository.GetByIdAsync(envId, cancellationToken);
        if (entity == null)
        {
            result.Errors = new List<string> { $"Environment {environmentId} not found" };
            return result;
        }

        var environment = entity.ToModel();
        var oldVersion = environment.TemplateVersion;

        // TODO: Validate new version exists and is compatible
        environment.TemplateVersion = newVersion;
        environment.UpdatedBy = upgradedBy;
        environment.UpdatedAt = DateTime.UtcNow;

        entity.UpdateFromModel(environment);
        await _repository.UpdateAsync(entity, cancellationToken);

        await AddAuditEntryAsync(envId, "Upgraded", upgradedBy, 
            $"Upgraded from version {oldVersion} to {newVersion}", cancellationToken);

        result.Success = true;
        result.Environment = environment;
        result.PreviousVersion = oldVersion;
        result.NewVersion = newVersion;
        result.Message = $"Environment upgraded from {oldVersion} to {newVersion}";

        _logger.LogInformation("⬆️ Upgraded environment {Name} from {OldVersion} to {NewVersion} by {UpgradedBy}",
            environment.Name, oldVersion, newVersion, upgradedBy);

        return result;
    }

    #endregion

    #region Drift Detection

    public async Task<DriftDetectionResult> DetectDriftAsync(
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        var result = new DriftDetectionResult { EnvironmentId = environmentId };

        if (!Guid.TryParse(environmentId, out var envId))
        {
            result.Errors = new List<string> { "Invalid environment ID" };
            return result;
        }

        var entity = await _repository.GetByIdAsync(envId, cancellationToken);
        if (entity == null)
        {
            result.Errors = new List<string> { $"Environment {environmentId} not found" };
            return result;
        }

        var environment = entity.ToModel();

        // TODO: Actually compare Azure resources to template
        var driftItems = SimulateDriftDetection(environment);

        environment.DriftItems = driftItems;
        environment.HasDrift = driftItems.Count > 0;
        environment.DriftCount = driftItems.Count;
        environment.LastDriftCheck = DateTime.UtcNow;

        entity.UpdateFromModel(environment);
        await _repository.UpdateAsync(entity, cancellationToken);

        result.Success = true;
        result.DriftItems = driftItems;
        result.HasDrift = driftItems.Count > 0;
        result.DriftCount = driftItems.Count;
        result.DetectedAt = DateTime.UtcNow;

        _logger.LogInformation("🔍 Drift detection for {Name}: {Count} items found",
            environment.Name, driftItems.Count);

        return result;
    }

    public async Task<List<EnvironmentDriftSummary>> DetectAllDriftAsync(CancellationToken cancellationToken = default)
    {
        var summaries = new List<EnvironmentDriftSummary>();
        var runningEntities = await _repository.GetByStatusAsync("Running", cancellationToken);

        foreach (var entity in runningEntities)
        {
            var environment = entity.ToModel();
            var driftItems = SimulateDriftDetection(environment);

            environment.DriftItems = driftItems;
            environment.HasDrift = driftItems.Count > 0;
            environment.DriftCount = driftItems.Count;
            environment.LastDriftCheck = DateTime.UtcNow;

            entity.UpdateFromModel(environment);
            await _repository.UpdateAsync(entity, cancellationToken);

            if (driftItems.Count > 0)
            {
                summaries.Add(new EnvironmentDriftSummary
                {
                    EnvironmentId = environment.Id,
                    EnvironmentName = environment.Name,
                    DriftItemCount = driftItems.Count,
                    CriticalDriftCount = driftItems.Count(d => d.Severity == "Critical"),
                    WarningDriftCount = driftItems.Count(d => d.Severity == "Warning"),
                    InfoDriftCount = driftItems.Count(d => d.Severity == "Info"),
                    HasDrift = true,
                    LastChecked = DateTime.UtcNow,
                    LastCheckAt = DateTime.UtcNow
                });
            }
        }

        _logger.LogInformation("🔍 Drift detection complete: {Count} environments with drift", summaries.Count);

        return summaries;
    }

    public async Task<RemediateDriftResult> RemediateDriftAsync(
        string environmentId,
        List<string>? driftItemIds,
        string remediatedBy,
        CancellationToken cancellationToken = default)
    {
        var result = new RemediateDriftResult();

        if (!Guid.TryParse(environmentId, out var envId))
        {
            result.Errors = new List<string> { "Invalid environment ID" };
            return result;
        }

        var entity = await _repository.GetByIdAsync(envId, cancellationToken);
        if (entity == null)
        {
            result.Errors = new List<string> { $"Environment {environmentId} not found" };
            return result;
        }

        var environment = entity.ToModel();

        if (environment.DriftItems == null || environment.DriftItems.Count == 0)
        {
            result.Success = true;
            result.ItemsRemediated = 0;
            result.RemainingDriftCount = 0;
            return result;
        }

        // Determine which items to remediate
        List<DriftItem> itemsToRemediate;
        if (driftItemIds != null && driftItemIds.Count > 0)
        {
            itemsToRemediate = environment.DriftItems
                .Where(d => driftItemIds.Contains(d.Id))
                .ToList();
        }
        else
        {
            itemsToRemediate = environment.DriftItems.ToList();
        }

        // TODO: Actually remediate drift via ARM/Azure SDK
        // For now, just clear the drift items
        foreach (var item in itemsToRemediate)
        {
            environment.DriftItems.Remove(item);
        }

        environment.HasDrift = environment.DriftItems.Count > 0;
        environment.DriftCount = environment.DriftItems.Count;
        environment.UpdatedBy = remediatedBy;
        environment.UpdatedAt = DateTime.UtcNow;

        entity.UpdateFromModel(environment);
        await _repository.UpdateAsync(entity, cancellationToken);

        await AddAuditEntryAsync(envId, "DriftRemediated", remediatedBy, 
            $"Remediated {itemsToRemediate.Count} drift items", cancellationToken);

        result.Success = true;
        result.ItemsRemediated = itemsToRemediate.Count;
        result.RemainingDriftCount = environment.DriftItems.Count;

        _logger.LogInformation("🔧 Remediated {Count} drift items for environment {Name} by {RemediatedBy}",
            itemsToRemediate.Count, environment.Name, remediatedBy);

        return result;
    }

    #endregion

    #region Health & Status

    public async Task<EnvironmentHealthStatus> GetHealthAsync(
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(environmentId, cancellationToken);
        if (entity == null)
        {
            return new EnvironmentHealthStatus
            {
                EnvironmentId = environmentId,
                OverallHealth = "Unknown",
                ErrorMessage = "Environment not found"
            };
        }

        var environment = entity.ToModel();

        var health = new EnvironmentHealthStatus
        {
            EnvironmentId = environment.Id,
            EnvironmentName = environment.Name,
            LastChecked = DateTime.UtcNow,
            CheckedAt = DateTime.UtcNow,
            HasDrift = environment.HasDrift,
            DriftCount = environment.DriftCount
        };

        // Determine health based on status and drift
        if (environment.Status == EnvironmentStatus.Failed)
        {
            health.OverallHealth = "Critical";
            health.Issues = new List<string> { "Environment deployment failed" };
        }
        else if (environment.Status == EnvironmentStatus.Deleted)
        {
            health.OverallHealth = "N/A";
            health.Issues = new List<string> { "Environment deleted" };
        }
        else if (environment.HasDrift && environment.DriftItems?.Any(d => d.Severity == "Critical") == true)
        {
            health.OverallHealth = "Degraded";
            health.Issues = new List<string> { $"Critical drift detected: {environment.DriftCount} items" };
        }
        else if (environment.HasDrift)
        {
            health.OverallHealth = "Degraded";
            health.Issues = new List<string> { $"Configuration drift detected: {environment.DriftCount} items" };
        }
        else if (environment.Status == EnvironmentStatus.Running)
        {
            health.OverallHealth = "Healthy";
        }
        else
        {
            health.OverallHealth = "Unknown";
            health.Issues = new List<string> { $"Environment status: {environment.Status}" };
        }

        // Resource health (simulated)
        var resources = environment.DeployedResources ?? environment.Resources;
        health.ResourceHealth = resources?.Select(r => new ResourceHealthItem
        {
            ResourceId = r.ResourceId,
            ResourceName = r.Name,
            ResourceType = r.Type,
            Health = r.ProvisioningState == "Succeeded" ? "Healthy" : "Unknown",
            HealthStatus = r.ProvisioningState == "Succeeded" ? "Healthy" : "Unknown"
        }).ToList() ?? new List<ResourceHealthItem>();

        return health;
    }

    public async Task<EnvironmentStatusSummary> GetStatusSummaryAsync(CancellationToken cancellationToken = default)
    {
        var statusCounts = await _repository.GetCountByStatusAsync(cancellationToken);
        var totalCount = await _repository.GetTotalCountAsync(cancellationToken: cancellationToken);
        var driftCount = await _repository.GetDriftCountAsync(cancellationToken);

        return new EnvironmentStatusSummary
        {
            TotalEnvironments = totalCount,
            ByStatus = statusCounts,
            EnvironmentsByStatus = statusCounts,
            WithDriftCount = driftCount,
            EnvironmentsWithDrift = driftCount,
            RunningEnvironments = statusCounts.GetValueOrDefault("Running", 0),
            ProvisioningEnvironments = statusCounts.GetValueOrDefault("Provisioning", 0),
            ProvisioningCount = statusCounts.GetValueOrDefault("Provisioning", 0),
            UpdatingEnvironments = statusCounts.GetValueOrDefault("Updating", 0),
            FailedEnvironments = statusCounts.GetValueOrDefault("Failed", 0),
            GeneratedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region Expiration Management

    public async Task<List<ProvisionedEnvironment>> GetExpiringEnvironmentsAsync(
        int withinDays = 7,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetExpiringAsync(withinDays, cancellationToken);
        return entities.ToModels().ToList();
    }

    public async Task<ProvisionedEnvironment> ExtendExpirationAsync(
        string environmentId,
        DateTime newExpiration,
        string extendedBy,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(environmentId, out var envId))
            throw new ArgumentException("Invalid environment ID");

        var entity = await _repository.GetByIdAsync(envId, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Environment {environmentId} not found");

        var oldExpiration = entity.ExpiresAt;
        entity.ExpiresAt = newExpiration;
        entity.UpdatedBy = extendedBy;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);

        await AddAuditEntryAsync(envId, "ExpirationExtended", extendedBy,
            $"Extended from {oldExpiration:yyyy-MM-dd} to {newExpiration:yyyy-MM-dd}", cancellationToken);

        _logger.LogInformation("📅 Extended expiration of environment {Name} to {NewExpiration} by {ExtendedBy}",
            entity.Name, newExpiration, extendedBy);

        return entity.ToModel();
    }

    public async Task<int> DeleteExpiredEnvironmentsAsync(
        string deletedBy,
        CancellationToken cancellationToken = default)
    {
        var expiredEntities = await _repository.GetExpiredAsync(cancellationToken);
        var deletedCount = 0;

        foreach (var entity in expiredEntities)
        {
            var deleted = await DeleteEnvironmentAsync(entity.Id.ToString(), deletedBy, false, cancellationToken);
            if (deleted) deletedCount++;
        }

        if (deletedCount > 0)
        {
            _logger.LogInformation("🗑️ Deleted {Count} expired environments", deletedCount);
        }

        return deletedCount;
    }

    #endregion

    #region Audit

    public async Task<List<EnvironmentAuditEntry>> GetAuditLogAsync(
        string environmentId,
        int? maxEntries = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(environmentId, out var envId))
            return new List<EnvironmentAuditEntry>();

        var entities = await _repository.GetAuditEntriesAsync(envId, maxEntries ?? 100, cancellationToken);
        return entities.ToModels();
    }

    #endregion

    #region Private Helpers

    private Dictionary<string, string> MergeTags(
        Dictionary<string, string> templateTags,
        Dictionary<string, string>? requestTags,
        string environmentName,
        string templateId)
    {
        var tags = new Dictionary<string, string>(templateTags ?? new Dictionary<string, string>());

        if (requestTags != null)
        {
            foreach (var tag in requestTags)
            {
                tags[tag.Key] = tag.Value;
            }
        }

        tags["Environment"] = environmentName;
        tags["TemplateId"] = templateId;
        tags["CreatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd");

        return tags;
    }

    private async Task SimulateDeploymentAsync(ProvisionedEnvironment environment, ServiceTemplate template, CancellationToken cancellationToken)
    {
        // Simulate deployment delay
        await Task.Delay(200, cancellationToken);

        // Generate simulated resources based on template
        environment.DeployedResources = GenerateSimulatedResources(environment, template);
        environment.Resources = environment.DeployedResources;
        environment.Status = EnvironmentStatus.Running;
        environment.DeploymentDurationMinutes = 5; // Simulated
    }

    private List<DeployedResource> GenerateSimulatedResources(ProvisionedEnvironment environment, ServiceTemplate template)
    {
        var resources = new List<DeployedResource>();
        var baseId = $"/subscriptions/{environment.SubscriptionId}/resourceGroups/{environment.ResourceGroupName}/providers";

        // Resource generation based on template type
        switch (template.Name.ToLowerInvariant())
        {
            case "aks-standard":
                resources.Add(CreateResource(baseId, "Microsoft.ContainerService/managedClusters", 
                    environment.ParameterValues.GetValueOrDefault("clusterName", "aks-cluster")?.ToString() ?? "aks-cluster"));
                break;

            case "webapp-standard":
                resources.Add(CreateResource(baseId, "Microsoft.Web/sites", 
                    environment.ParameterValues.GetValueOrDefault("appName", "webapp")?.ToString() ?? "webapp"));
                resources.Add(CreateResource(baseId, "Microsoft.Web/serverfarms", $"asp-{environment.Name}"));
                resources.Add(CreateResource(baseId, "Microsoft.Insights/components", $"ai-{environment.Name}"));
                break;

            case "containerapp-standard":
                resources.Add(CreateResource(baseId, "Microsoft.App/containerApps", 
                    environment.ParameterValues.GetValueOrDefault("appName", "containerapp")?.ToString() ?? "containerapp"));
                resources.Add(CreateResource(baseId, "Microsoft.App/managedEnvironments", $"cae-{environment.Name}"));
                break;

            case "microservice-fullstack":
                resources.Add(CreateResource(baseId, "Microsoft.ContainerService/managedClusters", $"aks-{environment.Name}"));
                resources.Add(CreateResource(baseId, "Microsoft.Sql/servers", $"sql-{environment.Name}"));
                resources.Add(CreateResource(baseId, "Microsoft.Cache/Redis", $"redis-{environment.Name}"));
                resources.Add(CreateResource(baseId, "Microsoft.ServiceBus/namespaces", $"sb-{environment.Name}"));
                break;

            default:
                resources.Add(CreateResource(baseId, "Microsoft.Resources/deployments", $"deployment-{environment.Name}"));
                break;
        }

        return resources;
    }

    private DeployedResource CreateResource(string baseId, string type, string name)
    {
        return new DeployedResource
        {
            ResourceId = $"{baseId}/{type}/{name}",
            Name = name,
            Type = type,
            ProvisioningState = "Succeeded",
            DeployedAt = DateTime.UtcNow
        };
    }

    private List<DriftItem> SimulateDriftDetection(ProvisionedEnvironment environment)
    {
        // Simulate occasional drift for demo purposes
        var random = new Random(environment.Id.GetHashCode());
        if (random.NextDouble() > 0.3) // 30% chance of drift
            return new List<DriftItem>();

        var driftItems = new List<DriftItem>();
        var resources = environment.DeployedResources ?? environment.Resources;

        if (resources?.Any() == true)
        {
            var resource = resources.First();
            driftItems.Add(new DriftItem
            {
                Id = Guid.NewGuid().ToString(),
                ResourceId = resource.ResourceId,
                ResourceName = resource.Name,
                Property = "tags.ManagedBy",
                PropertyPath = "tags.ManagedBy",
                ExpectedValue = "PlatformEngineering",
                ActualValue = "Manual",
                DriftType = "Modified",
                Severity = "Warning",
                DetectedAt = DateTime.UtcNow
            });
        }

        return driftItems;
    }

    private async Task AddAuditEntryAsync(Guid environmentId, string action, string performedBy, string? details, CancellationToken cancellationToken)
    {
        var entry = new Platform.Engineering.Copilot.Core.Data.Entities.EnvironmentAuditEntity
        {
            Id = Guid.NewGuid(),
            EnvironmentId = environmentId,
            Action = action,
            PerformedBy = performedBy,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        await _repository.AddAuditEntryAsync(entry, cancellationToken);
    }

    #endregion
}
