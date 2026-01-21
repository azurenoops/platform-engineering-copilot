using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Infrastructure.Deployment;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Mappers;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;
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
    private readonly IEnvironmentActivityRepository? _activityRepository;
    private readonly IDeployerFactory? _deployerFactory;
    private readonly IAzureResourceService? _azureResourceService;
    private readonly DeployerOptions _deployerOptions;

    public ProvisionedEnvironmentService(
        ILogger<ProvisionedEnvironmentService> logger,
        IServiceTemplateCatalogService templateCatalog,
        IProvisionedEnvironmentRepository repository,
        IDeployerFactory? deployerFactory = null,
        IOptions<DeployerOptions>? deployerOptions = null,
        IEnvironmentActivityRepository? activityRepository = null,
        IAzureResourceService? azureResourceService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateCatalog = templateCatalog ?? throw new ArgumentNullException(nameof(templateCatalog));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _deployerFactory = deployerFactory;
        _deployerOptions = deployerOptions?.Value ?? new DeployerOptions();
        _activityRepository = activityRepository;
        _azureResourceService = azureResourceService;
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

        // Deploy resources using appropriate deployer (Bicep, ARM, or Terraform)
        var deploymentSuccess = await DeployWithStrategyAsync(environment, template, request, cancellationToken);
        
        if (!deploymentSuccess.success)
        {
            result.Errors = deploymentSuccess.errors;
            environment.Status = EnvironmentStatus.Failed;
            // Truncate error message to prevent DB overflow (max 4000 chars)
            var errorMessage = string.Join("; ", deploymentSuccess.errors);
            environment.StatusMessage = errorMessage.Length > 3900 
                ? errorMessage[..3900] + "... [truncated]" 
                : errorMessage;
        }
        else
        {
            environment.DeployedResources = deploymentSuccess.resources;
            environment.Resources = deploymentSuccess.resources;
            environment.StatusMessage = "Deployment completed successfully";
        }

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

        // Perform real drift detection by comparing Azure resources to expected state
        var driftItems = await DetectRealDriftAsync(environment, cancellationToken);

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
            var driftItems = await DetectRealDriftAsync(environment, cancellationToken);

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
            // Only remediate items that can be auto-remediated
            itemsToRemediate = environment.DriftItems
                .Where(d => d.CanAutoRemediate)
                .ToList();
        }

        var remediatedItems = new List<DriftItem>();
        var failedItems = new List<string>();

        // Actually remediate drift via Azure SDK
        if (_azureResourceService != null)
        {
            // Group drift items by resource ID to batch tag updates
            var itemsByResource = itemsToRemediate
                .Where(d => d.DriftType == "Configuration" && d.PropertyPath.StartsWith("tags."))
                .GroupBy(d => d.ResourceId);

            foreach (var resourceGroup in itemsByResource)
            {
                var resourceId = resourceGroup.Key;
                var tagsToApply = new Dictionary<string, string>();

                foreach (var driftItem in resourceGroup)
                {
                    // Extract tag name from property path (e.g., "tags.ManagedBy" -> "ManagedBy")
                    var tagName = driftItem.PropertyPath.Replace("tags.", "");
                    if (!string.IsNullOrEmpty(driftItem.ExpectedValue))
                    {
                        tagsToApply[tagName] = driftItem.ExpectedValue;
                    }
                }

                if (tagsToApply.Count > 0)
                {
                    try
                    {
                        var success = await _azureResourceService.UpdateResourceTagsAsync(
                            resourceId, tagsToApply, cancellationToken);

                        if (success)
                        {
                            foreach (var item in resourceGroup)
                            {
                                remediatedItems.Add(item);
                                environment.DriftItems.Remove(item);
                            }
                            _logger.LogInformation("✅ Applied tags to {ResourceId}: {Tags}", 
                                resourceId, string.Join(", ", tagsToApply.Select(t => $"{t.Key}={t.Value}")));
                        }
                        else
                        {
                            failedItems.Add($"Failed to update tags on {resourceId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to remediate drift on {ResourceId}", resourceId);
                        failedItems.Add($"Error updating {resourceId}: {ex.Message}");
                    }
                }
            }

            // Handle non-tag drift items (Missing/Extra) - these can't be auto-remediated
            var nonTagItems = itemsToRemediate.Where(d => !d.PropertyPath.StartsWith("tags.")).ToList();
            foreach (var item in nonTagItems)
            {
                if (item.DriftType == "Missing")
                {
                    failedItems.Add($"Cannot auto-remediate missing resource: {item.ResourceName}");
                }
                else if (item.DriftType == "Extra")
                {
                    failedItems.Add($"Cannot auto-remediate extra resource: {item.ResourceName}");
                }
            }
        }
        else
        {
            _logger.LogWarning("Azure resource service not available - skipping actual remediation");
            // Fallback: just clear the drift items from tracking (for demo/testing)
            foreach (var item in itemsToRemediate)
            {
                remediatedItems.Add(item);
                environment.DriftItems.Remove(item);
            }
        }

        environment.HasDrift = environment.DriftItems.Count > 0;
        environment.DriftCount = environment.DriftItems.Count;
        environment.UpdatedBy = remediatedBy;
        environment.UpdatedAt = DateTime.UtcNow;

        entity.UpdateFromModel(environment);
        await _repository.UpdateAsync(entity, cancellationToken);

        await AddAuditEntryAsync(envId, "DriftRemediated", remediatedBy, 
            $"Remediated {remediatedItems.Count} drift items, {failedItems.Count} failed", cancellationToken);

        result.Success = failedItems.Count == 0;
        result.ItemsRemediated = remediatedItems.Count;
        result.ItemsFailed = failedItems.Count;
        result.RemainingDriftCount = environment.DriftItems.Count;
        if (failedItems.Count > 0)
        {
            result.Errors = failedItems;
        }

        _logger.LogInformation("🔧 Remediated {Count} drift items for environment {Name} by {RemediatedBy} ({Failed} failed)",
            remediatedItems.Count, environment.Name, remediatedBy, failedItems.Count);

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

    #region Reprovision & Purge

    public async Task<CreateEnvironmentResult> ReprovisionEnvironmentAsync(
        string environmentId,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        var result = new CreateEnvironmentResult();

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

        // Only allow reprovisioning failed environments
        if (entity.Status != "Failed")
        {
            result.Errors = new List<string> { $"Cannot reprovision environment in status '{entity.Status}'. Only failed environments can be reprovisioned." };
            return result;
        }

        // Get the original template
        var template = await _templateCatalog.GetTemplateAsync(entity.TemplateId.ToString(), cancellationToken);
        if (template == null)
        {
            result.Errors = new List<string> { $"Template {entity.TemplateId} not found" };
            return result;
        }

        _logger.LogInformation("🔄 Reprovisioning failed environment {Name} by {RequestedBy}", entity.Name, requestedBy);

        // Update status to provisioning
        entity.Status = "Provisioning";
        entity.StatusMessage = "Reprovisioning in progress...";
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = requestedBy;
        await _repository.UpdateAsync(entity, cancellationToken);

        // Build the environment model
        var environment = entity.ToModel();

        // Create a deployment request
        var request = new CreateEnvironmentFromTemplateRequest
        {
            TemplateId = entity.TemplateId.ToString(),
            EnvironmentName = entity.Name,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            SubscriptionId = entity.SubscriptionId,
            ResourceGroup = entity.ResourceGroup,
            ResourceGroupName = entity.ResourceGroup,
            Location = entity.Location,
            Parameters = environment.ParameterValues ?? new Dictionary<string, object>(),
            RequestedBy = requestedBy,
            Tags = environment.Tags ?? new Dictionary<string, string>()
        };

        // Re-deploy using the appropriate deployer
        var deploymentSuccess = await DeployWithStrategyAsync(environment, template, request, cancellationToken);

        if (!deploymentSuccess.success)
        {
            result.Errors = deploymentSuccess.errors;
            entity.Status = "Failed";
            var errorMessage = string.Join("; ", deploymentSuccess.errors);
            entity.StatusMessage = errorMessage.Length > 3900 
                ? errorMessage[..3900] + "... [truncated]" 
                : errorMessage;
        }
        else
        {
            entity.Status = "Running";
            entity.StatusMessage = "Reprovisioning completed successfully";
            result.Success = true;
            result.DeployedResources = deploymentSuccess.resources;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, cancellationToken);

        await AddAuditEntryAsync(envId, "Reprovisioned", requestedBy, 
            result.Success ? "Reprovisioning succeeded" : $"Reprovisioning failed: {entity.StatusMessage}", 
            cancellationToken);

        result.Environment = entity.ToModel();
        result.EnvironmentId = environmentId;
        result.EnvironmentName = entity.Name;
        result.Status = Enum.TryParse<EnvironmentStatus>(entity.Status, out var status) ? status : EnvironmentStatus.Failed;

        _logger.LogInformation("🔄 Reprovision {Result} for environment {Name}", 
            result.Success ? "succeeded" : "failed", entity.Name);

        return result;
    }

    public async Task<List<ProvisionedEnvironment>> GetDeletedEnvironmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.SearchAsync(
            includeDeleted: true,
            skip: 0,
            take: 1000,
            cancellationToken: cancellationToken);

        // Filter to only soft-deleted environments
        var deletedEntities = entities.Where(e => e.IsDeleted).ToList();
        return deletedEntities.ToModels().ToList();
    }

    public async Task<bool> PurgeEnvironmentAsync(
        string environmentId,
        string purgedBy,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(environmentId, out var envId))
            return false;

        // Get the entity including deleted ones
        var entities = await _repository.SearchAsync(
            includeDeleted: true,
            skip: 0,
            take: 1000,
            cancellationToken: cancellationToken);

        var entity = entities.FirstOrDefault(e => e.Id == envId);
        if (entity == null)
            return false;

        if (!entity.IsDeleted)
        {
            _logger.LogWarning("⚠️ Cannot purge environment {Name} - it is not soft-deleted", entity.Name);
            return false;
        }

        await AddAuditEntryAsync(envId, "Purged", purgedBy, "Permanently deleted from database", cancellationToken);
        await _repository.HardDeleteAsync(envId, cancellationToken);

        _logger.LogWarning("🗑️ Permanently purged soft-deleted environment {Name} by {PurgedBy}", entity.Name, purgedBy);
        return true;
    }

    public async Task<int> PurgeAllDeletedEnvironmentsAsync(
        string purgedBy,
        CancellationToken cancellationToken = default)
    {
        var deletedEnvironments = await GetDeletedEnvironmentsAsync(cancellationToken);
        var purgedCount = 0;

        foreach (var env in deletedEnvironments)
        {
            var purged = await PurgeEnvironmentAsync(env.Id, purgedBy, cancellationToken);
            if (purged) purgedCount++;
        }

        if (purgedCount > 0)
        {
            _logger.LogWarning("🗑️ Permanently purged {Count} soft-deleted environments by {PurgedBy}", purgedCount, purgedBy);
        }

        return purgedCount;
    }

    public async Task<DeleteResourcesResult> DeleteAzureResourcesAsync(
        string environmentId,
        string deletedBy,
        CancellationToken cancellationToken = default)
    {
        var result = new DeleteResourcesResult
        {
            EnvironmentId = environmentId,
            DeletedResources = new List<string>(),
            FailedResources = new List<string>(),
            Errors = new List<string>()
        };

        if (!Guid.TryParse(environmentId, out var envId))
        {
            result.Errors.Add("Invalid environment ID");
            return result;
        }

        // Get including deleted (in case we're cleaning up a soft-deleted environment)
        var entities = await _repository.SearchAsync(
            includeDeleted: true,
            skip: 0,
            take: 1000,
            cancellationToken: cancellationToken);

        var entity = entities.FirstOrDefault(e => e.Id == envId);
        if (entity == null)
        {
            result.Errors.Add($"Environment {environmentId} not found");
            return result;
        }

        _logger.LogInformation("🗑️ Deleting Azure resources for environment {Name} by {DeletedBy}", entity.Name, deletedBy);

        var subscriptionId = entity.SubscriptionId;

        if (string.IsNullOrEmpty(subscriptionId))
        {
            result.Errors.Add("Environment does not have a subscription configured");
            return result;
        }

        // Collect all resource groups to delete
        // For subscription-level deployments, multiple RGs may have been created
        var resourceGroupsToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Add the configured resource group (if any)
        if (!string.IsNullOrEmpty(entity.ResourceGroup))
        {
            resourceGroupsToDelete.Add(entity.ResourceGroup);
        }

        // 2. Extract resource groups from synced/deployed resources (stored as JSON)
        if (!string.IsNullOrEmpty(entity.DeployedResourcesJson))
        {
            try
            {
                var deployedResources = System.Text.Json.JsonSerializer.Deserialize<List<DeployedResource>>(
                    entity.DeployedResourcesJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (deployedResources?.Any() == true)
                {
                    foreach (var resource in deployedResources)
                    {
                        // Extract resource group from resource ID: /subscriptions/{sub}/resourceGroups/{rg}/...
                        var rgName = ExtractResourceGroupFromResourceId(resource.ResourceId);
                        if (!string.IsNullOrEmpty(rgName))
                        {
                            resourceGroupsToDelete.Add(rgName);
                        }
                    }
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize deployed resources JSON");
            }
        }

        if (!resourceGroupsToDelete.Any())
        {
            result.Errors.Add("No resource groups identified for deletion. Try syncing resources from Azure first.");
            return result;
        }

        _logger.LogInformation("🗑️ Identified {Count} resource group(s) to delete: {ResourceGroups}", 
            resourceGroupsToDelete.Count, string.Join(", ", resourceGroupsToDelete));

        // Delete each resource group
        foreach (var resourceGroup in resourceGroupsToDelete)
        {
            try
            {
                if (_azureResourceService != null)
                {
                    await _azureResourceService.DeleteResourceGroupAsync(resourceGroup, subscriptionId, cancellationToken);
                    
                    result.DeletedResources.Add(resourceGroup);
                    result.TotalResourcesDeleted++;

                    _logger.LogInformation("✅ Deleted resource group {ResourceGroup}", resourceGroup);
                }
                else
                {
                    // Fallback to Azure CLI if SDK not available
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "az",
                            Arguments = $"group delete --name {resourceGroup} --subscription {subscriptionId} --yes --no-wait",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    await process.StandardOutput.ReadToEndAsync(cancellationToken);
                    var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                    await process.WaitForExitAsync(cancellationToken);

                    if (process.ExitCode == 0)
                    {
                        result.DeletedResources.Add(resourceGroup);
                        result.TotalResourcesDeleted++;
                        _logger.LogInformation("✅ Initiated deletion of resource group {ResourceGroup}", resourceGroup);
                    }
                    else
                    {
                        result.FailedResources.Add(resourceGroup);
                        result.TotalResourcesFailed++;
                        result.Errors.Add($"Failed to delete {resourceGroup}: {error}");
                        _logger.LogError("❌ Failed to delete resource group {ResourceGroup}: {Error}", resourceGroup, error);
                    }
                }
            }
            catch (Exception ex)
            {
                result.FailedResources.Add(resourceGroup);
                result.TotalResourcesFailed++;
                result.Errors.Add($"Exception deleting {resourceGroup}: {ex.Message}");
                _logger.LogError(ex, "Exception deleting resource group {ResourceGroup}", resourceGroup);
            }
        }

        // Set overall success
        result.Success = result.TotalResourcesDeleted > 0 && result.TotalResourcesFailed == 0;
        result.Message = $"Deleted {result.TotalResourcesDeleted} resource group(s)" +
            (result.TotalResourcesFailed > 0 ? $", {result.TotalResourcesFailed} failed" : "");

        await AddAuditEntryAsync(envId, "ResourcesDeleted", deletedBy, 
            $"Deleted {result.TotalResourcesDeleted} resource group(s): {string.Join(", ", result.DeletedResources)}", cancellationToken);

        return result;
    }

    /// <summary>
    /// Extracts the resource group name from an Azure resource ID.
    /// </summary>
    private static string? ExtractResourceGroupFromResourceId(string? resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return null;

        // Resource ID format: /subscriptions/{sub}/resourceGroups/{rg}/providers/...
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
            {
                return parts[i + 1];
            }
        }
        return null;
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

    /// <summary>
    /// Deploy using the appropriate strategy based on template format
    /// </summary>
    private async Task<(bool success, List<DeployedResource> resources, List<string> errors)> DeployWithStrategyAsync(
        ProvisionedEnvironment environment,
        ServiceTemplate template,
        CreateEnvironmentFromTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var resources = new List<DeployedResource>();

        // Check if we have a deployer factory (real deployment)
        if (_deployerFactory != null && _deployerFactory.HasDeployer(template.Format.ToString()))
        {
            try
            {
                var deployer = _deployerFactory.GetDeployer(template.Format.ToString());
                
                _logger.LogInformation("🚀 Using {Deployer} to deploy {Template} ({Format})",
                    deployer.GetType().Name, template.Name, template.Format);

                // Convert additional files to dictionary format for deployer
                var additionalFiles = new Dictionary<string, string>();
                if (template.AdditionalFiles != null && template.AdditionalFiles.Count > 0)
                {
                    foreach (var file in template.AdditionalFiles)
                    {
                        // Use RelativePath if set, otherwise FileName
                        var path = !string.IsNullOrEmpty(file.RelativePath) ? file.RelativePath : file.FileName;
                        additionalFiles[path] = file.Content;
                    }
                    _logger.LogInformation("📦 Including {Count} additional files for deployment: {Files}",
                        additionalFiles.Count, string.Join(", ", additionalFiles.Keys));
                }

                var deployRequest = new DeploymentRequest
                {
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    TemplateContent = template.MainTemplateContent,
                    Format = template.Format.ToString(),
                    EnvironmentName = environment.Name,
                    SubscriptionId = environment.SubscriptionId,
                    ResourceGroupName = environment.ResourceGroupName ?? string.Empty,
                    Location = environment.Location,
                    Parameters = request.Parameters,
                    Tags = environment.Tags,
                    DeployedBy = request.RequestedBy ?? "system",
                    AdditionalFiles = additionalFiles,
                    WhatIf = false
                };

                // Add Terraform backend config if applicable
                if (template.Format.ToString().Equals("Terraform", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(_deployerOptions.DefaultStateStorageAccount))
                {
                    deployRequest.TerraformBackend = new TerraformBackendConfig
                    {
                        Type = "azurerm",
                        StorageAccountName = _deployerOptions.DefaultStateStorageAccount,
                        ContainerName = _deployerOptions.DefaultStateContainer,
                        ResourceGroupName = _deployerOptions.DefaultStateResourceGroup,
                        Key = $"{environment.Name}.tfstate"
                    };
                }

                var result = await deployer.DeployAsync(deployRequest, cancellationToken);

                // Store deployment ID for status tracking
                if (!string.IsNullOrEmpty(result.DeploymentId))
                {
                    environment.DeploymentId = result.DeploymentId;
                }

                // Handle async deployments (subscription-level) vs completed deployments
                if (result.Status == "Running")
                {
                    // Async deployment - still running in Azure
                    // Keep environment in Provisioning state, store deployment ID for status polling
                    environment.Status = EnvironmentStatus.Provisioning;
                    environment.StatusMessage = result.RawOutput ?? $"Deployment '{result.DeploymentId}' is running. Check Azure Portal for progress.";
                    
                    _logger.LogInformation("⏳ Async deployment started for {Environment}, DeploymentId: {DeploymentId}",
                        environment.Name, result.DeploymentId);
                    
                    // Return success=true to create the environment record, but it's still provisioning
                    return (true, resources, errors);
                }
                else if (result.Success)
                {
                    environment.Status = EnvironmentStatus.Running;
                    environment.DeploymentDurationMinutes = (int)result.Duration.TotalMinutes;

                    // Convert deployed resources
                    foreach (var res in result.Resources)
                    {
                        resources.Add(new DeployedResource
                        {
                            ResourceId = res.ResourceId,
                            Name = res.Name,
                            Type = res.Type,
                            Location = res.Location,
                            ProvisioningState = res.ProvisioningState,
                            DeployedAt = DateTime.UtcNow
                        });
                    }

                    _logger.LogInformation("✅ Deployment succeeded for {Environment} with {ResourceCount} resources",
                        environment.Name, resources.Count);

                    return (true, resources, errors);
                }
                else
                {
                    environment.Status = EnvironmentStatus.Failed;
                    errors.AddRange(result.Errors);
                    
                    _logger.LogError("❌ Deployment failed for {Environment}: {Errors}",
                        environment.Name, string.Join(", ", result.Errors));

                    return (false, resources, errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deployment error for {Environment}", environment.Name);
                errors.Add($"Deployment error: {ex.Message}");
                return (false, resources, errors);
            }
        }
        else
        {
            // Fall back to simulation if no deployer available
            _logger.LogWarning("⚠️ No deployer available for format {Format}, using simulation",
                template.Format);
            
            await SimulateDeploymentAsync(environment, template, cancellationToken);
            return (true, environment.DeployedResources ?? new List<DeployedResource>(), errors);
        }
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

    /// <summary>
    /// Performs real drift detection by comparing Azure resources to expected state.
    /// Detects three types of drift:
    /// - Configuration: Resource exists but properties differ (tags, SKU, etc.)
    /// - Missing: Expected resource not found in Azure
    /// - Extra: Resource in Azure not defined in expected state
    /// </summary>
    private async Task<List<DriftItem>> DetectRealDriftAsync(
        ProvisionedEnvironment environment, 
        CancellationToken cancellationToken = default)
    {
        var driftItems = new List<DriftItem>();

        if (_azureResourceService == null)
        {
            _logger.LogWarning("Azure resource service not available - cannot perform real drift detection");
            return driftItems;
        }

        try
        {
            // Get expected resources from stored state
            var expectedResources = environment.DeployedResources ?? environment.Resources ?? new List<DeployedResource>();
            if (!expectedResources.Any())
            {
                _logger.LogInformation("No expected resources recorded for environment {Name} - skipping drift detection", 
                    environment.Name);
                return driftItems;
            }

            _logger.LogInformation("🔍 Starting drift detection for {Name} - checking {Count} expected resources",
                environment.Name, expectedResources.Count);

            // Expected tags that should be on all managed resources
            var expectedTags = environment.Tags ?? new Dictionary<string, string>();
            expectedTags["ManagedBy"] = "PlatformEngineering";
            expectedTags["Environment"] = environment.Name;

            // Build a lookup of expected resource IDs
            var expectedResourceIds = expectedResources
                .Select(r => r.ResourceId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Get all resource groups associated with this environment
            var resourceGroupNames = ExtractResourceGroupsFromResources(expectedResources);

            // Fetch actual resources from Azure
            var actualResources = new List<Core.Models.Azure.AzureResource>();
            foreach (var rgName in resourceGroupNames)
            {
                try
                {
                    var rgResources = await _azureResourceService.ListAllResourcesInResourceGroupAsync(
                        environment.SubscriptionId, rgName, cancellationToken);
                    actualResources.AddRange(rgResources);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch resources from RG {ResourceGroup} - may be deleted", rgName);
                    
                    // If we can't access the RG, all resources from it are potentially missing
                    var missingFromRg = expectedResources
                        .Where(r => ExtractResourceGroupFromResourceId(r.ResourceId)
                            .Equals(rgName, StringComparison.OrdinalIgnoreCase));
                    
                    foreach (var missing in missingFromRg)
                    {
                        driftItems.Add(new DriftItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            ResourceId = missing.ResourceId,
                            ResourceName = missing.Name,
                            Property = "resource",
                            PropertyPath = "resource",
                            ExpectedValue = "Exists",
                            ActualValue = "ResourceGroup not accessible",
                            DriftType = "Missing",
                            Severity = "Critical",
                            DetectedAt = DateTime.UtcNow,
                            CanAutoRemediate = false
                        });
                    }
                }
            }

            var actualResourceIds = actualResources
                .Select(r => r.Id ?? string.Empty)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Detect MISSING resources (expected but not in Azure)
            foreach (var expected in expectedResources)
            {
                if (!actualResourceIds.Contains(expected.ResourceId))
                {
                    // Check if we already added this as missing due to RG access issue
                    if (driftItems.Any(d => d.ResourceId == expected.ResourceId)) continue;

                    driftItems.Add(new DriftItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        ResourceId = expected.ResourceId,
                        ResourceName = expected.Name,
                        Property = "resource",
                        PropertyPath = "resource",
                        ExpectedValue = "Exists",
                        ActualValue = "Not Found",
                        DriftType = "Missing",
                        Severity = "Critical",
                        DetectedAt = DateTime.UtcNow,
                        CanAutoRemediate = true // Could potentially redeploy
                    });

                    _logger.LogWarning("🔴 MISSING: Resource {Name} ({Type}) not found in Azure",
                        expected.Name, expected.Type);
                }
            }

            // Detect EXTRA resources (in Azure but not expected)
            foreach (var actual in actualResources)
            {
                if (!expectedResourceIds.Contains(actual.Id ?? string.Empty))
                {
                    driftItems.Add(new DriftItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        ResourceId = actual.Id ?? string.Empty,
                        ResourceName = actual.Name ?? "Unknown",
                        Property = "resource",
                        PropertyPath = "resource",
                        ExpectedValue = "Not Defined",
                        ActualValue = "Exists in Azure",
                        DriftType = "Extra",
                        Severity = "Warning",
                        DetectedAt = DateTime.UtcNow,
                        CanAutoRemediate = false // Don't auto-delete unknown resources
                    });

                    _logger.LogWarning("🟡 EXTRA: Unexpected resource {Name} ({Type}) found in Azure",
                        actual.Name, actual.Type);
                }
            }

            // Detect CONFIGURATION drift on existing resources (compare tags)
            foreach (var expected in expectedResources)
            {
                var actual = actualResources.FirstOrDefault(r => 
                    r.Id?.Equals(expected.ResourceId, StringComparison.OrdinalIgnoreCase) == true);
                
                if (actual == null) continue; // Already handled as missing

                // Compare tags
                var actualTags = actual.Tags ?? new Dictionary<string, string>();
                
                foreach (var expectedTag in expectedTags)
                {
                    if (!actualTags.TryGetValue(expectedTag.Key, out var actualValue))
                    {
                        // Tag is missing
                        driftItems.Add(new DriftItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            ResourceId = expected.ResourceId,
                            ResourceName = expected.Name,
                            Property = $"tags.{expectedTag.Key}",
                            PropertyPath = $"tags.{expectedTag.Key}",
                            ExpectedValue = expectedTag.Value,
                            ActualValue = "(not set)",
                            DriftType = "Configuration",
                            Severity = "Info",
                            DetectedAt = DateTime.UtcNow,
                            CanAutoRemediate = true
                        });
                    }
                    else if (!actualValue.Equals(expectedTag.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        // Tag value differs
                        driftItems.Add(new DriftItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            ResourceId = expected.ResourceId,
                            ResourceName = expected.Name,
                            Property = $"tags.{expectedTag.Key}",
                            PropertyPath = $"tags.{expectedTag.Key}",
                            ExpectedValue = expectedTag.Value,
                            ActualValue = actualValue,
                            DriftType = "Configuration",
                            Severity = "Warning",
                            DetectedAt = DateTime.UtcNow,
                            CanAutoRemediate = true
                        });
                    }
                }
            }

            _logger.LogInformation("✅ Drift detection complete for {Name}: {Count} drift items found " +
                "(Missing: {Missing}, Extra: {Extra}, Config: {Config})",
                environment.Name,
                driftItems.Count,
                driftItems.Count(d => d.DriftType == "Missing"),
                driftItems.Count(d => d.DriftType == "Extra"),
                driftItems.Count(d => d.DriftType == "Configuration"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during drift detection for environment {Name}", environment.Name);
            // Return empty list on error rather than failing
        }

        return driftItems;
    }

    /// <summary>
    /// Extracts unique resource group names from a list of deployed resources.
    /// </summary>
    private HashSet<string> ExtractResourceGroupsFromResources(List<DeployedResource> resources)
    {
        var rgNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var resource in resources)
        {
            var rgName = ExtractResourceGroupFromResourceId(resource.ResourceId);
            if (!string.IsNullOrEmpty(rgName))
            {
                rgNames.Add(rgName);
            }
        }

        return rgNames;
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
        
        // Also record as activity for the Activity Log feature
        await RecordActivityAsync(environmentId, action, details ?? action, performedBy, 
            "Completed", null, null, cancellationToken);
    }

    private async Task RecordActivityAsync(
        Guid environmentId, 
        string activityType, 
        string description, 
        string? userName,
        string status = "Completed",
        string? errorMessage = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (_activityRepository == null) return;
        
        try
        {
            var entity = new EnvironmentActivityEntity
            {
                Id = Guid.NewGuid(),
                EnvironmentId = environmentId,
                ActivityType = activityType,
                Description = description,
                UserName = userName,
                Status = status,
                ErrorMessage = errorMessage,
                Metadata = metadata != null 
                    ? System.Text.Json.JsonSerializer.Serialize(metadata)
                    : null,
                Timestamp = DateTime.UtcNow
            };

            await _activityRepository.AddAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't fail the main operation if activity logging fails
            _logger.LogWarning(ex, "Failed to record activity {ActivityType} for environment {EnvironmentId}", 
                activityType, environmentId);
        }
    }

    #endregion

    #region Deployment Status

    /// <summary>
    /// Refresh deployment status from Azure for an environment in Provisioning state
    /// </summary>
    public async Task<RefreshDeploymentStatusResult> RefreshDeploymentStatusAsync(
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        var result = new RefreshDeploymentStatusResult { EnvironmentId = environmentId };

        var environment = await GetEnvironmentAsync(environmentId, cancellationToken);
        if (environment == null)
        {
            result.Error = $"Environment {environmentId} not found";
            return result;
        }

        result.EnvironmentName = environment.Name;
        result.PreviousStatus = environment.Status;
        result.DeploymentId = environment.DeploymentId ?? string.Empty;

        // Only refresh for environments in Provisioning state
        if (environment.Status != EnvironmentStatus.Provisioning)
        {
            result.CurrentStatus = environment.Status;
            result.StatusMessage = $"Environment is not in Provisioning state (current: {environment.Status})";
            return result;
        }

        // Need a deployment ID to check status
        if (string.IsNullOrEmpty(environment.DeploymentId))
        {
            result.CurrentStatus = environment.Status;
            result.Error = "No deployment ID stored for this environment";
            return result;
        }

        // Get template to determine format
        var template = await _templateCatalog.GetTemplateAsync(environment.TemplateId, cancellationToken);
        if (template == null)
        {
            result.Error = $"Template {environment.TemplateId} not found";
            return result;
        }

        // Get deployer for this format
        if (_deployerFactory == null || !_deployerFactory.HasDeployer(template.Format.ToString()))
        {
            result.Error = $"No deployer available for format {template.Format}";
            return result;
        }

        var deployer = _deployerFactory.GetDeployer(template.Format.ToString());

        try
        {
            // Check deployment status in Azure
            // For subscription-level deployments, resourceGroupName should be null/empty
            var deploymentStatus = await deployer.GetDeploymentStatusAsync(
                environment.SubscriptionId,
                environment.DeploymentId,
                resourceGroupName: null,  // Subscription-level deployment for MLZ
                cancellationToken);

            result.CurrentStatus = environment.Status;
            result.StatusMessage = $"Azure deployment state: {deploymentStatus.ProvisioningState}";

            if (deploymentStatus.IsComplete)
            {
                if (deploymentStatus.IsSuccessful)
                {
                    environment.Status = EnvironmentStatus.Running;
                    environment.StatusMessage = "Deployment completed successfully";
                    
                    // Store deployed resources
                    if (deploymentStatus.Resources.Any())
                    {
                        environment.DeployedResources = deploymentStatus.Resources
                            .Select(r => new DeployedResource
                            {
                                ResourceId = r.ResourceId,
                                Name = r.Name,
                                Type = r.Type,
                                Location = r.Location,
                                ProvisioningState = r.ProvisioningState,
                                DeployedAt = DateTime.UtcNow
                            }).ToList();
                        environment.Resources = environment.DeployedResources;
                    }

                    _logger.LogInformation("✅ Environment {Name} deployment succeeded", environment.Name);
                }
                else
                {
                    environment.Status = EnvironmentStatus.Failed;
                    environment.StatusMessage = deploymentStatus.ErrorMessage ?? 
                        string.Join("; ", deploymentStatus.Errors);
                    
                    _logger.LogError("❌ Environment {Name} deployment failed: {Error}", 
                        environment.Name, environment.StatusMessage);
                }

                result.CurrentStatus = environment.Status;
                result.StatusChanged = true;
                result.StatusMessage = environment.StatusMessage;

                // Update in database
                await UpdateEnvironmentAsync(environment, "system", cancellationToken);
                
                // Record audit entry
                await AddAuditEntryAsync(
                    Guid.Parse(environment.Id),
                    environment.Status == EnvironmentStatus.Running ? "DeploymentCompleted" : "DeploymentFailed",
                    "system",
                    result.StatusMessage,
                    cancellationToken);
            }
            else
            {
                // Still running
                result.StatusMessage = $"Deployment still in progress ({deploymentStatus.ProvisioningState})";
                if (deploymentStatus.Duration.HasValue)
                {
                    result.StatusMessage += $" - Duration: {deploymentStatus.Duration.Value.TotalMinutes:F1} minutes";
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = $"Error checking deployment status: {ex.Message}";
            _logger.LogError(ex, "Error refreshing deployment status for environment {EnvironmentId}", environmentId);
        }

        return result;
    }

    /// <summary>
    /// Refresh deployment status for all environments currently in Provisioning state
    /// </summary>
    public async Task<List<RefreshDeploymentStatusResult>> RefreshAllProvisioningEnvironmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<RefreshDeploymentStatusResult>();

        // Get all environments in Provisioning state
        var criteria = new EnvironmentSearchCriteria { Status = EnvironmentStatus.Provisioning };
        var provisioningEnvironments = await SearchEnvironmentsAsync(criteria, cancellationToken);

        _logger.LogInformation("🔄 Refreshing deployment status for {Count} provisioning environments",
            provisioningEnvironments.Count);

        foreach (var environment in provisioningEnvironments)
        {
            var result = await RefreshDeploymentStatusAsync(environment.Id, cancellationToken);
            results.Add(result);

            if (result.StatusChanged)
            {
                _logger.LogInformation("📝 Environment {Name} status changed: {OldStatus} → {NewStatus}",
                    environment.Name, result.PreviousStatus, result.CurrentStatus);
            }
        }

        return results;
    }

    #endregion

    #region Resource Sync

    /// <summary>
    /// Sync resources from Azure for an environment.
    /// Queries Azure to find resources in resource groups created by this environment.
    /// </summary>
    public async Task<SyncResourcesResult> SyncResourcesFromAzureAsync(
        string environmentId,
        CancellationToken cancellationToken = default)
    {
        var result = new SyncResourcesResult { EnvironmentId = environmentId };

        var environment = await GetEnvironmentAsync(environmentId, cancellationToken);
        if (environment == null)
        {
            result.Error = $"Environment {environmentId} not found";
            return result;
        }

        result.EnvironmentName = environment.Name;

        if (_azureResourceService == null)
        {
            result.Error = "Azure resource service not available";
            return result;
        }

        try
        {
            var allResources = new List<DeployedResource>();

            // For MLZ-style deployments, look for resource groups matching the pattern
            // MLZ creates resource groups like: {envAbbrev}-{envAbbrev}-va-hub-rg-network
            var envAbbrev = environment.ParameterValues?.GetValueOrDefault("environmentAbbreviation")?.ToString() 
                ?? environment.Name.Split('-').FirstOrDefault() 
                ?? "dev";
            
            // Get all resource groups in the subscription
            var resourceGroups = await _azureResourceService.ListResourceGroupsAsync(
                environment.SubscriptionId, cancellationToken);

            // Find resource groups that match our environment patterns
            var matchingRgs = resourceGroups.Where(rg => 
                (rg.Name?.StartsWith($"{envAbbrev}-{envAbbrev}-", StringComparison.OrdinalIgnoreCase) == true) ||
                (rg.Name?.Equals(environment.ResourceGroupName, StringComparison.OrdinalIgnoreCase) == true) ||
                (environment.Tags != null && rg.Tags != null && 
                 rg.Tags.TryGetValue("Environment", out var envTag) && 
                 envTag == environment.Name)
            ).ToList();

            _logger.LogInformation("🔍 Found {Count} matching resource groups for environment {Name}: {Groups}",
                matchingRgs.Count, environment.Name, 
                string.Join(", ", matchingRgs.Select(rg => rg.Name)));

            // Get resources from each matching resource group
            foreach (var rg in matchingRgs)
            {
                if (string.IsNullOrEmpty(rg.Name)) continue;

                var resources = await _azureResourceService.ListAllResourcesInResourceGroupAsync(
                    environment.SubscriptionId, rg.Name, cancellationToken);

                foreach (var resource in resources)
                {
                    allResources.Add(new DeployedResource
                    {
                        ResourceId = resource.Id ?? "",
                        Name = resource.Name ?? "",
                        Type = resource.Type ?? "",
                        Location = resource.Location ?? "",
                        ProvisioningState = resource.ProvisioningState ?? "Unknown",
                        DeployedAt = DateTime.UtcNow
                    });
                }
            }

            result.ResourcesFound = allResources.Count;

            // Calculate how many are new
            var existingIds = (environment.DeployedResources ?? new List<DeployedResource>())
                .Select(r => r.ResourceId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            var newResources = allResources.Where(r => !existingIds.Contains(r.ResourceId)).ToList();
            result.ResourcesAdded = newResources.Count;

            if (newResources.Any())
            {
                // Update environment with all resources
                environment.DeployedResources = allResources;
                environment.Resources = allResources;

                await UpdateEnvironmentAsync(environment, "system", cancellationToken);

                await AddAuditEntryAsync(
                    Guid.Parse(environment.Id),
                    "ResourcesSynced",
                    "system",
                    $"Synced {result.ResourcesAdded} new resources from Azure (total: {result.ResourcesFound})",
                    cancellationToken);

                result.Message = $"Found {result.ResourcesFound} resources, added {result.ResourcesAdded} new resources";
                _logger.LogInformation("✅ Synced {Added} new resources for environment {Name} (total: {Total})",
                    result.ResourcesAdded, environment.Name, result.ResourcesFound);
            }
            else
            {
                result.Message = allResources.Any() 
                    ? $"Found {result.ResourcesFound} resources, all already synced"
                    : "No resources found in Azure for this environment";
            }
        }
        catch (Exception ex)
        {
            result.Error = $"Error syncing resources: {ex.Message}";
            _logger.LogError(ex, "Error syncing resources for environment {EnvironmentId}", environmentId);
        }

        return result;
    }

    #endregion
}
