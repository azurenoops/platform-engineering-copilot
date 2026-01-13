using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;

namespace Platform.Engineering.Copilot.Core.Interfaces.Infrastructure
{
    /// <summary>
    /// Engine for managing Azure environment lifecycle operations (AKS, Web Apps, Function Apps, Container Apps).
    /// Provides centralized business logic for environment creation, monitoring, scaling, and management.
    /// </summary>
    public interface IEnvironmentManagementEngine
    {
        // ========== LIFECYCLE OPERATIONS ==========
        
        /// <summary>
        /// Create a new Azure environment (AKS, Web App, Function App, Container App)
        /// </summary>
        /// <param name="request">Environment creation configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Environment creation result with resource details</returns>
        Task<EnvironmentCreationResult> CreateEnvironmentAsync(
            EnvironmentCreationRequest request, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Clone an existing environment with optional data/pipeline preservation
        /// </summary>
        /// <param name="request">Clone configuration including source and targets</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Clone result with details of all cloned environments</returns>
        Task<EnvironmentCloneResult> CloneEnvironmentAsync(
            EnvironmentCloneRequest request,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Delete environment with safety checks and backup options
        /// </summary>
        /// <param name="environmentName">Name of environment to delete</param>
        /// <param name="resourceGroup">Azure resource group name</param>
        /// <param name="createBackup">Whether to create backup before deletion</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deletion result with confirmation and backup details</returns>
        Task<EnvironmentDeletionResult> DeleteEnvironmentAsync(
            string environmentName,
            string resourceGroup,
            bool createBackup = true,
            string? subscriptionId = null,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Bulk delete multiple environments based on filters
        /// </summary>
        /// <param name="filter">Filter criteria for environments to delete</param>
        /// <param name="settings">Bulk operation safety settings</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Bulk operation result with success/failure counts</returns>
        Task<BulkOperationResult> BulkDeleteEnvironmentsAsync(
            EnvironmentFilter filter,
            BulkOperationSettings settings,
            CancellationToken cancellationToken = default);
        
        // ========== MONITORING & HEALTH ==========
        
        /// <summary>
        /// Get comprehensive environment health status
        /// </summary>
        /// <param name="environmentName">Name of environment to check</param>
        /// <param name="resourceGroup">Azure resource group name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health report with resource and application status</returns>
        Task<EnvironmentHealthReport> GetEnvironmentHealthAsync(
            string environmentName,
            string resourceGroup,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get environment metrics (CPU, memory, requests, errors)
        /// </summary>
        /// <param name="environmentName">Name of environment</param>
        /// <param name="resourceGroup">Azure resource group name</param>
        /// <param name="metricsRequest">Metrics configuration and time range</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Metrics data including performance and resource usage</returns>
        Task<EnvironmentMetrics> GetEnvironmentMetricsAsync(
            string environmentName,
            string resourceGroup,
            MetricsRequest metricsRequest,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get current environment status and configuration
        /// </summary>
        /// <param name="environmentName">Name of environment</param>
        /// <param name="resourceGroup">Azure resource group name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current status, configuration, and deployment information</returns>
        Task<EnvironmentStatus> GetEnvironmentStatusAsync(
            string environmentName,
            string resourceGroup,
            string? subscriptionId = null,
            CancellationToken cancellationToken = default);
        
        // ========== OPERATIONS ==========
        
        /// <summary>
        /// Scale environment (manual or auto-scaling configuration)
        /// </summary>
        /// <param name="environmentName">Name of environment to scale</param>
        /// <param name="resourceGroup">Azure resource group name</param>
        /// <param name="scaleSettings">Scaling configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Scaling result with before/after replica counts</returns>
        Task<ScalingResult> ScaleEnvironmentAsync(
            string environmentName,
            string resourceGroup,
            ScaleSettings scaleSettings,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Migrate environment to different region or subscription
        /// </summary>
        /// <param name="request">Migration configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Migration result with new resource details</returns>
        Task<MigrationResult> MigrateEnvironmentAsync(
            MigrationRequest request,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Discover all environments across subscriptions
        /// </summary>
        /// <param name="filter">Discovery filter criteria</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of discovered environments with metadata</returns>
        Task<List<EnvironmentDiscoveryResult>> DiscoverEnvironmentsAsync(
            EnvironmentFilter filter,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Clean up old or unused environments based on policy
        /// </summary>
        /// <param name="policy">Cleanup policy with age and usage criteria</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cleanup result with deleted environments</returns>
        Task<CleanupResult> CleanupOldEnvironmentsAsync(
            CleanupPolicy policy,
            CancellationToken cancellationToken = default);
        
        // ========== ADVANCED OPERATIONS ==========
        
        /// <summary>
        /// Deploy environment from predefined template
        /// </summary>
        /// <param name="request">Template deployment configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deployment result with created resources</returns>
        Task<TemplateDeploymentResult> DeployFromTemplateAsync(
            TemplateDeploymentRequest request,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Setup compliance controls for environment (NIST, ISO, FedRAMP)
        /// </summary>
        /// <param name="environmentName">Name of environment</param>
        /// <param name="resourceGroup">Azure resource group name</param>
        /// <param name="settings">Compliance settings and standard</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Compliance setup result with applied controls</returns>
        Task<ComplianceSetupResult> SetupComplianceAsync(
            string environmentName,
            string resourceGroup,
            ComplianceSettings settings,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Orchestrate multi-region environment deployment
        /// </summary>
        /// <param name="request">Multi-region deployment configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Multi-region deployment result with all region details</returns>
        Task<MultiRegionDeploymentResult> OrchestrateMultiRegionAsync(
            MultiRegionRequest request,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// List all environments with optional filtering
        /// </summary>
        /// <param name="filter">Filter criteria for environment list</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of environment summaries matching filter</returns>
        Task<List<EnvironmentSummary>> ListEnvironmentsAsync(
            EnvironmentFilter filter,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// List all environments with optional filtering by subscription and resource group
        /// </summary>
        /// <param name="subscriptionId">Optional subscription ID filter</param>
        /// <param name="resourceGroup">Optional resource group filter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of environment statuses</returns>
        Task<List<EnvironmentStatus>> ListEnvironmentsAsync(
            string? subscriptionId = null,
            string? resourceGroup = null,
            CancellationToken cancellationToken = default);
        
        // ========== DEPLOYMENT STRATEGIES ==========
        
        /// <summary>
        /// Execute blue-green deployment strategy
        /// </summary>
        /// <param name="request">Blue-green deployment configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Blue-green deployment result</returns>
        Task<BlueGreenDeploymentResult> ExecuteBlueGreenDeploymentAsync(
            BlueGreenDeploymentRequest request,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Execute canary deployment with traffic shifting
        /// </summary>
        /// <param name="request">Canary deployment configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Canary deployment result with metrics</returns>
        Task<CanaryDeploymentResult> ExecuteCanaryDeploymentAsync(
            CanaryDeploymentRequest request,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Execute rolling update deployment
        /// </summary>
        /// <param name="request">Rolling update configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Rolling update result with instance status</returns>
        Task<RollingUpdateResult> ExecuteRollingUpdateAsync(
            RollingUpdateRequest request,
            CancellationToken cancellationToken = default);
        
        // ========== DRIFT DETECTION ==========
        
        /// <summary>
        /// Detect configuration drift for an environment
        /// </summary>
        /// <param name="environmentName">Environment to check</param>
        /// <param name="resourceGroup">Resource group name</param>
        /// <param name="compareToEnvironment">Optional environment to compare against</param>
        /// <param name="compareToResourceGroup">Resource group of comparison environment</param>
        /// <param name="subscriptionId">Optional subscription ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Drift detection result with differences</returns>
        Task<DriftDetectionResult> DetectDriftAsync(
            string environmentName,
            string resourceGroup,
            string? compareToEnvironment = null,
            string? compareToResourceGroup = null,
            string? subscriptionId = null,
            CancellationToken cancellationToken = default);
    }
}
