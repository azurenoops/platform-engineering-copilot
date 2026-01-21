using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Core.Interfaces.Templates;

/// <summary>
/// Service for managing provisioned environments created from service templates.
/// </summary>
public interface IProvisionedEnvironmentService
{
    #region Environment Lifecycle

    /// <summary>
    /// Create a new environment from a service template
    /// </summary>
    Task<CreateEnvironmentResult> CreateFromTemplateAsync(
        CreateEnvironmentFromTemplateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an environment by ID
    /// </summary>
    Task<ProvisionedEnvironment?> GetEnvironmentAsync(
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an environment by name
    /// </summary>
    Task<ProvisionedEnvironment?> GetEnvironmentByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search environments by criteria
    /// </summary>
    Task<List<ProvisionedEnvironment>> SearchEnvironmentsAsync(
        EnvironmentSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all environments for a subscription
    /// </summary>
    Task<List<ProvisionedEnvironment>> ListEnvironmentsAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update environment
    /// </summary>
    Task<ProvisionedEnvironment> UpdateEnvironmentAsync(
        ProvisionedEnvironment environment,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an environment and its Azure resources
    /// </summary>
    Task<bool> DeleteEnvironmentAsync(
        string environmentId,
        string deletedBy,
        bool forceDelete = false,
        CancellationToken cancellationToken = default);

    #endregion

    #region Environment Operations

    /// <summary>
    /// Scale an environment based on its template constraints
    /// </summary>
    Task<ScaleEnvironmentResult> ScaleEnvironmentAsync(
        ScaleEnvironmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clone an environment
    /// </summary>
    Task<CreateEnvironmentResult> CloneEnvironmentAsync(
        string sourceEnvironmentId,
        string newName,
        string clonedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upgrade environment to new template version
    /// </summary>
    Task<UpgradeEnvironmentResult> UpgradeToTemplateVersionAsync(
        string environmentId,
        string newVersion,
        string upgradedBy,
        CancellationToken cancellationToken = default);

    #endregion

    #region Drift Detection

    /// <summary>
    /// Detect configuration drift for an environment
    /// </summary>
    Task<DriftDetectionResult> DetectDriftAsync(
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect drift for all environments
    /// </summary>
    Task<List<EnvironmentDriftSummary>> DetectAllDriftAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remediate drift for an environment (apply template config)
    /// </summary>
    Task<RemediateDriftResult> RemediateDriftAsync(
        string environmentId,
        List<string>? driftItemIds,
        string remediatedBy,
        CancellationToken cancellationToken = default);

    #endregion

    #region Health & Status

    /// <summary>
    /// Get health status for an environment
    /// </summary>
    Task<EnvironmentHealthStatus> GetHealthAsync(
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get environment status summary
    /// </summary>
    Task<EnvironmentStatusSummary> GetStatusSummaryAsync(
        CancellationToken cancellationToken = default);

    #endregion

    #region Expiration Management

    /// <summary>
    /// Get environments that are expiring soon
    /// </summary>
    Task<List<ProvisionedEnvironment>> GetExpiringEnvironmentsAsync(
        int withinDays = 7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extend environment expiration
    /// </summary>
    Task<ProvisionedEnvironment> ExtendExpirationAsync(
        string environmentId,
        DateTime newExpiration,
        string extendedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete expired environments (called by background job)
    /// </summary>
    Task<int> DeleteExpiredEnvironmentsAsync(
        string deletedBy,
        CancellationToken cancellationToken = default);

    #endregion

    #region Reprovision & Purge

    /// <summary>
    /// Reprovision a failed environment (retry deployment with same parameters)
    /// </summary>
    Task<CreateEnvironmentResult> ReprovisionEnvironmentAsync(
        string environmentId,
        string requestedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all soft-deleted environments
    /// </summary>
    Task<List<ProvisionedEnvironment>> GetDeletedEnvironmentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently purge a soft-deleted environment
    /// </summary>
    Task<bool> PurgeEnvironmentAsync(
        string environmentId,
        string purgedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purge all soft-deleted environments
    /// </summary>
    Task<int> PurgeAllDeletedEnvironmentsAsync(
        string purgedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete Azure resources for an environment
    /// </summary>
    Task<DeleteResourcesResult> DeleteAzureResourcesAsync(
        string environmentId,
        string deletedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sync resources from Azure for an environment.
    /// Queries Azure to find resources created by this environment's deployment.
    /// </summary>
    Task<SyncResourcesResult> SyncResourcesFromAzureAsync(
        string environmentId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Audit

    /// <summary>
    /// Get audit log for an environment
    /// </summary>
    Task<List<EnvironmentAuditEntry>> GetAuditLogAsync(
        string environmentId,
        int? maxEntries = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Deployment Status

    /// <summary>
    /// Refresh deployment status from Azure for environments in Provisioning state.
    /// Updates status to Running (if succeeded) or Failed (if failed).
    /// </summary>
    Task<RefreshDeploymentStatusResult> RefreshDeploymentStatusAsync(
        string environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh deployment status for all environments currently in Provisioning state.
    /// </summary>
    Task<List<RefreshDeploymentStatusResult>> RefreshAllProvisioningEnvironmentsAsync(
        CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// Result of refreshing deployment status from Azure
/// </summary>
public class RefreshDeploymentStatusResult
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string DeploymentId { get; set; } = string.Empty;
    public EnvironmentStatus PreviousStatus { get; set; }
    public EnvironmentStatus CurrentStatus { get; set; }
    public string? StatusMessage { get; set; }
    public bool StatusChanged { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result of syncing resources from Azure
/// </summary>
public class SyncResourcesResult
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public int ResourcesFound { get; set; }
    public int ResourcesAdded { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Audit entry for environment operations (used by IProvisionedEnvironmentService)
/// </summary>
public class EnvironmentAuditEntry
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string EnvironmentId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
