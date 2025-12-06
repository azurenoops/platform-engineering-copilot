using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// Repository interface for environment template operations
/// </summary>
public interface IEnvironmentTemplateRepository
{
    // ==================== Template Operations ====================
    
    /// <summary>
    /// Get a template by ID
    /// </summary>
    Task<EnvironmentTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a template by ID with related files
    /// </summary>
    Task<EnvironmentTemplate?> GetByIdWithFilesAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get an active template by name
    /// </summary>
    Task<EnvironmentTemplate?> GetActiveByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get an active template by name (alias for GetActiveByNameAsync)
    /// </summary>
    Task<EnvironmentTemplate?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the most recently created active template
    /// </summary>
    Task<EnvironmentTemplate?> GetLatestAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get templates by conversation ID (searches Tags JSON field)
    /// </summary>
    Task<IReadOnlyList<EnvironmentTemplate>> GetByConversationIdAsync(string conversationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all active templates
    /// </summary>
    Task<IReadOnlyList<EnvironmentTemplate>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get templates by type
    /// </summary>
    Task<IReadOnlyList<EnvironmentTemplate>> GetByTypeAsync(string templateType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search templates by tags (JSON contains search)
    /// </summary>
    Task<IReadOnlyList<EnvironmentTemplate>> SearchByTagsAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search templates by name or description
    /// </summary>
    Task<IReadOnlyList<EnvironmentTemplate>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get soft-deleted templates by name
    /// </summary>
    Task<IReadOnlyList<EnvironmentTemplate>> GetSoftDeletedByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if an active template with the given name exists
    /// </summary>
    Task<bool> ExistsActiveAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Count active templates
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a new template
    /// </summary>
    Task<EnvironmentTemplate> AddAsync(EnvironmentTemplate template, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a new template (alias for AddAsync)
    /// </summary>
    Task<EnvironmentTemplate> CreateAsync(EnvironmentTemplate template, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clean up soft-deleted templates by name (hard delete them and unlink deployments)
    /// </summary>
    Task CleanupSoftDeletedByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing template
    /// </summary>
    Task<EnvironmentTemplate> UpdateAsync(EnvironmentTemplate template, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Soft delete a template (set IsActive = false)
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Hard delete a template and all related entities
    /// </summary>
    Task<bool> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Hard delete multiple templates by IDs
    /// </summary>
    Task<int> HardDeleteRangeAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    
    // ==================== Template File Operations ====================
    
    /// <summary>
    /// Get all files for a template
    /// </summary>
    Task<IReadOnlyList<TemplateFile>> GetFilesAsync(Guid templateId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a file to a template
    /// </summary>
    Task<TemplateFile> AddFileAsync(TemplateFile file, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple files to a template
    /// </summary>
    Task<IReadOnlyList<TemplateFile>> AddFilesAsync(IEnumerable<TemplateFile> files, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete all files for a template
    /// </summary>
    Task<int> DeleteFilesAsync(Guid templateId, CancellationToken cancellationToken = default);
    
    // ==================== Template Version Operations ====================
    
    /// <summary>
    /// Get all versions for a template
    /// </summary>
    Task<IReadOnlyList<TemplateVersion>> GetVersionsAsync(Guid templateId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the latest version for a template
    /// </summary>
    Task<TemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a new version
    /// </summary>
    Task<TemplateVersion> AddVersionAsync(TemplateVersion version, CancellationToken cancellationToken = default);
    
    // ==================== Deployment Reference Operations ====================
    
    /// <summary>
    /// Unlink all deployments from a template (set TemplateId = null)
    /// Used before hard deleting a template to preserve deployment history
    /// </summary>
    Task<int> UnlinkDeploymentsAsync(Guid templateId, CancellationToken cancellationToken = default);
}
