using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Discovery.Configuration;
using Platform.Engineering.Copilot.Agents.Discovery.State;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Services;
using System.Text;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Tool to generate a complete inventory summary for an Azure subscription.
/// Provides comprehensive breakdown by resource type, location, resource group, and tagging compliance.
/// </summary>
public class SubscriptionInventoryTool : BaseTool
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzureResourceHealthService _healthService;
    private readonly DiscoveryStateAccessors _stateAccessors;
    private readonly DiscoveryAgentOptions _options;
    private readonly ConfigService _configService;

    public override string Name => "get_subscription_inventory";

    public override string Description =>
        "Generate a complete inventory summary report for an Azure subscription. " +
        "If subscription_id is not provided, uses the configured default subscription. " +
        "Provides comprehensive breakdown by resource type, location, resource group, tagging compliance, and health status. " +
        "Use when user asks for 'complete inventory', 'full report', 'what resources do I have', or 'subscription summary'.";

    public SubscriptionInventoryTool(
        ILogger<SubscriptionInventoryTool> logger,
        IAzureResourceService azureResourceService,
        IAzureResourceHealthService healthService,
        DiscoveryStateAccessors stateAccessors,
        IOptions<DiscoveryAgentOptions> options,
        ConfigService configService) : base(logger)
    {
        _azureResourceService = azureResourceService;
        _healthService = healthService;
        _stateAccessors = stateAccessors;
        _options = options.Value;
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        Parameters.Add(new ToolParameter(
            "subscription_id",
            "Azure subscription ID to generate inventory for. If not provided, uses the configured default subscription.",
            required: false));

        Parameters.Add(new ToolParameter(
            "include_health",
            "Include health status summary (default: true)",
            required: false));

        Parameters.Add(new ToolParameter(
            "include_tags_analysis",
            "Include tagging compliance analysis (default: true)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscription_id");
        var includeHealth = GetOptionalBool(arguments, "include_health") ?? true;
        var includeTagsAnalysis = GetOptionalBool(arguments, "include_tags_analysis") ?? true;

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
                error = "Subscription ID is required. Either provide subscription_id parameter or set a default using 'Set my subscription to <id>'" 
            });
        }

        Logger.LogInformation("Generating complete inventory for subscription {SubscriptionId}", subscriptionId);

        try
        {
            // Get all resources in the subscription
            var resources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var resourceList = resources.ToList();

            if (resourceList.Count == 0)
            {
                return ToJson(new
                {
                    success = true,
                    formattedSummary = $"## 📦 Subscription Inventory\n\n**Subscription:** `{subscriptionId}`\n\n⚠️ No resources found in this subscription.",
                    summary = new { totalResources = 0 }
                });
            }

            // Get all resource groups
            var resourceGroups = await _azureResourceService.ListResourceGroupsAsync(subscriptionId, cancellationToken);
            var rgList = resourceGroups.ToList();

            // Analyze by type
            var byType = resourceList
                .GroupBy(r => r.Type ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count(), resources = g.Select(r => r.Name).Take(5).ToList() })
                .OrderByDescending(x => x.count)
                .Cast<dynamic>()
                .ToList();

            // Analyze by location
            var byLocation = resourceList
                .GroupBy(r => r.Location ?? "Unknown")
                .Select(g => new { location = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Cast<dynamic>()
                .ToList();

            // Analyze by resource group
            var byResourceGroup = resourceList
                .GroupBy(r => r.ResourceGroup ?? "Unknown")
                .Select(g => new { resourceGroup = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Cast<dynamic>()
                .ToList();

            // Tag analysis
            var taggedResources = resourceList.Where(r => r.Tags != null && r.Tags.Count > 0).ToList();
            var untaggedResources = resourceList.Where(r => r.Tags == null || r.Tags.Count == 0).ToList();
            var tagCoverage = resourceList.Count > 0
                ? Math.Round((double)taggedResources.Count / resourceList.Count * 100, 1)
                : 0;

            // Common tags analysis
            var allTags = taggedResources
                .Where(r => r.Tags != null)
                .SelectMany(r => r.Tags!.Keys)
                .GroupBy(k => k)
                .Select(g => new { tag = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .Cast<dynamic>()
                .ToList();

            // Health summary (optional)
            dynamic? healthSummary = null;
            if (includeHealth && _options.EnableHealthMonitoring)
            {
                try
                {
                    var health = await _healthService.GetResourceHealthSummaryAsync(subscriptionId, cancellationToken);
                    healthSummary = new
                    {
                        totalMonitored = health.TotalResources,
                        healthy = health.HealthyResources,
                        degraded = health.WarningResources,
                        unhealthy = health.UnhealthyResources,
                        unknown = health.UnknownResources,
                        healthPercentage = health.HealthPercentage
                    };
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Could not retrieve health summary for inventory");
                }
            }

            // Build formatted report
            var formattedSummary = BuildFormattedInventory(
                subscriptionId,
                resourceList.Count,
                rgList.Count,
                byType,
                byLocation,
                byResourceGroup,
                taggedResources.Count,
                untaggedResources.Count,
                tagCoverage,
                allTags,
                healthSummary,
                includeTagsAnalysis);

            // Store in shared memory for follow-up queries
            await _stateAccessors.SetLastInventoryAsync(new
            {
                subscriptionId,
                timestamp = DateTime.UtcNow,
                totalResources = resourceList.Count,
                resourceGroups = rgList.Count
            });

            return ToJson(new
            {
                success = true,
                formattedSummary,
                summary = new
                {
                    subscriptionId,
                    totalResources = resourceList.Count,
                    resourceGroups = rgList.Count,
                    uniqueTypes = byType.Count,
                    uniqueLocations = byLocation.Count,
                    taggedResources = taggedResources.Count,
                    untaggedResources = untaggedResources.Count,
                    tagCoveragePercent = tagCoverage
                },
                byType = byType.Take(15).Cast<dynamic>().ToList(),
                byLocation = byLocation.Cast<dynamic>().ToList(),
                byResourceGroup = byResourceGroup.Take(10).Cast<dynamic>().ToList(),
                topTags = allTags.Cast<dynamic>().ToList(),
                health = healthSummary,
                nextSteps = new[]
                {
                    "Say **'Show resources in resource group <name>'** to drill into a specific resource group.",
                    "Say **'Check health of this subscription'** to see detailed health status.",
                    untaggedResources.Count > 0
                        ? $"Say **'Find untagged resources'** to list the {untaggedResources.Count} resources without tags."
                        : null,
                    "Say **'Search for resources with tag Environment=Production'** to filter by tags."
                }.Where(s => s != null).ToArray()
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating subscription inventory for {SubscriptionId}", subscriptionId);
            return ToJson(new
            {
                success = false,
                error = $"Failed to generate inventory: {ex.Message}",
                subscriptionId
            });
        }
    }

    private static string BuildFormattedInventory(
        string subscriptionId,
        int totalResources,
        int resourceGroupCount,
        List<dynamic> byType,
        List<dynamic> byLocation,
        List<dynamic> byResourceGroup,
        int taggedCount,
        int untaggedCount,
        double tagCoverage,
        List<dynamic> topTags,
        dynamic? healthSummary,
        bool includeTagsAnalysis)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("## 📦 Complete Subscription Inventory Report");
        sb.AppendLine();
        sb.AppendLine($"**Subscription:** `{subscriptionId}`");
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        // Overview
        sb.AppendLine("### 📊 Overview");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Total Resources | **{totalResources}** |");
        sb.AppendLine($"| Resource Groups | {resourceGroupCount} |");
        sb.AppendLine($"| Resource Types | {byType.Count} |");
        sb.AppendLine($"| Locations | {byLocation.Count} |");
        sb.AppendLine();

        // Health Summary (if available)
        if (healthSummary != null)
        {
            sb.AppendLine("### 🏥 Health Status");
            sb.AppendLine();
            var healthPct = (double)healthSummary.healthPercentage;
            var healthIcon = healthPct >= 95 ? "🟢" : healthPct >= 80 ? "🟡" : "🔴";
            sb.AppendLine($"**Overall Health:** {healthIcon} {healthPct:F1}%");
            sb.AppendLine();
            sb.AppendLine("| Status | Count |");
            sb.AppendLine("|--------|-------|");
            sb.AppendLine($"| ✅ Healthy | {healthSummary.healthy} |");
            if ((int)healthSummary.degraded > 0)
                sb.AppendLine($"| ⚠️ Degraded | {healthSummary.degraded} |");
            if ((int)healthSummary.unhealthy > 0)
                sb.AppendLine($"| 🔴 Unhealthy | {healthSummary.unhealthy} |");
            if ((int)healthSummary.unknown > 0)
                sb.AppendLine($"| ❓ Unknown | {healthSummary.unknown} |");
            sb.AppendLine();
        }

        // Resources by Type
        sb.AppendLine("### 🏷️ Resources by Type");
        sb.AppendLine();
        sb.AppendLine("| Resource Type | Count |");
        sb.AppendLine("|---------------|-------|");
        foreach (var item in byType.Take(10))
        {
            var typeName = ((string)item.type).Split('/').Last();
            sb.AppendLine($"| {typeName} | {item.count} |");
        }
        if (byType.Count > 10)
            sb.AppendLine($"| *...and {byType.Count - 10} more types* | |");
        sb.AppendLine();

        // Resources by Location
        sb.AppendLine("### 🌍 Resources by Location");
        sb.AppendLine();
        sb.AppendLine("| Location | Count |");
        sb.AppendLine("|----------|-------|");
        foreach (var item in byLocation)
        {
            sb.AppendLine($"| {item.location} | {item.count} |");
        }
        sb.AppendLine();

        // Resources by Resource Group
        sb.AppendLine("### 📁 Top Resource Groups");
        sb.AppendLine();
        sb.AppendLine("| Resource Group | Resources |");
        sb.AppendLine("|----------------|-----------|");
        foreach (var item in byResourceGroup.Take(5))
        {
            sb.AppendLine($"| {item.resourceGroup} | {item.count} |");
        }
        if (byResourceGroup.Count > 5)
            sb.AppendLine($"| *...and {byResourceGroup.Count - 5} more groups* | |");
        sb.AppendLine();

        // Tagging Analysis
        if (includeTagsAnalysis)
        {
            sb.AppendLine("### 🏷️ Tagging Compliance");
            sb.AppendLine();
            var tagIcon = tagCoverage >= 90 ? "🟢" : tagCoverage >= 70 ? "🟡" : "🔴";
            sb.AppendLine($"**Tag Coverage:** {tagIcon} {tagCoverage:F1}%");
            sb.AppendLine();
            sb.AppendLine($"- ✅ Tagged: {taggedCount} resources");
            sb.AppendLine($"- ⚠️ Untagged: {untaggedCount} resources");
            sb.AppendLine();

            if (topTags.Count > 0)
            {
                sb.AppendLine("**Most Common Tags:**");
                foreach (var tag in topTags.Take(5))
                {
                    sb.AppendLine($"- `{tag.tag}` ({tag.count} resources)");
                }
                sb.AppendLine();
            }

            if (untaggedCount > 0)
            {
                sb.AppendLine($"> 💡 **Tip:** {untaggedCount} resources lack tags. Tagging resources helps with cost allocation, access control, and organization.");
                sb.AppendLine();
            }
        }

        // Next Steps
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("### 💡 What's Next?");
        sb.AppendLine();
        sb.AppendLine("- Say **\"Show me details for resource group `<name>`\"** to drill down");
        sb.AppendLine("- Say **\"Check health status\"** to see resource health details");
        sb.AppendLine("- Say **\"Find resources with tag Environment=Production\"** to filter by tags");
        if (untaggedCount > 0)
            sb.AppendLine($"- Say **\"Find untagged resources\"** to address tagging compliance");

        return sb.ToString();
    }

    private static bool? GetOptionalBool(IDictionary<string, object?> arguments, string key)
    {
        if (arguments.TryGetValue(key, out var value) && value != null)
        {
            if (value is bool boolValue)
                return boolValue;
            if (bool.TryParse(value.ToString(), out var parsed))
                return parsed;
        }
        return null;
    }
}
