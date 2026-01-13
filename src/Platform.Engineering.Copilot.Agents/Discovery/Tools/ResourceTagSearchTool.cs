using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Discovery.Configuration;
using Platform.Engineering.Copilot.Agents.Discovery.State;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using System.Text;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Tool for searching Azure resources by tags.
/// Finds resources with specific tag keys or key-value pairs for tag-based discovery,
/// compliance checks, and resource organization.
/// </summary>
public class ResourceTagSearchTool : BaseTool
{
    private readonly DiscoveryStateAccessors _stateAccessors;
    private readonly IAzureResourceService _azureResourceService;
    private readonly DiscoveryAgentOptions _options;

    public override string Name => "search_resources_by_tag";

    public override string Description =>
        "Search for Azure resources using tags. " +
        "Find resources with specific tag keys or key-value pairs. " +
        "Use for tag-based discovery, compliance checks, and resource organization.";

    public ResourceTagSearchTool(
        ILogger<ResourceTagSearchTool> logger,
        DiscoveryStateAccessors stateAccessors,
        IAzureResourceService azureResourceService,
        IOptions<DiscoveryAgentOptions> options) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _options = options?.Value ?? new DiscoveryAgentOptions();

        // Define parameters
        Parameters.Add(new ToolParameter(
            name: "subscription_id",
            description: "Azure subscription ID to search in",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "tag_key",
            description: "Tag key to search for (e.g., 'Environment', 'Owner', 'CostCenter')",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "tag_value",
            description: "Tag value to match (optional - finds all resources with the tag key if not specified)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscription_id");
        var tagKey = GetOptionalString(arguments, "tag_key");
        var tagValue = GetOptionalString(arguments, "tag_value");

        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return ToJson(new { success = false, error = "Subscription ID is required" });
        }

        if (string.IsNullOrWhiteSpace(tagKey))
        {
            return ToJson(new { success = false, error = "Tag key is required" });
        }

        Logger.LogInformation("Searching resources by tag {TagKey}={TagValue} in subscription {SubscriptionId}",
            tagKey, tagValue ?? "any", subscriptionId);

        try
        {
            // Get all resources from Azure
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            // Filter by tag
            var matchedResources = allResources.Where(r =>
            {
                if (r.Tags == null) return false;

                if (!r.Tags.ContainsKey(tagKey)) return false;

                if (string.IsNullOrWhiteSpace(tagValue)) return true;

                return r.Tags[tagKey]?.Equals(tagValue, StringComparison.OrdinalIgnoreCase) == true;
            }).ToList();

            // Apply configuration: MaxResourcesPerQuery
            if (_options.Discovery.MaxResourcesPerQuery > 0)
            {
                matchedResources = matchedResources.Take(_options.Discovery.MaxResourcesPerQuery).ToList();
            }

            // Group by resource type
            var byType = matchedResources
                .GroupBy(r => r.Type ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Cast<dynamic>()
                .ToList();

            // Group by tag value
            var byTagValue = matchedResources
                .Where(r => r.Tags != null && r.Tags.ContainsKey(tagKey))
                .GroupBy(r => r.Tags![tagKey] ?? "null")
                .Select(g => new { tagValue = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Cast<dynamic>()
                .ToList();

            // Build next steps based on results
            var nextSteps = new List<string>();
            if (matchedResources.Count == 0)
            {
                nextSteps.Add($"No resources found with tag '{tagKey}'. Try searching for a different tag or check your tag naming.");
            }
            if (matchedResources.Count > 100)
            {
                nextSteps.Add("Results limited to 100 resources - consider filtering by tag value to narrow results.");
            }
            nextSteps.Add("Say 'show me details for resource <resource-id>' to inspect specific resources.");
            nextSteps.Add("Consider adding tags to untagged resources by saying 'I need to tag resources in this subscription'.");

            // Build formatted summary
            var formattedSummary = BuildFormattedSummary(
                tagKey, tagValue, matchedResources, byType, byTagValue);

            return ToJson(new
            {
                success = true,
                subscriptionId,
                formattedSummary,
                search = new
                {
                    tagKey,
                    tagValue = tagValue ?? "any value",
                    matchType = string.IsNullOrWhiteSpace(tagValue) ? "key only" : "key and value"
                },
                summary = new
                {
                    totalMatches = matchedResources.Count,
                    uniqueTypes = byType.Count,
                    uniqueValues = byTagValue.Count
                },
                breakdown = new
                {
                    byType,
                    byTagValue
                },
                resources = matchedResources.Take(100).Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    type = r.Type,
                    resourceGroup = r.ResourceGroup,
                    location = r.Location,
                    tagValue = r.Tags?.GetValueOrDefault(tagKey),
                    allTags = r.Tags
                }),
                nextSteps
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching resources by tag in subscription {SubscriptionId}", subscriptionId);
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    private static string BuildFormattedSummary(
        string tagKey,
        string? tagValue,
        List<Core.Models.Azure.AzureResource> matchedResources,
        List<dynamic> byType,
        List<dynamic> byTagValue)
    {
        var sb = new StringBuilder();
        
        var searchDesc = string.IsNullOrWhiteSpace(tagValue) 
            ? $"tag key `{tagKey}`" 
            : $"tag `{tagKey}={tagValue}`";
        
        sb.AppendLine($"## 🏷️ Tag Search Results");
        sb.AppendLine();
        sb.AppendLine($"**Search:** Resources with {searchDesc}");
        sb.AppendLine($"**Matches:** {DiscoveryFormatHelpers.Pluralize(matchedResources.Count, "resource")}");
        sb.AppendLine();

        if (matchedResources.Count == 0)
        {
            sb.AppendLine("❌ No resources found matching this tag.");
            sb.AppendLine();
            sb.AppendLine("### 💡 Suggestions");
            sb.AppendLine("- Check the tag key spelling (tags are case-sensitive)");
            sb.AppendLine("- Try searching for just the tag key without a value");
            sb.AppendLine("- Common tag keys: `Environment`, `Owner`, `CostCenter`, `Project`");
            return sb.ToString();
        }

        // By tag value (if searching by key only)
        if (string.IsNullOrWhiteSpace(tagValue) && byTagValue.Any())
        {
            sb.AppendLine($"### 📊 Values for `{tagKey}`");
            sb.AppendLine("| Value | Count |");
            sb.AppendLine("|-------|-------|");
            foreach (var item in byTagValue.Take(10))
            {
                sb.AppendLine($"| `{item.tagValue}` | {item.count} |");
            }
            if (byTagValue.Count > 10)
                sb.AppendLine($"| ... | +{byTagValue.Skip(10).Sum(x => (int)x.count)} more |");
            sb.AppendLine();
        }

        // By resource type
        if (byType.Any())
        {
            sb.AppendLine("### 🏷️ By Resource Type");
            sb.AppendLine("| Type | Count |");
            sb.AppendLine("|------|-------|");
            foreach (var item in byType.Take(8))
            {
                var icon = DiscoveryFormatHelpers.GetResourceTypeIcon((string)item.type);
                var friendlyName = DiscoveryFormatHelpers.GetFriendlyTypeName((string)item.type);
                sb.AppendLine($"| {icon} {friendlyName} | {item.count} |");
            }
            if (byType.Count > 8)
                sb.AppendLine($"| ... | +{byType.Skip(8).Sum(x => (int)x.count)} more |");
            sb.AppendLine();
        }

        // Sample resources
        sb.AppendLine("### 🔍 Matching Resources");
        foreach (var r in matchedResources.Take(8))
        {
            var icon = DiscoveryFormatHelpers.GetResourceTypeIcon(r.Type ?? "Unknown");
            var tagVal = r.Tags?.GetValueOrDefault(tagKey) ?? "N/A";
            sb.AppendLine($"- {icon} **{r.Name}** in `{r.ResourceGroup}` → `{tagKey}={tagVal}`");
        }
        if (matchedResources.Count > 8)
            sb.AppendLine($"- ... and {matchedResources.Count - 8} more resources");
        sb.AppendLine();

        sb.AppendLine("### 💡 Next Steps");
        sb.AppendLine("- Say **\"Show me details for resource `<name>`\"** to inspect a specific resource");
        if (string.IsNullOrWhiteSpace(tagValue))
            sb.AppendLine($"- Say **\"Search for resources with tag `{tagKey}=<value>`\"** to filter by specific value");

        return sb.ToString();
    }
}
