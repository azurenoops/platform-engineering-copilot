using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// Repository interface for environment deployment operations.
/// Manages InfrastructureDeployments (deployment records) and related entities:
/// - DeploymentHistory (audit trail of deployment actions)
/// - EnvironmentMetrics (performance/usage metrics for deployments)
/// </summary>
public interface IInfrastructureDeploymentRepository
{
    // ==================== Deployment Operations ====================
    
    /// <summary>
    /// Get a deployment by ID
    /// </summary>
    Task<InfrastructureDeployment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a deployment by ID with all related data (Template, History, Metrics)
    /// </summary>
    Task<InfrastructureDeployment?> GetByIdWithRelatedAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a deployment by name and resource group
    /// </summary>
    Task<InfrastructureDeployment?> GetByNameAndResourceGroupAsync(string name, string resourceGroup, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all active deployments (not soft-deleted)
    /// </summary>
    Task<IReadOnlyList<InfrastructureDeployment>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments by environment type
    /// </summary>
    Task<IReadOnlyList<InfrastructureDeployment>> GetByTypeAsync(string environmentType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments by resource group
    /// </summary>
    Task<IReadOnlyList<InfrastructureDeployment>> GetByResourceGroupAsync(string resourceGroup, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments by status
    /// </summary>
    Task<IReadOnlyList<InfrastructureDeployment>> GetByStatusAsync(DeploymentStatus status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments by subscription
    /// </summary>
    Task<IReadOnlyList<InfrastructureDeployment>> GetBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get deployments with active polling
    /// </summary>
    Task<IReadOnlyList<InfrastructureDeployment>> GetWithActivePollingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search deployments with multiple filters
    /// </summary>
    Task<IReadOnlyList<InfrastructureDeployment>> SearchAsync(
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
    Task<InfrastructureDeployment> AddAsync(InfrastructureDeployment deployment, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing deployment
    /// </summary>
    Task<InfrastructureDeployment> UpdateAsync(InfrastructureDeployment deployment, CancellationToken cancellationToken = default);
    
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
}
