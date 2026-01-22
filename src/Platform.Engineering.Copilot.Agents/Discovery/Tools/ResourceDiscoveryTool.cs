using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Discovery.State;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Tool for discovering Azure resources across subscriptions.
/// Provides filtering by resource group, type, location, and tags.
/// </summary>
public class ResourceDiscoveryTool : BaseTool
{
    private readonly DiscoveryStateAccessors _stateAccessors;
    private readonly IAzureResourceService _resourceService;
    private readonly ConfigService _configService;

    public override string Name => "discover_azure_resources";

    public override string Description =>
        "Discover and list Azure infrastructure resources (VMs, storage accounts, databases, etc.) with comprehensive filtering. " +
        "Search by subscription, resource group, type, location, or tags. " +
        "If subscriptionId is not provided, uses the configured default subscription. " +
        "Use for resource inventory, infrastructure discovery, and finding specific Azure resources. " +
        "NOTE: To list PROVISIONED ENVIRONMENTS (template-based deployments), use 'list_provisioned_environments' instead.";

    public ResourceDiscoveryTool(
        ILogger<ResourceDiscoveryTool> logger,
        DiscoveryStateAccessors stateAccessors,
        IAzureResourceService resourceService,
        ConfigService configService) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        // Define parameters
        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Azure subscription ID. If not provided, uses the configured default subscription.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "resourceGroup",
            description: "Resource group name to filter by (optional)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "resourceType",
            description: "Resource type to filter by (e.g., 'Microsoft.Storage/storageAccounts', optional)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "location",
            description: "Location/region to filter by (e.g., 'eastus', 'usgovvirginia', optional)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "tagFilter",
            description: "Tag filter in format 'key=value' (optional)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var resourceGroup = GetOptionalString(arguments, "resourceGroup");
        var resourceType = GetOptionalString(arguments, "resourceType");
        var location = GetOptionalString(arguments, "location");
        var tagFilter = GetOptionalString(arguments, "tagFilter");

        // Auto-fill subscription from configuration if not provided
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                Logger.LogInformation("Using configured default subscription: {SubscriptionId}", subscriptionId);
            }
        }

        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return ToJson(new { 
                success = false, 
                error = "Subscription ID is required. Either provide subscriptionId parameter or set a default using 'Set my subscription to <id>'" 
            });
        }

        Logger.LogInformation("Discovering Azure resources in subscription {SubscriptionId}", subscriptionId);

        try
        {
            // Check cache first
            var cached = _stateAccessors.GetCachedResources(subscriptionId);
            var resources = cached?.Resources ?? new List<DiscoveredResourceSummary>();
            var fromCache = cached != null;

            if (!fromCache)
            {
                // Call real Azure API to list all resources
                Logger.LogInformation("Fetching resources from Azure for subscription {SubscriptionId}", subscriptionId);
                var azureResources = await _resourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

                resources = azureResources.Select(r => new DiscoveredResourceSummary
                {
                    ResourceId = r.Id ?? string.Empty,
                    Name = r.Name ?? string.Empty,
                    Type = r.Type ?? string.Empty,
                    Location = r.Location ?? string.Empty,
                    ResourceGroup = r.ResourceGroup ?? string.Empty,
                    Tags = r.Tags ?? new Dictionary<string, string>()
                }).ToList();

                Logger.LogInformation("Retrieved {Count} resources from Azure", resources.Count);
            }

            // Apply filters
            var filteredResources = resources.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                filteredResources = filteredResources.Where(r =>
                    r.ResourceGroup.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                filteredResources = filteredResources.Where(r =>
                    r.Type.Equals(resourceType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                filteredResources = filteredResources.Where(r =>
                    r.Location.Equals(location, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(tagFilter) && tagFilter.Contains('='))
            {
                var parts = tagFilter.Split('=', 2);
                var tagKey = parts[0];
                var tagValue = parts.Length > 1 ? parts[1] : "";

                filteredResources = filteredResources.Where(r =>
                    r.Tags != null &&
                    r.Tags.TryGetValue(tagKey, out var value) &&
                    value.Equals(tagValue, StringComparison.OrdinalIgnoreCase));
            }

            var resultList = filteredResources.ToList();

            // Group results
            var byType = resultList.GroupBy(r => r.Type)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());
            var byLocation = resultList.GroupBy(r => r.Location)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());
            var byResourceGroup = resultList.GroupBy(r => r.ResourceGroup)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            // Build a formatted summary for the LLM to use
            var formattedSummary = BuildFormattedSummary(
                subscriptionId, resultList.Count, byType, byLocation, byResourceGroup, resultList);

            return ToJson(new
            {
                success = true,
                totalCount = resultList.Count,
                fromCache,
                formattedSummary,
                filters = new
                {
                    subscriptionId,
                    resourceGroup = resourceGroup ?? "all",
                    resourceType = resourceType ?? "all types",
                    location = location ?? "all locations",
                    tagFilter
                },
                summary = new
                {
                    totalResources = resultList.Count,
                    uniqueTypes = byType.Count,
                    uniqueLocations = byLocation.Count,
                    uniqueResourceGroups = byResourceGroup.Count
                },
                breakdown = new
                {
                    byType = byType.Take(10),
                    byLocation,
                    byResourceGroup = byResourceGroup.Take(10)
                },
                resources = resultList.Take(50).Select(r => new
                {
                    r.ResourceId,
                    r.Name,
                    type = r.Type,
                    r.Location,
                    r.ResourceGroup
                }),
                nextSteps = resultList.Count > 50 
                    ? "Results limited to 50 resources - use more specific filters. Say 'I want to see details for resource <resource-id>' to inspect specific resources, 'search for resources with tag Environment' to find tagged resources, or 'give me a complete inventory summary for this subscription' for a full report."
                    : "Say 'I want to see details for resource <resource-id>' to inspect specific resources, 'search for resources with tag Environment' to find tagged resources, or 'give me a complete inventory summary for this subscription' for a full report."
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error discovering resources in subscription {SubscriptionId}", subscriptionId);
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Build a formatted markdown summary for better LLM presentation
    /// </summary>
    private static string BuildFormattedSummary(
        string subscriptionId,
        int totalCount,
        Dictionary<string, int> byType,
        Dictionary<string, int> byLocation,
        Dictionary<string, int> byResourceGroup,
        List<DiscoveredResourceSummary> resources)
    {
        var sb = new System.Text.StringBuilder();
        
        // Header with emoji
        sb.AppendLine($"## 📊 Azure Resource Inventory");
        sb.AppendLine();
        sb.AppendLine($"**Subscription:** `{subscriptionId}`");
        sb.AppendLine($"**Total Resources:** {totalCount}");
        sb.AppendLine();

        // Resource types with icons
        sb.AppendLine("### 🏷️ By Resource Type");
        sb.AppendLine("| Type | Count |");
        sb.AppendLine("|------|-------|");
        foreach (var (type, count) in byType.Take(10))
        {
            var icon = DiscoveryFormatHelpers.GetResourceTypeIcon(type);
            var friendlyName = DiscoveryFormatHelpers.GetFriendlyTypeName(type);
            sb.AppendLine($"| {icon} {friendlyName} | {count} |");
        }
        if (byType.Count > 10)
            sb.AppendLine($"| ... | +{byType.Skip(10).Sum(x => x.Value)} more |");
        sb.AppendLine();

        // Locations
        sb.AppendLine("### 🌎 By Location");
        sb.AppendLine("| Region | Count |");
        sb.AppendLine("|--------|-------|");
        foreach (var (loc, count) in byLocation)
        {
            var regionName = DiscoveryFormatHelpers.GetFriendlyLocationName(loc);
            sb.AppendLine($"| {regionName} | {count} |");
        }
        sb.AppendLine();

        // Resource groups
        sb.AppendLine("### 📁 By Resource Group");
        sb.AppendLine("| Resource Group | Count |");
        sb.AppendLine("|----------------|-------|");
        foreach (var (rg, count) in byResourceGroup.Take(10))
        {
            sb.AppendLine($"| `{rg}` | {count} |");
        }
        if (byResourceGroup.Count > 10)
            sb.AppendLine($"| ... | +{byResourceGroup.Skip(10).Sum(x => x.Value)} more |");
        sb.AppendLine();

        // Resource preview (first 5)
        if (resources.Count > 0)
        {
            // If all resources are the same type, use that type name; otherwise say "Resources"
            var typeName = byType.Count == 1 
                ? DiscoveryFormatHelpers.GetFriendlyTypeName(byType.Keys.First()) + "s"
                : "Resources";
            sb.AppendLine($"### 🔍 {typeName}");
            foreach (var r in resources.Take(5))
            {
                var icon = DiscoveryFormatHelpers.GetResourceTypeIcon(r.Type);
                sb.AppendLine($"- {icon} **{r.Name}** ({DiscoveryFormatHelpers.GetFriendlyTypeName(r.Type)}) in `{r.ResourceGroup}`");
            }
            if (resources.Count > 5)
                sb.AppendLine($"- ... and {resources.Count - 5} more");
            sb.AppendLine();
        }

        sb.AppendLine("### 💡 Next Steps");
        sb.AppendLine("- Say **\"Show me details for resource `<name>`\"** to inspect a specific resource");
        sb.AppendLine("- Say **\"List resource groups\"** to see organization");
        sb.AppendLine("- Say **\"Search for resources with tag `<key>`\"** to find tagged resources");

        return sb.ToString();
    }
}
