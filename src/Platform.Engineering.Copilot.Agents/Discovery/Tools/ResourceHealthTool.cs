using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Text;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Tool for getting resource health status and alerts.
/// Uses Azure Resource Health API for availability monitoring.
/// </summary>
public class ResourceHealthTool : BaseTool
{
    private readonly IAzureResourceHealthService _healthService;
    private readonly IAzureResourceService _resourceService;

    public override string Name => "get_resource_health";

    public override string Description =>
        "Get health status and alerts for Azure resources. " +
        "Returns availability state, recent health events, and recommendations. " +
        "Use for monitoring and troubleshooting resource issues.";

    public ResourceHealthTool(
        ILogger<ResourceHealthTool> logger,
        IAzureResourceHealthService healthService,
        IAzureResourceService resourceService) : base(logger)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        Parameters.Add(new ToolParameter(
            name: "resourceId",
            description: "Full Azure resource ID to check health for",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Subscription ID to get health summary for all resources",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "resourceType",
            description: "Filter health by resource type (e.g., 'Microsoft.Compute/virtualMachines')",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var resourceId = GetOptionalString(arguments, "resourceId");
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var resourceType = GetOptionalString(arguments, "resourceType");

        if (string.IsNullOrWhiteSpace(resourceId) && string.IsNullOrWhiteSpace(subscriptionId))
        {
            return ToJson(new { success = false, error = "Either resourceId or subscriptionId is required" });
        }

        Logger.LogInformation("Getting resource health for {ResourceId} / subscription {SubscriptionId}",
            resourceId, subscriptionId);

        try
        {
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                // Single resource health
                Logger.LogInformation("Fetching health status for resource: {ResourceId}", resourceId);
                
                var healthStatus = await _healthService.GetResourceHealthAsync(resourceId, cancellationToken);
                
                // Build health response
                var health = new
                {
                    availabilityState = healthStatus?.HealthState ?? "Unknown",
                    summary = healthStatus?.StatusMessage ?? "Health status unavailable",
                    reasonType = healthStatus?.Reason,
                    occurredTime = healthStatus?.LastChecked ?? DateTime.UtcNow,
                    reportedTime = DateTime.UtcNow
                };
                
                var recommendations = new List<object>();
                
                // Add any alerts as recommendations
                if (healthStatus?.Alerts != null)
                {
                    foreach (var alert in healthStatus.Alerts)
                    {
                        recommendations.Add(new
                        {
                            category = alert.Title ?? "Health",
                            impact = alert.Severity ?? "Medium",
                            recommendation = alert.Description ?? alert.RecommendedAction ?? "Review resource health"
                        });
                    }
                }

                var formattedSummary = BuildSingleResourceHealthSummary(
                    resourceId, 
                    healthStatus?.HealthState ?? "Unknown",
                    healthStatus?.StatusMessage ?? "Health status unavailable",
                    healthStatus?.Reason,
                    healthStatus?.LastChecked ?? DateTime.UtcNow,
                    recommendations);

                return ToJson(new
                {
                    success = true,
                    resource = resourceId,
                    formattedSummary,
                    health,
                    recentEvents = Array.Empty<object>(),
                    recommendations,
                    dataSource = healthStatus != null ? "AzureResourceHealth" : "Unavailable"
                });
            }
            else
            {
                // Subscription-level health summary
                Logger.LogInformation("Fetching health summary for subscription: {SubscriptionId}", subscriptionId);
                
                var healthSummary = await _healthService.GetResourceHealthSummaryAsync(subscriptionId!, cancellationToken);
                
                // Get unhealthy resources for detailed issues list
                var unhealthyResources = await _healthService.GetUnhealthyResourcesAsync(subscriptionId!, cancellationToken);
                
                // Filter by resource type if specified
                if (!string.IsNullOrWhiteSpace(resourceType) && unhealthyResources != null)
                {
                    unhealthyResources = unhealthyResources
                        .Where(r => r.ResourceType.Equals(resourceType, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var summary = new
                {
                    totalResources = healthSummary?.TotalResources ?? 0,
                    available = healthSummary?.HealthyResources ?? 0,
                    degraded = healthSummary?.WarningResources ?? 0,
                    unavailable = healthSummary?.UnhealthyResources ?? 0,
                    unknown = healthSummary?.UnknownResources ?? 0
                };
                
                var issues = (unhealthyResources ?? new List<ResourceHealthStatus>())
                    .Select(r => new
                    {
                        resourceId = r.ResourceId,
                        resourceName = r.ResourceName,
                        resourceType = r.ResourceType,
                        availabilityState = r.HealthState,
                        summary = r.StatusMessage ?? "No details available"
                    })
                    .Cast<dynamic>()
                    .ToList();

                var formattedSummary = BuildSubscriptionHealthSummary(subscriptionId, resourceType, summary, issues);

                return ToJson(new
                {
                    success = true,
                    subscriptionId,
                    resourceTypeFilter = resourceType,
                    formattedSummary,
                    summary,
                    issues
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting resource health");
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    private static string BuildSingleResourceHealthSummary(
        string resourceId, 
        string healthState,
        string summary,
        string? reason,
        DateTime lastChecked,
        List<object> recommendations)
    {
        var sb = new StringBuilder();
        
        // Extract resource name from ID
        var parts = resourceId.Split('/');
        var resourceName = parts.Length > 0 ? parts[^1] : resourceId;
        var resourceType = parts.Length > 2 ? $"{parts[^3]}/{parts[^2]}" : "Resource";
        
        var icon = DiscoveryFormatHelpers.GetHealthIcon(healthState);
        var typeIcon = DiscoveryFormatHelpers.GetResourceTypeIcon(resourceType);
        
        sb.AppendLine($"## 🏥 Resource Health: `{resourceName}`");
        sb.AppendLine();
        sb.AppendLine($"| Property | Value |");
        sb.AppendLine($"|----------|-------|");
        sb.AppendLine($"| **Resource** | {typeIcon} {resourceName} |");
        sb.AppendLine($"| **Status** | {icon} {healthState} |");
        sb.AppendLine($"| **Summary** | {summary} |");
        if (!string.IsNullOrWhiteSpace(reason))
            sb.AppendLine($"| **Reason** | {reason} |");
        sb.AppendLine($"| **Last Checked** | {lastChecked:g} |");
        sb.AppendLine();

        if (recommendations.Count > 0)
        {
            sb.AppendLine("### 💡 Recommendations");
            foreach (dynamic rec in recommendations)
            {
                var impactIcon = ((string)rec.impact).ToLowerInvariant() switch
                {
                    "high" or "critical" => "🔴",
                    "medium" or "warning" => "🟠",
                    "low" or "informational" => "🟢",
                    _ => "🔵"
                };
                sb.AppendLine($"- {impactIcon} **{rec.category}** ({rec.impact}): {rec.recommendation}");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("### 💡 Next Steps");
        sb.AppendLine($"- Say **\"Map dependencies for `{resourceName}`\"** to see related resources");
        sb.AppendLine($"- Say **\"Show me details for `{resourceName}`\"** to see configuration");
        sb.AppendLine($"- Say **\"Scan `{resourceName}` for compliance\"** to check security posture");

        return sb.ToString();
    }

    private static string BuildSubscriptionHealthSummary(string? subscriptionId, string? resourceType, dynamic summary, List<dynamic> issues)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("## 🏥 Subscription Health Overview");
        sb.AppendLine();
        sb.AppendLine($"**Subscription:** `{subscriptionId}`");
        if (!string.IsNullOrWhiteSpace(resourceType))
            sb.AppendLine($"**Filtered by:** {DiscoveryFormatHelpers.GetFriendlyTypeName(resourceType)}");
        sb.AppendLine();

        // Health summary with visual indicators
        var available = (int)summary.available;
        var degraded = (int)summary.degraded;
        var unavailable = (int)summary.unavailable;
        var total = (int)summary.totalResources;
        var healthyPercent = total > 0 ? (double)available / total * 100 : 100;

        sb.AppendLine("### 📊 Health Summary");
        sb.AppendLine($"| Status | Count | Percentage |");
        sb.AppendLine($"|--------|-------|------------|");
        if (total > 0)
        {
            sb.AppendLine($"| ✅ Available | {available} | {available * 100.0 / total:F1}% |");
            if (degraded > 0)
                sb.AppendLine($"| ⚠️ Degraded | {degraded} | {degraded * 100.0 / total:F1}% |");
            if (unavailable > 0)
                sb.AppendLine($"| ❌ Unavailable | {unavailable} | {unavailable * 100.0 / total:F1}% |");
        }
        else
        {
            sb.AppendLine($"| ℹ️ No resources | 0 | N/A |");
        }
        sb.AppendLine();

        // Overall health bar
        sb.AppendLine($"**Overall Health:** {DiscoveryFormatHelpers.FormatPercentage(healthyPercent)}");
        sb.AppendLine();

        // Issues
        if (issues.Count > 0)
        {
            sb.AppendLine("### ⚠️ Current Issues");
            foreach (var issue in issues)
            {
                var icon = DiscoveryFormatHelpers.GetHealthIcon((string)issue.availabilityState);
                var typeIcon = DiscoveryFormatHelpers.GetResourceTypeIcon((string)issue.resourceType);
                sb.AppendLine($"- {icon} {typeIcon} **{issue.resourceName}**: {issue.summary}");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("### ✅ No Issues");
            sb.AppendLine("All resources are healthy!");
            sb.AppendLine();
        }

        sb.AppendLine("### 💡 Next Steps");
        if (issues.Count > 0)
        {
            sb.AppendLine("- Say **\"Show me details for resource `<name>`\"** to investigate issues");
            sb.AppendLine("- Say **\"Map dependencies for `<resource>`\"** to understand impact");
        }
        else
        {
            sb.AppendLine("- Say **\"Show me all resources\"** to explore your subscription");
        }

        return sb.ToString();
    }
}
