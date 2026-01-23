using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// Repository interface for Provisioned Environment database operations.
/// Provides CRUD operations for ProvisionedEnvironmentEntity and related entities.
/// </summary>
public interface IProvisionedEnvironmentRepository
{
    #region Environment CRUD

    /// <summary>
    /// Get an environment by ID
    /// </summary>
    Task<ProvisionedEnvironmentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an environment by ID (string)
    /// </summary>
    Task<ProvisionedEnvironmentEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an environment by name
    /// </summary>
    Task<ProvisionedEnvironmentEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all environments (excluding deleted)
    /// </summary>
    Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get environments by subscription ID
    /// </summary>
    Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get environments by template ID
    /// </summary>
    Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetByTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get environments by status
    /// </summary>
    Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search environments by criteria
    /// </summary>
    Task<IReadOnlyList<ProvisionedEnvironmentEntity>> SearchAsync(
        string? keyword = null,
        Guid? templateId = null,
        string? subscriptionId = null,
        string? status = null,
        string? ownerEmail = null,
        bool includeDeleted = false,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get environments with drift
    /// </summary>
    Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetWithDriftAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new environment
    /// </summary>
    Task<ProvisionedEnvironmentEntity> CreateAsync(ProvisionedEnvironmentEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing environment
    /// </summary>
    Task<ProvisionedEnvironmentEntity> UpdateAsync(ProvisionedEnvironmentEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an environment (soft delete)
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, string deletedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an environment (hard delete)
    /// </summary>
    Task<bool> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    #endregion

    #region Expiration Management

    /// <summary>
    /// Get environments expiring within a number of days
    /// </summary>
    Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetExpiringAsync(int withinDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get expired environments that are still running
    /// </summary>
    Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetExpiredAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Audit Log

    /// <summary>
    /// Add an audit log entry
    /// </summary>
    Task<EnvironmentAuditEntity> AddAuditEntryAsync(EnvironmentAuditEntity entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit log entries for an environment
    /// </summary>
    Task<IReadOnlyList<EnvironmentAuditEntity>> GetAuditEntriesAsync(
        Guid environmentId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    #endregion

    #region Statistics

    /// <summary>
    /// Get environment count by status
    /// </summary>
    Task<Dictionary<string, int>> GetCountByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get total environment count
    /// </summary>
    Task<int> GetTotalCountAsync(bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get environment count with drift
    /// </summary>
    Task<int> GetDriftCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if name is unique
    /// </summary>
    Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    #endregion
}
