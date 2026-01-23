using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// EF Core implementation of environment deployment repository.
/// Manages InfrastructureDeployments and related DeploymentHistory and EnvironmentMetrics.
/// </summary>
public class InfrastructureDeploymentRepository : IInfrastructureDeploymentRepository
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<InfrastructureDeploymentRepository> _logger;

    public InfrastructureDeploymentRepository(
        PlatformEngineeringCopilotContext context,
        ILogger<InfrastructureDeploymentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ==================== Deployment Operations ====================

    public async Task<InfrastructureDeployment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);
    }

    public async Task<InfrastructureDeployment?> GetByIdWithRelatedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .Include(d => d.Template)
            .Include(d => d.History)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);
    }

    public async Task<InfrastructureDeployment?> GetByNameAndResourceGroupAsync(string name, string resourceGroup, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .FirstOrDefaultAsync(d => d.Name == name && d.ResourceGroupName == resourceGroup && !d.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureDeployment>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .Include(d => d.Template)
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureDeployment>> GetByTypeAsync(string environmentType, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .Include(d => d.Template)
            .Where(d => d.EnvironmentType == environmentType && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureDeployment>> GetByResourceGroupAsync(string resourceGroup, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .Include(d => d.Template)
            .Where(d => d.ResourceGroupName == resourceGroup && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureDeployment>> GetByStatusAsync(DeploymentStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .Include(d => d.Template)
            .Where(d => d.Status == status && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureDeployment>> GetBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .Include(d => d.Template)
            .Where(d => d.SubscriptionId == subscriptionId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureDeployment>> GetWithActivePollingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .Where(d => d.IsPollingActive && !d.IsDeleted)
            .OrderBy(d => d.LastPolledAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureDeployment>> SearchAsync(
        string? environmentType = null,
        string? resourceGroup = null,
        DeploymentStatus? status = null,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InfrastructureDeployments
            .Include(d => d.Template)
            .Where(d => !d.IsDeleted);

        if (!string.IsNullOrEmpty(environmentType))
        {
            query = query.Where(d => d.EnvironmentType == environmentType);
        }

        if (!string.IsNullOrEmpty(resourceGroup))
        {
            query = query.Where(d => d.ResourceGroupName == resourceGroup);
        }

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        if (!string.IsNullOrEmpty(subscriptionId))
        {
            query = query.Where(d => d.SubscriptionId == subscriptionId);
        }

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string name, string resourceGroup, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .AnyAsync(d => d.Name == name && d.ResourceGroupName == resourceGroup && !d.IsDeleted, cancellationToken);
    }

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .CountAsync(d => !d.IsDeleted, cancellationToken);
    }

    public async Task<int> CountByStatusAsync(DeploymentStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureDeployments
            .CountAsync(d => d.Status == status && !d.IsDeleted, cancellationToken);
    }

    public async Task<InfrastructureDeployment> AddAsync(InfrastructureDeployment deployment, CancellationToken cancellationToken = default)
    {
        deployment.Id = deployment.Id == Guid.Empty ? Guid.NewGuid() : deployment.Id;
        deployment.CreatedAt = DateTime.UtcNow;
        deployment.UpdatedAt = DateTime.UtcNow;

        _context.InfrastructureDeployments.Add(deployment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created InfrastructureDeployment {DeploymentId}: {DeploymentName}", deployment.Id, deployment.Name);

        return deployment;
    }

    public async Task<InfrastructureDeployment> UpdateAsync(InfrastructureDeployment deployment, CancellationToken cancellationToken = default)
    {
        deployment.UpdatedAt = DateTime.UtcNow;

        _context.InfrastructureDeployments.Update(deployment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated InfrastructureDeployment {DeploymentId}: {DeploymentName}", deployment.Id, deployment.Name);

        return deployment;
    }

    public async Task<bool> UpdateStatusAsync(Guid deploymentId, DeploymentStatus status, CancellationToken cancellationToken = default)
    {
        var deployment = await _context.InfrastructureDeployments.FindAsync(new object[] { deploymentId }, cancellationToken);
        if (deployment == null)
        {
            _logger.LogWarning("Deployment {DeploymentId} not found for status update", deploymentId);
            return false;
        }

        deployment.Status = status;
        deployment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated deployment {DeploymentId} status to {Status}", deploymentId, status);
        return true;
    }

    public async Task<bool> UpdatePollingStatusAsync(
        Guid deploymentId,
        bool isPollingActive,
        int? pollingAttempts = null,
        TimeSpan? currentPollingInterval = null,
        int? progressPercentage = null,
        TimeSpan? estimatedTimeRemaining = null,
        CancellationToken cancellationToken = default)
    {
        var deployment = await _context.InfrastructureDeployments.FindAsync(new object[] { deploymentId }, cancellationToken);
        if (deployment == null)
        {
            _logger.LogWarning("Deployment {DeploymentId} not found for polling status update", deploymentId);
            return false;
        }

        deployment.IsPollingActive = isPollingActive;
        deployment.LastPolledAt = DateTime.UtcNow;
        
        if (pollingAttempts.HasValue)
            deployment.PollingAttempts = pollingAttempts.Value;
        
        if (currentPollingInterval.HasValue)
            deployment.CurrentPollingInterval = currentPollingInterval.Value;
        
        if (progressPercentage.HasValue)
            deployment.ProgressPercentage = progressPercentage.Value;
        
        if (estimatedTimeRemaining.HasValue)
            deployment.EstimatedTimeRemaining = estimatedTimeRemaining.Value;
        
        deployment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated deployment {DeploymentId} polling status: Active={IsActive}", deploymentId, isPollingActive);
        return true;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deployment = await _context.InfrastructureDeployments.FindAsync(new object[] { id }, cancellationToken);
        if (deployment == null)
            return false;

        deployment.IsDeleted = true;
        deployment.DeletedAt = DateTime.UtcNow;
        deployment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Soft deleted InfrastructureDeployment {DeploymentId}", id);
        return true;
    }

    public async Task<bool> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deployment = await _context.InfrastructureDeployments
            .Include(d => d.History)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (deployment == null)
            return false;

        // Delete related entities first
        if (deployment.History.Any())
        {
            _context.DeploymentHistory.RemoveRange(deployment.History);
        }

        _context.InfrastructureDeployments.Remove(deployment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Hard deleted InfrastructureDeployment {DeploymentId} with all related entities", id);
        return true;
    }

    // ==================== Deployment History Operations ====================

    public async Task<IReadOnlyList<DeploymentHistory>> GetHistoryAsync(Guid deploymentId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await _context.DeploymentHistory
            .Where(h => h.DeploymentId == deploymentId)
            .OrderByDescending(h => h.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeploymentHistory?> GetLatestHistoryAsync(Guid deploymentId, CancellationToken cancellationToken = default)
    {
        return await _context.DeploymentHistory
            .Where(h => h.DeploymentId == deploymentId)
            .OrderByDescending(h => h.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DeploymentHistory> AddHistoryAsync(DeploymentHistory history, CancellationToken cancellationToken = default)
    {
        history.Id = history.Id == Guid.Empty ? Guid.NewGuid() : history.Id;

        _context.DeploymentHistory.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added DeploymentHistory {HistoryId} for deployment {DeploymentId}: {Action}", 
            history.Id, history.DeploymentId, history.Action);

        return history;
    }

    public async Task<DeploymentHistory> RecordActionAsync(
        Guid deploymentId,
        string action,
        string initiatedBy,
        bool success,
        string? details = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var history = new DeploymentHistory
        {
            Id = Guid.NewGuid(),
            DeploymentId = deploymentId,
            Action = action,
            Status = success ? "succeeded" : "failed",
            InitiatedBy = initiatedBy,
            Details = details,
            ErrorMessage = errorMessage,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Duration = TimeSpan.Zero
        };

        return await AddHistoryAsync(history, cancellationToken);
    }
}
