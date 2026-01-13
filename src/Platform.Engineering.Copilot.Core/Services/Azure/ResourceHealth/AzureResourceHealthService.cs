using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceHealth;
using Azure.ResourceManager.ResourceHealth.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Azure;

namespace Platform.Engineering.Copilot.Core.Services.Azure.ResourceHealth;

/// <summary>
/// Azure Resource Health Service that provides health monitoring capabilities for Azure resources.
/// Uses the Azure Resource Health API to get availability statuses and health events.
/// </summary>
public class AzureResourceHealthService : IAzureResourceHealthService
{
    private readonly ILogger<AzureResourceHealthService> _logger;
    private readonly IAzureClientFactory _clientFactory;

    public AzureResourceHealthService(
        ILogger<AzureResourceHealthService> logger,
        IAzureClientFactory clientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public async Task<ResourceHealthSummary> GetResourceHealthSummaryAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting resource health summary for subscription {SubscriptionId}", subscriptionId);

        var summary = new ResourceHealthSummary
        {
            SubscriptionId = subscriptionId,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            var armClient = _clientFactory.GetArmClient(subscriptionId);
            var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

            var resourceStatuses = new List<ResourceHealthStatus>();

            // Get availability statuses for all resources in subscription
            await foreach (var status in subscription.GetAvailabilityStatusesBySubscriptionAsync(cancellationToken: cancellationToken))
            {
                var resourceHealth = MapAvailabilityStatusToResourceHealth(status);
                resourceStatuses.Add(resourceHealth);
                summary.TotalResources++;

                switch (resourceHealth.HealthState)
                {
                    case "Available":
                        summary.HealthyResources++;
                        break;
                    case "Degraded":
                        summary.WarningResources++;
                        break;
                    case "Unavailable":
                        summary.UnhealthyResources++;
                        break;
                    default:
                        summary.UnknownResources++;
                        break;
                }
            }

            summary.Resources = resourceStatuses;
            _logger.LogInformation("Retrieved health for {TotalResources} resources: {Healthy} healthy, {Warning} degraded, {Unhealthy} unhealthy, {Unknown} unknown",
                summary.TotalResources, summary.HealthyResources, summary.WarningResources, summary.UnhealthyResources, summary.UnknownResources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource health summary for subscription {SubscriptionId}", subscriptionId);
            // Return empty summary on error rather than throwing
        }

        return summary;
    }

    public async Task<List<ResourceHealthStatus>> GetUnhealthyResourcesAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting unhealthy resources for subscription {SubscriptionId}", subscriptionId);

        var unhealthyResources = new List<ResourceHealthStatus>();

        try
        {
            var armClient = _clientFactory.GetArmClient(subscriptionId);
            var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

            // Get all availability statuses and filter client-side
            // Note: The OData filter doesn't work on properties.availabilityState
            await foreach (var status in subscription.GetAvailabilityStatusesBySubscriptionAsync(cancellationToken: cancellationToken))
            {
                var resourceHealth = MapAvailabilityStatusToResourceHealth(status);
                // Filter for non-available resources client-side
                if (resourceHealth.HealthState != "Available")
                {
                    unhealthyResources.Add(resourceHealth);
                }
            }

            _logger.LogInformation("Found {Count} unhealthy resources in subscription {SubscriptionId}", 
                unhealthyResources.Count, subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unhealthy resources for subscription {SubscriptionId}", subscriptionId);
        }

        return unhealthyResources;
    }

    public async Task<ResourceHealthStatus?> GetResourceHealthAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting health status for resource {ResourceId}", resourceId);

        try
        {
            var armClient = _clientFactory.GetArmClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            // Get availability status for specific resource
            var availabilityStatus = await armClient.GetAvailabilityStatusAsync(resourceIdentifier, cancellationToken: cancellationToken);

            if (availabilityStatus?.Value != null)
            {
                var resourceHealth = MapAvailabilityStatusToResourceHealth(availabilityStatus.Value);
                
                // Try to get health events for this resource to populate alerts
                try
                {
                    var events = new List<ResourceHealthAlert>();
                    await foreach (var healthEvent in armClient.GetHealthEventsOfSingleResourceAsync(resourceIdentifier, cancellationToken: cancellationToken))
                    {
                        events.Add(MapHealthEventToAlert(healthEvent));
                    }
                    resourceHealth.Alerts = events;
                }
                catch (Exception eventEx)
                {
                    _logger.LogWarning(eventEx, "Could not retrieve health events for resource {ResourceId}", resourceId);
                }

                _logger.LogInformation("Resource {ResourceId} health state: {HealthState}", resourceId, resourceHealth.HealthState);
                return resourceHealth;
            }
        }
        catch (global::Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Resource health not available for {ResourceId} - resource type may not support health monitoring", resourceId);
            var resourceType = ExtractResourceType(resourceId);
            return new ResourceHealthStatus
            {
                ResourceId = resourceId,
                ResourceName = ExtractResourceName(resourceId),
                ResourceType = resourceType,
                HealthState = "NotSupported",
                StatusMessage = GetNotSupportedGuidance(resourceType),
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status for resource {ResourceId}", resourceId);
        }

        return null;
    }

    /// <summary>
    /// Provides guidance for resource types that don't support Azure Resource Health
    /// </summary>
    private static string GetNotSupportedGuidance(string resourceType)
    {
        var lowerType = resourceType.ToLowerInvariant();

        // Common resource types that don't support Resource Health
        var alternatives = lowerType switch
        {
            "microsoft.cognitiveservices/accounts" =>
                "Cognitive Services doesn't support Azure Resource Health. " +
                "Alternative: Use Azure Monitor metrics (Requests, Latency, Errors) and configure alerts on the 'TotalCalls' and 'TotalErrors' metrics.",

            "microsoft.keyvault/vaults" =>
                "Key Vault has limited Resource Health support. " +
                "Alternative: Monitor via Azure Monitor metrics (ServiceApiHit, ServiceApiLatency) and enable Diagnostic Settings for audit logs.",

            "microsoft.containerregistry/registries" =>
                "Container Registry doesn't support Azure Resource Health. " +
                "Alternative: Use Azure Monitor metrics and enable Diagnostic Settings to track pull/push operations and errors.",

            "microsoft.network/networksecuritygroups" or "microsoft.network/virtualnetworks" =>
                "Network resources typically don't support Resource Health. " +
                "Alternative: Enable NSG Flow Logs and use Network Watcher for connectivity diagnostics.",

            "microsoft.managedidentity/userassignedidentities" =>
                "Managed Identities don't support Resource Health as they are identity objects, not compute resources. " +
                "Monitor the resources that use these identities instead.",

            _ =>
                $"This resource type ({resourceType}) does not support Azure Resource Health monitoring. " +
                "Alternative: Configure Azure Monitor metrics and Diagnostic Settings to send telemetry to a Log Analytics workspace. " +
                "See https://learn.microsoft.com/azure/service-health/resource-health-checks-resource-types for supported resource types."
        };

        return alternatives;
    }

    public async Task<List<ResourceHealthAlert>> GetHealthAlertsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting health alerts for subscription {SubscriptionId}", subscriptionId);

        var alerts = new List<ResourceHealthAlert>();

        try
        {
            var armClient = _clientFactory.GetArmClient(subscriptionId);
            var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

            // Get health events collection
            var healthEvents = subscription.GetResourceHealthEvents();

            await foreach (var healthEvent in healthEvents.GetAllAsync(cancellationToken: cancellationToken))
            {
                alerts.Add(MapHealthEventToAlert(healthEvent.Data));
            }

            _logger.LogInformation("Retrieved {Count} health alerts for subscription {SubscriptionId}", 
                alerts.Count, subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health alerts for subscription {SubscriptionId}", subscriptionId);
        }

        return alerts;
    }

    public async Task<ResourceHealthDashboard> GenerateHealthDashboardAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating health dashboard for subscription {SubscriptionId}", subscriptionId);

        var dashboard = new ResourceHealthDashboard
        {
            GeneratedAt = DateTime.UtcNow
        };

        try
        {
            // Get overall health summary
            dashboard.Summary = await GetResourceHealthSummaryAsync(subscriptionId, cancellationToken);

            // Group by resource type
            var byType = new Dictionary<string, ResourceHealthSummary>();
            var byLocation = new Dictionary<string, ResourceHealthSummary>();

            foreach (var resource in dashboard.Summary.Resources)
            {
                // By resource type
                if (!byType.ContainsKey(resource.ResourceType))
                {
                    byType[resource.ResourceType] = new ResourceHealthSummary
                    {
                        SubscriptionId = subscriptionId,
                        Timestamp = DateTime.UtcNow
                    };
                }
                UpdateSummaryWithResource(byType[resource.ResourceType], resource);

                // By location
                if (!byLocation.ContainsKey(resource.Location))
                {
                    byLocation[resource.Location] = new ResourceHealthSummary
                    {
                        SubscriptionId = subscriptionId,
                        Timestamp = DateTime.UtcNow
                    };
                }
                UpdateSummaryWithResource(byLocation[resource.Location], resource);
            }

            dashboard.ByResourceType = byType;
            dashboard.ByLocation = byLocation;

            // Get active alerts
            dashboard.ActiveAlerts = await GetHealthAlertsAsync(subscriptionId, cancellationToken);

            _logger.LogInformation("Generated health dashboard: {TotalResources} resources, {AlertCount} alerts, Grade: {Grade}",
                dashboard.Summary.TotalResources, dashboard.ActiveAlerts.Count, GetHealthGrade(dashboard.Summary.HealthPercentage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating health dashboard for subscription {SubscriptionId}", subscriptionId);
        }

        return dashboard;
    }

    private static ResourceHealthStatus MapAvailabilityStatusToResourceHealth(ResourceHealthAvailabilityStatus status)
    {
        var resourceId = status.Id?.ToString() ?? string.Empty;
        // The resource ID in availability status points to the availabilityStatuses resource
        // We need to extract the actual resource ID
        var actualResourceId = ExtractActualResourceId(resourceId);
        var resourceType = ExtractResourceType(actualResourceId);
        var healthState = status.Properties?.AvailabilityState?.ToString() ?? "Unknown";
        var azureMessage = status.Properties?.Summary;

        // Build status message with actionable guidance for Unknown status
        string statusMessage;
        if (healthState == "Unknown")
        {
            var guidance = GetUnknownStatusGuidance(resourceType);
            statusMessage = string.IsNullOrEmpty(azureMessage) 
                ? guidance 
                : $"{azureMessage} **How to fix:** {guidance}";
        }
        else
        {
            statusMessage = azureMessage;
        }

        return new ResourceHealthStatus
        {
            ResourceId = actualResourceId,
            ResourceName = ExtractResourceName(actualResourceId),
            ResourceType = resourceType,
            Location = status.Location?.Name ?? "unknown",
            HealthState = healthState,
            StatusMessage = statusMessage,
            Reason = status.Properties?.ReasonType?.ToString(),
            LastChecked = status.Properties?.OccuredOn?.UtcDateTime ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Provides actionable guidance for resources with Unknown health status
    /// </summary>
    private static string GetUnknownStatusGuidance(string resourceType)
    {
        var lowerType = resourceType.ToLowerInvariant();

        return lowerType switch
        {
            "microsoft.operationalinsights/workspaces" => 
                "Health status is unknown because no data has been ingested recently. To enable health monitoring: " +
                "1) Configure diagnostic settings on Azure resources to send logs to this workspace, or " +
                "2) Connect data sources like Azure VMs, containers, or applications. " +
                "Once data flows into the workspace, Azure can assess its health.",

            "microsoft.storage/storageaccounts" =>
                "Health status is unknown. Ensure the storage account has recent activity (read/write operations). " +
                "Enable Storage Analytics or Diagnostic Settings to generate telemetry for health assessment.",

            "microsoft.sql/servers" or "microsoft.sql/servers/databases" =>
                "Health status is unknown. Database health is assessed based on connectivity and query activity. " +
                "Ensure the database has active connections and enable Azure SQL Analytics for detailed monitoring.",

            "microsoft.web/sites" =>
                "Health status is unknown. App Service health is based on HTTP response codes and availability. " +
                "Enable Application Insights and configure health check endpoints for comprehensive monitoring.",

            "microsoft.compute/virtualmachines" =>
                "Health status is unknown. VM health requires the Azure Monitor Agent or VM extensions. " +
                "Install the Azure Monitor Agent and enable VM Insights for detailed health telemetry.",

            "microsoft.containerinstance/containergroups" =>
                "Health status is unknown. Container health is assessed based on container state and restarts. " +
                "Check container logs and ensure the container is running with proper health probes configured.",

            _ => 
                "Health status is unknown because Azure has insufficient telemetry to assess this resource. " +
                "To enable health monitoring, configure Diagnostic Settings to send logs and metrics to a Log Analytics workspace, " +
                "or enable Azure Monitor for this resource type."
        };
    }

    private static ResourceHealthAlert MapHealthEventToAlert(ResourceHealthEventData eventData)
    {
        var severity = eventData.Level?.ToString() switch
        {
            "Critical" => "Critical",
            "Error" => "Critical",
            "Warning" => "Warning",
            _ => "Informational"
        };

        return new ResourceHealthAlert
        {
            AlertId = eventData.Name ?? Guid.NewGuid().ToString(),
            Severity = severity,
            Title = eventData.Title ?? "Health Event",
            Description = eventData.Summary ?? eventData.Description ?? string.Empty,
            TriggeredAt = eventData.ImpactStartOn?.UtcDateTime ?? DateTime.UtcNow,
            IsActive = eventData.Status?.ToString() == "Active",
            RecommendedAction = eventData.RecommendedActions?.Actions?.FirstOrDefault()?.ActionText
        };
    }

    private static void UpdateSummaryWithResource(ResourceHealthSummary summary, ResourceHealthStatus resource)
    {
        summary.TotalResources++;
        summary.Resources.Add(resource);

        switch (resource.HealthState)
        {
            case "Available":
                summary.HealthyResources++;
                break;
            case "Degraded":
                summary.WarningResources++;
                break;
            case "Unavailable":
                summary.UnhealthyResources++;
                break;
            default:
                summary.UnknownResources++;
                break;
        }
    }

    private static string ExtractActualResourceId(string availabilityStatusId)
    {
        // Availability status ID format: /subscriptions/{sub}/resourceGroups/{rg}/providers/{type}/{name}/providers/Microsoft.ResourceHealth/availabilityStatuses/current
        // We need to extract the resource part before /providers/Microsoft.ResourceHealth
        var healthProviderIndex = availabilityStatusId.IndexOf("/providers/Microsoft.ResourceHealth", StringComparison.OrdinalIgnoreCase);
        if (healthProviderIndex > 0)
        {
            return availabilityStatusId.Substring(0, healthProviderIndex);
        }
        return availabilityStatusId;
    }

    private static string ExtractResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return "unknown";
        var parts = resourceId.Split('/');
        return parts.Length > 0 ? parts[^1] : "unknown";
    }

    private static string ExtractResourceType(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return "unknown";
        
        // Find /providers/ and extract the type
        var providersIndex = resourceId.LastIndexOf("/providers/", StringComparison.OrdinalIgnoreCase);
        if (providersIndex >= 0)
        {
            var afterProviders = resourceId.Substring(providersIndex + 11);
            var parts = afterProviders.Split('/');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}/{parts[1]}";
            }
        }
        return "unknown";
    }

    private static string GetHealthGrade(double healthPercentage)
    {
        return healthPercentage switch
        {
            >= 95 => "A",
            >= 85 => "B",
            >= 75 => "C",
            >= 60 => "D",
            _ => "F"
        };
    }
}
