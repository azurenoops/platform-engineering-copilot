using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// Repository interface for environment deployment operations.
/// Manages EnvironmentDeployments (deployment records) and related entities:
/// - DeploymentHistory (audit trail of deployment actions)
/// - EnvironmentMetrics (performance/usage metrics for deployments)
/// </summary>
public interface IEnvironmentDeploymentRepository
{
    // ==================== Deployment Operations ====================
    
    /// <summary>
    /// Get a deployment by ID
    /// </summary>
    Task<EnvironmentDeployment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a deployment by ID with all related data (Template, History, Metrics)
    /// </summary>
    Task<EnvironmentDeployment?> GetByIdWithRelatedAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a deployment by name and resource group
    /// </summary>
    Task<EnvironmentDeployment?> GetByNameAndResourceGroupAsync(string name, string resourceGroup, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all active deployments (not soft-deleted)
    /// </summary>
    Task<IReadOnlyList<EnvironmentDeployment>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments by environment type
    /// </summary>
    Task<IReadOnlyList<EnvironmentDeployment>> GetByTypeAsync(string environmentType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments by resource group
    /// </summary>
    Task<IReadOnlyList<EnvironmentDeployment>> GetByResourceGroupAsync(string resourceGroup, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments by status
    /// </summary>
    Task<IReadOnlyList<EnvironmentDeployment>> GetByStatusAsync(DeploymentStatus status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments by subscription
    /// </summary>
    Task<IReadOnlyList<EnvironmentDeployment>> GetBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments with active polling
    /// </summary>
    Task<IReadOnlyList<EnvironmentDeployment>> GetWithActivePollingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search deployments with multiple filters
    /// </summary>
    Task<IReadOnlyList<EnvironmentDeployment>> SearchAsync(
        string? environmentType = null,
        string? resourceGroup = null,
        DeploymentStatus? status = null,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a deployment exists by name and resource group
    /// </summary>
    Task<bool> ExistsAsync(string name, string resourceGroup, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Count active deployments
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Count deployments by status
    /// </summary>
    Task<int> CountByStatusAsync(DeploymentStatus status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a new deployment
    /// </summary>
    Task<EnvironmentDeployment> AddAsync(EnvironmentDeployment deployment, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing deployment
    /// </summary>
    Task<EnvironmentDeployment> UpdateAsync(EnvironmentDeployment deployment, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update deployment status
    /// </summary>
    Task<bool> UpdateStatusAsync(Guid deploymentId, DeploymentStatus status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update polling status for a deployment
    /// </summary>
    Task<bool> UpdatePollingStatusAsync(
        Guid deploymentId, 
        bool isPollingActive, 
        int? pollingAttempts = null,
        TimeSpan? currentPollingInterval = null,
        int? progressPercentage = null,
        TimeSpan? estimatedTimeRemaining = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Soft delete a deployment
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Hard delete a deployment and all related entities
    /// </summary>
    Task<bool> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    // ==================== Deployment History Operations ====================
    
    /// <summary>
    /// Get all history for a deployment
    /// </summary>
    Task<IReadOnlyList<DeploymentHistory>> GetHistoryAsync(Guid deploymentId, int limit = 50, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the latest history entry for a deployment
    /// </summary>
    Task<DeploymentHistory?> GetLatestHistoryAsync(Guid deploymentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a history entry
    /// </summary>
    Task<DeploymentHistory> AddHistoryAsync(DeploymentHistory history, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record a deployment action (convenience method that creates a history entry)
    /// </summary>
    Task<DeploymentHistory> RecordActionAsync(
        Guid deploymentId,
        string action,
        string initiatedBy,
        bool success,
        string? details = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
    
    // ==================== Environment Metrics Operations ====================
    
    /// <summary>
    /// Get metrics for a deployment
    /// </summary>
    Task<IReadOnlyList<EnvironmentMetrics>> GetMetricsAsync(
        Guid deploymentId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? metricType = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get metrics summary with aggregations
    /// </summary>
    Task<Dictionary<string, MetricsSummary>> GetMetricsSummaryAsync(Guid deploymentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a metric entry
    /// </summary>
    Task<EnvironmentMetrics> AddMetricAsync(EnvironmentMetrics metric, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple metric entries
    /// </summary>
    Task<IReadOnlyList<EnvironmentMetrics>> AddMetricsAsync(IEnumerable<EnvironmentMetrics> metrics, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete old metrics (for cleanup/archival)
    /// </summary>
    Task<int> DeleteMetricsOlderThanAsync(Guid deploymentId, DateTime cutoffDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary of metrics for a specific metric type
/// </summary>
public class MetricsSummary
{
    public int Count { get; set; }
    public decimal AvgValue { get; set; }
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public DateTime? LastTimestamp { get; set; }
}
