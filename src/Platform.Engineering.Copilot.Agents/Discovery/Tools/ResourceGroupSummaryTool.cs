using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Discovery.Configuration;
using Platform.Engineering.Copilot.Agents.Discovery.State;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using System.Text;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Tool to get comprehensive summary and analysis of a specific resource group.
/// Shows resource breakdown by type, location distribution, tag analysis, and health status.
/// </summary>
public class ResourceGroupSummaryTool : BaseTool
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly DiscoveryStateAccessors _stateAccessors;
    private readonly DiscoveryAgentOptions _options;

    public override string Name => "get_resource_group_summary";

    public override string Description =>
        "Get comprehensive summary and analysis of a specific resource group. " +
        "Shows resource breakdown by type, location distribution, tag analysis, and health status. " +
        "Use for resource group inventory, compliance, and optimization analysis.";

    public ResourceGroupSummaryTool(
        ILogger<ResourceGroupSummaryTool> logger,
        IAzureResourceService azureResourceService,
        DiscoveryStateAccessors stateAccessors,
        IOptions<DiscoveryAgentOptions> options) : base(logger)
    {
        _azureResourceService = azureResourceService;
        _stateAccessors = stateAccessors;
        _options = options.Value;

        Parameters.Add(new ToolParameter(
            "resource_group_name",
            "Resource group name to get summary for",
            required: true));

        Parameters.Add(new ToolParameter(
            "subscription_id",
            "Azure subscription ID (optional - uses default if not specified)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var resourceGroupName = GetRequiredString(arguments, "resource_group_name");
        var subscriptionId = GetOptionalString(arguments, "subscription_id");

        Logger.LogInformation("Getting summary for resource group {ResourceGroup}", resourceGroupName);

        try
        {
            // Use configured subscription if not provided
            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = _options.DefaultSubscriptionId;
            }

            // Get resource group details
            var resourceGroup = await _azureResourceService.GetResourceGroupAsync(
                resourceGroupName, subscriptionId, cancellationToken);

            if (resourceGroup == null)
            {
                return ToJson(new
                {
                    success = false,
                    error = $"Resource group not found: {resourceGroupName}",
                    subscriptionId = subscriptionId
                });
            }

            // Get all resources in the resource group
            var resources = await _azureResourceService.ListAllResourcesInResourceGroupAsync(
                subscriptionId, resourceGroupName, cancellationToken);
            var resourceList = resources.ToList();

            // Analyze resources by type
            var byType = resourceList
                .GroupBy(r => GetDynamicProperty(r, "type") ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Cast<dynamic>()
                .ToList();

            // Analyze resources by location
            var byLocation = resourceList
                .GroupBy(r => GetDynamicProperty(r, "location") ?? "Unknown")
                .Select(g => new { location = g.Key, count = g.Count() })
                .Cast<dynamic>()
                .ToList();

            // Tag analysis
            var taggedCount = resourceList.Count(r => HasTags(r));
            var untaggedCount = resourceList.Count - taggedCount;
            var tagCoverage = resourceList.Count > 0
                ? Math.Round((double)taggedCount / resourceList.Count * 100, 2)
                : 0;

            // Extract resource group properties
            var rgLocation = GetDynamicProperty(resourceGroup, "location");
            var rgTags = GetDynamicTags(resourceGroup);
            var rgProvisioningState = GetNestedProperty(resourceGroup, "properties", "provisioningState");

            // Build next steps suggestions
            var nextSteps = new List<string>();
            
            if (resourceList.Count > 20)
            {
                nextSteps.Add($"Showing first 20 of {resourceList.Count} resources - use discover_azure_resources with resource group filter to see the complete list.");
            }
            
            if (untaggedCount > 0)
            {
                nextSteps.Add($"Found {untaggedCount} resources without tags - consider tagging resources in resource group {resourceGroupName} to improve organization.");
            }
            
            nextSteps.Add("Use get_resource_details with a resource ID to inspect specific resources in this group.");
            nextSteps.Add("Use get_resource_health to check if any resources have health issues.");

            // Build formatted summary
            var formattedSummary = BuildFormattedSummary(
                resourceGroupName, rgLocation, rgProvisioningState,
                resourceList.Cast<object>().ToList(), byType, byLocation, taggedCount, untaggedCount, tagCoverage);

            return ToJson(new
            {
                success = true,
                formattedSummary,
                resourceGroup = new
                {
                    name = resourceGroupName,
                    location = rgLocation,
                    tags = rgTags,
                    provisioningState = rgProvisioningState,
                    subscriptionId = subscriptionId
                },
                summary = new
                {
                    totalResources = resourceList.Count,
                    uniqueTypes = byType.Count,
                    uniqueLocations = byLocation.Count,
                    taggedResources = taggedCount,
                    untaggedResources = untaggedCount,
                    tagCoveragePercent = tagCoverage
                },
                breakdown = new
                {
                    byType = byType,
                    byLocation = byLocation
                },
                resources = resourceList.Take(20).Select(r => new
                {
                    id = GetDynamicProperty(r, "id"),
                    name = GetDynamicProperty(r, "name"),
                    type = GetDynamicProperty(r, "type"),
                    location = GetDynamicProperty(r, "location"),
                    tags = GetDynamicTags(r)
                }),
                nextSteps = nextSteps
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting summary for resource group {ResourceGroup}", resourceGroupName);
            return ToJson(new
            {
                success = false,
                error = ex.Message,
                resourceGroup = resourceGroupName
            });
        }
    }

    private static string? GetDynamicProperty(object obj, string propertyName)
    {
        try
        {
            var property = obj.GetType().GetProperty(propertyName);
            return property?.GetValue(obj)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetNestedProperty(object obj, string outerProperty, string innerProperty)
    {
        try
        {
            var outer = obj.GetType().GetProperty(outerProperty)?.GetValue(obj);
            if (outer == null) return null;
            return outer.GetType().GetProperty(innerProperty)?.GetValue(outer)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static bool HasTags(object resource)
    {
        try
        {
            var tags = resource.GetType().GetProperty("tags")?.GetValue(resource);
            if (tags == null) return false;
            
            // Check if it's an empty dictionary
            if (tags is IDictionary<string, string> dict)
                return dict.Count > 0;
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? GetDynamicTags(object resource)
    {
        try
        {
            return resource.GetType().GetProperty("tags")?.GetValue(resource);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildFormattedSummary(
        string resourceGroupName,
        string? location,
        string? provisioningState,
        List<object> resources,
        List<dynamic> byType,
        List<dynamic> byLocation,
        int taggedCount,
        int untaggedCount,
        double tagCoverage)
    {
        var sb = new StringBuilder();
        
        var statusIcon = provisioningState == "Succeeded" ? "✅" : "⚠️";
        var locationName = DiscoveryFormatHelpers.GetFriendlyLocationName(location ?? "Unknown");
        
        sb.AppendLine($"## 📁 Resource Group: `{resourceGroupName}`");
        sb.AppendLine();
        sb.AppendLine($"| Property | Value |");
        sb.AppendLine($"|----------|-------|");
        sb.AppendLine($"| **Location** | {locationName} |");
        sb.AppendLine($"| **Status** | {statusIcon} {provisioningState} |");
        sb.AppendLine($"| **Total Resources** | {resources.Count} |");
        sb.AppendLine($"| **Tag Coverage** | {DiscoveryFormatHelpers.FormatPercentage(tagCoverage)} |");
        sb.AppendLine();

        // Resources by type
        if (byType.Any())
        {
            sb.AppendLine("### 🏷️ Resources by Type");
            sb.AppendLine("| Type | Count |");
            sb.AppendLine("|------|-------|");
            foreach (var item in byType.Take(10))
            {
                var icon = DiscoveryFormatHelpers.GetResourceTypeIcon((string)item.type);
                var friendlyName = DiscoveryFormatHelpers.GetFriendlyTypeName((string)item.type);
                sb.AppendLine($"| {icon} {friendlyName} | {item.count} |");
            }
            if (byType.Count > 10)
                sb.AppendLine($"| ... | +{byType.Skip(10).Sum(x => (int)x.count)} more |");
            sb.AppendLine();
        }

        // Tag analysis
        sb.AppendLine("### 🏷️ Tag Analysis");
        if (untaggedCount > 0)
        {
            sb.AppendLine($"⚠️ **{untaggedCount}** resources are missing tags ({100 - tagCoverage:F1}% untagged)");
            sb.AppendLine();
            sb.AppendLine("> 💡 Adding tags helps with cost allocation, compliance, and resource management.");
        }
        else
        {
            sb.AppendLine("✅ All resources are properly tagged!");
        }
        sb.AppendLine();

        // Sample resources
        if (resources.Any())
        {
            sb.AppendLine("### 🔍 Sample Resources");
            foreach (var r in resources.Take(5))
            {
                var name = GetDynamicProperty(r, "name") ?? "Unknown";
                var type = GetDynamicProperty(r, "type") ?? "Unknown";
                var icon = DiscoveryFormatHelpers.GetResourceTypeIcon(type);
                var friendlyType = DiscoveryFormatHelpers.GetFriendlyTypeName(type);
                sb.AppendLine($"- {icon} **{name}** ({friendlyType})");
            }
            if (resources.Count > 5)
                sb.AppendLine($"- ... and {resources.Count - 5} more resources");
            sb.AppendLine();
        }

        sb.AppendLine("### 💡 Next Steps");
        sb.AppendLine("- Say **\"Show me details for resource `<name>`\"** to inspect a specific resource");
        sb.AppendLine("- Say **\"Check health of resources in this group\"** to see any issues");
        if (untaggedCount > 0)
            sb.AppendLine($"- Say **\"Tag untagged resources in `{resourceGroupName}`\"** to improve organization");

        return sb.ToString();
    }
}
