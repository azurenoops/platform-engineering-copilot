using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Azure.Graph;
using System.Text;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Tool for getting detailed information about a specific Azure resource.
/// Uses Azure Resource Graph for rich resource details with extended properties.
/// </summary>
public class ResourceDetailsTool : BaseTool
{
    private readonly IAzureResourceService _resourceService;
    private readonly AzureResourceGraphService _resourceGraphService;
    private readonly ConfigService _configService;

    public override string Name => "get_resource_details";

    public override string Description =>
        "Get comprehensive details about a specific Azure resource by ID or name. " +
        "Returns properties, configuration, SKU, tags, and provider-specific metadata. " +
        "If subscriptionId is not provided when using resourceName, uses the configured default subscription. " +
        "Use for deep inspection of individual resources.";

    public ResourceDetailsTool(
        ILogger<ResourceDetailsTool> logger,
        IAzureResourceService resourceService,
        AzureResourceGraphService resourceGraphService,
        ConfigService configService) : base(logger)
    {
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        _resourceGraphService = resourceGraphService ?? throw new ArgumentNullException(nameof(resourceGraphService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        Parameters.Add(new ToolParameter(
            name: "resourceId",
            description: "Full Azure resource ID (e.g., /subscriptions/.../resourceGroups/.../providers/...)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "resourceName",
            description: "Resource name to search for (requires subscriptionId)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Subscription ID to search in (required if using resourceName, or uses default if not provided)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "resourceGroup",
            description: "Resource group to narrow search (optional, used with resourceName)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var resourceId = GetOptionalString(arguments, "resourceId");
        var resourceName = GetOptionalString(arguments, "resourceName");
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var resourceGroup = GetOptionalString(arguments, "resourceGroup");

        Logger.LogInformation("Getting resource details for {ResourceId} / {ResourceName}", 
            resourceId, resourceName);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(resourceId) && string.IsNullOrWhiteSpace(resourceName))
        {
            return ToJson(new { success = false, error = "Either resourceId or resourceName is required" });
        }

        // Auto-fill subscription from configuration if not provided
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                Logger.LogInformation("Using configured default subscription: {SubscriptionId}", subscriptionId);
            }
        }

        if (!string.IsNullOrWhiteSpace(resourceName) && string.IsNullOrWhiteSpace(subscriptionId))
        {
            return ToJson(new { 
                success = false, 
                error = "Subscription ID is required when using resourceName. Either provide subscriptionId parameter or set a default using 'Set my subscription to <id>'" 
            });
        }

        try
        {
            Core.Models.Azure.AzureResource? azureResource = null;
            string dataSource = "ARM";

            // If we have a resource ID, try to fetch directly
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                Logger.LogInformation("Fetching resource details from Azure for resource ID: {ResourceId}", resourceId);

                // Try Resource Graph first for extended properties
                try
                {
                    azureResource = await _resourceGraphService.GetResourceDetailsAsync(resourceId, cancellationToken);
                    if (azureResource != null)
                    {
                        dataSource = "ResourceGraph";
                        Logger.LogInformation("Retrieved resource from Resource Graph: {ResourceId}", resourceId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Resource Graph lookup failed, falling back to ARM API");
                }

                // Fall back to ARM API if Resource Graph didn't return results
                if (azureResource == null)
                {
                    azureResource = await _resourceService.GetResourceAsync(resourceId);
                    dataSource = "ARM";
                }
            }
            else if (!string.IsNullOrWhiteSpace(resourceName))
            {
                // Search by name - need to list resources and find the match
                Logger.LogInformation("Searching for resource by name: {ResourceName} in subscription {SubscriptionId}", 
                    resourceName, subscriptionId);

                var allResources = await _resourceService.ListAllResourcesAsync(subscriptionId!, cancellationToken);
                
                // Filter by name and optionally by resource group
                var matchingResources = allResources.Where(r => 
                    r.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(resourceGroup))
                {
                    matchingResources = matchingResources.Where(r => 
                        r.ResourceGroup.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase));
                }

                azureResource = matchingResources.FirstOrDefault();

                // If we found a resource, try to get extended properties from Resource Graph
                if (azureResource != null && !string.IsNullOrWhiteSpace(azureResource.Id))
                {
                    try
                    {
                        var extendedResource = await _resourceGraphService.GetResourceDetailsAsync(azureResource.Id, cancellationToken);
                        if (extendedResource != null)
                        {
                            azureResource = extendedResource;
                            dataSource = "ResourceGraph";
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Could not get extended properties from Resource Graph");
                    }
                }
            }

            if (azureResource == null)
            {
                return ToJson(new
                {
                    success = false,
                    error = !string.IsNullOrWhiteSpace(resourceId) 
                        ? $"Resource not found with ID: {resourceId}"
                        : $"Resource not found with name: {resourceName}"
                });
            }

            // Build the response object with all available properties
            var resource = new
            {
                id = azureResource.Id,
                name = azureResource.Name,
                type = azureResource.Type,
                location = azureResource.Location,
                resourceGroup = azureResource.ResourceGroup,
                tags = azureResource.Tags ?? new Dictionary<string, string>(),
                sku = new { name = azureResource.Sku ?? "N/A", tier = "Standard" },
                kind = azureResource.Kind,
                provisioningState = azureResource.ProvisioningState ?? "Unknown",
                properties = azureResource.Properties
            };

            // Build formatted summary
            var formattedSummary = BuildFormattedSummary(resource);

            return ToJson(new
            {
                success = true,
                formattedSummary,
                resource,
                dataSource
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting resource details");
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    private static string BuildFormattedSummary(dynamic resource)
    {
        var sb = new StringBuilder();
        
        string resourceType = resource.type ?? "Unknown";
        string resourceName = resource.name ?? "Unknown";
        string resourceLocation = resource.location ?? "Unknown";
        string resourceGroup = resource.resourceGroup ?? "Unknown";
        string provisioningState = resource.provisioningState ?? "Unknown";
        string skuName = resource.sku?.name ?? "N/A";
        string skuTier = resource.sku?.tier ?? "Standard";
        string kind = resource.kind;
        
        var typeIcon = DiscoveryFormatHelpers.GetResourceTypeIcon(resourceType);
        var friendlyType = DiscoveryFormatHelpers.GetFriendlyTypeName(resourceType);
        var locationName = DiscoveryFormatHelpers.GetFriendlyLocationName(resourceLocation);
        var statusIcon = provisioningState == "Succeeded" ? "✅" : 
                        provisioningState == "Failed" ? "❌" : "⚠️";
        
        sb.AppendLine($"## {typeIcon} Resource Details: `{resourceName}`");
        sb.AppendLine();
        
        // Basic info table
        sb.AppendLine("### 📋 Basic Information");
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| **Name** | {resourceName} |");
        sb.AppendLine($"| **Type** | {typeIcon} {friendlyType} |");
        sb.AppendLine($"| **Location** | {locationName} |");
        sb.AppendLine($"| **Resource Group** | `{resourceGroup}` |");
        sb.AppendLine($"| **Status** | {statusIcon} {provisioningState} |");
        if (!string.IsNullOrWhiteSpace(kind))
        {
            sb.AppendLine($"| **Kind** | {kind} |");
        }
        sb.AppendLine();

        // SKU/Configuration
        if (skuName != "N/A")
        {
            sb.AppendLine("### ⚙️ Configuration");
            sb.AppendLine("| Setting | Value |");
            sb.AppendLine("|---------|-------|");
            sb.AppendLine($"| **SKU** | {skuName} |");
            sb.AppendLine($"| **Tier** | {skuTier} |");
            sb.AppendLine();
        }

        // Tags
        var tags = resource.tags as Dictionary<string, string>;
        if (tags != null && tags.Count > 0)
        {
            sb.AppendLine("### 🏷️ Tags");
            sb.AppendLine("| Key | Value |");
            sb.AppendLine("|-----|-------|");
            foreach (var tag in tags)
            {
                sb.AppendLine($"| `{tag.Key}` | {tag.Value} |");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("### 🏷️ Tags");
            sb.AppendLine("⚠️ No tags assigned to this resource.");
            sb.AppendLine();
        }

        // Resource ID
        sb.AppendLine("### 🔗 Resource ID");
        sb.AppendLine($"```");
        sb.AppendLine((string)resource.id);
        sb.AppendLine($"```");
        sb.AppendLine();

        sb.AppendLine("### 💡 Next Steps");
        sb.AppendLine($"- Say **\"Map dependencies for `{resourceName}`\"** to see related resources");
        sb.AppendLine($"- Say **\"Check health of `{resourceName}`\"** to see status and alerts");
        sb.AppendLine($"- Say **\"Scan `{resourceName}` for compliance\"** to check security posture");

        return sb.ToString();
    }
}
