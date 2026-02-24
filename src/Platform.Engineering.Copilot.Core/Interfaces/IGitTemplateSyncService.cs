using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Service for importing and syncing templates from Git repositories.
/// </summary>
public interface IGitTemplateSyncService
{
    Task<ServiceTemplate> ImportFromGitAsync(string gitRepoUrl, string? branch = null, string? filePath = null,
        string? name = null, string? category = null, bool gitAutoSync = false, int gitSyncIntervalMinutes = 60,
        CancellationToken cancellationToken = default);

    Task<ServiceTemplate> SyncAsync(Guid templateId, bool force = false,
        CancellationToken cancellationToken = default);

    Task<object> SyncAllAsync(CancellationToken cancellationToken = default);

    Task<object> GetGitStatusAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task<ServiceTemplate> ResetParametersAsync(Guid templateId, CancellationToken cancellationToken = default);
}
