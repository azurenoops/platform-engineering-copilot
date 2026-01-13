using Platform.Engineering.Copilot.Core.Models.TemplateMatching;

namespace Platform.Engineering.Copilot.Core.Interfaces.Templates;

/// <summary>
/// Interface for synchronizing templates between Git repositories and the database cache.
/// Git serves as the source of truth; the database provides fast querying and offline access.
/// </summary>
public interface IGitTemplateSyncService
{
    /// <summary>
    /// Synchronize a single template from its Git source.
    /// </summary>
    /// <param name="templateId">The template ID to sync</param>
    /// <param name="force">Force sync even if not due</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync result with status and details</returns>
    Task<GitSyncResult> SyncTemplateAsync(
        Guid templateId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronize all templates that need syncing.
    /// </summary>
    /// <param name="force">Force sync all templates regardless of schedule</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch result with counts and failures</returns>
    Task<GitSyncBatchResult> SyncAllTemplatesAsync(
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Import a template from a Git repository URL.
    /// Creates a new template in the database from the Git source.
    /// </summary>
    /// <param name="repositoryUrl">Git repository URL (GitHub or Azure DevOps)</param>
    /// <param name="branch">Branch to import from</param>
    /// <param name="path">Path to the template file within the repo</param>
    /// <param name="importedBy">User performing the import</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Import result with new template ID</returns>
    Task<GitImportResult> ImportFromGitAsync(
        string repositoryUrl,
        string branch,
        string path,
        string importedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a template has changes in Git compared to the database.
    /// </summary>
    /// <param name="templateId">Template ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Diff result indicating if changes exist</returns>
    Task<GitDiffResult> CheckForChangesAsync(
        Guid templateId,
        CancellationToken cancellationToken = default);
}
