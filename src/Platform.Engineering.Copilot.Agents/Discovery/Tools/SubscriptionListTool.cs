using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using System.Text;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Tool for listing Azure subscriptions accessible to the current identity.
/// </summary>
public class SubscriptionListTool : BaseTool
{
    private readonly IAzureResourceService _resourceService;
    
    public override string Name => "list_subscriptions";

    public override string Description =>
        "List all Azure subscriptions accessible to the current identity. " +
        "Returns subscription IDs, names, states, and tenant IDs. " +
        "Use this to find the subscription ID before running discovery operations.";

    public SubscriptionListTool(ILogger<SubscriptionListTool> logger, IAzureResourceService resourceService) : base(logger)
    {
        _resourceService = resourceService;
        
        // No required parameters - lists all accessible subscriptions
        Parameters.Add(new ToolParameter(
            name: "state",
            description: "Filter by subscription state (e.g., 'Enabled', 'Disabled'). Optional.",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var stateFilter = GetOptionalString(arguments, "state");

        Logger.LogInformation("Listing Azure subscriptions");

        try
        {
            var subscriptions = await _resourceService.ListSubscriptionsAsync(cancellationToken);
            
            // Apply state filter if provided
            if (!string.IsNullOrWhiteSpace(stateFilter))
            {
                subscriptions = subscriptions.Where(s => 
                    s.State.Equals(stateFilter, StringComparison.OrdinalIgnoreCase));
                Logger.LogDebug("Filtering subscriptions by state: {State}", stateFilter);
            }

            var subscriptionList = subscriptions.Select(s => new
            {
                subscriptionId = s.SubscriptionId,
                displayName = s.SubscriptionName,
                state = s.State,
                tenantId = s.TenantId
            }).Cast<dynamic>().ToList();

            Logger.LogInformation("Found {Count} Azure subscriptions", subscriptionList.Count);

            // Build formatted summary
            var formattedSummary = BuildFormattedSummary(subscriptionList);

            return ToJson(new
            {
                success = true,
                count = subscriptionList.Count,
                formattedSummary,
                subscriptions = subscriptionList
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error listing subscriptions");
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    private static string BuildFormattedSummary(List<dynamic> subscriptions)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("## 🔑 Azure Subscriptions");
        sb.AppendLine();
        
        if (subscriptions.Count == 0)
        {
            sb.AppendLine("No subscriptions found. Please check your Azure authentication.");
            return sb.ToString();
        }

        sb.AppendLine($"**Total:** {DiscoveryFormatHelpers.Pluralize(subscriptions.Count, "subscription")}");
        sb.AppendLine();

        // Table of subscriptions
        sb.AppendLine("| Status | Subscription Name | Subscription ID |");
        sb.AppendLine("|--------|-------------------|-----------------|");
        
        foreach (var sub in subscriptions)
        {
            var icon = DiscoveryFormatHelpers.GetSubscriptionStateIcon((string)sub.state);
            var name = (string)sub.displayName;
            var id = (string)sub.subscriptionId;
            
            // Truncate long names for table readability
            if (name.Length > 40)
                name = name[..37] + "...";
                
            sb.AppendLine($"| {icon} {sub.state} | {name} | `{id}` |");
        }
        
        sb.AppendLine();
        sb.AppendLine("### 💡 Next Steps");
        sb.AppendLine("- Say **\"Set my subscription to `<subscription-id>`\"** to configure a default subscription");
        sb.AppendLine("- Say **\"Show me all resources in subscription `<id>`\"** to discover resources");
        sb.AppendLine("- Say **\"List resource groups in subscription `<id>`\"** to see organization");

        return sb.ToString();
    }
}
