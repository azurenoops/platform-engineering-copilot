using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;

namespace Platform.Engineering.Copilot.Core.Interfaces.Environments;

/// <summary>
/// Service interface for recording and retrieving environment activities.
/// Activities track deployment events, drift detection, scaling, etc.
/// </summary>
public interface IEnvironmentActivityService
{
    /// <summary>
    /// Record a new activity for an environment
    /// </summary>
    Task<EnvironmentActivity> RecordActivityAsync(
        AddEnvironmentActivityRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get activities for an environment with paging
    /// </summary>
    Task<EnvironmentActivityPagedResult> GetActivitiesAsync(
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
    Task<IReadOnlyList<EnvironmentActivity>> GetRecentActivitiesAsync(
        int count = 50,
        CancellationToken cancellationToken = default);
}
