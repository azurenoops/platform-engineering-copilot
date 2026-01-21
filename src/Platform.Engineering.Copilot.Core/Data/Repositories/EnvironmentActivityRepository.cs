using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// EF Core repository implementation for Environment Activity operations.
/// </summary>
public class EnvironmentActivityRepository : IEnvironmentActivityRepository
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<EnvironmentActivityRepository> _logger;

    public EnvironmentActivityRepository(
        PlatformEngineeringCopilotContext context,
        ILogger<EnvironmentActivityRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EnvironmentActivityEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentActivities
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EnvironmentActivityEntity>> GetByEnvironmentIdAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentActivities
            .Where(a => a.EnvironmentId == environmentId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<EnvironmentActivityEntity> Items, int TotalCount)> GetByEnvironmentIdPagedAsync(
        Guid environmentId,
        string? activityType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EnvironmentActivities
            .Where(a => a.EnvironmentId == environmentId);

        if (!string.IsNullOrEmpty(activityType))
            query = query.Where(a => a.ActivityType == activityType);

        if (fromDate.HasValue)
            query = query.Where(a => a.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.Timestamp <= toDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<EnvironmentActivityEntity>> GetRecentActivitiesAsync(
        int count = 50,
        CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentActivities
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnvironmentActivityEntity>> GetByTypeAsync(
        string activityType,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EnvironmentActivities
            .Where(a => a.ActivityType == activityType);

        if (fromDate.HasValue)
            query = query.Where(a => a.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.Timestamp <= toDate.Value);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<EnvironmentActivityEntity> AddAsync(
        EnvironmentActivityEntity activity,
        CancellationToken cancellationToken = default)
    {
        if (activity.Id == Guid.Empty)
            activity.Id = Guid.NewGuid();

        if (activity.Timestamp == default)
            activity.Timestamp = DateTime.UtcNow;

        _context.EnvironmentActivities.Add(activity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added activity {ActivityType} for environment {EnvironmentId}: {Description}",
            activity.ActivityType, activity.EnvironmentId, activity.Description);

        return activity;
    }

    public async Task UpdateAsync(
        EnvironmentActivityEntity activity,
        CancellationToken cancellationToken = default)
    {
        _context.EnvironmentActivities.Update(activity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default)
    {
        var count = await _context.EnvironmentActivities
            .Where(a => a.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} activities older than {CutoffDate}", count, cutoffDate);
        return count;
    }

    public async Task<int> DeleteByEnvironmentIdAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        var count = await _context.EnvironmentActivities
            .Where(a => a.EnvironmentId == environmentId)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} activities for environment {EnvironmentId}", count, environmentId);
        return count;
    }
}
