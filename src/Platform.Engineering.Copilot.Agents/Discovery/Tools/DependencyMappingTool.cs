using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Services.Azure.Graph;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Tool for mapping dependencies between Azure resources.
/// Uses Azure Resource Graph to identify related resources like NICs, Disks, VNets connected to VMs, etc.
/// </summary>
public class DependencyMappingTool : BaseTool
{
    private readonly IAzureResourceService _resourceService;
    private readonly AzureResourceGraphService _resourceGraphService;

    public override string Name => "map_resource_dependencies";

    public override string Description =>
        "Map dependencies between Azure resources. " +
        "Identifies related resources (NICs, Disks, VNets, etc.) connected to a root resource. " +
        "Use for understanding resource relationships and impact analysis.";

    public DependencyMappingTool(
        ILogger<DependencyMappingTool> logger,
        IAzureResourceService resourceService,
        AzureResourceGraphService resourceGraphService) : base(logger)
    {
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        _resourceGraphService = resourceGraphService ?? throw new ArgumentNullException(nameof(resourceGraphService));
        Parameters.Add(new ToolParameter(
            name: "resourceId",
            description: "Full Azure resource ID to start dependency mapping from",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "depth",
            description: "How deep to traverse dependencies (1-5, default: 2)",
            required: false,
            type: "integer"));

        Parameters.Add(new ToolParameter(
            name: "direction",
            description: "Direction to map: 'downstream' (what this depends on), 'upstream' (what depends on this), or 'both' (default)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var resourceId = GetOptionalString(arguments, "resourceId");
        var depthArg = GetOptionalInt(arguments, "depth");
        var direction = GetOptionalString(arguments, "direction") ?? "both";

        var depth = Math.Clamp(depthArg ?? 2, 1, 5);

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return ToJson(new { success = false, error = "resourceId is required" });
        }

        Logger.LogInformation("Mapping dependencies for {ResourceId} with depth {Depth}, direction {Direction}",
            resourceId, depth, direction);

        try
        {
            // Parse resource ID to extract info
            var resourceType = ExtractResourceType(resourceId);
            var subscriptionId = ExtractSubscriptionId(resourceId);
            var resourceGroup = ExtractResourceGroup(resourceId);

            var dependencies = new List<DependencyInfo>();

            // Get resource details to analyze properties for dependencies
            var resource = await _resourceGraphService.GetResourceDetailsAsync(resourceId, cancellationToken);
            
            if (resource != null && resource.Properties != null)
            {
                // Extract dependencies from resource properties
                var extractedDeps = ExtractDependenciesFromProperties(
                    resource.Properties, 
                    resourceType, 
                    direction);
                dependencies.AddRange(extractedDeps);
            }

            // Use Resource Graph to find related resources
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                var relatedDeps = await FindRelatedResourcesAsync(
                    resourceId, 
                    resourceType, 
                    subscriptionId, 
                    resourceGroup,
                    direction, 
                    cancellationToken);
                dependencies.AddRange(relatedDeps);
            }

            // Remove duplicates based on ResourceId
            dependencies = dependencies
                .GroupBy(d => d.ResourceId.ToLowerInvariant())
                .Select(g => g.First())
                .ToList();

            // Build formatted summary
            var formattedSummary = BuildFormattedSummary(resourceId, resourceType, depth, direction, dependencies);

            return ToJson(new
            {
                success = true,
                rootResource = resourceId,
                resourceType,
                formattedSummary,
                parameters = new { depth, direction },
                dependencyCount = dependencies.Count,
                dependencies,
                dataSource = dependencies.Count > 0 ? "ResourceGraph" : "None",
                graphSummary = new
                {
                    totalNodes = dependencies.Count + 1,
                    downstreamCount = dependencies.Count(d => d.Relationship == "downstream"),
                    upstreamCount = dependencies.Count(d => d.Relationship == "upstream")
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error mapping dependencies for {ResourceId}", resourceId);
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    private static string? ExtractResourceType(string resourceId)
    {
        // Extract resource type from ARM resource ID
        // Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/{provider}/{type}/{name}
        var parts = resourceId.Split('/');
        var providerIndex = Array.IndexOf(parts, "providers");
        if (providerIndex >= 0 && providerIndex + 2 < parts.Length)
        {
            return $"{parts[providerIndex + 1]}/{parts[providerIndex + 2]}";
        }
        return null;
    }

    private static string? ExtractSubscriptionId(string resourceId)
    {
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var subIndex = Array.IndexOf(parts, "subscriptions");
        if (subIndex >= 0 && subIndex + 1 < parts.Length)
        {
            return parts[subIndex + 1];
        }
        return null;
    }

    private static string? ExtractResourceGroup(string resourceId)
    {
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rgIndex = Array.IndexOf(parts, "resourceGroups");
        if (rgIndex >= 0 && rgIndex + 1 < parts.Length)
        {
            return parts[rgIndex + 1];
        }
        return null;
    }

    /// <summary>
    /// Extract dependencies from resource properties (downstream dependencies referenced in properties)
    /// </summary>
    private static List<DependencyInfo> ExtractDependenciesFromProperties(
        Dictionary<string, object>? properties, 
        string? resourceType, 
        string direction)
    {
        var dependencies = new List<DependencyInfo>();
        if (properties == null) return dependencies;
        
        // Only extract downstream if direction allows
        if (direction != "both" && direction != "downstream") return dependencies;

        // Search for resource ID references in properties
        var resourceIds = ExtractResourceIdsFromObject(properties);
        
        foreach (var refId in resourceIds)
        {
            var refType = ExtractResourceType(refId);
            dependencies.Add(new DependencyInfo
            {
                ResourceId = refId,
                Type = refType ?? "Unknown",
                Relationship = "downstream",
                Description = GetDependencyDescription(resourceType, refType)
            });
        }

        return dependencies;
    }

    /// <summary>
    /// Recursively extract resource IDs from a properties object
    /// </summary>
    private static List<string> ExtractResourceIdsFromObject(object obj)
    {
        var resourceIds = new List<string>();
        
        if (obj is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                var value = jsonElement.GetString();
                if (IsResourceId(value))
                {
                    resourceIds.Add(value!);
                }
            }
            else if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in jsonElement.EnumerateObject())
                {
                    resourceIds.AddRange(ExtractResourceIdsFromObject(property.Value));
                }
            }
            else if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in jsonElement.EnumerateArray())
                {
                    resourceIds.AddRange(ExtractResourceIdsFromObject(element));
                }
            }
        }
        else if (obj is Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
            {
                if (kvp.Value is string strValue && IsResourceId(strValue))
                {
                    resourceIds.Add(strValue);
                }
                else if (kvp.Value != null)
                {
                    resourceIds.AddRange(ExtractResourceIdsFromObject(kvp.Value));
                }
            }
        }
        else if (obj is string strVal && IsResourceId(strVal))
        {
            resourceIds.Add(strVal);
        }

        return resourceIds;
    }

    /// <summary>
    /// Check if a string looks like an Azure resource ID
    /// </summary>
    private static bool IsResourceId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && 
               value.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase) &&
               value.Contains("/providers/");
    }

    /// <summary>
    /// Get a human-readable description for the dependency relationship
    /// </summary>
    private static string GetDependencyDescription(string? sourceType, string? targetType)
    {
        if (targetType == null) return "Related resource";

        return targetType.ToLowerInvariant() switch
        {
            var t when t.Contains("networkinterfaces") => "Network interface attached to this resource",
            var t when t.Contains("disks") => "Disk attached to this resource",
            var t when t.Contains("virtualnetworks") => "Virtual network this resource is connected to",
            var t when t.Contains("subnets") => "Subnet this resource is in",
            var t when t.Contains("publicipaddresses") => "Public IP address associated with this resource",
            var t when t.Contains("networksecuritygroups") => "Network security group protecting this resource",
            var t when t.Contains("loadbalancers") => "Load balancer fronting this resource",
            var t when t.Contains("storageaccounts") => "Storage account used by this resource",
            var t when t.Contains("keyvault") => "Key Vault referenced by this resource",
            var t when t.Contains("managedidentities") => "Managed identity assigned to this resource",
            _ => $"Referenced {DiscoveryFormatHelpers.GetFriendlyTypeName(targetType)}"
        };
    }

    /// <summary>
    /// Find related resources using Azure Resource Graph queries
    /// </summary>
    private async Task<List<DependencyInfo>> FindRelatedResourcesAsync(
        string resourceId,
        string? resourceType,
        string subscriptionId,
        string? resourceGroup,
        string direction,
        CancellationToken cancellationToken)
    {
        var dependencies = new List<DependencyInfo>();

        try
        {
            // Only look for upstream dependencies if direction allows
            if (direction == "both" || direction == "upstream")
            {
                // Query for resources that reference this resource ID in their properties
                var query = $@"
Resources
| where subscriptionId == '{subscriptionId}'
| where properties contains '{resourceId}'
| where id !~ '{resourceId}'
| project id, name, type, location, resourceGroup
| take 20";

                var result = await _resourceGraphService.ExecuteQueryAsync(query, subscriptionId, cancellationToken);
                
                if (result.Success && result.Results != null)
                {
                    foreach (var r in result.Results)
                    {
                        var id = r.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;
                        var type = r.TryGetValue("type", out var typeVal) ? typeVal?.ToString() : null;
                        
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            dependencies.Add(new DependencyInfo
                            {
                                ResourceId = id,
                                Type = type ?? "Unknown",
                                Relationship = "upstream",
                                Description = $"This resource references {ExtractResourceName(resourceId)}"
                            });
                        }
                    }
                }
            }

            // For specific resource types, add type-specific queries
            if (resourceType != null)
            {
                var typeSpecificDeps = await GetTypeSpecificDependenciesAsync(
                    resourceId, resourceType, subscriptionId, resourceGroup, direction, cancellationToken);
                dependencies.AddRange(typeSpecificDeps);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error finding related resources via Resource Graph");
        }

        return dependencies;
    }

    /// <summary>
    /// Get type-specific dependencies using known relationship patterns
    /// </summary>
    private async Task<List<DependencyInfo>> GetTypeSpecificDependenciesAsync(
        string resourceId,
        string resourceType,
        string subscriptionId,
        string? resourceGroup,
        string direction,
        CancellationToken cancellationToken)
    {
        var dependencies = new List<DependencyInfo>();
        var resourceName = ExtractResourceName(resourceId);
        
        // For VMs, find associated NICs, disks
        if (resourceType.Contains("virtualMachines", StringComparison.OrdinalIgnoreCase))
        {
            if (direction == "both" || direction == "downstream")
            {
                // Find NICs in same resource group with similar naming
                var nicQuery = $@"
Resources
| where type =~ 'Microsoft.Network/networkInterfaces'
| where subscriptionId == '{subscriptionId}'
| where resourceGroup =~ '{resourceGroup}'
| project id, name, type, resourceGroup";

                var nicResult = await _resourceGraphService.ExecuteQueryAsync(nicQuery, subscriptionId, cancellationToken);
                if (nicResult.Success && nicResult.Results != null)
                {
                    foreach (var nic in nicResult.Results)
                    {
                        var id = nic.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;
                        var name = nic.TryGetValue("name", out var nameVal) ? nameVal?.ToString() : null;
                        
                        // Look for NICs that might be related (same resource group, similar naming)
                        if (!string.IsNullOrWhiteSpace(id) && 
                            (name?.Contains(resourceName, StringComparison.OrdinalIgnoreCase) == true ||
                             name?.StartsWith("nic-", StringComparison.OrdinalIgnoreCase) == true))
                        {
                            dependencies.Add(new DependencyInfo
                            {
                                ResourceId = id,
                                Type = "Microsoft.Network/networkInterfaces",
                                Relationship = "downstream",
                                Description = "Network interface in same resource group"
                            });
                        }
                    }
                }

                // Find disks in same resource group
                var diskQuery = $@"
Resources
| where type =~ 'Microsoft.Compute/disks'
| where subscriptionId == '{subscriptionId}'
| where resourceGroup =~ '{resourceGroup}'
| project id, name, type, resourceGroup";

                var diskResult = await _resourceGraphService.ExecuteQueryAsync(diskQuery, subscriptionId, cancellationToken);
                if (diskResult.Success && diskResult.Results != null)
                {
                    foreach (var disk in diskResult.Results)
                    {
                        var id = disk.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;
                        var name = disk.TryGetValue("name", out var nameVal) ? nameVal?.ToString() : null;
                        
                        if (!string.IsNullOrWhiteSpace(id) && 
                            name?.Contains(resourceName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            dependencies.Add(new DependencyInfo
                            {
                                ResourceId = id,
                                Type = "Microsoft.Compute/disks",
                                Relationship = "downstream",
                                Description = "Disk attached to VM"
                            });
                        }
                    }
                }
            }
        }

        return dependencies;
    }

    private static string ExtractResourceName(string resourceId)
    {
        var parts = resourceId.Split('/');
        return parts.Length > 0 ? parts[^1] : resourceId;
    }

    private class DependencyInfo
    {
        public string ResourceId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private static string BuildFormattedSummary(
        string resourceId,
        string? resourceType,
        int depth,
        string direction,
        List<DependencyInfo> dependencies)
    {
        var sb = new StringBuilder();
        
        // Extract resource name from ID
        var parts = resourceId.Split('/');
        var resourceName = parts.Length > 0 ? parts[^1] : resourceId;
        var typeIcon = DiscoveryFormatHelpers.GetResourceTypeIcon(resourceType ?? "Unknown");
        var friendlyType = DiscoveryFormatHelpers.GetFriendlyTypeName(resourceType ?? "Unknown");
        
        sb.AppendLine($"## 🔗 Dependency Map: `{resourceName}`");
        sb.AppendLine();
        sb.AppendLine($"| Property | Value |");
        sb.AppendLine($"|----------|-------|");
        sb.AppendLine($"| **Resource** | {typeIcon} {resourceName} |");
        sb.AppendLine($"| **Type** | {friendlyType} |");
        sb.AppendLine($"| **Direction** | {DiscoveryFormatHelpers.GetDependencyIcon(direction)} {direction} |");
        sb.AppendLine($"| **Depth** | {depth} levels |");
        sb.AppendLine($"| **Dependencies** | {dependencies.Count} |");
        sb.AppendLine();

        if (dependencies.Count == 0)
        {
            sb.AppendLine("### 📌 No Dependencies Found");
            sb.AppendLine("This resource has no detected dependencies.");
            sb.AppendLine();
        }
        else
        {
            // Downstream dependencies
            var downstream = dependencies.Where(d => d.Relationship == "downstream").ToList();
            if (downstream.Any())
            {
                sb.AppendLine("### ⬇️ Downstream Dependencies (What this depends on)");
                foreach (var dep in downstream)
                {
                    var depParts = dep.ResourceId.Split('/');
                    var depName = depParts.Length > 0 ? depParts[^1] : dep.ResourceId;
                    var depIcon = DiscoveryFormatHelpers.GetResourceTypeIcon(dep.Type);
                    var depType = DiscoveryFormatHelpers.GetFriendlyTypeName(dep.Type);
                    sb.AppendLine($"- {depIcon} **{depName}** ({depType})");
                    sb.AppendLine($"  - {dep.Description}");
                }
                sb.AppendLine();
            }

            // Upstream dependencies
            var upstream = dependencies.Where(d => d.Relationship == "upstream").ToList();
            if (upstream.Any())
            {
                sb.AppendLine("### ⬆️ Upstream Dependencies (What depends on this)");
                foreach (var dep in upstream)
                {
                    var depParts = dep.ResourceId.Split('/');
                    var depName = depParts.Length > 0 ? depParts[^1] : dep.ResourceId;
                    var depIcon = DiscoveryFormatHelpers.GetResourceTypeIcon(dep.Type);
                    var depType = DiscoveryFormatHelpers.GetFriendlyTypeName(dep.Type);
                    sb.AppendLine($"- {depIcon} **{depName}** ({depType})");
                    sb.AppendLine($"  - {dep.Description}");
                }
                sb.AppendLine();
            }
        }

        // Visual dependency tree
        sb.AppendLine("### 🌳 Dependency Tree");
        sb.AppendLine("```");
        sb.AppendLine($"{typeIcon} {resourceName} (root)");
        foreach (var dep in dependencies.Take(5))
        {
            var depParts = dep.ResourceId.Split('/');
            var depName = depParts.Length > 0 ? depParts[^1] : dep.ResourceId;
            var arrow = dep.Relationship == "downstream" ? "└── ⬇️" : "└── ⬆️";
            sb.AppendLine($"  {arrow} {depName}");
        }
        if (dependencies.Count > 5)
            sb.AppendLine($"  └── ... and {dependencies.Count - 5} more");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("### 💡 Next Steps");
        sb.AppendLine("- Say **\"Show me details for `<resource-name>`\"** to inspect a dependency");
        sb.AppendLine("- Say **\"Check health of `<resource-name>`\"** to see if dependencies are healthy");
        sb.AppendLine($"- Say **\"Map dependencies for `<resource>` with depth 3\"** for deeper analysis");

        return sb.ToString();
    }
}
