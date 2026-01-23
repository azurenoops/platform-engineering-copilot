using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// Repository interface for Service Template database operations.
/// Provides CRUD operations for ServiceTemplateEntity and related entities.
/// </summary>
public interface IServiceTemplateRepository
{
    #region Template CRUD

    /// <summary>
    /// Get a template by ID
    /// </summary>
    Task<ServiceTemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a template by ID (string)
    /// </summary>
    Task<ServiceTemplateEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a template by name and optional version
    /// </summary>
    Task<ServiceTemplateEntity?> GetByNameAsync(string name, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all templates (excluding archived)
    /// </summary>
    Task<IReadOnlyList<ServiceTemplateEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get templates by status
    /// </summary>
    Task<IReadOnlyList<ServiceTemplateEntity>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get templates by category
    /// </summary>
    Task<IReadOnlyList<ServiceTemplateEntity>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search templates by keyword (name, display name, description, keywords)
    /// </summary>
    Task<IReadOnlyList<ServiceTemplateEntity>> SearchAsync(
        string? keyword = null,
        string? category = null,
        string? status = null,
        string? format = null,
        bool includeDeprecated = false,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get published templates only
    /// </summary>
    Task<IReadOnlyList<ServiceTemplateEntity>> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new template
    /// </summary>
    Task<ServiceTemplateEntity> CreateAsync(ServiceTemplateEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing template
    /// </summary>
    Task<ServiceTemplateEntity> UpdateAsync(ServiceTemplateEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a template (hard delete)
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    #endregion

    #region Audit Log

    /// <summary>
    /// Add an audit log entry
    /// </summary>
    Task<ServiceTemplateAuditEntity> AddAuditEntryAsync(ServiceTemplateAuditEntity entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit log entries for an entity
    /// </summary>
    Task<IReadOnlyList<ServiceTemplateAuditEntity>> GetAuditEntriesAsync(
        Guid entityId,
        string? entityType = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    #endregion

    #region Statistics

    /// <summary>
    /// Get template count by status
    /// </summary>
    Task<Dictionary<string, int>> GetCountByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get template count by category
    /// </summary>
    Task<Dictionary<string, int>> GetCountByCategoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get template count by format
    /// </summary>
    Task<Dictionary<string, int>> GetCountByFormatAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all unique categories
    /// </summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment deployment count for a template
    /// </summary>
    Task IncrementDeploymentCountAsync(Guid templateId, CancellationToken cancellationToken = default);

    #endregion

    #region Git Sync

    /// <summary>
    /// Get templates that need Git sync (AutoSync enabled and past sync interval)
    /// </summary>
    Task<IReadOnlyList<ServiceTemplateEntity>> GetTemplatesNeedingSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update Git sync timestamp
    /// </summary>
    Task UpdateGitSyncTimestampAsync(Guid templateId, string? commitSha, CancellationToken cancellationToken = default);

    #endregion
}
