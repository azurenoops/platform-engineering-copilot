using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Service for managing Azure resources: sync, health, drift detection, and remediation.
/// </summary>
public interface IAzureResourceService
{
    Task<object> SyncResourcesAsync(Guid environmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeployedResource>> GetResourcesAsync(Guid environmentId, CancellationToken cancellationToken = default);

    Task<object> GetResourceHealthAsync(Guid environmentId, CancellationToken cancellationToken = default);

    Task<object> DetectDriftAsync(Guid environmentId, CancellationToken cancellationToken = default);

    Task<object> RemediateDriftAsync(Guid environmentId, IReadOnlyList<Guid>? driftItemIds = null,
        CancellationToken cancellationToken = default);

    Task<object> DeleteResourcesAsync(Guid environmentId, CancellationToken cancellationToken = default);
}
