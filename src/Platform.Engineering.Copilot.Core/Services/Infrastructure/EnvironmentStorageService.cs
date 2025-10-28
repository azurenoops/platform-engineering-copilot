using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Data.Entities;
using CoreModels = Platform.Engineering.Copilot.Core.Models;
using DataDeploymentStatus = Platform.Engineering.Copilot.Data.Entities.DeploymentStatus;

namespace Platform.Engineering.Copilot.Core.Services.Infrastructure
{
    /// <summary>
    /// Storage service for environment deployment records.
    /// Persists EnvironmentDeployment, DeploymentHistory, and EnvironmentMetrics to database.
    /// Maps between Core models (EnvironmentCreationResult) and Data entities (EnvironmentDeployment).
    /// </summary>
    public class EnvironmentStorageService
    {
        private readonly ILogger<EnvironmentStorageService> _logger;
        private readonly PlatformEngineeringCopilotContext _context;

        public EnvironmentStorageService(
            ILogger<EnvironmentStorageService> logger,
            PlatformEngineeringCopilotContext context)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
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

            _context.EnvironmentDeployments.Add(deployment);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Environment deployment stored with ID: {Id}", deployment.Id);

            return deployment;
        }

        /// <summary>
        /// Get environment deployment by ID with related data
        /// </summary>
        public async Task<EnvironmentDeployment?> GetEnvironmentByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _context.EnvironmentDeployments
                .Include(e => e.Template)
                .Include(e => e.History)
                .Include(e => e.Metrics)
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
        }

        /// <summary>
        /// Get environment deployment by name and resource group
        /// </summary>
        public async Task<EnvironmentDeployment?> GetEnvironmentByNameAsync(
            string name,
            string resourceGroup,
            CancellationToken cancellationToken = default)
        {
            return await _context.EnvironmentDeployments
                .FirstOrDefaultAsync(
                    e => e.Name == name && e.ResourceGroupName == resourceGroup && !e.IsDeleted,
                    cancellationToken);
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
            var query = _context.EnvironmentDeployments
                .Include(e => e.Template)
                .Where(e => !e.IsDeleted);

            if (!string.IsNullOrEmpty(environmentType))
            {
                query = query.Where(e => e.EnvironmentType == environmentType);
            }

            if (!string.IsNullOrEmpty(resourceGroup))
            {
                query = query.Where(e => e.ResourceGroupName == resourceGroup);
            }

            if (status.HasValue)
            {
                query = query.Where(e => e.Status == status.Value);
            }

            return await query
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(cancellationToken);
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
            var environment = await _context.EnvironmentDeployments
                .FirstOrDefaultAsync(e => e.Id == environmentId, cancellationToken);

            if (environment == null)
            {
                _logger.LogWarning("Environment {Id} not found for status update", environmentId);
                return false;
            }

            environment.Status = status;
            environment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Environment {Id} status updated to {Status}", environmentId, status);
            return true;
        }

        /// <summary>
        /// Soft delete an environment deployment
        /// </summary>
        public async Task<bool> DeleteEnvironmentAsync(
            Guid environmentId,
            CancellationToken cancellationToken = default)
        {
            var environment = await _context.EnvironmentDeployments
                .FirstOrDefaultAsync(e => e.Id == environmentId, cancellationToken);

            if (environment == null)
            {
                _logger.LogWarning("Environment {Id} not found for deletion", environmentId);
                return false;
            }

            environment.IsDeleted = true;
            environment.DeletedAt = DateTime.UtcNow;
            environment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Environment {Id} soft deleted", environmentId);
            return true;
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

            var history = new DeploymentHistory
            {
                Id = Guid.NewGuid(),
                DeploymentId = deploymentId,
                Action = action,
                Status = success ? "succeeded" : "failed",
                InitiatedBy = initiatedBy,
                Details = details,
                ErrorMessage = errorMessage,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Duration = TimeSpan.Zero
            };

            _context.DeploymentHistory.Add(history);
            await _context.SaveChangesAsync(cancellationToken);

            return history;
        }

        /// <summary>
        /// Get deployment history for an environment
        /// </summary>
        public async Task<List<DeploymentHistory>> GetDeploymentHistoryAsync(
            Guid deploymentId,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            return await _context.DeploymentHistory
                .Where(h => h.DeploymentId == deploymentId)
                .OrderByDescending(h => h.StartedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
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

            _context.EnvironmentMetrics.Add(metric);
            await _context.SaveChangesAsync(cancellationToken);

            return metric;
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
            var query = _context.EnvironmentMetrics
                .Where(m => m.DeploymentId == deploymentId);

            if (startTime.HasValue)
            {
                query = query.Where(m => m.Timestamp >= startTime.Value);
            }

            if (endTime.HasValue)
            {
                query = query.Where(m => m.Timestamp <= endTime.Value);
            }

            if (!string.IsNullOrEmpty(metricType))
            {
                query = query.Where(m => m.MetricType == metricType);
            }

            return await query
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Get metrics summary with aggregations
        /// </summary>
        public async Task<Dictionary<string, object>> GetMetricsSummaryAsync(
            Guid deploymentId,
            CancellationToken cancellationToken = default)
        {
            var metrics = await _context.EnvironmentMetrics
                .Where(m => m.DeploymentId == deploymentId)
                .GroupBy(m => m.MetricType)
                .Select(g => new
                {
                    MetricType = g.Key,
                    Count = g.Count(),
                    AvgValue = g.Average(m => m.Value),
                    MinValue = g.Min(m => m.Value),
                    MaxValue = g.Max(m => m.Value),
                    LastTimestamp = g.Max(m => m.Timestamp)
                })
                .ToListAsync(cancellationToken);

            var summary = new Dictionary<string, object>();
            foreach (var metric in metrics)
            {
                summary[metric.MetricType] = new
                {
                    metric.Count,
                    metric.AvgValue,
                    metric.MinValue,
                    metric.MaxValue,
                    metric.LastTimestamp
                };
            }

            return summary;
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
