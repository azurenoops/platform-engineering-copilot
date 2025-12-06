using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Interface for template storage operations in the universal template database
/// </summary>
public interface ITemplateStorageService
{
    /// <summary>
    /// Store a new template in the database
    /// </summary>
    /// <param name="name">Template name</param>
    /// <param name="template">Template object containing all template data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created template entity</returns>
    Task<EnvironmentTemplate> StoreTemplateAsync(string name, object template, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List all available templates
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of template entities</returns>
    Task<List<EnvironmentTemplate>> ListAllTemplatesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific template by name
    /// </summary>
    /// <param name="name">Template name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Template entity or null if not found</returns>
    Task<EnvironmentTemplate?> GetTemplateByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific template by ID
    /// </summary>
    /// <param name="id">Template ID (GUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Template entity or null if not found</returns>
    Task<EnvironmentTemplate?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a template by name
    /// </summary>
    /// <param name="name">Template name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteTemplateAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sync templates from external sources
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync result information</returns>
    Task<object> SyncTemplatesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Push a template to Git repository
    /// </summary>
    /// <param name="templateName">Template name</param>
    /// <param name="repositoryUrl">Git repository URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Push result information</returns>
    Task<object> PushTemplateToGitAsync(string templateName, string repositoryUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing template
    /// </summary>
    /// <param name="name">Template name</param>
    /// <param name="template">Updated template object</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated template entity</returns>
    Task<EnvironmentTemplate> UpdateTemplateAsync(string name, object template, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get templates by type
    /// </summary>
    /// <param name="templateType">Template type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of filtered templates</returns>
    Task<List<EnvironmentTemplate>> GetTemplatesByTypeAsync(string templateType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get template versions
    /// </summary>
    /// <param name="templateName">Template name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of template versions</returns>
    Task<List<TemplateVersion>> GetTemplateVersionsAsync(string templateName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get templates by conversation ID (from metadata)
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of templates for the conversation</returns>
    Task<List<EnvironmentTemplate>> GetTemplatesByConversationIdAsync(string conversationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the most recently created template
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Latest template or null if none exist</returns>
    Task<EnvironmentTemplate?> GetLatestTemplateAsync(CancellationToken cancellationToken = default);
}