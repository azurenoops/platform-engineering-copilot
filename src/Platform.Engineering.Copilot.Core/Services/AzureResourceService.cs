using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Stub service for managing Azure resources: sync, health, drift detection, remediation.
/// In production, this connects to Azure Resource Manager for real resource management.
/// </summary>
public class AzureResourceService : IAzureResourceService
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<AzureResourceService> _logger;

    public AzureResourceService(PlatformEngineeringCopilotContext context, ILogger<AzureResourceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<object> SyncResourcesAsync(Guid environmentId, CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { environmentId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {environmentId} not found.");

        // Stub: In production, query Azure for actual resources
        var existingResources = await _context.DeployedResources
            .Where(r => r.EnvironmentId == environmentId)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Synced resources for environment {EnvironmentId}: {Count} resources",
            environmentId, existingResources.Count);

        return new
        {
            environmentId,
            resourceCount = existingResources.Count,
            lastSynced = DateTimeOffset.UtcNow,
            status = "Synced"
        };
    }

    public async Task<IReadOnlyList<DeployedResource>> GetResourcesAsync(Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DeployedResources
            .Where(r => r.EnvironmentId == environmentId)
            .OrderBy(r => r.Type)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<object> GetResourceHealthAsync(Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        var resources = await _context.DeployedResources
            .Where(r => r.EnvironmentId == environmentId)
            .ToListAsync(cancellationToken);

        var healthResults = resources.Select(r => new
        {
            resourceId = r.AzureResourceId,
            resourceName = r.Name,
            resourceType = r.Type,
            status = r.ProvisioningState ?? "Unknown",
            isHealthy = r.ProvisioningState == "Succeeded"
        }).ToList();

        var healthyCount = healthResults.Count(r => r.isHealthy);

        return new
        {
            environmentId,
            overallStatus = healthyCount == resources.Count ? "Healthy"
                : healthyCount > 0 ? "Degraded" : "Unhealthy",
            totalResources = resources.Count,
            healthyCount,
            unhealthyCount = resources.Count - healthyCount,
            resources = healthResults
        };
    }

    public async Task<object> DetectDriftAsync(Guid environmentId, CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { environmentId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {environmentId} not found.");

        var existingDrift = await _context.DriftItems
            .Where(d => d.EnvironmentId == environmentId && !d.IsRemediated)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Drift detection for environment {EnvironmentId}: {Count} drift items",
            environmentId, existingDrift.Count);

        env.HasDrift = existingDrift.Count > 0;
        env.DriftCount = existingDrift.Count;
        env.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new
        {
            environmentId,
            hasDrift = existingDrift.Count > 0,
            driftCount = existingDrift.Count,
            lastChecked = DateTimeOffset.UtcNow,
            items = existingDrift.Select(d => new
            {
                d.Id,
                d.ResourceId,
                d.PropertyPath,
                d.ExpectedValue,
                d.ActualValue,
                severity = d.Severity.ToString(),
                d.IsRemediated
            })
        };
    }

    public async Task<object> RemediateDriftAsync(Guid environmentId, IReadOnlyList<Guid>? driftItemIds = null,
        CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { environmentId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {environmentId} not found.");

        var query = _context.DriftItems.Where(d => d.EnvironmentId == environmentId && !d.IsRemediated);

        if (driftItemIds is { Count: > 0 })
            query = query.Where(d => driftItemIds.Contains(d.Id));

        var driftItems = await query.ToListAsync(cancellationToken);
        var remediatedCount = 0;
        var failures = new List<object>();

        foreach (var item in driftItems)
        {
            // Stub: In production, apply remediation via ARM/Bicep
            item.IsRemediated = true;
            item.RemediatedAt = DateTimeOffset.UtcNow;
            remediatedCount++;
        }

        // Save remediation changes before counting remaining
        await _context.SaveChangesAsync(cancellationToken);

        // Update environment drift status
        var remainingDrift = await _context.DriftItems
            .CountAsync(d => d.EnvironmentId == environmentId && !d.IsRemediated, cancellationToken);

        env.HasDrift = remainingDrift > 0;
        env.DriftCount = remainingDrift;
        env.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Remediated {Count} drift items for environment {EnvironmentId}", remediatedCount, environmentId);

        return new
        {
            environmentId,
            remediatedCount,
            failedCount = failures.Count,
            remainingDriftCount = remainingDrift,
            failures
        };
    }

    public async Task<object> DeleteResourcesAsync(Guid environmentId, CancellationToken cancellationToken = default)
    {
        var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { environmentId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Environment {environmentId} not found.");

        var resources = await _context.DeployedResources
            .Where(r => r.EnvironmentId == environmentId)
            .ToListAsync(cancellationToken);

        // Stub: In production, delete actual Azure resources
        _context.DeployedResources.RemoveRange(resources);
        env.Status = EnvironmentStatus.Deleting;
        env.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} resources for environment {EnvironmentId}", resources.Count, environmentId);

        return new
        {
            environmentId,
            deletedCount = resources.Count,
            failedCount = 0,
            failures = Array.Empty<object>()
        };
    }
}
