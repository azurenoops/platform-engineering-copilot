using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Azure;
using System.ComponentModel;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Production-ready plugin for Azure resource discovery, inventory management, and health monitoring.
/// Enhanced with Azure MCP Server integration for best practices, diagnostics, and documentation.
/// Provides comprehensive resource querying, filtering, and analysis capabilities.
/// </summary>
public class ResourceDiscoveryPlugin : BaseSupervisorPlugin
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly AzureMcpClient _azureMcpClient;

    public ResourceDiscoveryPlugin(
        ILogger<ResourceDiscoveryPlugin> logger,
        Kernel kernel,
        IAzureResourceService azureResourceService,
        AzureMcpClient azureMcpClient) : base(logger, kernel)
    {
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
    }

    // ========== DISCOVERY & INVENTORY FUNCTIONS ==========

    [KernelFunction("discover_azure_resources")]
    [Description("Discover and list Azure resources with comprehensive filtering. " +
                 "Search by subscription, resource group, type, location, or tags. " +
                 "Use for resource inventory, discovery, and finding specific resources.")]
    public async Task<string> DiscoverAzureResourcesAsync(
        [Description("Azure subscription ID. Required for resource discovery.")] string subscriptionId,
        [Description("Resource group name to filter by (optional)")] string? resourceGroup = null,
        [Description("Resource type to filter by (e.g., 'Microsoft.Storage/storageAccounts', optional)")] string? resourceType = null,
        [Description("Location/region to filter by (e.g., 'eastus', optional)")] string? location = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Discovering Azure resources in subscription {SubscriptionId}", subscriptionId);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get all resources
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId);

            // Apply filters
            var filteredResources = allResources.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                filteredResources = filteredResources.Where(r => 
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                filteredResources = filteredResources.Where(r => 
                    r.Type?.Equals(resourceType, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                filteredResources = filteredResources.Where(r => 
                    r.Location?.Equals(location, StringComparison.OrdinalIgnoreCase) == true);
            }

            var resourceList = filteredResources.ToList();

            // Group by type and location for summary
            var byType = resourceList.GroupBy(r => r.Type ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            var byLocation = resourceList.GroupBy(r => r.Location ?? "Unknown")
                .Select(g => new { location = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            var byResourceGroup = resourceList.GroupBy(r => r.ResourceGroup ?? "Unknown")
                .Select(g => new { resourceGroup = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                filters = new
                {
                    resourceGroup = resourceGroup ?? "all",
                    resourceType = resourceType ?? "all types",
                    location = location ?? "all locations"
                },
                summary = new
                {
                    totalResources = resourceList.Count,
                    uniqueTypes = byType.Count(),
                    uniqueLocations = byLocation.Count(),
                    uniqueResourceGroups = byResourceGroup.Count()
                },
                breakdown = new
                {
                    byType = byType.Take(10),
                    byLocation = byLocation,
                    byResourceGroup = byResourceGroup.Take(10)
                },
                resources = resourceList.Take(50).Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    type = r.Type,
                    resourceGroup = r.ResourceGroup,
                    location = r.Location,
                    tags = r.Tags
                }),
                nextSteps = resourceList.Count > 50 
                    ? "Results limited to 50 resources - use more specific filters. Say 'I want to see details for resource <resource-id>' to inspect specific resources, 'search for resources with tag Environment' to find tagged resources, or 'give me a complete inventory summary for this subscription' for a full report."
                    : "Say 'I want to see details for resource <resource-id>' to inspect specific resources, 'search for resources with tag Environment' to find tagged resources, or 'give me a complete inventory summary for this subscription' for a full report."
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering resources in subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("discover Azure resources", ex);
        }
    }

    [KernelFunction("get_resource_details")]
    [Description("Get comprehensive details about a specific Azure resource. " +
                 "Shows configuration, properties, tags, location, health status, and metadata. " +
                 "Use to inspect specific resources and understand their configuration.")]
    public async Task<string> GetResourceDetailsAsync(
        [Description("Full Azure resource ID (e.g., /subscriptions/{sub}/resourceGroups/{rg}/providers/{type}/{name})")] string resourceId,
        [Description("Include health status information (default: true)")] bool includeHealth = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting details for resource {ResourceId}", resourceId);

            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Resource ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var resource = await _azureResourceService.GetResourceAsync(resourceId);

            if (resource == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Resource not found: {resourceId}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get health status if requested
            object? healthStatus = null;
            if (includeHealth)
            {
                try
                {
                    healthStatus = await _azureResourceService.GetResourceHealthAsync(resourceId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve health status for resource {ResourceId}", resourceId);
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceId = resourceId,
                resource = new
                {
                    id = resource.Id ?? resourceId,
                    name = resource.Name ?? "Unknown",
                    type = resource.Type ?? "Unknown",
                    location = resource.Location ?? "Unknown",
                    tags = resource.Tags,
                    sku = resource.Sku,
                    kind = resource.Kind,
                    provisioningState = resource.ProvisioningState
                },
                health = healthStatus != null ? (object)new
                {
                    available = true,
                    status = healthStatus
                } : new
                {
                    available = false,
                    message = "Health status not available for this resource type"
                },
                nextSteps = healthStatus != null
                    ? "Review the resource configuration and properties shown above. Check the health status for any issues. Say 'analyze dependencies for this resource' to see what it depends on, or 'show me the health history for this resource' for historical health data."
                    : "Review the resource configuration and properties shown above. Say 'analyze dependencies for this resource' to see what it depends on, or 'show me the health history for this resource' for historical health data."
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting details for resource {ResourceId}", resourceId);
            return CreateErrorResponse("get resource details", ex);
        }
    }

    [KernelFunction("search_resources_by_tag")]
    [Description("Search for Azure resources using tags. " +
                 "Find resources with specific tag keys or key-value pairs. " +
                 "Use for tag-based discovery, compliance checks, and resource organization.")]
    public async Task<string> SearchResourcesByTagAsync(
        [Description("Azure subscription ID to search in")] string subscriptionId,
        [Description("Tag key to search for (e.g., 'Environment', 'Owner', 'CostCenter')")] string tagKey,
        [Description("Tag value to match (optional - finds all resources with the tag key if not specified)")] string? tagValue = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching resources by tag {TagKey}={TagValue} in subscription {SubscriptionId}", 
                tagKey, tagValue ?? "any", subscriptionId);

            if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(tagKey))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID and tag key are required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get all resources
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId);

            // Filter by tag
            var matchedResources = allResources.Where(r =>
            {
                if (r.Tags == null) return false;

                if (!r.Tags.ContainsKey(tagKey)) return false;

                if (string.IsNullOrWhiteSpace(tagValue)) return true;

                return r.Tags[tagKey]?.Equals(tagValue, StringComparison.OrdinalIgnoreCase) == true;
            }).ToList();

            // Group by resource type and tag value
            var byType = matchedResources.GroupBy(r => r.Type ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            var byTagValue = matchedResources
                .Where(r => r.Tags != null && r.Tags.ContainsKey(tagKey))
                .GroupBy(r => r.Tags![tagKey] ?? "null")
                .Select(g => new { tagValue = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                search = new
                {
                    tagKey = tagKey,
                    tagValue = tagValue ?? "any value",
                    matchType = string.IsNullOrWhiteSpace(tagValue) ? "key only" : "key and value"
                },
                summary = new
                {
                    totalMatches = matchedResources.Count,
                    uniqueTypes = byType.Count(),
                    uniqueValues = byTagValue.Count()
                },
                breakdown = new
                {
                    byType = byType,
                    byTagValue = byTagValue
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
                nextSteps = new[]
                {
                    matchedResources.Count == 0 ? $"No resources found with tag '{tagKey}'. Try searching for a different tag or check your tag naming." : null,
                    matchedResources.Count > 100 ? "Results limited to 100 resources - consider filtering by tag value to narrow results." : null,
                    "Say 'show me details for resource <resource-id>' to inspect specific resources.",
                    "Consider adding tags to untagged resources by saying 'I need to tag resources in this subscription'."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching resources by tag in subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("search resources by tag", ex);
        }
    }

    [KernelFunction("analyze_resource_dependencies")]
    [Description("Analyze dependencies and relationships between Azure resources. " +
                 "Identifies network connections, storage dependencies, and resource relationships. " +
                 "Use for architecture analysis, impact assessment, and change planning.")]
    public async Task<string> AnalyzeResourceDependenciesAsync(
        [Description("Azure subscription ID to analyze")] string subscriptionId,
        [Description("Specific resource ID to analyze dependencies for (optional - analyzes all if not specified)")] string? resourceId = null,
        [Description("Resource group to limit analysis to (optional)")] string? resourceGroup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing resource dependencies in subscription {SubscriptionId}", subscriptionId);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get resources to analyze
            var resources = await _azureResourceService.ListAllResourcesAsync(subscriptionId);

            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                resources = resources.Where(r => 
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                resources = resources.Where(r => r.Id == resourceId).ToList();
            }

            // Analyze dependencies
            var dependencies = new List<object>();
            var dependencyCount = new Dictionary<string, int>();

            foreach (var resource in resources)
            {
                // Get detailed resource info to analyze properties
                try
                {
                    var details = await _azureResourceService.GetResourceAsync(resource.Id!);

                    if (details != null)
                    {
                        var resourceDeps = ExtractDependencies(resource, details);
                        if (resourceDeps.Any())
                        {
                            dependencies.Add(new
                            {
                                resourceId = resource.Id,
                                resourceName = resource.Name,
                                resourceType = resource.Type,
                                dependencies = resourceDeps
                            });

                            foreach (var dep in resourceDeps)
                            {
                                var depType = dep.GetType().GetProperty("type")?.GetValue(dep)?.ToString() ?? "Unknown";
                                if (!dependencyCount.ContainsKey(depType))
                                {
                                    dependencyCount[depType] = 0;
                                }
                                dependencyCount[depType]++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not analyze dependencies for resource {ResourceId}", resource.Id);
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                scope = new
                {
                    resourceId = resourceId ?? "all resources",
                    resourceGroup = resourceGroup ?? "all resource groups",
                    resourcesAnalyzed = resources.Count
                },
                summary = new
                {
                    totalDependencies = dependencies.Count,
                    dependencyTypes = dependencyCount.Select(kvp => new { type = kvp.Key, count = kvp.Value })
                },
                dependencies = dependencies.Take(50),
                nextSteps = new[]
                {
                    dependencies.Count > 50 ? "Results limited to 50 resources - say 'analyze dependencies for resource group <name>' or 'analyze dependencies for resource <id>' to focus the analysis." : null,
                    dependencies.Count > 0 ? "Review the dependencies listed above before making changes to avoid breaking dependent resources." : "No dependencies found - this may indicate isolated resources or limited property visibility.",
                    "Say 'show me details for resource <resource-id>' to inspect specific resources and their configurations.",
                    "Consider the impact of changes on dependent resources before proceeding with modifications."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing resource dependencies in subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("analyze resource dependencies", ex);
        }
    }

    // ========== RESOURCE GROUP FUNCTIONS ==========

    [KernelFunction("list_resource_groups")]
    [Description("List all resource groups in a subscription with details. " +
                 "Shows resource counts, locations, tags, and provisioning state. " +
                 "Use for resource group inventory and organization analysis.")]
    public async Task<string> ListResourceGroupsAsync(
        [Description("Azure subscription ID (optional - uses default if not specified)")] string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing resource groups in subscription {SubscriptionId}", subscriptionId ?? "default");

            var resourceGroups = await _azureResourceService.ListResourceGroupsAsync(subscriptionId, cancellationToken);
            var rgList = resourceGroups.ToList();

            // Get resource count for each resource group
            var rgWithCounts = new List<object>();
            foreach (var rg in rgList)
            {
                dynamic rgData = rg;
                string? rgName = rgData.name?.ToString();

                if (!string.IsNullOrEmpty(rgName))
                {
                    try
                    {
                        var resources = await _azureResourceService.ListResourcesAsync(rgName!, subscriptionId, cancellationToken);
                        var resourceCount = resources.Count();

                        rgWithCounts.Add(new
                        {
                            name = rgName,
                            location = rgData.location?.ToString(),
                            tags = TryGetProperty(rgData, "tags"),
                            provisioningState = TryGetNestedProperty(rgData, "properties", "provisioningState"),
                            resourceCount = resourceCount
                        });
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "Could not get resource count for resource group {ResourceGroup}", rgName);
                        rgWithCounts.Add(new
                        {
                            name = rgName,
                            location = rgData.location?.ToString(),
                            tags = TryGetProperty(rgData, "tags"),
                            provisioningState = TryGetNestedProperty(rgData, "properties", "provisioningState"),
                            resourceCount = -1
                        });
                    }
                }
            }

            var byLocation = rgWithCounts
                .GroupBy(rg => ((dynamic)rg).location?.ToString() ?? "Unknown")
                .Select(g => new { location = g.Key, count = g.Count() });

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId ?? "default",
                summary = new
                {
                    totalResourceGroups = rgWithCounts.Count,
                    locations = byLocation.Count()
                },
                breakdown = new
                {
                    byLocation = byLocation
                },
                resourceGroups = rgWithCounts,
                nextSteps = new[]
                {
                    "Say 'give me a summary of resource group <name>' for detailed analysis of a specific resource group.",
                    "Say 'show me all resources in resource group <name>' to see what's inside a specific group.",
                    "Review resource groups with 0 resources - you may want to delete empty groups to keep things organized."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing resource groups");
            return CreateErrorResponse("list resource groups", ex);
        }
    }

    [KernelFunction("get_resource_group_summary")]
    [Description("Get comprehensive summary and analysis of a specific resource group. " +
                 "Shows resource breakdown by type, location distribution, tag analysis, and health status. " +
                 "Use for resource group inventory, compliance, and optimization analysis.")]
    public async Task<string> GetResourceGroupSummaryAsync(
        [Description("Resource group name")] string resourceGroupName,
        [Description("Azure subscription ID (optional - uses default if not specified)")] string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting summary for resource group {ResourceGroup}", resourceGroupName);

            if (string.IsNullOrWhiteSpace(resourceGroupName))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Resource group name is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get resource group details
            var resourceGroup = await _azureResourceService.GetResourceGroupAsync(resourceGroupName, subscriptionId, cancellationToken);

            if (resourceGroup == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Resource group not found: {resourceGroupName}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get all resources in the resource group
            var resources = await _azureResourceService.ListResourcesAsync(resourceGroupName, subscriptionId, cancellationToken);
            var resourceList = resources.ToList();

            // Analyze resources
            var byType = resourceList
                .GroupBy(r => ((dynamic)r).type?.ToString() ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            var byLocation = resourceList
                .GroupBy(r => ((dynamic)r).location?.ToString() ?? "Unknown")
                .Select(g => new { location = g.Key, count = g.Count() });

            // Tag analysis
            var taggedCount = resourceList.Count(r => ((dynamic)r).tags != null);
            var untaggedCount = resourceList.Count - taggedCount;

            dynamic rgData = resourceGroup;

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceGroup = new
                {
                    name = resourceGroupName,
                    location = rgData?.location?.ToString(),
                    tags = TryGetProperty(rgData, "tags"),
                    provisioningState = TryGetNestedProperty(rgData, "properties", "provisioningState")
                },
                summary = new
                {
                    totalResources = resourceList.Count,
                    uniqueTypes = byType.Count(),
                    uniqueLocations = byLocation.Count(),
                    taggedResources = taggedCount,
                    untaggedResources = untaggedCount,
                    tagCoverage = resourceList.Count > 0 
                        ? Math.Round((double)taggedCount / resourceList.Count * 100, 2) 
                        : 0
                },
                breakdown = new
                {
                    byType = byType,
                    byLocation = byLocation
                },
                resources = resourceList.Take(20).Select(r => new
                {
                    id = ((dynamic)r).id?.ToString(),
                    name = ((dynamic)r).name?.ToString(),
                    type = ((dynamic)r).type?.ToString(),
                    location = ((dynamic)r).location?.ToString(),
                    tags = ((dynamic)r).tags
                }),
                nextSteps = new[]
                {
                    resourceList.Count > 20 ? $"Showing first 20 of {resourceList.Count} resources - say 'show me all resources in resource group {resourceGroupName}' to see the complete list." : null,
                    untaggedCount > 0 ? $"Found {untaggedCount} resources without tags - consider saying 'I need to tag resources in resource group {resourceGroupName}' to improve organization." : null,
                    "Say 'show me details for resource <resource-id>' to inspect specific resources in this group.",
                    "Say 'check the health status for this subscription' to see if any resources have health issues."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting summary for resource group {ResourceGroup}", resourceGroupName);
            return CreateErrorResponse("get resource group summary", ex);
        }
    }

    [KernelFunction("list_subscriptions")]
    [Description("List all accessible Azure subscriptions with details. " +
                 "Shows subscription state, location, and metadata. " +
                 "Use for multi-subscription environments and subscription inventory.")]
    public async Task<string> ListSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing all accessible subscriptions");

            var subscriptions = await _azureResourceService.ListSubscriptionsAsync(cancellationToken);
            var subList = subscriptions.ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                summary = new
                {
                    totalSubscriptions = subList.Count
                },
                subscriptions = subList.Select(sub =>
                {
                    dynamic subData = sub;
                    return new
                    {
                        subscriptionId = subData.subscriptionId?.ToString(),
                        displayName = subData.displayName?.ToString(),
                        state = subData.state?.ToString(),
                        subscriptionPolicies = TryGetProperty(subData, "subscriptionPolicies"),
                        authorizationSource = TryGetProperty(subData, "authorizationSource")?.ToString(),
                        managedByTenants = TryGetProperty(subData, "managedByTenants")
                    };
                }),
                nextSteps = new[]
                {
                    "Say 'discover resources in subscription <subscription-id>' to explore resources in each subscription.",
                    "Say 'show me the health overview for subscription <subscription-id>' to check subscription health.",
                    "Say 'list all resource groups in subscription <subscription-id>' to see resource groups per subscription."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing subscriptions");
            return CreateErrorResponse("list subscriptions", ex);
        }
    }

    // ========== HEALTH & MONITORING FUNCTIONS ==========

    [KernelFunction("get_resource_health_status")]
    [Description("Get current health status for a specific Azure resource. " +
                 "Shows availability state, health events, and recommendations. " +
                 "Use to check resource health and troubleshoot issues.")]
    public async Task<string> GetResourceHealthStatusAsync(
        [Description("Full Azure resource ID")] string resourceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting health status for resource {ResourceId}", resourceId);

            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Resource ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var health = await _azureResourceService.GetResourceHealthAsync(resourceId, cancellationToken);

            if (health == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Health information not available for this resource",
                    resourceId = resourceId
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            dynamic healthData = health;

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceId = resourceId,
                health = new
                {
                    availabilityState = healthData.availabilityState?.ToString(),
                    summary = healthData.summary?.ToString(),
                    reasonType = healthData.reasonType?.ToString(),
                    occurredTime = healthData.occurredTime?.ToString(),
                    reasonChronicity = healthData.reasonChronicity?.ToString(),
                    properties = TryGetProperty(healthData, "properties")
                },
                nextSteps = new[]
                {
                    "Review the availability state and reason shown above to understand the current health status.",
                    "Say 'show me the health history for this resource' to see historical health data and trends.",
                    "Say 'show me details for this resource' to inspect the resource configuration.",
                    "Check the Azure Service Health dashboard at https://status.azure.com for platform-wide issues."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status for resource {ResourceId}", resourceId);
            return CreateErrorResponse("get resource health status", ex);
        }
    }

    [KernelFunction("get_subscription_health_overview")]
    [Description("Get subscription-wide health overview and dashboard. " +
                 "Shows health status distribution, critical events, and service health. " +
                 "Use for monitoring subscription health and identifying issues.")]
    public async Task<string> GetSubscriptionHealthOverviewAsync(
        [Description("Azure subscription ID")] string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting health overview for subscription {SubscriptionId}", subscriptionId);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get health events
            var healthEvents = await _azureResourceService.GetResourceHealthEventsAsync(subscriptionId, cancellationToken);
            var eventsList = healthEvents.ToList();

            // Categorize events
            // Note: The health events have fields directly, not nested under 'properties'
            var bySeverity = eventsList
                .GroupBy(e => ((dynamic)e).reasonType?.ToString() ?? "Unknown")  // Use reasonType instead of impactType
                .Select(g => new { severity = g.Key, count = g.Count() });

            var byStatus = eventsList
                .GroupBy(e => ((dynamic)e).availabilityState?.ToString() ?? "Unknown")  // Use availabilityState instead of status
                .Select(g => new { status = g.Key, count = g.Count() });

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                summary = new
                {
                    totalEvents = eventsList.Count,
                    activeEvents = eventsList.Count(e => 
                        ((dynamic)e).serviceImpacting == true)  // Use serviceImpacting field directly
                },
                breakdown = new
                {
                    bySeverity = bySeverity,
                    byStatus = byStatus
                },
                recentEvents = eventsList.Take(10).Select(e =>
                {
                    dynamic eventData = e;
                    
                    return new
                    {
                        resourceId = eventData.resourceId?.ToString(),
                        resourceName = eventData.resourceName?.ToString(),
                        resourceType = eventData.resourceType?.ToString(),
                        reasonType = eventData.reasonType?.ToString(),
                        availabilityState = eventData.availabilityState?.ToString(),
                        summary = eventData.summary?.ToString(),
                        detailedStatus = eventData.detailedStatus?.ToString(),
                        occurredDateTime = eventData.occurredDateTime?.ToString(),
                        serviceImpacting = eventData.serviceImpacting
                    };
                }),
                nextSteps = new[]
                {
                    eventsList.Any() ? "Review the active health events listed above and take appropriate action to resolve any issues." : "No active health events detected - your subscription resources appear healthy.",
                    "Say 'check the health status for resource <resource-id>' to drill down into specific resources.",
                    "Say 'show me the health history for this subscription' to see historical health trends and patterns.",
                    "Check Azure Service Health at https://status.azure.com for platform-wide service issues."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health overview for subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("get subscription health overview", ex);
        }
    }

    [KernelFunction("get_resource_health_history")]
    [Description("Get historical health data and incident timeline for resources. " +
                 "Shows health state changes, incidents, and availability metrics over time. " +
                 "Use for troubleshooting, trend analysis, and SLA validation.")]
    public async Task<string> GetResourceHealthHistoryAsync(
        [Description("Azure subscription ID")] string subscriptionId,
        [Description("Specific resource ID to get history for (optional - gets all if not specified)")] string? resourceId = null,
        [Description("Time range to query (e.g., '24h', '7d', '30d', default: '24h')")] string timeRange = "24h",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting health history for subscription {SubscriptionId}, timeRange {TimeRange}", 
                subscriptionId, timeRange);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var history = await _azureResourceService.GetResourceHealthHistoryAsync(
                subscriptionId, 
                resourceId, 
                timeRange, 
                cancellationToken);
            
            var historyList = history.ToList();

            // Analyze history
            var byAvailabilityState = historyList
                .GroupBy(h => ((dynamic)h).properties?.availabilityState?.ToString() ?? "Unknown")
                .Select(g => new { state = g.Key, count = g.Count() });

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                resourceId = resourceId ?? "all resources",
                timeRange = timeRange,
                summary = new
                {
                    totalRecords = historyList.Count,
                    stateDistribution = byAvailabilityState
                },
                history = historyList.Take(50).Select(h =>
                {
                    dynamic histData = h;
                    
                    return new
                    {
                        id = histData.id?.ToString(),
                        name = histData.name?.ToString(),
                        availabilityState = TryGetNestedProperty(histData, "properties", "availabilityState"),
                        summary = TryGetNestedProperty(histData, "properties", "summary"),
                        occurredTime = TryGetNestedProperty(histData, "properties", "occurredTime"),
                        reasonType = TryGetNestedProperty(histData, "properties", "reasonType")
                    };
                }),
                nextSteps = new[]
                {
                    historyList.Count > 50 ? "Results limited to 50 records. Say 'show me health history for resource <resource-id>' to focus on a specific resource." : null,
                    "Analyze the availability state changes shown above to identify patterns or recurring issues.",
                    "Say 'check the current health status for this subscription' to see the latest health information.",
                    "Investigate any periods of unavailability or degradation - say 'show me details for resource <resource-id>' to learn more."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health history for subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("get resource health history", ex);
        }
    }

    // ========== QUERY & FILTER FUNCTIONS ==========

    [KernelFunction("filter_resources_by_location")]
    [Description("Filter and find resources in specific Azure regions. " +
                 "Supports multi-region filtering and regional distribution analysis. " +
                 "Use for compliance, disaster recovery planning, and geographic optimization.")]
    public async Task<string> FilterResourcesByLocationAsync(
        [Description("Azure subscription ID")] string subscriptionId,
        [Description("Location(s) to filter by (comma-separated for multiple, e.g., 'eastus,westus')")] string locations,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Filtering resources by location in subscription {SubscriptionId}", subscriptionId);

            if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(locations))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID and location(s) are required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var locationList = locations.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().ToLowerInvariant())
                .ToList();

            // Get all resources
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId);

            // Filter by location
            var filteredResources = allResources.Where(r => 
                r.Location != null && locationList.Contains(r.Location.ToLowerInvariant())
            ).ToList();

            // Group by location and type
            var byLocation = filteredResources.GroupBy(r => r.Location ?? "Unknown")
                .Select(g => new 
                { 
                    location = g.Key, 
                    count = g.Count(),
                    types = g.GroupBy(r => r.Type ?? "Unknown").Count()
                });

            var byType = filteredResources.GroupBy(r => r.Type ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                filter = new
                {
                    requestedLocations = locationList,
                    matchedLocations = byLocation.Count()
                },
                summary = new
                {
                    totalResources = filteredResources.Count,
                    uniqueTypes = byType.Count(),
                    locations = byLocation.Count()
                },
                breakdown = new
                {
                    byLocation = byLocation,
                    byType = byType.Take(10)
                },
                resources = filteredResources.Take(50).Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    type = r.Type,
                    location = r.Location,
                    resourceGroup = r.ResourceGroup,
                    tags = r.Tags
                }),
                nextSteps = new[]
                {
                    filteredResources.Count == 0 ? $"No resources found in the specified locations: {locations}. Try different location names or check your spelling." : null,
                    filteredResources.Count > 50 ? "Results limited to 50 resources. Say 'filter resources by location eastus' to narrow to a single region." : null,
                    "Say 'show me details for resource <resource-id>' to inspect specific resources.",
                    "Review the resource distribution above for disaster recovery planning - consider if resources are properly distributed across regions."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering resources by location in subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("filter resources by location", ex);
        }
    }

    [KernelFunction("get_resource_inventory_summary")]
    [Description("Generate comprehensive resource inventory report for a subscription. " +
                 "Includes resource counts, type distribution, location analysis, tag coverage, and optimization opportunities. " +
                 "Use for governance, compliance reporting, and resource optimization.")]
    public async Task<string> GetResourceInventorySummaryAsync(
        [Description("Azure subscription ID")] string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating inventory summary for subscription {SubscriptionId}", subscriptionId);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get all resources
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId);
            var resourceList = allResources.ToList();

            // Comprehensive analysis
            var byType = resourceList.GroupBy(r => r.Type ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            var byLocation = resourceList.GroupBy(r => r.Location ?? "Unknown")
                .Select(g => new { location = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            var byResourceGroup = resourceList.GroupBy(r => r.ResourceGroup ?? "Unknown")
                .Select(g => new { resourceGroup = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            // Tag analysis
            var taggedResources = resourceList.Where(r => r.Tags != null && r.Tags.Any()).ToList();
            var untaggedResources = resourceList.Count - taggedResources.Count;

            var commonTags = taggedResources
                .Where(r => r.Tags != null)
                .SelectMany(r => r.Tags!.Keys)
                .GroupBy(k => k)
                .Select(g => new { tagKey = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10);

            // Optimization opportunities
            var opportunities = new List<string>();
            if (untaggedResources > 0)
            {
                opportunities.Add($"{untaggedResources} resources without tags - improve resource organization");
            }

            var emptyResourceGroups = byResourceGroup.Count(rg => rg.count == 0);
            if (emptyResourceGroups > 0)
            {
                opportunities.Add($"{emptyResourceGroups} empty resource groups - consider cleanup");
            }

            if (byLocation.Count() > 5)
            {
                opportunities.Add($"Resources spread across {byLocation.Count()} locations - review for consolidation");
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                summary = new
                {
                    totalResources = resourceList.Count,
                    uniqueResourceTypes = byType.Count(),
                    locations = byLocation.Count(),
                    resourceGroups = byResourceGroup.Count(),
                    taggedResources = taggedResources.Count,
                    untaggedResources = untaggedResources,
                    tagCoveragePercentage = resourceList.Count > 0 
                        ? Math.Round((double)taggedResources.Count / resourceList.Count * 100, 2) 
                        : 0
                },
                distribution = new
                {
                    top10ResourceTypes = byType.Take(10),
                    byLocation = byLocation,
                    top10ResourceGroups = byResourceGroup.Take(10)
                },
                tagAnalysis = new
                {
                    mostCommonTags = commonTags,
                    taggedCount = taggedResources.Count,
                    untaggedCount = untaggedResources
                },
                optimization = new
                {
                    opportunitiesFound = opportunities.Count,
                    recommendations = opportunities
                },
                nextSteps = new[]
                {
                    "Review the optimization recommendations listed above to improve your resource management.",
                    "Say 'search for resources with tag Environment' to analyze tag usage and improve resource organization.",
                    "Say 'list all resource groups in this subscription' to review resource group organization.",
                    "Implement tagging standards for the untagged resources identified - say 'I need to tag resources in this subscription'.",
                    "Consider consolidating resources across regions if you have resources spread across many locations."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating inventory summary for subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("get resource inventory summary", ex);
        }
    }

    // ========== HELPER METHODS ==========

    private object? TryGetProperty(dynamic obj, string propertyName)
    {
        try
        {
            var type = obj.GetType();
            var property = type.GetProperty(propertyName);
            return property?.GetValue(obj);
        }
        catch
        {
            return null;
        }
    }

    private object? TryGetNestedProperty(dynamic obj, string firstProperty, string secondProperty)
    {
        try
        {
            var type = obj.GetType();
            var property = type.GetProperty(firstProperty);
            if (property == null) return null;
            
            var firstValue = property.GetValue(obj);
            if (firstValue == null) return null;
            
            var nestedType = firstValue.GetType();
            var nestedProperty = nestedType.GetProperty(secondProperty);
            return nestedProperty?.GetValue(firstValue);
        }
        catch
        {
            return null;
        }
    }

    private List<object> ExtractDependencies(AzureResource resource, AzureResource? details)
    {
        var dependencies = new List<object>();

        if (details == null) return dependencies;

        try
        {
            // Extract from properties dictionary
            if (details.Properties != null && details.Properties.Count > 0)
            {
                foreach (var kvp in details.Properties)
                {
                    // Look for properties that end with "Id" as they often indicate dependencies
                    if (kvp.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && 
                        kvp.Value != null)
                    {
                        var valueStr = kvp.Value.ToString();
                        if (!string.IsNullOrEmpty(valueStr) && valueStr.StartsWith("/subscriptions/"))
                        {
                            dependencies.Add(new { type = kvp.Key, value = valueStr });
                        }
                    }
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error extracting dependencies for resource {ResourceId}", resource.Id);
        }

        return dependencies;
    }

    // ========== AZURE MCP ENHANCED FUNCTIONS ==========

    [KernelFunction("discover_azure_resources_with_guidance")]
    [Description("Discover Azure resources with best practices and optimization recommendations. " +
                 "Combines fast SDK-based resource discovery with Azure MCP best practices guidance. " +
                 "Use when you want actionable recommendations along with your resource inventory.")]
    public async Task<string> DiscoverAzureResourcesWithGuidanceAsync(
        [Description("Azure subscription ID. Required for resource discovery.")] string subscriptionId,
        [Description("Resource group name to filter by (optional)")] string? resourceGroup = null,
        [Description("Resource type to filter by (e.g., 'Microsoft.Storage/storageAccounts', optional)")] string? resourceType = null,
        [Description("Include best practices guidance (default: true)")] bool includeBestPractices = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Discovering Azure resources with guidance in subscription {SubscriptionId}", subscriptionId);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // 1. Use SDK for fast resource discovery
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId);

            // Apply filters
            var filteredResources = allResources.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                filteredResources = filteredResources.Where(r => 
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                filteredResources = filteredResources.Where(r => 
                    r.Type?.Equals(resourceType, StringComparison.OrdinalIgnoreCase) == true);
            }

            var resourceList = filteredResources.ToList();

            // Group by type
            var byType = resourceList.GroupBy(r => r.Type ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            // 2. Use Azure MCP to get best practices for discovered resource types
            object? bestPracticesData = null;
            if (includeBestPractices && resourceList.Any())
            {
                try
                {
                    await _azureMcpClient.InitializeAsync(cancellationToken);
                    
                    var uniqueTypes = resourceList.Select(r => r.Type).Distinct().Take(5).ToList();
                    _logger.LogInformation("Fetching best practices for {Count} resource types via Azure MCP", uniqueTypes.Count);

                    var bestPractices = await _azureMcpClient.CallToolAsync("get_bestpractices", 
                        new Dictionary<string, object?>
                        {
                            ["resourceTypes"] = string.Join(", ", uniqueTypes)
                        }, cancellationToken);

                    bestPracticesData = new
                    {
                        available = bestPractices.Success,
                        data = bestPractices.Success ? bestPractices.Result : "Best practices not available"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve best practices from Azure MCP");
                    bestPracticesData = new
                    {
                        available = false,
                        error = "Best practices service temporarily unavailable"
                    };
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                filters = new
                {
                    resourceGroup = resourceGroup ?? "all",
                    resourceType = resourceType ?? "all types"
                },
                summary = new
                {
                    totalResources = resourceList.Count,
                    uniqueTypes = byType.Count()
                },
                breakdown = new
                {
                    byType = byType.Take(10)
                },
                resources = resourceList.Take(50).Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    type = r.Type,
                    resourceGroup = r.ResourceGroup,
                    location = r.Location
                }),
                bestPractices = bestPracticesData,
                nextSteps = new[]
                {
                    resourceList.Count > 50 ? "Results limited to 50 resources - use more specific filters." : null,
                    includeBestPractices ? "Review the best practices above to optimize your Azure resources." : "Say 'show me best practices for these resources' to get optimization guidance.",
                    "Say 'show me details for resource <resource-id>' to inspect specific resources with diagnostics."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering resources with guidance in subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("discover Azure resources with guidance", ex);
        }
    }

    [KernelFunction("get_resource_details_with_diagnostics")]
    [Description("Get comprehensive resource details with AppLens diagnostics and troubleshooting guidance. " +
                 "Combines SDK resource data with Azure MCP's AppLens diagnostics for deep analysis. " +
                 "Use when troubleshooting resource issues or performing detailed health analysis.")]
    public async Task<string> GetResourceDetailsWithDiagnosticsAsync(
        [Description("Full Azure resource ID")] string resourceId,
        [Description("Include AppLens diagnostics (default: true)")] bool includeDiagnostics = true,
        [Description("Include health status (default: true)")] bool includeHealth = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting resource details with diagnostics for {ResourceId}", resourceId);

            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Resource ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // 1. Use SDK for resource details
            var resource = await _azureResourceService.GetResourceAsync(resourceId);

            if (resource == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Resource not found: {resourceId}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // 2. Get health status if requested
            object? healthStatus = null;
            if (includeHealth)
            {
                try
                {
                    healthStatus = await _azureResourceService.GetResourceHealthAsync(resourceId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve health status for resource {ResourceId}", resourceId);
                }
            }

            // 3. Use Azure MCP AppLens for advanced diagnostics
            object? diagnosticsData = null;
            if (includeDiagnostics)
            {
                try
                {
                    await _azureMcpClient.InitializeAsync(cancellationToken);
                    
                    _logger.LogInformation("Fetching AppLens diagnostics via Azure MCP for {ResourceId}", resourceId);

                    var diagnostics = await _azureMcpClient.CallToolAsync("applens", 
                        new Dictionary<string, object?>
                        {
                            ["command"] = "diagnose",
                            ["parameters"] = new { resourceId }
                        }, cancellationToken);

                    diagnosticsData = new
                    {
                        available = diagnostics.Success,
                        data = diagnostics.Success ? diagnostics.Result : "Diagnostics not available"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve AppLens diagnostics from Azure MCP");
                    diagnosticsData = new
                    {
                        available = false,
                        error = "AppLens diagnostics service temporarily unavailable"
                    };
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceId = resourceId,
                resource = new
                {
                    id = resource.Id ?? resourceId,
                    name = resource.Name ?? "Unknown",
                    type = resource.Type ?? "Unknown",
                    location = resource.Location ?? "Unknown",
                    tags = resource.Tags,
                    sku = resource.Sku,
                    kind = resource.Kind,
                    provisioningState = resource.ProvisioningState
                },
                health = healthStatus != null ? (object)new
                {
                    available = true,
                    status = healthStatus
                } : new
                {
                    available = false,
                    message = "Health status not available for this resource type"
                },
                diagnostics = diagnosticsData,
                nextSteps = new[]
                {
                    diagnosticsData != null ? "Review AppLens diagnostics above for detailed troubleshooting insights." : null,
                    healthStatus != null ? "Check the health status for any issues requiring attention." : null,
                    "Say 'search Azure documentation for <resource-type> troubleshooting' for official guidance.",
                    "Say 'get best practices for this resource type' for optimization recommendations."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource details with diagnostics for {ResourceId}", resourceId);
            return CreateErrorResponse("get resource details with diagnostics", ex);
        }
    }

    [KernelFunction("search_azure_documentation")]
    [Description("Search official Microsoft Azure documentation for guidance and troubleshooting. " +
                 "Powered by Azure MCP Server with access to up-to-date Microsoft Learn content. " +
                 "Use when you need official documentation, how-to guides, or troubleshooting steps.")]
    public async Task<string> SearchAzureDocumentationAsync(
        [Description("Search query (e.g., 'how to configure storage firewall', 'troubleshoot AKS connectivity')")] string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching Azure documentation for: {Query}", query);

            if (string.IsNullOrWhiteSpace(query))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Search query is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            await _azureMcpClient.InitializeAsync(cancellationToken);

            var docs = await _azureMcpClient.CallToolAsync("documentation", 
                new Dictionary<string, object?>
                {
                    ["query"] = query
                }, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = docs.Success,
                query = query,
                results = docs.Success ? docs.Result : "Documentation search unavailable",
                nextSteps = new[]
                {
                    "Review the documentation results above for official Microsoft guidance.",
                    "Say 'get best practices for <resource-type>' for specific recommendations.",
                    "Say 'show me details for resource <resource-id>' to apply this guidance to your resources."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Azure documentation for query: {Query}", query);
            return CreateErrorResponse("search Azure documentation", ex);
        }
    }

    [KernelFunction("get_resource_best_practices")]
    [Description("Get Azure and Terraform best practices for specific resource types. " +
                 "Powered by Azure MCP Server with curated recommendations from Microsoft. " +
                 "Use for optimization, compliance, security, and infrastructure improvements.")]
    public async Task<string> GetResourceBestPracticesAsync(
        [Description("Resource type (e.g., 'Microsoft.Storage/storageAccounts', 'AKS', 'App Service')")] string resourceType,
        [Description("Include Terraform best practices (default: false)")] bool includeTerraform = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting best practices for resource type: {ResourceType}", resourceType);

            if (string.IsNullOrWhiteSpace(resourceType))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Resource type is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            await _azureMcpClient.InitializeAsync(cancellationToken);

            var bestPracticesResults = new List<object>();

            // 1. Get Azure best practices
            var azureBp = await _azureMcpClient.CallToolAsync("get_bestpractices", 
                new Dictionary<string, object?>
                {
                    ["resourceType"] = resourceType
                }, cancellationToken);

            bestPracticesResults.Add(new
            {
                source = "Azure",
                available = azureBp.Success,
                data = azureBp.Success ? azureBp.Result : "Azure best practices not available"
            });

            // 2. Get Terraform best practices if requested
            if (includeTerraform)
            {
                var tfBp = await _azureMcpClient.CallToolAsync("azureterraformbestpractices", 
                    new Dictionary<string, object?>
                    {
                        ["resourceType"] = resourceType
                    }, cancellationToken);

                bestPracticesResults.Add(new
                {
                    source = "Terraform",
                    available = tfBp.Success,
                    data = tfBp.Success ? tfBp.Result : "Terraform best practices not available"
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceType = resourceType,
                bestPractices = bestPracticesResults,
                nextSteps = new[]
                {
                    "Review the best practices above to optimize your resources.",
                    "Say 'discover resources of type <type>' to find resources that could benefit from these practices.",
                    "Say 'search Azure documentation for <topic>' for more detailed implementation guidance.",
                    includeTerraform ? null : "Say 'include Terraform best practices' to see Infrastructure as Code recommendations."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting best practices for resource type: {ResourceType}", resourceType);
            return CreateErrorResponse("get resource best practices", ex);
        }
    }

    [KernelFunction("generate_bicep_for_resource")]
    [Description("Generate Bicep Infrastructure as Code for an existing Azure resource. " +
                 "Powered by Azure MCP Server to export resources as reusable Bicep templates. " +
                 "Use for IaC adoption, disaster recovery templates, or resource replication.")]
    public async Task<string> GenerateBicepForResourceAsync(
        [Description("Full Azure resource ID to generate Bicep code for")] string resourceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating Bicep code for resource: {ResourceId}", resourceId);

            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Resource ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // 1. Get resource details via SDK
            var resource = await _azureResourceService.GetResourceAsync(resourceId);

            if (resource == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Resource not found: {resourceId}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // 2. Use Azure MCP to generate Bicep
            await _azureMcpClient.InitializeAsync(cancellationToken);

            var bicep = await _azureMcpClient.CallToolAsync("bicepschema", 
                new Dictionary<string, object?>
                {
                    ["command"] = "generate",
                    ["parameters"] = new 
                    { 
                        resourceType = resource.Type,
                        resourceId = resourceId
                    }
                }, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = bicep.Success,
                resourceId = resourceId,
                resourceType = resource.Type,
                resourceName = resource.Name,
                bicepCode = bicep.Success ? bicep.Result : "Bicep generation not available",
                nextSteps = new[]
                {
                    "Copy the Bicep code above to a .bicep file for deployment.",
                    "Say 'get best practices for Bicep' for IaC recommendations.",
                    "Say 'generate Bicep for resource group <name>' to export multiple resources.",
                    "Review and customize the generated code before deploying to production."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Bicep for resource: {ResourceId}", resourceId);
            return CreateErrorResponse("generate Bicep for resource", ex);
        }
    }
}
