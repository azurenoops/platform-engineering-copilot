using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// Repository interface for Environment Activity database operations.
/// Provides CRUD operations for tracking environment activity history.
/// </summary>
public interface IEnvironmentActivityRepository
{
    /// <summary>
    /// Get activity by ID
    /// </summary>
    Task<EnvironmentActivityEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all activities for an environment
    /// </summary>
    Task<IReadOnlyList<EnvironmentActivityEntity>> GetByEnvironmentIdAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get activities for an environment with paging and filtering
    /// </summary>
    Task<(IReadOnlyList<EnvironmentActivityEntity> Items, int TotalCount)> GetByEnvironmentIdPagedAsync(
        Guid environmentId,
        string? activityType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent activities across all environments
    /// </summary>
    Task<IReadOnlyList<EnvironmentActivityEntity>> GetRecentActivitiesAsync(
        int count = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get activities by type
    /// </summary>
    Task<IReadOnlyList<EnvironmentActivityEntity>> GetByTypeAsync(
        string activityType,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new activity record
    /// </summary>
    Task<EnvironmentActivityEntity> AddAsync(
        EnvironmentActivityEntity activity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing activity record (e.g., mark as completed/failed)
    /// </summary>
    Task UpdateAsync(
        EnvironmentActivityEntity activity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete activities older than a specified date
    /// </summary>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all activities for an environment (used when environment is deleted)
    /// </summary>
    Task<int> DeleteByEnvironmentIdAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default);
}
