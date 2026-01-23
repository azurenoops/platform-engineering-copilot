using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Discovery.Configuration;
using Platform.Engineering.Copilot.Agents.Discovery.State;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using System.Text;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Tool for listing all resource groups in a subscription with details.
/// Shows resource counts, locations, tags, and provisioning state.
/// Use for resource group inventory and organization analysis.
/// </summary>
public class ResourceGroupListTool : BaseTool
{
    private readonly DiscoveryStateAccessors _stateAccessors;
    private readonly IAzureResourceService _azureResourceService;
    private readonly DiscoveryAgentOptions _options;

    public override string Name => "list_resource_groups";

    public override string Description =>
        "List all resource groups in a subscription with details. " +
        "Shows resource counts, locations, tags, and provisioning state. " +
        "Use for resource group inventory and organization analysis.";

    public ResourceGroupListTool(
        ILogger<ResourceGroupListTool> logger,
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
            description: "Azure subscription ID (optional - uses default if not specified)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscription_id");

        // Try to resolve subscription from config if not provided
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            subscriptionId = await ResolveSubscriptionIdAsync(null);
        }

        Logger.LogInformation("Listing resource groups in subscription {SubscriptionId}", subscriptionId ?? "default");

        try
        {
            var resourceGroups = await _azureResourceService.ListResourceGroupsAsync(subscriptionId, cancellationToken);
            var rgList = resourceGroups.ToList();

            // Get resource count for each resource group
            var rgWithCounts = new List<ResourceGroupInfo>();
            foreach (var rg in rgList)
            {
                var rgName = rg.Name;
                var rgLocation = rg.Location;
                var rgTags = rg.Tags;
                var rgProvisioningState = rg.ProvisioningState;

                if (!string.IsNullOrEmpty(rgName))
                {
                    try
                    {
                        var resources = await _azureResourceService.ListAllResourcesInResourceGroupAsync(
                            subscriptionId, rgName, cancellationToken);
                        var resourceCount = resources.Count();

                        rgWithCounts.Add(new ResourceGroupInfo
                        {
                            Name = rgName,
                            Location = rgLocation,
                            Tags = rgTags,
                            ProvisioningState = rgProvisioningState,
                            ResourceCount = resourceCount
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Could not get resource count for resource group {ResourceGroup}", rgName);
                        rgWithCounts.Add(new ResourceGroupInfo
                        {
                            Name = rgName,
                            Location = rgLocation,
                            Tags = rgTags,
                            ProvisioningState = rgProvisioningState,
                            ResourceCount = -1,
                            Error = "Could not retrieve resource count"
                        });
                    }
                }
            }

            // Group by location
            var byLocation = rgWithCounts
                .GroupBy(rg => rg.Location ?? "Unknown")
                .Select(g => new { location = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Cast<dynamic>()
                .ToList();

            // Find empty resource groups
            var emptyGroups = rgWithCounts.Where(rg => rg.ResourceCount == 0).Select(rg => rg.Name).ToList();

            // Build next steps
            var nextSteps = new List<string>
            {
                "Say 'give me a summary of resource group <name>' for detailed analysis of a specific resource group.",
                "Say 'show me all resources in resource group <name>' to see what's inside a specific group."
            };

            if (emptyGroups.Any())
            {
                nextSteps.Add($"Found {emptyGroups.Count} empty resource group(s) - consider deleting them to keep things organized.");
            }

            // Build formatted summary
            var formattedSummary = BuildFormattedSummary(
                subscriptionId, rgWithCounts, byLocation, emptyGroups);

            return ToJson(new
            {
                success = true,
                subscriptionId = subscriptionId ?? "default",
                formattedSummary,
                summary = new
                {
                    totalResourceGroups = rgWithCounts.Count,
                    locations = byLocation.Count,
                    emptyGroups = emptyGroups.Count,
                    totalResources = rgWithCounts.Where(rg => rg.ResourceCount >= 0).Sum(rg => rg.ResourceCount)
                },
                breakdown = new
                {
                    byLocation
                },
                resourceGroups = rgWithCounts.Select(rg => new
                {
                    name = rg.Name,
                    location = rg.Location,
                    tags = rg.Tags,
                    provisioningState = rg.ProvisioningState,
                    resourceCount = rg.ResourceCount,
                    error = rg.Error
                }),
                emptyResourceGroups = emptyGroups,
                nextSteps
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error listing resource groups in subscription {SubscriptionId}", subscriptionId);
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    private async Task<string?> ResolveSubscriptionIdAsync(string? subscriptionIdOrName)
    {
        if (!string.IsNullOrWhiteSpace(subscriptionIdOrName))
        {
            return subscriptionIdOrName;
        }

        // Try to get from config file
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".platform-copilot",
            "config.json");

        if (File.Exists(configPath))
        {
            try
            {
                var configJson = await File.ReadAllTextAsync(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<ConfigFile>(configJson);
                return config?.LastUsedSubscriptionId;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to read config file for subscription resolution");
            }
        }

        return null;
    }

    private class ConfigFile
    {
        public string? LastUsedSubscriptionId { get; set; }
    }

    private class ResourceGroupInfo
    {
        public string? Name { get; set; }
        public string? Location { get; set; }
        public IDictionary<string, string>? Tags { get; set; }
        public string? ProvisioningState { get; set; }
        public int ResourceCount { get; set; }
        public string? Error { get; set; }
    }

    private static string BuildFormattedSummary(
        string? subscriptionId,
        List<ResourceGroupInfo> resourceGroups,
        List<dynamic> byLocation,
        List<string?> emptyGroups)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("## 📁 Resource Groups");
        sb.AppendLine();
        sb.AppendLine($"**Subscription:** `{subscriptionId ?? "default"}`");
        sb.AppendLine($"**Total Resource Groups:** {resourceGroups.Count}");
        sb.AppendLine($"**Total Resources:** {resourceGroups.Where(rg => rg.ResourceCount >= 0).Sum(rg => rg.ResourceCount)}");
        sb.AppendLine();

        // Resource groups table
        sb.AppendLine("### 📋 Resource Group Details");
        sb.AppendLine("| Resource Group | Location | Resources | Status |");
        sb.AppendLine("|----------------|----------|-----------|--------|");
        
        foreach (var rg in resourceGroups.OrderByDescending(r => r.ResourceCount).Take(15))
        {
            var location = DiscoveryFormatHelpers.GetFriendlyLocationName(rg.Location ?? "Unknown");
            var status = rg.ProvisioningState == "Succeeded" ? "✅" : "⚠️";
            var count = rg.ResourceCount >= 0 ? rg.ResourceCount.ToString() : "❓";
            sb.AppendLine($"| `{rg.Name}` | {location} | {count} | {status} |");
        }
        
        if (resourceGroups.Count > 15)
            sb.AppendLine($"| ... | | | +{resourceGroups.Count - 15} more |");
        sb.AppendLine();

        // Location breakdown
        if (byLocation.Any())
        {
            sb.AppendLine("### 🌎 By Location");
            sb.AppendLine("| Region | Resource Groups |");
            sb.AppendLine("|--------|-----------------|");
            foreach (var loc in byLocation)
            {
                var friendlyName = DiscoveryFormatHelpers.GetFriendlyLocationName((string)loc.location);
                sb.AppendLine($"| {friendlyName} | {loc.count} |");
            }
            sb.AppendLine();
        }

        // Empty groups warning
        if (emptyGroups.Any())
        {
            sb.AppendLine($"### ⚠️ Empty Resource Groups ({emptyGroups.Count})");
            foreach (var empty in emptyGroups.Take(5))
            {
                sb.AppendLine($"- `{empty}`");
            }
            if (emptyGroups.Count > 5)
                sb.AppendLine($"- ... and {emptyGroups.Count - 5} more");
            sb.AppendLine();
            sb.AppendLine("> 💡 Consider deleting empty resource groups to keep your subscription organized.");
            sb.AppendLine();
        }

        sb.AppendLine("### 💡 Next Steps");
        sb.AppendLine("- Say **\"Give me a summary of resource group `<name>`\"** for detailed analysis");
        sb.AppendLine("- Say **\"Show me all resources in resource group `<name>`\"** to explore contents");

        return sb.ToString();
    }
}
