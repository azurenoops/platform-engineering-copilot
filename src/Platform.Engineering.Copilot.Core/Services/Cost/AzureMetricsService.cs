using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Azure.Core;
using global::Azure.Identity;
using global::Azure.Monitor.Query;
using global::Azure.Monitor.Query.Models;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Service for retrieving Azure Monitor metrics data
/// Provides access to resource metrics, performance data, and monitoring information
/// </summary>
public class AzureMetricsService : IAzureMetricsService
{
    private readonly ILogger<AzureMetricsService> _logger;
    private readonly MetricsQueryClient _metricsClient;
    private readonly LogsQueryClient _logsClient;

    public AzureMetricsService(ILogger<AzureMetricsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize Azure credentials with default authentication
        var credential = new ChainedTokenCredential(
            new AzureCliCredential(),
            new DefaultAzureCredential());

        _metricsClient = new MetricsQueryClient(credential);
        _logsClient = new LogsQueryClient(credential);
    }

    /// <summary>
    /// Get metrics data for a specific Azure resource
    /// </summary>
    /// <param name="resourceId">The full resource ID (e.g., /subscriptions/{id}/resourceGroups/{rg}/providers/{type}/{name})</param>
    /// <param name="metricName">The metric name (e.g., "Percentage CPU", "Network In", "Memory Usage")</param>
    /// <param name="startDate">Start date for the metrics query</param>
    /// <param name="endDate">End date for the metrics query</param>
    /// <returns>List of metric data points with timestamp and value</returns>
    public async Task<List<MetricDataPoint>> GetMetricsAsync(
        string resourceId, 
        string metricName, 
        DateTime startDate, 
        DateTime endDate)
    {
        _logger.LogInformation(
            "Retrieving metrics for resource {ResourceId}, metric: {MetricName}, from {StartDate} to {EndDate}",
            resourceId, metricName, startDate, endDate);

        var dataPoints = new List<MetricDataPoint>();

        try
        {
            // Validate resource ID format
            if (string.IsNullOrEmpty(resourceId) || !resourceId.StartsWith("/subscriptions/"))
            {
                _logger.LogWarning("Invalid resource ID format: {ResourceId}", resourceId);
                return dataPoints;
            }

            // Query metrics from Azure Monitor
            var response = await _metricsClient.QueryResourceAsync(
                resourceId,
                new[] { metricName },
                new MetricsQueryOptions
                {
                    TimeRange = new QueryTimeRange(startDate, endDate),
                    Granularity = TimeSpan.FromMinutes(5), // 5-minute granularity
                    Aggregations = { MetricAggregationType.Average, MetricAggregationType.Maximum }
                });

            // Process the response
            foreach (var metric in response.Value.Metrics)
            {
                _logger.LogDebug("Processing metric: {MetricName}", metric.Name);

                foreach (var timeSeries in metric.TimeSeries)
                {
                    foreach (var dataPoint in timeSeries.Values)
                    {
                        // Use average if available, otherwise use maximum
                        var value = dataPoint.Average ?? dataPoint.Maximum ?? 0;

                        dataPoints.Add(new MetricDataPoint
                        {
                            Timestamp = dataPoint.TimeStamp.DateTime,
                            Value = value,
                            Unit = metric.Unit.ToString()
                        });
                    }
                }
            }

            _logger.LogInformation(
                "Retrieved {Count} data points for metric {MetricName}",
                dataPoints.Count, metricName);

            return dataPoints.OrderBy(dp => dp.Timestamp).ToList();
        }
        catch (global::Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning(
                "Resource or metric not found. ResourceId: {ResourceId}, Metric: {MetricName}",
                resourceId, metricName);
            return dataPoints;
        }
        catch (global::Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogError(
                "Access denied to metrics. Ensure proper RBAC permissions. ResourceId: {ResourceId}",
                resourceId);
            return dataPoints;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving metrics for resource {ResourceId}, metric: {MetricName}",
                resourceId, metricName);

            // Return empty list instead of throwing to maintain service stability
            return dataPoints;
        }
    }

    /// <summary>
    /// Get available metrics for a resource (for discovery purposes)
    /// </summary>
    public async Task<List<string>> GetAvailableMetricsAsync(string resourceId)
    {
        _logger.LogInformation("Retrieving available metrics for resource {ResourceId}", resourceId);

        try
        {
            var response = await _metricsClient.QueryResourceAsync(
                resourceId,
                new[] { "*" }, // Query all metrics
                new MetricsQueryOptions
                {
                    TimeRange = new QueryTimeRange(TimeSpan.FromHours(1))
                });

            var availableMetrics = response.Value.Metrics
                .Select(m => m.Name)
                .Distinct()
                .ToList();

            _logger.LogInformation(
                "Found {Count} available metrics for resource {ResourceId}",
                availableMetrics.Count, resourceId);

            return availableMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving available metrics for resource {ResourceId}",
                resourceId);

            return new List<string>();
        }
    }

    /// <summary>
    /// Get multiple metrics in a single call for efficiency
    /// </summary>
    public async Task<Dictionary<string, List<MetricDataPoint>>> GetMultipleMetricsAsync(
        string resourceId,
        List<string> metricNames,
        DateTime startDate,
        DateTime endDate)
    {
        _logger.LogInformation(
            "Retrieving {Count} metrics for resource {ResourceId}",
            metricNames.Count, resourceId);

        var result = new Dictionary<string, List<MetricDataPoint>>();

        try
        {
            var response = await _metricsClient.QueryResourceAsync(
                resourceId,
                metricNames,
                new MetricsQueryOptions
                {
                    TimeRange = new QueryTimeRange(startDate, endDate),
                    Granularity = TimeSpan.FromMinutes(5),
                    Aggregations = { MetricAggregationType.Average, MetricAggregationType.Maximum }
                });

            foreach (var metric in response.Value.Metrics)
            {
                var dataPoints = new List<MetricDataPoint>();

                foreach (var timeSeries in metric.TimeSeries)
                {
                    foreach (var dataPoint in timeSeries.Values)
                    {
                        var value = dataPoint.Average ?? dataPoint.Maximum ?? 0;

                        dataPoints.Add(new MetricDataPoint
                        {
                            Timestamp = dataPoint.TimeStamp.DateTime,
                            Value = value,
                            Unit = metric.Unit.ToString()
                        });
                    }
                }

                result[metric.Name] = dataPoints.OrderBy(dp => dp.Timestamp).ToList();
            }

            _logger.LogInformation(
                "Retrieved metrics for {Count} metric names",
                result.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving multiple metrics for resource {ResourceId}",
                resourceId);

            return result;
        }
    }

    /// <summary>
    /// Query logs using KQL (Kusto Query Language) - useful for advanced scenarios
    /// </summary>
    public async Task<List<Dictionary<string, object>>> QueryLogsAsync(
        string workspaceId,
        string kqlQuery,
        DateTime startDate,
        DateTime endDate)
    {
        _logger.LogInformation(
            "Executing KQL query on workspace {WorkspaceId}: {Query}",
            workspaceId, kqlQuery);

        var results = new List<Dictionary<string, object>>();

        try
        {
            var response = await _logsClient.QueryWorkspaceAsync(
                workspaceId,
                kqlQuery,
                new QueryTimeRange(startDate, endDate));

            var table = response.Value.Table;

            foreach (var row in table.Rows)
            {
                var rowData = new Dictionary<string, object>();

                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var columnName = table.Columns[i].Name;
                    var cellValue = row[i];

                    rowData[columnName] = cellValue ?? string.Empty;
                }

                results.Add(rowData);
            }

            _logger.LogInformation("KQL query returned {Count} rows", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error executing KQL query on workspace {WorkspaceId}",
                workspaceId);

            return results;
        }
    }
}
