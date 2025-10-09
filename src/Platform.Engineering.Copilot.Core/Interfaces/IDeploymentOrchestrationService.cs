using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Platform.Engineering.Copilot.Core.Interfaces
{
    /// <summary>
    /// Service responsible for orchestrating actual infrastructure deployments.
    /// Handles execution of Bicep, Terraform, Kubernetes deployments across cloud providers.
    /// </summary>
    public interface IDeploymentOrchestrationService
    {
        /// <summary>
        /// Deploy Azure infrastructure using Bicep template
        /// </summary>
        Task<DeploymentResult> DeployBicepTemplateAsync(
            string templateContent,
            DeploymentOptions options,
            Dictionary<string, string>? additionalFiles = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deploy infrastructure using Terraform
        /// </summary>
        Task<DeploymentResult> DeployTerraformAsync(
            string terraformContent,
            DeploymentOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deploy Kubernetes resources
        /// </summary>
        Task<DeploymentResult> DeployKubernetesAsync(
            string kubernetesManifest,
            DeploymentOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get status of an ongoing or completed deployment
        /// </summary>
        Task<OrchestrationDeploymentStatus> GetDeploymentStatusAsync(
            string deploymentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get logs from a deployment
        /// </summary>
        Task<DeploymentLogs> GetDeploymentLogsAsync(
            string deploymentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancel an ongoing deployment
        /// </summary>
        Task<bool> CancelDeploymentAsync(
            string deploymentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate deployment template without executing
        /// </summary>
        Task<DeploymentValidationResult> ValidateDeploymentAsync(
            string templateContent,
            string templateType,
            DeploymentOptions options,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for deployment execution
    /// </summary>
    public class DeploymentOptions
    {
        public string DeploymentName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public string Location { get; set; } = "eastus";
        public string SubscriptionId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public bool WaitForCompletion { get; set; } = true;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Result of a deployment operation
    /// </summary>
    public class DeploymentResult
    {
        public bool Success { get; set; }
        public string DeploymentId { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public DeploymentState State { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public List<string> CreatedResources { get; set; } = new();
        public Dictionary<string, string> Outputs { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Status of a deployment (renamed to avoid conflict with EnvironmentManagement.DeploymentStatus)
    /// </summary>
    public class OrchestrationDeploymentStatus
    {
        public string DeploymentId { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = string.Empty;
        public DeploymentState State { get; set; }
        public int ProgressPercentage { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public List<ResourceDeploymentStatus> ResourceStatuses { get; set; } = new();
    }

    /// <summary>
    /// Status of individual resource deployment
    /// </summary>
    public class ResourceDeploymentStatus
    {
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Deployment logs
    /// </summary>
    public class DeploymentLogs
    {
        public string DeploymentId { get; set; } = string.Empty;
        public List<LogEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// Individual log entry
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "Info"; // Info, Warning, Error
        public string Message { get; set; } = string.Empty;
        public string? Source { get; set; }
    }

    /// <summary>
    /// Result of deployment validation
    /// </summary>
    public class DeploymentValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, string> EstimatedCosts { get; set; } = new();
    }

    /// <summary>
    /// Deployment state enum
    /// </summary>
    public enum DeploymentState
    {
        NotStarted,
        Queued,
        Running,
        Succeeded,
        Failed,
        Canceled,
        PartiallySucceeded
    }
}
