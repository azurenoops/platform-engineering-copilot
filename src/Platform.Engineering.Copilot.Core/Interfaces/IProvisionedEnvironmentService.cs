using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Service for managing provisioned environment lifecycle: CRUD, scale, clone, reprovision, soft-delete, monitoring.
/// </summary>
public interface IProvisionedEnvironmentService
{
    Task<(IReadOnlyList<ProvisionedEnvironment> Items, int TotalCount)> GetAllAsync(
        string? subscriptionId = null,
        Guid? templateId = null,
        string? status = null,
        bool? hasDrift = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<ProvisionedEnvironment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProvisionedEnvironment> CreateAsync(ProvisionedEnvironment environment, CancellationToken cancellationToken = default);

    Task<object> ScaleAsync(Guid id, int? nodeCount, int? replicaCount, string? sku, string? tier,
        Dictionary<string, string>? additionalParameters = null, CancellationToken cancellationToken = default);

    Task<ProvisionedEnvironment> CloneAsync(Guid sourceId, string newName, string? displayName = null,
        string? resourceGroup = null, string? subscriptionId = null, CancellationToken cancellationToken = default);

    Task<ProvisionedEnvironment> ReprovisionAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, string deletedBy, bool force = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProvisionedEnvironment>> GetDeletedAsync(CancellationToken cancellationToken = default);

    Task PurgeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> PurgeAllAsync(CancellationToken cancellationToken = default);

    Task<object> GetHealthAsync(Guid id, CancellationToken cancellationToken = default);

    Task<object> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProvisionedEnvironment>> GetExpiringAsync(int withinDays = 7, CancellationToken cancellationToken = default);

    Task<ProvisionedEnvironment> ExtendExpirationAsync(Guid id, DateTimeOffset newExpiresAt, CancellationToken cancellationToken = default);

    Task<object> GetActivitiesAsync(Guid environmentId, int skip = 0, int take = 10, CancellationToken cancellationToken = default);

    Task<object> RefreshStatusAsync(Guid id, CancellationToken cancellationToken = default);

    Task<object> RefreshAllProvisioningAsync(CancellationToken cancellationToken = default);

    Task<ProvisionedEnvironment> UpdateStatusAsync(Guid id, string status, string? reason = null, CancellationToken cancellationToken = default);
}
