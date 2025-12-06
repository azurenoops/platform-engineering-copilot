using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using CoreModels = Platform.Engineering.Copilot.Core.Models;
using DataDeploymentStatus = Platform.Engineering.Copilot.Core.Data.Entities.DeploymentStatus;

namespace Platform.Engineering.Copilot.Infrastructure.Core.Services
{
    /// <summary>
    /// Storage service for environment deployment records using Repository pattern.
    /// Persists EnvironmentDeployment, DeploymentHistory, and EnvironmentMetrics to database.
    /// Maps between Core models (EnvironmentCreationResult) and Data entities (EnvironmentDeployment).
    /// </summary>
    public class EnvironmentStorageService
    {
        private readonly ILogger<EnvironmentStorageService> _logger;
        private readonly IEnvironmentDeploymentRepository _deploymentRepository;

        public EnvironmentStorageService(
            ILogger<EnvironmentStorageService> logger,
            IEnvironmentDeploymentRepository deploymentRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        }

        #region Environment Deployment CRUD

        /// <summary>
        /// Store a new environment deployment record from EnvironmentCreationResult
        /// </summary>
        public async Task<EnvironmentDeployment> StoreEnvironmentAsync(
            CoreModels.EnvironmentManagement.EnvironmentCreationResult creationResult,
            string? templateId = null,
            string? subscriptionId = null,
            string location = "eastus",
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Storing environment deployment: {Name}", creationResult.EnvironmentName);

            // Use the provided EnvironmentId if available, otherwise generate new one
            Guid environmentId;
            if (!string.IsNullOrEmpty(creationResult.EnvironmentId) && Guid.TryParse(creationResult.EnvironmentId, out var parsedId))
            {
                environmentId = parsedId;
                _logger.LogInformation("Using provided environment ID: {Id}", environmentId);
            }
            else
            {
                environmentId = Guid.NewGuid();
                _logger.LogInformation("Generated new environment ID: {Id}", environmentId);
            }

            var deployment = new EnvironmentDeployment
            {
                Id = environmentId,
                Name = creationResult.EnvironmentName,
                TemplateId = !string.IsNullOrEmpty(templateId) ? Guid.Parse(templateId) : null,
                ResourceGroupName = creationResult.ResourceGroup,
                SubscriptionId = subscriptionId ?? "default-subscription",
                Location = location,
                EnvironmentType = MapEnvironmentType(creationResult.Type),
                // Map status from creation result or use InProgress for new deployments
                Status = creationResult.Status == "Deploying" 
                    ? DataDeploymentStatus.InProgress 
                    : (creationResult.Success ? DataDeploymentStatus.Succeeded : DataDeploymentStatus.Failed),
                CreatedAt = creationResult.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                DeployedBy = "system",
                Configuration = JsonSerializer.Serialize(creationResult.Configuration),
                IsDeleted = !creationResult.Success
            };

            var result = await _deploymentRepository.AddAsync(deployment, cancellationToken);

            _logger.LogInformation("Environment deployment stored with ID: {Id}", result.Id);

            return result;
        }

        /// <summary>
        /// Get environment deployment by ID with related data
        /// </summary>
        public async Task<EnvironmentDeployment?> GetEnvironmentByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _deploymentRepository.GetByIdWithRelatedAsync(id, cancellationToken);
        }

        /// <summary>
        /// Get environment deployment by name and resource group
        /// </summary>
        public async Task<EnvironmentDeployment?> GetEnvironmentByNameAsync(
            string name,
            string resourceGroup,
            CancellationToken cancellationToken = default)
        {
            return await _deploymentRepository.GetByNameAndResourceGroupAsync(name, resourceGroup, cancellationToken);
        }

        /// <summary>
        /// List all environment deployments with optional filtering
        /// </summary>
        public async Task<List<EnvironmentDeployment>> ListEnvironmentsAsync(
            string? environmentType = null,
            string? resourceGroup = null,
            DataDeploymentStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            var results = await _deploymentRepository.SearchAsync(
                environmentType: environmentType,
                resourceGroup: resourceGroup,
                status: status,
                cancellationToken: cancellationToken);

            return results.ToList();
        }

        /// <summary>
        /// Update environment deployment status
        /// </summary>
        public async Task<bool> UpdateEnvironmentStatusAsync(
            Guid environmentId,
            DataDeploymentStatus status,
            string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _deploymentRepository.UpdateStatusAsync(environmentId, status, cancellationToken);

            if (result)
            {
                _logger.LogInformation("Environment {Id} status updated to {Status}", environmentId, status);
            }
            else
            {
                _logger.LogWarning("Environment {Id} not found for status update", environmentId);
            }

            return result;
        }

        /// <summary>
        /// Soft delete an environment deployment
        /// </summary>
        public async Task<bool> DeleteEnvironmentAsync(
            Guid environmentId,
            CancellationToken cancellationToken = default)
        {
            var result = await _deploymentRepository.SoftDeleteAsync(environmentId, cancellationToken);

            if (result)
            {
                _logger.LogInformation("Environment {Id} soft deleted", environmentId);
            }
            else
            {
                _logger.LogWarning("Environment {Id} not found for deletion", environmentId);
            }

            return result;
        }

        #endregion

        #region Deployment History

        /// <summary>
        /// Record a deployment history event
        /// </summary>
        public async Task<DeploymentHistory> RecordDeploymentHistoryAsync(
            Guid deploymentId,
            string action,
            string initiatedBy,
            bool success,
            string? details = null,
            string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Recording deployment history for {DeploymentId}: {Action}", deploymentId, action);

            return await _deploymentRepository.RecordActionAsync(
                deploymentId,
                action,
                initiatedBy,
                success,
                details,
                errorMessage,
                cancellationToken);
        }

        /// <summary>
        /// Get deployment history for an environment
        /// </summary>
        public async Task<List<DeploymentHistory>> GetDeploymentHistoryAsync(
            Guid deploymentId,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            var results = await _deploymentRepository.GetHistoryAsync(deploymentId, limit, cancellationToken);
            return results.ToList();
        }

        #endregion

        #region Environment Metrics

        /// <summary>
        /// Store environment metrics
        /// </summary>
        public async Task<EnvironmentMetrics> StoreMetricsAsync(
            Guid deploymentId,
            string metricType,
            string metricName,
            decimal value,
            string? unit = null,
            string? source = null,
            Dictionary<string, string>? labels = null,
            CancellationToken cancellationToken = default)
        {
            var metric = new EnvironmentMetrics
            {
                Id = Guid.NewGuid(),
                DeploymentId = deploymentId,
                MetricType = metricType,
                MetricName = metricName,
                Value = value,
                Unit = unit,
                Source = source ?? "custom",
                Labels = labels != null ? JsonSerializer.Serialize(labels) : null,
                Timestamp = DateTime.UtcNow
            };

            return await _deploymentRepository.AddMetricAsync(metric, cancellationToken);
        }

        /// <summary>
        /// Get metrics for an environment within a time range
        /// </summary>
        public async Task<List<EnvironmentMetrics>> GetMetricsAsync(
            Guid deploymentId,
            DateTime? startTime = null,
            DateTime? endTime = null,
            string? metricType = null,
            CancellationToken cancellationToken = default)
        {
            var results = await _deploymentRepository.GetMetricsAsync(
                deploymentId,
                startTime,
                endTime,
                metricType,
                cancellationToken);

            return results.ToList();
        }

        /// <summary>
        /// Get metrics summary with aggregations
        /// </summary>
        public async Task<Dictionary<string, object>> GetMetricsSummaryAsync(
            Guid deploymentId,
            CancellationToken cancellationToken = default)
        {
            var summary = await _deploymentRepository.GetMetricsSummaryAsync(deploymentId, cancellationToken);

            // Convert to the expected return type for backward compatibility
            var result = new Dictionary<string, object>();
            foreach (var kvp in summary)
            {
                result[kvp.Key] = new
                {
                    kvp.Value.Count,
                    AvgValue = kvp.Value.AvgValue,
                    MinValue = kvp.Value.MinValue,
                    MaxValue = kvp.Value.MaxValue,
                    LastTimestamp = kvp.Value.LastTimestamp
                };
            }

            return result;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Map Core EnvironmentType enum to database string representation
        /// </summary>
        private string MapEnvironmentType(CoreModels.EnvironmentManagement.EnvironmentType type)
        {
            return type switch
            {
                CoreModels.EnvironmentManagement.EnvironmentType.AKS => "aks",
                CoreModels.EnvironmentManagement.EnvironmentType.WebApp => "webapp",
                CoreModels.EnvironmentManagement.EnvironmentType.FunctionApp => "function",
                CoreModels.EnvironmentManagement.EnvironmentType.ContainerApp => "containerapp",
                _ => "unknown"
            };
        }

        #endregion
    }
}
