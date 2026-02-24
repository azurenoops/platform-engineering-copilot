using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Manages provisioned environment lifecycle: CRUD, scale, clone, reprovision, soft-delete, monitoring.
/// </summary>
public class ProvisionedEnvironmentService : IProvisionedEnvironmentService
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly IDeployerFactory _deployerFactory;
    private readonly EnvironmentActivityService _activityService;
    private readonly ILogger<ProvisionedEnvironmentService> _logger;

    public ProvisionedEnvironmentService(
        PlatformEngineeringCopilotContext context,
        IDeployerFactory deployerFactory,
        EnvironmentActivityService activityService,
        ILogger<ProvisionedEnvironmentService> logger)
    {
        _context = context;
        _deployerFactory = deployerFactory;
        _activityService = activityService;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<ProvisionedEnvironment> Items, int TotalCount)> GetAllAsync(
        string? subscriptionId = null, Guid? templateId = null, string? status = null, bool? hasDrift = null,
        int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var query = _context.ProvisionedEnvironments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(subscriptionId))
            query = query.Where(e => e.SubscriptionId == subscriptionId);
        if (templateId.HasValue)
            query = query.Where(e => e.TemplateId == templateId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<EnvironmentStatus>(status, true, out var statusEnum))
            query = query.Where(e => e.Status == statusEnum);
        if (hasDrift.HasValue)
            query = query.Where(e => e.HasDrift == hasDrift.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.UpdatedAt)
            .Skip(skip).Take(Math.Min(take, 100))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ProvisionedEnvironment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<ProvisionedEnvironment> CreateAsync(ProvisionedEnvironment environment, CancellationToken cancellationToken = default)
    {
        // Validate template is Published
        var template = await _context.ServiceTemplates.FindAsync(new object[] { environment.TemplateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {environment.TemplateId} not found.");

        if (template.Status != TemplateStatus.Published)
            throw new InvalidOperationException($"Cannot provision from template with status '{template.Status}'. Template must be Published.");

        environment.Id = environment.Id == Guid.Empty ? Guid.NewGuid() : environment.Id;
        environment.TemplateName = template.Name;
        environment.Status = EnvironmentStatus.Provisioning;
        environment.CreatedAt = DateTimeOffset.UtcNow;
        environment.UpdatedAt = DateTimeOffset.UtcNow;

        // Trigger deployment
        var deployer = _deployerFactory.Create();
        var deploymentId = await deployer.DeployAsync(template.TemplateId, environment.SubscriptionId,
            environment.ResourceGroup, environment.Location, environment.ParameterValuesJson, cancellationToken);
        environment.DeploymentId = deploymentId;

        _context.ProvisionedEnvironments.Add(environment);
        await _context.SaveChangesAsync(cancellationToken);

        await _activityService.RecordAsync(environment.Id, "Created",
            $"Environment '{environment.Name}' created from template '{template.Name}'",
            environment.RequestedBy, cancellationToken: cancellationToken);

        _logger.LogInformation("Created environment {EnvironmentId} '{Name}' from template {TemplateId}", environment.Id, environment.Name, template.TemplateId);
        return environment;
    }

    public async Task<object> ScaleAsync(Guid id, int? nodeCount, int? replicaCount, string? sku, string? tier,
        Dictionary<string, string>? additionalParameters = null, CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {id} not found.");

        if (env.Status != EnvironmentStatus.Running)
            throw new InvalidOperationException($"Cannot scale environment in '{env.Status}' state. Must be Running.");

        // Parse existing parameter values to capture actual current values for audit trail
        var currentParams = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(env.ParameterValuesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(env.ParameterValuesJson);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    currentParams[prop.Name] = prop.Value.Clone();
            }
            catch (JsonException)
            {
                _logger.LogWarning("Could not parse ParameterValuesJson for environment {EnvironmentId} while capturing scale previous values", id);
            }
        }

        var previousValues = new Dictionary<string, object>();
        var newValues = new Dictionary<string, object>();

        if (nodeCount.HasValue)
        {
            previousValues["nodeCount"] = currentParams.TryGetValue("nodeCount", out var pnc) && pnc.TryGetInt32(out var pncInt) ? (object)pncInt : 0;
            newValues["nodeCount"] = nodeCount.Value;
        }
        if (replicaCount.HasValue)
        {
            previousValues["replicaCount"] = currentParams.TryGetValue("replicaCount", out var prc) && prc.TryGetInt32(out var prcInt) ? (object)prcInt : 0;
            newValues["replicaCount"] = replicaCount.Value;
        }
        if (sku is not null)
        {
            previousValues["sku"] = currentParams.TryGetValue("sku", out var psku) ? psku.GetString() ?? string.Empty : string.Empty;
            newValues["sku"] = sku;
        }
        if (tier is not null)
        {
            previousValues["tier"] = currentParams.TryGetValue("tier", out var ptier) ? ptier.GetString() ?? string.Empty : string.Empty;
            newValues["tier"] = tier;
        }

        env.Status = EnvironmentStatus.Scaling;
        env.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await _activityService.RecordAsync(id, "Scaled", $"Environment scaled", cancellationToken: cancellationToken);

        return new { environmentId = id, previousValues, newValues, status = "Scaling" };
    }

    public async Task<ProvisionedEnvironment> CloneAsync(Guid sourceId, string newName, string? displayName = null,
        string? resourceGroup = null, string? subscriptionId = null, CancellationToken cancellationToken = default)
    {
        var source = await _context.ProvisionedEnvironments.FindAsync(new object[] { sourceId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {sourceId} not found.");

        var clone = new ProvisionedEnvironment
        {
            Id = Guid.NewGuid(),
            Name = newName,
            DisplayName = displayName,
            Description = source.Description,
            TemplateId = source.TemplateId,
            TemplateName = source.TemplateName,
            SubscriptionId = subscriptionId ?? source.SubscriptionId,
            ResourceGroup = resourceGroup ?? $"{source.ResourceGroup}-clone",
            Location = source.Location,
            ParameterValuesJson = source.ParameterValuesJson,
            TagsJson = source.TagsJson,
            OwnerEmail = source.OwnerEmail,
            Status = EnvironmentStatus.Provisioning,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var deployer = _deployerFactory.Create();
        clone.DeploymentId = await deployer.DeployAsync(clone.TemplateId, clone.SubscriptionId,
            clone.ResourceGroup, clone.Location, clone.ParameterValuesJson, cancellationToken);

        _context.ProvisionedEnvironments.Add(clone);
        await _context.SaveChangesAsync(cancellationToken);

        await _activityService.RecordAsync(clone.Id, "Cloned", $"Cloned from environment '{source.Name}'", cancellationToken: cancellationToken);

        _logger.LogInformation("Cloned environment {SourceId} to {CloneId} '{CloneName}'", sourceId, clone.Id, newName);
        return clone;
    }

    public async Task<ProvisionedEnvironment> ReprovisionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {id} not found.");

        if (env.Status != EnvironmentStatus.Failed)
            throw new InvalidOperationException($"Cannot reprovision environment in '{env.Status}' state. Must be Failed.");

        env.Status = EnvironmentStatus.Provisioning;
        env.StatusMessage = null;
        env.UpdatedAt = DateTimeOffset.UtcNow;

        var deployer = _deployerFactory.Create();
        env.DeploymentId = await deployer.DeployAsync(env.TemplateId, env.SubscriptionId,
            env.ResourceGroup, env.Location, env.ParameterValuesJson, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _activityService.RecordAsync(id, "Reprovisioned", "Environment reprovisioned after failure", cancellationToken: cancellationToken);

        return env;
    }

    public async Task DeleteAsync(Guid id, string deletedBy, bool force = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedBy, nameof(deletedBy));

        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {id} not found.");

        env.IsDeleted = true;
        env.DeletedAt = DateTimeOffset.UtcNow;
        env.DeletedBy = deletedBy;
        env.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _activityService.RecordAsync(id, "Deleted", $"Environment soft-deleted by {deletedBy}", deletedBy, cancellationToken: cancellationToken);
        _logger.LogInformation("Soft-deleted environment {EnvironmentId} by {DeletedBy}", id, deletedBy);
    }

    public async Task<IReadOnlyList<ProvisionedEnvironment>> GetDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .IgnoreQueryFilters()
            .Where(e => e.IsDeleted)
            .OrderByDescending(e => e.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task PurgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Soft-deleted environment {id} not found.");

        _context.ProvisionedEnvironments.Remove(env);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Purged environment {EnvironmentId}", id);
    }

    public async Task<int> PurgeAllAsync(CancellationToken cancellationToken = default)
    {
        var deleted = await _context.ProvisionedEnvironments
            .IgnoreQueryFilters()
            .Where(e => e.IsDeleted)
            .ToListAsync(cancellationToken);

        _context.ProvisionedEnvironments.RemoveRange(deleted);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Purged {Count} environments", deleted.Count);
        return deleted.Count;
    }

    public async Task<object> GetHealthAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {id} not found.");

        var status = env.HasDrift ? "Degraded" : (env.Status == EnvironmentStatus.Running ? "Healthy" : "Unhealthy");

        return new
        {
            environmentId = id,
            overallStatus = status,
            hasDrift = env.HasDrift,
            driftCount = env.DriftCount,
            estimatedMonthlyCost = env.EstimatedMonthlyCost,
            issues = env.HasDrift ? new[] { $"{env.DriftCount} drift items detected" } : Array.Empty<string>(),
            resourceHealth = Array.Empty<object>()
        };
    }

    public async Task<object> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var envs = await _context.ProvisionedEnvironments.ToListAsync(cancellationToken);

        var byStatus = envs.GroupBy(e => e.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var byTemplate = envs.Where(e => e.TemplateName is not null)
            .GroupBy(e => e.TemplateName!)
            .Select(g => new { templateName = g.Key, count = g.Count() })
            .ToList();

        return new
        {
            totalCount = envs.Count,
            healthyCount = envs.Count(e => e.Status == EnvironmentStatus.Running && !e.HasDrift),
            degradedCount = envs.Count(e => e.HasDrift),
            unhealthyCount = envs.Count(e => e.Status == EnvironmentStatus.Failed),
            byStatus,
            driftCount = envs.Count(e => e.HasDrift),
            expiringWithin7Days = envs.Count(e => e.ExpiresAt.HasValue && e.ExpiresAt.Value <= DateTimeOffset.UtcNow.AddDays(7)),
            totalEstimatedMonthlyCost = envs.Sum(e => e.EstimatedMonthlyCost ?? 0),
            byTemplate
        };
    }

    public async Task<IReadOnlyList<ProvisionedEnvironment>> GetExpiringAsync(int withinDays = 7, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(withinDays);
        return await _context.ProvisionedEnvironments
            .Where(e => e.ExpiresAt.HasValue && e.ExpiresAt.Value <= cutoff)
            .OrderBy(e => e.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProvisionedEnvironment> ExtendExpirationAsync(Guid id, DateTimeOffset newExpiresAt, CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {id} not found.");

        env.ExpiresAt = newExpiresAt;
        env.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await _activityService.RecordAsync(id, "ExpirationExtended", $"Expiration extended to {newExpiresAt:O}", cancellationToken: cancellationToken);
        return env;
    }

    public async Task<object> GetActivitiesAsync(Guid environmentId, int skip = 0, int take = 10, CancellationToken cancellationToken = default)
    {
        return await _activityService.GetActivitiesAsync(environmentId, skip, take, cancellationToken);
    }

    public async Task<object> RefreshStatusAsync(Guid id, CancellationToken cancellationToken = default)
        => await RefreshStatusInternalAsync(id, cancellationToken);

    private async Task<StatusRefreshResult> RefreshStatusInternalAsync(Guid id, CancellationToken cancellationToken)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {id} not found.");

        var previousStatus = env.Status.ToString();

        if (env.Status == EnvironmentStatus.Provisioning && env.DeploymentId is not null)
        {
            var deployer = _deployerFactory.Create();
            var deployStatus = await deployer.GetStatusAsync(env.DeploymentId, cancellationToken);

            if (deployStatus == "Succeeded")
            {
                env.Status = EnvironmentStatus.Running;
                env.StatusMessage = "Deployment completed successfully";
            }
            else if (deployStatus == "Failed")
            {
                env.Status = EnvironmentStatus.Failed;
                env.StatusMessage = "Deployment failed";
            }

            env.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new StatusRefreshResult(id, previousStatus, env.Status.ToString());
    }

    public async Task<object> RefreshAllProvisioningAsync(CancellationToken cancellationToken = default)
    {
        var provisioning = await _context.ProvisionedEnvironments
            .Where(e => e.Status == EnvironmentStatus.Provisioning)
            .ToListAsync(cancellationToken);

        var statusChanges = new List<object>();
        foreach (var env in provisioning)
        {
            var result = await RefreshStatusInternalAsync(env.Id, cancellationToken);
            if (result.StatusChanged)
                statusChanges.Add(result);
        }

        return new { refreshedCount = provisioning.Count, statusChanges };
    }

    public async Task<ProvisionedEnvironment> UpdateStatusAsync(Guid id, string status, string? reason = null, CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {id} not found.");

        if (Enum.TryParse<EnvironmentStatus>(status, true, out var newStatus))
        {
            env.Status = newStatus;
            env.StatusMessage = reason;
            env.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await _activityService.RecordAsync(id, "StatusUpdated", $"Status manually updated to {status}: {reason}", cancellationToken: cancellationToken);
        }
        else
        {
            throw new InvalidOperationException($"Invalid status value: {status}");
        }

        return env;
    }
}

/// <summary>
/// Typed result for a single environment status refresh operation.
/// </summary>
internal sealed record StatusRefreshResult(Guid EnvironmentId, string PreviousStatus, string CurrentStatus)
{
    public bool StatusChanged => PreviousStatus != CurrentStatus;
}
