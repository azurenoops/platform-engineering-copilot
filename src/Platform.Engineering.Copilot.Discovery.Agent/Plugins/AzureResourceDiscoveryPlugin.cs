using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces.Discovery;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Azure;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Plugins;
using System.ComponentModel;
using Platform.Engineering.Copilot.Discovery.Core.Configuration;

namespace Platform.Engineering.Copilot.Discovery.Agent.Plugins;

/// <summary>
/// Production-ready plugin for Azure resource discovery, inventory management, and health monitoring.
/// Enhanced with Azure MCP Server integration for best practices, diagnostics, and documentation.
/// Provides comprehensive resource querying, filtering, and analysis capabilities.
/// </summary>
public class AzureResourceDiscoveryPlugin : BaseSupervisorPlugin
{
    private readonly IAzureResourceDiscoveryService _discoveryService;
    private readonly IAzureResourceService _azureResourceService;
    private readonly AzureMcpClient _azureMcpClient;
    private readonly ConfigService _configService;
    private readonly IMemoryCache _cache;
    private readonly DiscoveryAgentOptions _options;
    
    private const string LAST_SUBSCRIPTION_CACHE_KEY = "discovery_last_subscription";

    public AzureResourceDiscoveryPlugin(
        ILogger<AzureResourceDiscoveryPlugin> logger,
        Kernel kernel,
        IAzureResourceDiscoveryService discoveryService,
        IAzureResourceService azureResourceService,
        AzureMcpClient azureMcpClient,
        ConfigService configService,
        IMemoryCache cache,
        IOptions<DiscoveryAgentOptions> options) : base(logger, kernel)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }
    
    // ========== SUBSCRIPTION LOOKUP HELPERS ==========
    
    /// <summary>
    /// Stores the last used subscription ID in cache AND persistent config file for session continuity
    /// </summary>
    private void SetLastUsedSubscription(string subscriptionId)
    {
        _cache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
        try
        {
            _configService.SetDefaultSubscription(subscriptionId);
            _logger.LogInformation("Stored subscription in persistent config: {SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist subscription to config file, will only use cache");
        }
    }
    
    /// <summary>
    /// Gets the last used subscription ID from cache, or persistent config file if cache is empty
    /// </summary>
    private string? GetLastUsedSubscription()
    {
        if (_cache.TryGetValue<string>(LAST_SUBSCRIPTION_CACHE_KEY, out var subscriptionId))
        {
            _logger.LogDebug("Retrieved last used subscription from cache: {SubscriptionId}", subscriptionId);
            return subscriptionId;
        }
        
        try
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogInformation("Retrieved subscription from persistent config: {SubscriptionId}", subscriptionId);
                _cache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
                return subscriptionId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read subscription from config file");
        }
        
        return null;
    }
    
    /// <summary>
    /// Resolves a subscription identifier to a GUID. Accepts either a GUID or a friendly name.
    /// </summary>
    private async Task<string> ResolveSubscriptionIdAsync(string? subscriptionIdOrName)
    {
        if (string.IsNullOrWhiteSpace(subscriptionIdOrName))
        {
            var lastUsed = GetLastUsedSubscription();
            if (!string.IsNullOrWhiteSpace(lastUsed))
            {
                _logger.LogInformation("Using last used subscription from session: {SubscriptionId}", lastUsed);
                return lastUsed;
            }
            throw new ArgumentException("Subscription ID or name is required. No previous subscription found in session.", nameof(subscriptionIdOrName));
        }
        
        if (Guid.TryParse(subscriptionIdOrName, out _))
        {
            SetLastUsedSubscription(subscriptionIdOrName);
            return subscriptionIdOrName;
        }
        
        try
        {
            var subscription = await _azureResourceService.GetSubscriptionByNameAsync(subscriptionIdOrName);
            _logger.LogInformation("Resolved subscription name '{Name}' to ID '{SubscriptionId}' via Azure API", 
                subscriptionIdOrName, subscription.SubscriptionId);
            SetLastUsedSubscription(subscription.SubscriptionId);
            return subscription.SubscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve subscription '{Name}' via Azure API", subscriptionIdOrName);
        }
        
        throw new ArgumentException($"Subscription '{subscriptionIdOrName}' not found. Provide a valid GUID or subscription name.", nameof(subscriptionIdOrName));
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

            // Use caching with configured CacheDurationMinutes
            var cacheKey = $"discovery_resources_{subscriptionId}";
            IEnumerable<AzureResource>? allResources;
            
            if (!_cache.TryGetValue<List<AzureResource>>(cacheKey, out var cachedResources) || cachedResources == null)
            {
                var resources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
                cachedResources = resources.ToList();
                
                var cacheExpiration = TimeSpan.FromMinutes(_options.Discovery.CacheDurationMinutes);
                _cache.Set(cacheKey, cachedResources, cacheExpiration);
                _logger.LogDebug("Cached {Count} resources for {Minutes} minutes", cachedResources.Count, _options.Discovery.CacheDurationMinutes);
            }
            else
            {
                _logger.LogDebug("Using cached resources for subscription {SubscriptionId}", subscriptionId);
            }
            
            allResources = cachedResources;

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
            
            // Apply configuration: IncludeDeletedResources filter
            if (!_options.Discovery.IncludeDeletedResources)
            {
                filteredResources = filteredResources.Where(r => 
                    r.ProvisioningState == null || 
                    !r.ProvisioningState.Equals("Deleting", StringComparison.OrdinalIgnoreCase));
            }

            // Apply configuration: MaxResourcesPerQuery
            if (_options.Discovery.MaxResourcesPerQuery > 0)
            {
                filteredResources = filteredResources.Take(_options.Discovery.MaxResourcesPerQuery);
            }

            var resourceList = filteredResources.ToList();
            
            // Check RequiredTags compliance and build warnings
            var missingTagsWarnings = new List<object>();
            if (_options.Discovery.RequiredTags?.Any() == true)
            {
                var resourcesWithMissingTags = resourceList
                    .Where(r => _options.Discovery.RequiredTags.Any(tag => 
                        r.Tags == null || !r.Tags.ContainsKey(tag)))
                    .Take(10) // Limit warnings
                    .Select(r => new
                    {
                        resourceName = r.Name,
                        resourceType = r.Type,
                        missingTags = _options.Discovery.RequiredTags
                            .Where(tag => r.Tags == null || !r.Tags.ContainsKey(tag))
                            .ToList()
                    })
                    .ToList();
                    
                if (resourcesWithMissingTags.Any())
                {
                    _logger.LogWarning("{Count} resources missing required tags: {Tags}", 
                        resourcesWithMissingTags.Count, 
                        string.Join(", ", _options.Discovery.RequiredTags));
                    missingTagsWarnings.AddRange(resourcesWithMissingTags);
                }
            }

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
                    location = location ?? "all locations",
                    includeDeletedResources = _options.Discovery.IncludeDeletedResources
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
                compliance = missingTagsWarnings.Any() ? new
                {
                    requiredTags = _options.Discovery.RequiredTags,
                    resourcesWithMissingTags = missingTagsWarnings.Count,
                    warnings = missingTagsWarnings
                } : null,
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
    [Description("Get detailed information about a specific Azure resource using Azure Resource Graph for fast retrieval. " +
                 "PRIMARY FUNCTION: Use this for all normal resource detail queries. " +
                 "Returns configuration, properties, SKU, kind, tags, location, provisioning state, and health status. " +
                 "Optimized for speed using Azure Resource Graph API.")]
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

            _logger.LogInformation("Getting resource details for: {ResourceId}", resourceId);

            // Extract subscription ID from resource ID
            var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var subIndex = Array.IndexOf(parts, "subscriptions");
            var subscriptionId = (subIndex >= 0 && subIndex + 1 < parts.Length) ? parts[subIndex + 1] : string.Empty;
            
            if (string.IsNullOrEmpty(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Could not extract subscription ID from resource ID"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Call Discovery Service (orchestration engine) - it handles Resource Graph + API fallback + MCP
            _logger.LogInformation("🔍 DIAGNOSTIC [get_resource_details]: About to call _discoveryService.GetResourceDetailsAsync");
            _logger.LogInformation("🔍 DIAGNOSTIC [get_resource_details]: _discoveryService is null: {IsNull}", _discoveryService == null);
            _logger.LogInformation("🔍 DIAGNOSTIC [get_resource_details]: Resource ID: {ResourceId}, Subscription: {SubscriptionId}", resourceId, subscriptionId);
            
            var result = await _discoveryService.GetResourceDetailsAsync(
                resourceId,  // Pass resource ID as query (AI will parse it)
                subscriptionId,
                cancellationToken);
            
            _logger.LogInformation("🔍 DIAGNOSTIC [get_resource_details]: _discoveryService.GetResourceDetailsAsync returned. Success: {Success}", result.Success);

            if (!result.Success || result.Resource == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = result.ErrorDetails ?? "Resource not found"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceId = resourceId,
                dataSource = result.DataSource ?? "Unknown",
                resource = new
                {
                    id = result.Resource.ResourceId ?? resourceId,
                    name = result.Resource.Name ?? "Unknown",
                    type = result.Resource.Type ?? "Unknown",
                    location = result.Resource.Location ?? "Unknown",
                    tags = result.Resource.Tags ?? new Dictionary<string, string>(),
                    sku = result.Resource.Sku ?? "Not specified",
                    kind = result.Resource.Kind ?? "Not specified",
                    provisioningState = result.Resource.ProvisioningState ?? "Not specified",
                    properties = result.Resource.Properties ?? new Dictionary<string, object>()
                },
                health = result.HealthStatus != null ? (object)new
                {
                    available = true,
                    status = result.HealthStatus
                } : new
                {
                    available = false,
                    message = "Health status not available for this resource type"
                },
                nextSteps = result.HealthStatus != null
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
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            // Filter by tag
            var matchedResources = allResources.Where(r =>
            {
                if (r.Tags == null) return false;

                if (!r.Tags.ContainsKey(tagKey)) return false;

                if (string.IsNullOrWhiteSpace(tagValue)) return true;

                return r.Tags[tagKey]?.Equals(tagValue, StringComparison.OrdinalIgnoreCase) == true;
            });

            // Apply configuration: MaxResourcesPerQuery
            if (_options.Discovery.MaxResourcesPerQuery > 0)
            {
                matchedResources = matchedResources.Take(_options.Discovery.MaxResourcesPerQuery);
            }

            var resourceList = matchedResources.ToList();

            // Group by resource type and tag value
            var byType = resourceList.GroupBy(r => r.Type ?? "Unknown")
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count);

            var byTagValue = resourceList
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
                    totalMatches = matchedResources.Count(),
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
                    matchedResources.Count() == 0 ? $"No resources found with tag '{tagKey}'. Try searching for a different tag or check your tag naming." : null,
                    matchedResources.Count() > 100 ? "Results limited to 100 resources - consider filtering by tag value to narrow results." : null,
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

            if (!_options.EnableDependencyMapping)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Dependency mapping is currently disabled. Please enable it in the Discovery Agent configuration."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get resources to analyze
            var resources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

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
                    resourcesAnalyzed = resources.Count()
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
                        var resources = await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, rgName!, cancellationToken);
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
            var resources = await _azureResourceService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
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
                subscriptions = subList.Select(sub => new
                {
                    subscriptionId = sub.SubscriptionId,
                    displayName = sub.SubscriptionName,
                    state = sub.State,
                    tenantId = sub.TenantId,
                    createdDate = sub.CreatedDate,
                    tags = sub.Tags
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

            if (!_options.EnableHealthMonitoring)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Health monitoring is currently disabled. Please enable it in the Discovery Agent configuration."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Use caching with configured HealthMonitoring.RefreshIntervalMinutes
            var cacheKey = $"health_events_{subscriptionId}";
            List<object> eventsList;
            
            if (!_cache.TryGetValue<List<object>>(cacheKey, out var cachedEvents) || cachedEvents == null)
            {
                var healthEvents = await _azureResourceService.GetResourceHealthEventsAsync(subscriptionId, cancellationToken);
                eventsList = healthEvents.ToList();
                
                var cacheExpiration = TimeSpan.FromMinutes(_options.HealthMonitoring.RefreshIntervalMinutes);
                _cache.Set(cacheKey, eventsList, cacheExpiration);
                _logger.LogDebug("Cached health events for {Minutes} minutes", _options.HealthMonitoring.RefreshIntervalMinutes);
            }
            else
            {
                eventsList = cachedEvents;
                _logger.LogDebug("Using cached health events for subscription {SubscriptionId}", subscriptionId);
            }

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
                configuration = new
                {
                    refreshIntervalMinutes = _options.HealthMonitoring.RefreshIntervalMinutes,
                    metricsRetentionDays = _options.HealthMonitoring.MetricsRetentionDays,
                    performanceMetricsEnabled = _options.EnablePerformanceMetrics,
                    trackedMetrics = _options.EnablePerformanceMetrics ? _options.HealthMonitoring.PerformanceMetrics : null
                },
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

            if (!_options.EnableHealthMonitoring)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Health monitoring is currently disabled. Please enable it in the Discovery Agent configuration."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

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
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

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
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
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

    // ========== ORPHANED RESOURCE DETECTION ==========

    [KernelFunction("find_orphaned_resources")]
    [Description("Find orphaned and unused Azure resources that may be candidates for cleanup. " +
                 "Detects: unattached managed disks, unused NICs, empty NSGs, idle public IPs, empty load balancers, and orphaned snapshots. " +
                 "Use for cost optimization, resource cleanup, and governance compliance.")]
    public async Task<string> FindOrphanedResourcesAsync(
        [Description("Azure subscription ID to scan for orphaned resources")] string subscriptionId,
        [Description("Resource types to check (comma-separated: disks,nics,nsgs,publicips,loadbalancers,snapshots,all). Default: all")] string resourceTypes = "all",
        [Description("Resource group to limit scan to (optional - scans all if not specified)")] string? resourceGroup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Finding orphaned resources in subscription {SubscriptionId}", subscriptionId);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Subscription ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var typesToCheck = resourceTypes.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToHashSet();
            var checkAll = typesToCheck.Contains("all") || !typesToCheck.Any();

            var orphanedResources = new List<object>();
            var summary = new Dictionary<string, int>();
            var estimatedMonthlySavings = 0.0;

            // Get all resources
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            // Filter by resource group if specified
            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                allResources = allResources.Where(r => 
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            // 1. Check for unattached managed disks
            if (checkAll || typesToCheck.Contains("disks"))
            {
                var disks = allResources.Where(r => 
                    r.Type?.Equals("Microsoft.Compute/disks", StringComparison.OrdinalIgnoreCase) == true).ToList();

                foreach (var disk in disks)
                {
                    try
                    {
                        var details = await _azureResourceService.GetResourceAsync(disk.Id!);
                        if (details?.Properties != null)
                        {
                            // Check if disk is attached (diskState = "Unattached" or no managedBy property)
                            var diskState = details.Properties.GetValueOrDefault("diskState")?.ToString();
                            var managedBy = details.Properties.GetValueOrDefault("managedBy")?.ToString();

                            if (string.IsNullOrEmpty(managedBy) || diskState?.Equals("Unattached", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var diskSizeGb = GetPropertyValueFromDict<int>(details.Properties, "diskSizeGB", 0);
                                var estimatedCost = EstimateDiskMonthlyCost(disk.Sku ?? "Standard_LRS", diskSizeGb);

                                orphanedResources.Add(new
                                {
                                    resourceId = disk.Id,
                                    name = disk.Name,
                                    type = "Unattached Disk",
                                    resourceGroup = disk.ResourceGroup,
                                    location = disk.Location,
                                    reason = "Disk is not attached to any VM",
                                    details = new { sku = disk.Sku, sizeGB = diskSizeGb, diskState },
                                    estimatedMonthlyCost = estimatedCost
                                });

                                estimatedMonthlySavings += estimatedCost;
                                summary["Unattached Disks"] = summary.GetValueOrDefault("Unattached Disks") + 1;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not check disk {DiskId}", disk.Id);
                    }
                }
            }

            // 2. Check for unused Network Interfaces
            if (checkAll || typesToCheck.Contains("nics"))
            {
                var nics = allResources.Where(r => 
                    r.Type?.Equals("Microsoft.Network/networkInterfaces", StringComparison.OrdinalIgnoreCase) == true).ToList();

                foreach (var nic in nics)
                {
                    try
                    {
                        var details = await _azureResourceService.GetResourceAsync(nic.Id!);
                        if (details?.Properties != null)
                        {
                            // Check if NIC is attached to a VM
                            var virtualMachine = details.Properties.GetValueOrDefault("virtualMachine")?.ToString();

                            if (string.IsNullOrEmpty(virtualMachine) || virtualMachine == "{}")
                            {
                                orphanedResources.Add(new
                                {
                                    resourceId = nic.Id,
                                    name = nic.Name,
                                    type = "Unused NIC",
                                    resourceGroup = nic.ResourceGroup,
                                    location = nic.Location,
                                    reason = "Network interface is not attached to any VM",
                                    details = new { tags = nic.Tags },
                                    estimatedMonthlyCost = 0.0 // NICs are free but consume IP addresses
                                });

                                summary["Unused NICs"] = summary.GetValueOrDefault("Unused NICs") + 1;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not check NIC {NicId}", nic.Id);
                    }
                }
            }

            // 3. Check for empty/unattached NSGs
            if (checkAll || typesToCheck.Contains("nsgs"))
            {
                var nsgs = allResources.Where(r => 
                    r.Type?.Equals("Microsoft.Network/networkSecurityGroups", StringComparison.OrdinalIgnoreCase) == true).ToList();

                foreach (var nsg in nsgs)
                {
                    try
                    {
                        var details = await _azureResourceService.GetResourceAsync(nsg.Id!);
                        if (details?.Properties != null)
                        {
                            // Check if NSG is attached to any NIC or Subnet
                            var networkInterfaces = details.Properties.GetValueOrDefault("networkInterfaces");
                            var subnets = details.Properties.GetValueOrDefault("subnets");

                            var hasNics = networkInterfaces != null && networkInterfaces.ToString() != "[]" && networkInterfaces.ToString() != "{}";
                            var hasSubnets = subnets != null && subnets.ToString() != "[]" && subnets.ToString() != "{}";

                            if (!hasNics && !hasSubnets)
                            {
                                orphanedResources.Add(new
                                {
                                    resourceId = nsg.Id,
                                    name = nsg.Name,
                                    type = "Unattached NSG",
                                    resourceGroup = nsg.ResourceGroup,
                                    location = nsg.Location,
                                    reason = "NSG is not attached to any NIC or subnet",
                                    details = new { tags = nsg.Tags },
                                    estimatedMonthlyCost = 0.0 // NSGs are free
                                });

                                summary["Unattached NSGs"] = summary.GetValueOrDefault("Unattached NSGs") + 1;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not check NSG {NsgId}", nsg.Id);
                    }
                }
            }

            // 4. Check for unused Public IPs
            if (checkAll || typesToCheck.Contains("publicips"))
            {
                var publicIps = allResources.Where(r => 
                    r.Type?.Equals("Microsoft.Network/publicIPAddresses", StringComparison.OrdinalIgnoreCase) == true).ToList();

                foreach (var pip in publicIps)
                {
                    try
                    {
                        var details = await _azureResourceService.GetResourceAsync(pip.Id!);
                        if (details?.Properties != null)
                        {
                            // Check if Public IP is associated with anything
                            var ipConfiguration = details.Properties.GetValueOrDefault("ipConfiguration")?.ToString();

                            if (string.IsNullOrEmpty(ipConfiguration) || ipConfiguration == "{}")
                            {
                                var sku = pip.Sku ?? "Basic";
                                var estimatedCost = sku.Contains("Standard", StringComparison.OrdinalIgnoreCase) ? 3.65 : 0.0;

                                orphanedResources.Add(new
                                {
                                    resourceId = pip.Id,
                                    name = pip.Name,
                                    type = "Unused Public IP",
                                    resourceGroup = pip.ResourceGroup,
                                    location = pip.Location,
                                    reason = "Public IP is not associated with any resource",
                                    details = new { sku, tags = pip.Tags },
                                    estimatedMonthlyCost = estimatedCost
                                });

                                estimatedMonthlySavings += estimatedCost;
                                summary["Unused Public IPs"] = summary.GetValueOrDefault("Unused Public IPs") + 1;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not check Public IP {PipId}", pip.Id);
                    }
                }
            }

            // 5. Check for empty Load Balancers
            if (checkAll || typesToCheck.Contains("loadbalancers"))
            {
                var lbs = allResources.Where(r => 
                    r.Type?.Equals("Microsoft.Network/loadBalancers", StringComparison.OrdinalIgnoreCase) == true).ToList();

                foreach (var lb in lbs)
                {
                    try
                    {
                        var details = await _azureResourceService.GetResourceAsync(lb.Id!);
                        if (details?.Properties != null)
                        {
                            // Check if Load Balancer has backend pool members
                            var backendPools = details.Properties.GetValueOrDefault("backendAddressPools");
                            var hasBackends = false;

                            if (backendPools != null)
                            {
                                var poolsStr = backendPools.ToString();
                                // Simple check - if it contains backendIPConfigurations or loadBalancerBackendAddresses with content
                                hasBackends = poolsStr != null && 
                                    (poolsStr.Contains("backendIPConfigurations") || poolsStr.Contains("loadBalancerBackendAddresses")) &&
                                    !poolsStr.Contains("\"backendIPConfigurations\":[]") &&
                                    !poolsStr.Contains("\"loadBalancerBackendAddresses\":[]");
                            }

                            if (!hasBackends)
                            {
                                var sku = lb.Sku ?? "Basic";
                                var estimatedCost = sku.Contains("Standard", StringComparison.OrdinalIgnoreCase) ? 18.25 : 0.0;

                                orphanedResources.Add(new
                                {
                                    resourceId = lb.Id,
                                    name = lb.Name,
                                    type = "Empty Load Balancer",
                                    resourceGroup = lb.ResourceGroup,
                                    location = lb.Location,
                                    reason = "Load balancer has no backend pool members",
                                    details = new { sku, tags = lb.Tags },
                                    estimatedMonthlyCost = estimatedCost
                                });

                                estimatedMonthlySavings += estimatedCost;
                                summary["Empty Load Balancers"] = summary.GetValueOrDefault("Empty Load Balancers") + 1;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not check Load Balancer {LbId}", lb.Id);
                    }
                }
            }

            // 6. Check for orphaned snapshots (older than 30 days)
            if (checkAll || typesToCheck.Contains("snapshots"))
            {
                var snapshots = allResources.Where(r => 
                    r.Type?.Equals("Microsoft.Compute/snapshots", StringComparison.OrdinalIgnoreCase) == true).ToList();

                foreach (var snapshot in snapshots)
                {
                    try
                    {
                        var details = await _azureResourceService.GetResourceAsync(snapshot.Id!);
                        if (details?.Properties != null)
                        {
                            // Check creation time - flag snapshots older than 30 days
                            var timeCreated = details.Properties.GetValueOrDefault("timeCreated")?.ToString();
                            var snapshotSizeGb = GetPropertyValueFromDict<int>(details.Properties, "diskSizeGB", 0);

                            if (DateTime.TryParse(timeCreated, out var createdDate) && 
                                createdDate < DateTime.UtcNow.AddDays(-30))
                            {
                                var estimatedCost = snapshotSizeGb * 0.05; // ~$0.05/GB/month for snapshots

                                orphanedResources.Add(new
                                {
                                    resourceId = snapshot.Id,
                                    name = snapshot.Name,
                                    type = "Old Snapshot",
                                    resourceGroup = snapshot.ResourceGroup,
                                    location = snapshot.Location,
                                    reason = $"Snapshot is {(DateTime.UtcNow - createdDate).Days} days old",
                                    details = new { sizeGB = snapshotSizeGb, created = timeCreated, tags = snapshot.Tags },
                                    estimatedMonthlyCost = estimatedCost
                                });

                                estimatedMonthlySavings += estimatedCost;
                                summary["Old Snapshots"] = summary.GetValueOrDefault("Old Snapshots") + 1;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not check snapshot {SnapshotId}", snapshot.Id);
                    }
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                scope = new
                {
                    resourceGroup = resourceGroup ?? "all resource groups",
                    resourceTypesChecked = checkAll ? "all" : string.Join(", ", typesToCheck)
                },
                summary = new
                {
                    totalOrphanedResources = orphanedResources.Count,
                    estimatedMonthlySavings = Math.Round(estimatedMonthlySavings, 2),
                    byType = summary
                },
                orphanedResources = orphanedResources.Take(100),
                nextSteps = new[]
                {
                    orphanedResources.Count == 0 ? "✅ No orphaned resources found - your subscription is clean!" : null,
                    orphanedResources.Count > 0 ? $"Found {orphanedResources.Count} orphaned resources with estimated savings of ${Math.Round(estimatedMonthlySavings, 2)}/month" : null,
                    orphanedResources.Count > 0 ? "Review each resource before deletion to ensure it's truly unused." : null,
                    orphanedResources.Count > 0 ? "Say 'delete orphaned resources in resource group <name>' to clean up a specific group." : null,
                    orphanedResources.Count > 100 ? "Results limited to 100 resources - use resource group filter to narrow scope." : null,
                    "Consider implementing Azure Policy to prevent future orphaned resources.",
                    "Schedule regular orphaned resource scans for ongoing cost optimization."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding orphaned resources in subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("find orphaned resources", ex);
        }
    }

    private double EstimateDiskMonthlyCost(string sku, int sizeGb)
    {
        // Rough estimates based on Azure pricing (varies by region)
        return sku.ToLowerInvariant() switch
        {
            "premium_lrs" or "premium_zrs" => sizeGb * 0.135, // ~$0.135/GB
            "standardssd_lrs" or "standardssd_zrs" => sizeGb * 0.075, // ~$0.075/GB
            "standard_lrs" or "standard_zrs" => sizeGb * 0.04, // ~$0.04/GB
            "ultrassd_lrs" => sizeGb * 0.12, // ~$0.12/GB provisioned
            _ => sizeGb * 0.05 // Default estimate
        };
    }

    private T GetPropertyValueFromDict<T>(Dictionary<string, object>? props, string key, T defaultValue)
    {
        if (props == null) return defaultValue;
        if (!props.TryGetValue(key, out var value)) return defaultValue;
        
        try
        {
            if (value is T typedValue) return typedValue;
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (typeof(T) == typeof(int) && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                    return (T)(object)jsonElement.GetInt32();
                if (typeof(T) == typeof(string) && jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    return (T)(object)jsonElement.GetString()!;
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
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

    [KernelFunction("discover_resources_with_guidance")]
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
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

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

    [KernelFunction("get_resource_with_diagnostics")]
    [Description("TROUBLESHOOTING ONLY: Get resource details with AppLens diagnostics for troubleshooting. " +
                 "Only use when user EXPLICITLY asks to troubleshoot, diagnose, or fix problems. " +
                 "NOT for normal resource queries - use get_resource_details for standard requests. " +
                 "Includes AppLens diagnostics which adds significant latency.")]
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

            // Extract subscription ID from resource ID
            var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var subIndex = Array.IndexOf(parts, "subscriptions");
            var subscriptionId = (subIndex >= 0 && subIndex + 1 < parts.Length) ? parts[subIndex + 1] : string.Empty;
            
            if (string.IsNullOrEmpty(subscriptionId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Could not extract subscription ID from resource ID"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // 1. Use Discovery Service (which uses Resource Graph + API fallback)
            _logger.LogInformation("🔍 DIAGNOSTIC: About to call _discoveryService.GetResourceDetailsAsync");
            _logger.LogInformation("🔍 DIAGNOSTIC: _discoveryService is null: {IsNull}", _discoveryService == null);
            _logger.LogInformation("🔍 DIAGNOSTIC: Resource ID: {ResourceId}, Subscription: {SubscriptionId}", resourceId, subscriptionId);
            
            var result = await _discoveryService.GetResourceDetailsAsync(
                resourceId,
                subscriptionId,
                cancellationToken);
            
            _logger.LogInformation("🔍 DIAGNOSTIC: _discoveryService.GetResourceDetailsAsync returned. Success: {Success}", result.Success);

            if (!result.Success || result.Resource == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Resource not found: {resourceId}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var resource = result.Resource;

            // 2. Health status is already included in the Discovery Service result
            object? healthStatus = result.HealthStatus;

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
                    id = resource.ResourceId ?? resourceId,
                    name = resource.Name ?? "Unknown",
                    type = resource.Type ?? "Unknown",
                    location = resource.Location ?? "Unknown",
                    resourceGroup = resource.ResourceGroup,
                    tags = resource.Tags,
                    sku = resource.Sku,
                    kind = resource.Kind,
                    provisioningState = resource.ProvisioningState,
                    dataSource = result.DataSource  // Show if data came from ResourceGraph or API
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

    // ========== AZURE ARC HYBRID MACHINE FUNCTIONS ==========

    [KernelFunction("list_arc_machines")]
    [Description("List all Azure Arc-connected hybrid machines (on-premises servers, multi-cloud VMs from AWS/GCP, edge devices). " +
                 "Shows connection status, OS type, agent version, and cloud provider origin. " +
                 "Use for hybrid infrastructure inventory, finding disconnected servers, or auditing Arc deployments. " +
                 "Example: 'List all Arc-connected servers', 'Show me disconnected Arc machines', 'What on-prem servers are in Azure Arc?'")]
    public async Task<string> ListArcMachinesAsync(
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group to filter by (optional - lists all if not specified)")] string? resourceGroup = null,
        [Description("Filter by connection status: 'Connected', 'Disconnected', 'Error', or 'all' (default)")] string connectionStatus = "all",
        [Description("Filter by OS type: 'Windows', 'Linux', or 'all' (default)")] string osType = "all",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            _logger.LogInformation("Listing Azure Arc machines in subscription {SubscriptionId}", subscriptionId);

            // Get all resources of type Microsoft.HybridCompute/machines
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var arcMachines = allResources.Where(r => 
                r.Type?.Equals("Microsoft.HybridCompute/machines", StringComparison.OrdinalIgnoreCase) == true).ToList();

            // Apply resource group filter
            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                arcMachines = arcMachines.Where(r => 
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            // Get detailed info for each Arc machine
            var machineDetails = new List<object>();
            var statusSummary = new Dictionary<string, int>
            {
                ["Connected"] = 0,
                ["Disconnected"] = 0,
                ["Error"] = 0,
                ["Unknown"] = 0
            };
            var osSummary = new Dictionary<string, int>
            {
                ["Windows"] = 0,
                ["Linux"] = 0,
                ["Unknown"] = 0
            };
            var cloudProviderSummary = new Dictionary<string, int>();

            foreach (var machine in arcMachines)
            {
                try
                {
                    var details = await _azureResourceService.GetResourceAsync(machine.Id!);
                    if (details?.Properties != null)
                    {
                        var status = GetPropertyValueFromDict<string>(details.Properties, "status", "Unknown");
                        var machineOsType = GetPropertyValueFromDict<string>(details.Properties, "osType", "Unknown");
                        var agentVersion = GetPropertyValueFromDict<string>(details.Properties, "agentVersion", "Unknown");
                        var machineFqdn = GetPropertyValueFromDict<string>(details.Properties, "machineFqdn", "");
                        var lastStatusChange = GetPropertyValueFromDict<string>(details.Properties, "lastStatusChange", "");
                        var osName = GetPropertyValueFromDict<string>(details.Properties, "osName", "");
                        var osVersion = GetPropertyValueFromDict<string>(details.Properties, "osVersion", "");
                        
                        // Get cloud provider from detectedProperties if available
                        var cloudProvider = "On-Premises";
                        if (details.Properties.TryGetValue("detectedProperties", out var detectedPropsObj) && 
                            detectedPropsObj is JsonElement detectedProps)
                        {
                            if (detectedProps.TryGetProperty("cloudprovider", out var cpProp))
                            {
                                cloudProvider = cpProp.GetString() ?? "On-Premises";
                                if (cloudProvider == "N/A") cloudProvider = "On-Premises";
                            }
                        }

                        // Get Arc kind (HCI, VMware, SCVMM, AWS, GCP, etc.)
                        var arcKind = machine.Kind ?? cloudProvider;

                        // Apply connection status filter
                        if (!connectionStatus.Equals("all", StringComparison.OrdinalIgnoreCase) &&
                            !status.Equals(connectionStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Apply OS type filter
                        if (!osType.Equals("all", StringComparison.OrdinalIgnoreCase) &&
                            !machineOsType.Equals(osType, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Update summaries
                        statusSummary[status] = statusSummary.GetValueOrDefault(status) + 1;
                        var osKey = machineOsType.Equals("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows" :
                                   machineOsType.Equals("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux" : "Unknown";
                        osSummary[osKey]++;
                        cloudProviderSummary[cloudProvider] = cloudProviderSummary.GetValueOrDefault(cloudProvider) + 1;

                        machineDetails.Add(new
                        {
                            resourceId = machine.Id,
                            name = machine.Name,
                            resourceGroup = machine.ResourceGroup,
                            location = machine.Location,
                            status = status,
                            statusIcon = status switch
                            {
                                "Connected" => "🟢",
                                "Disconnected" => "🔴",
                                "Error" => "⚠️",
                                _ => "❓"
                            },
                            osType = machineOsType,
                            osName = osName,
                            osVersion = osVersion,
                            machineFqdn = machineFqdn,
                            agentVersion = agentVersion,
                            lastStatusChange = lastStatusChange,
                            cloudProvider = cloudProvider,
                            arcKind = arcKind,
                            tags = machine.Tags
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get details for Arc machine {MachineId}", machine.Id);
                    machineDetails.Add(new
                    {
                        resourceId = machine.Id,
                        name = machine.Name,
                        resourceGroup = machine.ResourceGroup,
                        location = machine.Location,
                        status = "Unknown",
                        statusIcon = "❓",
                        error = "Could not retrieve machine details"
                    });
                }
            }

            // Calculate health metrics
            var totalMachines = machineDetails.Count;
            var connectedCount = statusSummary.GetValueOrDefault("Connected");
            var disconnectedCount = statusSummary.GetValueOrDefault("Disconnected");
            var healthPercentage = totalMachines > 0 ? (connectedCount * 100.0 / totalMachines) : 0;

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "🌐 AZURE ARC HYBRID MACHINES",
                    icon = "🖥️",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    filters = new { connectionStatus, osType }
                },
                summary = new
                {
                    totalMachines = totalMachines,
                    connected = connectedCount,
                    disconnected = disconnectedCount,
                    errors = statusSummary.GetValueOrDefault("Error"),
                    healthPercentage = $"{healthPercentage:F1}%",
                    healthStatus = healthPercentage >= 90 ? "Healthy" : healthPercentage >= 70 ? "Warning" : "Critical"
                },
                breakdown = new
                {
                    byStatus = statusSummary.Where(kv => kv.Value > 0),
                    byOsType = osSummary.Where(kv => kv.Value > 0),
                    byCloudProvider = cloudProviderSummary.OrderByDescending(kv => kv.Value)
                },
                machines = machineDetails,
                alerts = disconnectedCount > 0 ? new[]
                {
                    $"⚠️ {disconnectedCount} machine(s) are disconnected and may need attention",
                    "Disconnected machines cannot receive updates or policy assignments",
                    "Check network connectivity and agent status on disconnected machines"
                } : null,
                nextSteps = new[]
                {
                    totalMachines == 0 ? "No Arc machines found. Say 'generate Arc onboarding script' to connect your first server." : null,
                    disconnectedCount > 0 ? $"Say 'get Arc machine details for <machine-name>' to investigate disconnected machines." : null,
                    "Say 'get Arc machine details for <machine-name>' to see hardware, extensions, and configuration.",
                    "Say 'get Arc extensions' to see what extensions are installed on your hybrid machines.",
                    "Say 'check Arc connection health' for a detailed health report."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Azure Arc machines");
            return CreateErrorResponse("list Azure Arc machines", ex);
        }
    }

    [KernelFunction("get_arc_machine_details")]
    [Description("Get detailed information about a specific Azure Arc-connected machine including hardware profile, " +
                 "network configuration, installed extensions, agent configuration, and license status. " +
                 "Use for troubleshooting, inventory audits, or understanding machine capabilities. " +
                 "Example: 'Show details for Arc machine webserver01', 'What extensions are on Arc server db-prod-01?'")]
    public async Task<string> GetArcMachineDetailsAsync(
        [Description("Name of the Arc machine to get details for")] string machineName,
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group containing the Arc machine (optional - will search all if not specified)")] string? resourceGroup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            _logger.LogInformation("Getting details for Azure Arc machine {MachineName} in subscription {SubscriptionId}", 
                machineName, subscriptionId);

            // Find the Arc machine
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var arcMachine = allResources.FirstOrDefault(r =>
                r.Type?.Equals("Microsoft.HybridCompute/machines", StringComparison.OrdinalIgnoreCase) == true &&
                r.Name?.Equals(machineName, StringComparison.OrdinalIgnoreCase) == true &&
                (string.IsNullOrWhiteSpace(resourceGroup) || 
                 r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true));

            if (arcMachine == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Arc machine '{machineName}' not found in subscription {subscriptionId}",
                    suggestion = "Use 'list Arc machines' to see available machines"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Get detailed properties
            var details = await _azureResourceService.GetResourceAsync(arcMachine.Id!);
            if (details?.Properties == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Could not retrieve details for Arc machine '{machineName}'"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var props = details.Properties;

            // Extract all the detailed properties
            var status = GetPropertyValueFromDict<string>(props, "status", "Unknown");
            var agentVersion = GetPropertyValueFromDict<string>(props, "agentVersion", "Unknown");
            var machineFqdn = GetPropertyValueFromDict<string>(props, "machineFqdn", "");
            var lastStatusChange = GetPropertyValueFromDict<string>(props, "lastStatusChange", "");
            var machineOsType = GetPropertyValueFromDict<string>(props, "osType", "Unknown");
            var osName = GetPropertyValueFromDict<string>(props, "osName", "");
            var osVersion = GetPropertyValueFromDict<string>(props, "osVersion", "");
            var osSku = GetPropertyValueFromDict<string>(props, "osSku", "");
            var osEdition = GetPropertyValueFromDict<string>(props, "osEdition", "");
            var domainName = GetPropertyValueFromDict<string>(props, "domainName", "");
            var adFqdn = GetPropertyValueFromDict<string>(props, "adFqdn", "");
            var dnsFqdn = GetPropertyValueFromDict<string>(props, "dnsFqdn", "");
            var vmId = GetPropertyValueFromDict<string>(props, "vmId", "");
            var provisioningState = GetPropertyValueFromDict<string>(props, "provisioningState", "");
            var mssqlDiscovered = GetPropertyValueFromDict<string>(props, "mssqlDiscovered", "false");

            // Extract hardware profile if available
            object? hardwareProfile = null;
            if (props.TryGetValue("hardwareProfile", out var hwObj) && hwObj is JsonElement hwElement)
            {
                hardwareProfile = new
                {
                    totalPhysicalMemoryGB = hwElement.TryGetProperty("totalPhysicalMemoryInBytes", out var memProp) 
                        ? (memProp.GetInt64() / (1024.0 * 1024 * 1024)).ToString("F2") + " GB" : "Unknown",
                    cpuSockets = hwElement.TryGetProperty("numberOfCpuSockets", out var cpuProp) 
                        ? cpuProp.GetInt32() : 0
                };
            }

            // Extract network profile if available
            object? networkProfile = null;
            if (props.TryGetValue("networkProfile", out var netObj) && netObj is JsonElement netElement)
            {
                var interfaces = new List<object>();
                if (netElement.TryGetProperty("networkInterfaces", out var nifsElement) && 
                    nifsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var nif in nifsElement.EnumerateArray())
                    {
                        var ipAddresses = new List<string>();
                        if (nif.TryGetProperty("ipAddresses", out var ipsElement) && 
                            ipsElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var ip in ipsElement.EnumerateArray())
                            {
                                if (ip.TryGetProperty("address", out var addrProp))
                                {
                                    ipAddresses.Add(addrProp.GetString() ?? "");
                                }
                            }
                        }

                        interfaces.Add(new
                        {
                            name = nif.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "Unknown",
                            macAddress = nif.TryGetProperty("macAddress", out var macProp) ? macProp.GetString() : "",
                            ipAddresses = ipAddresses
                        });
                    }
                }
                networkProfile = new { networkInterfaces = interfaces };
            }

            // Extract agent configuration
            object? agentConfiguration = null;
            if (props.TryGetValue("agentConfiguration", out var agentObj) && agentObj is JsonElement agentElement)
            {
                agentConfiguration = new
                {
                    extensionsEnabled = agentElement.TryGetProperty("extensionsEnabled", out var extProp) 
                        ? extProp.GetString() : "Unknown",
                    guestConfigurationEnabled = agentElement.TryGetProperty("guestConfigurationEnabled", out var gcProp) 
                        ? gcProp.GetString() : "Unknown",
                    configMode = agentElement.TryGetProperty("configMode", out var modeProp) 
                        ? modeProp.GetString() : "Unknown",
                    proxyUrl = agentElement.TryGetProperty("proxyUrl", out var proxyProp) 
                        ? proxyProp.GetString() : null
                };
            }

            // Extract detected properties (cloud provider, manufacturer, model)
            object? detectedProperties = null;
            if (props.TryGetValue("detectedProperties", out var detectedObj) && detectedObj is JsonElement detectedElement)
            {
                detectedProperties = new
                {
                    cloudProvider = detectedElement.TryGetProperty("cloudprovider", out var cpProp) 
                        ? cpProp.GetString() : "Unknown",
                    manufacturer = detectedElement.TryGetProperty("manufacturer", out var mfgProp) 
                        ? mfgProp.GetString() : "Unknown",
                    model = detectedElement.TryGetProperty("model", out var modelProp) 
                        ? modelProp.GetString() : "Unknown"
                };
            }

            // Get extensions for this machine
            var extensions = new List<object>();
            try
            {
                var extensionResources = allResources.Where(r =>
                    r.Type?.Equals("Microsoft.HybridCompute/machines/extensions", StringComparison.OrdinalIgnoreCase) == true &&
                    r.Id?.Contains($"/machines/{machineName}/", StringComparison.OrdinalIgnoreCase) == true).ToList();

                foreach (var ext in extensionResources)
                {
                    var extDetails = await _azureResourceService.GetResourceAsync(ext.Id!);
                    extensions.Add(new
                    {
                        name = ext.Name,
                        type = extDetails?.Properties?.GetValueOrDefault("type")?.ToString() ?? "Unknown",
                        publisher = extDetails?.Properties?.GetValueOrDefault("publisher")?.ToString() ?? "Unknown",
                        provisioningState = extDetails?.Properties?.GetValueOrDefault("provisioningState")?.ToString() ?? "Unknown",
                        autoUpgradeMinorVersion = extDetails?.Properties?.GetValueOrDefault("autoUpgradeMinorVersion")?.ToString() ?? "Unknown"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve extensions for Arc machine {MachineName}", machineName);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = $"🖥️ ARC MACHINE: {machineName.ToUpperInvariant()}",
                    icon = machineOsType.Equals("Windows", StringComparison.OrdinalIgnoreCase) ? "🪟" : "🐧",
                    status = status,
                    statusIcon = status switch
                    {
                        "Connected" => "🟢",
                        "Disconnected" => "🔴",
                        "Error" => "⚠️",
                        _ => "❓"
                    }
                },
                machine = new
                {
                    resourceId = arcMachine.Id,
                    name = machineName,
                    resourceGroup = arcMachine.ResourceGroup,
                    location = arcMachine.Location,
                    provisioningState = provisioningState
                },
                connectionStatus = new
                {
                    status = status,
                    lastStatusChange = lastStatusChange,
                    agentVersion = agentVersion,
                    vmId = vmId
                },
                operatingSystem = new
                {
                    type = machineOsType,
                    name = osName,
                    version = osVersion,
                    sku = osSku,
                    edition = osEdition
                },
                identity = new
                {
                    machineFqdn = machineFqdn,
                    domainName = domainName,
                    adFqdn = adFqdn,
                    dnsFqdn = dnsFqdn
                },
                hardware = hardwareProfile,
                network = networkProfile,
                agentConfiguration = agentConfiguration,
                detectedProperties = detectedProperties,
                extensions = new
                {
                    count = extensions.Count,
                    installed = extensions
                },
                sqlServerDiscovered = mssqlDiscovered.Equals("true", StringComparison.OrdinalIgnoreCase),
                tags = arcMachine.Tags,
                nextSteps = new[]
                {
                    status == "Disconnected" ? "⚠️ Machine is disconnected. Check network connectivity and agent status." : null,
                    extensions.Count == 0 ? "No extensions installed. Consider adding monitoring or security extensions." : null,
                    "Say 'scan Arc machine compliance for <machine-name>' to check configuration compliance.",
                    "Say 'get Arc connection health' for overall fleet health.",
                    "Say 'generate Arc extension deployment' to deploy extensions to this machine."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Arc machine details for {MachineName}", machineName);
            return CreateErrorResponse("get Arc machine details", ex);
        }
    }

    [KernelFunction("get_arc_extensions")]
    [Description("List all VM extensions installed across Azure Arc-connected machines. " +
                 "Shows extension types, versions, publishers, and provisioning status. " +
                 "Use to audit what agents/tools are deployed to your hybrid infrastructure. " +
                 "Example: 'What extensions are installed on my Arc servers?', 'Which Arc machines have Log Analytics agent?'")]
    public async Task<string> GetArcExtensionsAsync(
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group to filter by (optional)")] string? resourceGroup = null,
        [Description("Extension type to filter by (e.g., 'MicrosoftMonitoringAgent', 'AzureMonitorLinuxAgent', optional)")] string? extensionType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            _logger.LogInformation("Listing Azure Arc extensions in subscription {SubscriptionId}", subscriptionId);

            // Get all Arc machine extension resources
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            
            // Filter for Arc machines first
            var arcMachines = allResources.Where(r => 
                r.Type?.Equals("Microsoft.HybridCompute/machines", StringComparison.OrdinalIgnoreCase) == true).ToList();

            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                arcMachines = arcMachines.Where(r => 
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            var extensionsByMachine = new Dictionary<string, List<object>>();
            var extensionTypeSummary = new Dictionary<string, int>();
            var extensionStatusSummary = new Dictionary<string, int>();

            foreach (var machine in arcMachines)
            {
                var machineExtensions = new List<object>();

                // Get extensions for this machine
                var extResources = allResources.Where(r =>
                    r.Type?.Equals("Microsoft.HybridCompute/machines/extensions", StringComparison.OrdinalIgnoreCase) == true &&
                    r.Id?.Contains($"{machine.Id}/extensions/", StringComparison.OrdinalIgnoreCase) == true).ToList();

                foreach (var ext in extResources)
                {
                    try
                    {
                        var extDetails = await _azureResourceService.GetResourceAsync(ext.Id!);
                        var extType = extDetails?.Properties?.GetValueOrDefault("type")?.ToString() ?? "Unknown";
                        var publisher = extDetails?.Properties?.GetValueOrDefault("publisher")?.ToString() ?? "Unknown";
                        var provState = extDetails?.Properties?.GetValueOrDefault("provisioningState")?.ToString() ?? "Unknown";
                        var version = extDetails?.Properties?.GetValueOrDefault("typeHandlerVersion")?.ToString() ?? "Unknown";
                        var autoUpgrade = extDetails?.Properties?.GetValueOrDefault("enableAutomaticUpgrade")?.ToString() ?? "false";

                        // Apply extension type filter
                        if (!string.IsNullOrWhiteSpace(extensionType) &&
                            !extType.Contains(extensionType, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        machineExtensions.Add(new
                        {
                            name = ext.Name,
                            type = extType,
                            publisher = publisher,
                            version = version,
                            provisioningState = provState,
                            autoUpgradeEnabled = autoUpgrade.Equals("true", StringComparison.OrdinalIgnoreCase)
                        });

                        // Update summaries
                        extensionTypeSummary[extType] = extensionTypeSummary.GetValueOrDefault(extType) + 1;
                        extensionStatusSummary[provState] = extensionStatusSummary.GetValueOrDefault(provState) + 1;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get details for extension {ExtensionId}", ext.Id);
                    }
                }

                if (machineExtensions.Count > 0)
                {
                    extensionsByMachine[machine.Name!] = machineExtensions;
                }
            }

            var totalExtensions = extensionTypeSummary.Values.Sum();
            var machinesWithExtensions = extensionsByMachine.Count;
            var machinesWithoutExtensions = arcMachines.Count - machinesWithExtensions;

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "🔌 AZURE ARC EXTENSIONS",
                    icon = "📦",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    extensionTypeFilter = extensionType ?? "all types"
                },
                summary = new
                {
                    totalArcMachines = arcMachines.Count,
                    machinesWithExtensions = machinesWithExtensions,
                    machinesWithoutExtensions = machinesWithoutExtensions,
                    totalExtensionInstances = totalExtensions,
                    uniqueExtensionTypes = extensionTypeSummary.Count
                },
                breakdown = new
                {
                    byExtensionType = extensionTypeSummary.OrderByDescending(kv => kv.Value)
                        .Select(kv => new { type = kv.Key, count = kv.Value }),
                    byProvisioningState = extensionStatusSummary
                        .Select(kv => new { state = kv.Key, count = kv.Value })
                },
                extensionsByMachine = extensionsByMachine.Select(kv => new
                {
                    machineName = kv.Key,
                    extensionCount = kv.Value.Count,
                    extensions = kv.Value
                }),
                recommendations = new[]
                {
                    machinesWithoutExtensions > 0 ? $"⚠️ {machinesWithoutExtensions} Arc machine(s) have no extensions installed" : null,
                    !extensionTypeSummary.ContainsKey("AzureMonitorWindowsAgent") && !extensionTypeSummary.ContainsKey("AzureMonitorLinuxAgent") 
                        ? "Consider deploying Azure Monitor Agent for centralized monitoring" : null,
                    !extensionTypeSummary.ContainsKey("MDE.Windows") && !extensionTypeSummary.ContainsKey("MDE.Linux")
                        ? "Consider deploying Microsoft Defender for Endpoint for security" : null
                }.Where(s => s != null),
                nextSteps = new[]
                {
                    "Say 'generate Arc extension deployment for <extension-type>' to deploy extensions at scale.",
                    "Say 'get Arc machine details for <machine-name>' to see extensions on a specific machine.",
                    "Say 'list Arc machines' to see all connected hybrid machines."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Azure Arc extensions");
            return CreateErrorResponse("list Azure Arc extensions", ex);
        }
    }

    [KernelFunction("get_arc_connection_health")]
    [Description("Check the connection health and status of Azure Arc-connected machines across your subscription. " +
                 "Identifies disconnected machines, agent version distribution, machines approaching expiration, and overall fleet health. " +
                 "Use for monitoring Arc infrastructure health, identifying issues, and ensuring continuous management. " +
                 "Example: 'Check Arc connection health', 'Are any Arc servers disconnected?', 'Show me Arc agent version distribution'")]
    public async Task<string> GetArcConnectionHealthAsync(
        [Description("Azure subscription ID (GUID) or use previously set subscription")] string? subscriptionIdOrName = null,
        [Description("Resource group to filter by (optional)")] string? resourceGroup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptionId = await ResolveSubscriptionIdAsync(subscriptionIdOrName);
            _logger.LogInformation("Checking Azure Arc connection health in subscription {SubscriptionId}", subscriptionId);

            // Get all Arc machines
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var arcMachines = allResources.Where(r => 
                r.Type?.Equals("Microsoft.HybridCompute/machines", StringComparison.OrdinalIgnoreCase) == true).ToList();

            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                arcMachines = arcMachines.Where(r => 
                    r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            var statusCounts = new Dictionary<string, int>
            {
                ["Connected"] = 0,
                ["Disconnected"] = 0,
                ["Error"] = 0,
                ["Unknown"] = 0
            };
            var agentVersions = new Dictionary<string, int>();
            var disconnectedMachines = new List<object>();
            var errorMachines = new List<object>();
            var machinesNeedingAttention = new List<object>();

            foreach (var machine in arcMachines)
            {
                try
                {
                    var details = await _azureResourceService.GetResourceAsync(machine.Id!);
                    if (details?.Properties != null)
                    {
                        var status = GetPropertyValueFromDict<string>(details.Properties, "status", "Unknown");
                        var agentVersion = GetPropertyValueFromDict<string>(details.Properties, "agentVersion", "Unknown");
                        var lastStatusChange = GetPropertyValueFromDict<string>(details.Properties, "lastStatusChange", "");
                        var machineOsType = GetPropertyValueFromDict<string>(details.Properties, "osType", "Unknown");

                        // Update status counts
                        statusCounts[status] = statusCounts.GetValueOrDefault(status) + 1;

                        // Track agent versions
                        agentVersions[agentVersion] = agentVersions.GetValueOrDefault(agentVersion) + 1;

                        // Track disconnected machines
                        if (status.Equals("Disconnected", StringComparison.OrdinalIgnoreCase))
                        {
                            var disconnectedInfo = new
                            {
                                name = machine.Name,
                                resourceGroup = machine.ResourceGroup,
                                osType = machineOsType,
                                lastStatusChange = lastStatusChange,
                                agentVersion = agentVersion,
                                daysSinceLastContact = CalculateDaysSinceDate(lastStatusChange)
                            };
                            disconnectedMachines.Add(disconnectedInfo);

                            // Check if approaching expiration (45+ days disconnected)
                            var daysSinceContact = CalculateDaysSinceDate(lastStatusChange);
                            if (daysSinceContact >= 30)
                            {
                                machinesNeedingAttention.Add(new
                                {
                                    name = machine.Name,
                                    issue = daysSinceContact >= 45 ? "⚠️ Approaching expiration (will expire at 45 days)" : "⚡ Disconnected for extended period",
                                    daysSinceLastContact = daysSinceContact,
                                    action = "Reconnect or remove from Arc management"
                                });
                            }
                        }
                        else if (status.Equals("Error", StringComparison.OrdinalIgnoreCase))
                        {
                            errorMachines.Add(new
                            {
                                name = machine.Name,
                                resourceGroup = machine.ResourceGroup,
                                osType = machineOsType,
                                lastStatusChange = lastStatusChange,
                                agentVersion = agentVersion
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get health info for Arc machine {MachineId}", machine.Id);
                }
            }

            // Calculate overall health score
            var totalMachines = arcMachines.Count;
            var connectedCount = statusCounts.GetValueOrDefault("Connected");
            var healthScore = totalMachines > 0 ? (connectedCount * 100.0 / totalMachines) : 0;
            var healthStatus = healthScore >= 95 ? "Excellent" : 
                              healthScore >= 85 ? "Good" : 
                              healthScore >= 70 ? "Warning" : "Critical";

            // Find the latest and oldest agent versions
            var sortedVersions = agentVersions.Keys.OrderByDescending(v => v).ToList();
            var latestVersion = sortedVersions.FirstOrDefault() ?? "Unknown";
            var oldestVersion = sortedVersions.LastOrDefault() ?? "Unknown";
            var machinesOnOldAgent = agentVersions.Where(kv => kv.Key != latestVersion).Sum(kv => kv.Value);

            return JsonSerializer.Serialize(new
            {
                success = true,
                header = new
                {
                    title = "💚 AZURE ARC CONNECTION HEALTH",
                    icon = healthScore >= 85 ? "✅" : healthScore >= 70 ? "⚠️" : "🔴",
                    subscriptionId = subscriptionId,
                    resourceGroup = resourceGroup ?? "all resource groups",
                    timestamp = DateTimeOffset.UtcNow.ToString("u")
                },
                overallHealth = new
                {
                    score = $"{healthScore:F1}%",
                    status = healthStatus,
                    totalMachines = totalMachines,
                    connected = connectedCount,
                    disconnected = statusCounts.GetValueOrDefault("Disconnected"),
                    errors = statusCounts.GetValueOrDefault("Error")
                },
                agentVersionAnalysis = new
                {
                    uniqueVersions = agentVersions.Count,
                    latestVersion = latestVersion,
                    oldestVersion = oldestVersion,
                    machinesOnLatest = agentVersions.GetValueOrDefault(latestVersion),
                    machinesNeedingUpdate = machinesOnOldAgent,
                    versionDistribution = agentVersions.OrderByDescending(kv => kv.Key)
                        .Select(kv => new { version = kv.Key, count = kv.Value })
                },
                issues = new
                {
                    disconnectedMachines = new
                    {
                        count = disconnectedMachines.Count,
                        machines = disconnectedMachines.Take(10)
                    },
                    errorMachines = new
                    {
                        count = errorMachines.Count,
                        machines = errorMachines.Take(10)
                    },
                    machinesNeedingAttention = machinesNeedingAttention
                },
                recommendations = new[]
                {
                    disconnectedMachines.Count > 0 
                        ? $"🔴 {disconnectedMachines.Count} machine(s) are disconnected - investigate network connectivity and agent status" 
                        : null,
                    errorMachines.Count > 0 
                        ? $"⚠️ {errorMachines.Count} machine(s) have errors - review error details in Azure Portal" 
                        : null,
                    machinesOnOldAgent > 0 
                        ? $"📦 {machinesOnOldAgent} machine(s) running older agent versions - consider updating to {latestVersion}" 
                        : null,
                    machinesNeedingAttention.Count > 0 
                        ? $"⏰ {machinesNeedingAttention.Count} machine(s) disconnected for extended periods - may lose Arc management if not reconnected" 
                        : null,
                    healthScore >= 95 ? "✅ Arc fleet health is excellent!" : null
                }.Where(s => s != null),
                nextSteps = new[]
                {
                    disconnectedMachines.Count > 0 
                        ? "Say 'get Arc machine details for <machine-name>' to investigate disconnected machines." 
                        : null,
                    "Say 'list Arc machines with status Disconnected' to see all disconnected machines.",
                    "Say 'list Arc machines' for a complete inventory.",
                    machinesOnOldAgent > 0 
                        ? "Consider enabling automatic agent upgrades for your Arc machines." 
                        : null
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Azure Arc connection health");
            return CreateErrorResponse("check Azure Arc connection health", ex);
        }
    }

    /// <summary>
    /// Helper method to calculate days since a given date string
    /// </summary>
    private int CalculateDaysSinceDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return -1;

        if (DateTimeOffset.TryParse(dateString, out var date))
        {
            return (int)(DateTimeOffset.UtcNow - date).TotalDays;
        }

        return -1;
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
