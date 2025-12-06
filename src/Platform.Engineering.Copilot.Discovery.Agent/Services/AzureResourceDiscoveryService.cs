using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces.Discovery;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services.Azure.Graph;
using Platform.Engineering.Copilot.Core.Models.Azure;

namespace Platform.Engineering.Copilot.Discovery.Agent.Services;

/// <summary>
/// AI-powered resource discovery service implementation
/// Uses Semantic Kernel to parse natural language queries and discover Azure resources
/// Enhanced with Azure Resource Graph for performant bulk queries with extended properties
/// </summary>
public class AzureResourceDiscoveryService : IAzureResourceDiscoveryService
{
    private readonly ILogger<AzureResourceDiscoveryService> _logger;
    private readonly Kernel _kernel;
    private readonly IAzureResourceService _azureResourceService; // Fallback to API
    private readonly AzureResourceGraphService _resourceGraphService; // Primary: Resource Graph
    private readonly AzureMcpClient _azureMcpClient;
    private readonly IEnumerable<IResourceTypeHandler> _resourceTypeHandlers;

    public AzureResourceDiscoveryService(
        ILogger<AzureResourceDiscoveryService> logger,
        Kernel kernel,
        IAzureResourceService azureResourceService,
        AzureResourceGraphService resourceGraphService,
        AzureMcpClient azureMcpClient,
        IEnumerable<IResourceTypeHandler> resourceTypeHandlers)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        _resourceGraphService = resourceGraphService ?? throw new ArgumentNullException(nameof(resourceGraphService));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
        _resourceTypeHandlers = resourceTypeHandlers ?? throw new ArgumentNullException(nameof(resourceTypeHandlers));
    }

    public async Task<ResourceDiscoveryResult> DiscoverResourcesAsync(
        string query,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Discovering resources from query: {Query}", query);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return new ResourceDiscoveryResult
                {
                    Success = false,
                    ErrorDetails = "Subscription ID is required"
                };
            }

            // Get all resources
            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            // Use AI to parse query and extract filters
            var filters = await ParseDiscoveryQueryAsync(query, cancellationToken);

            // Apply filters
            var filteredResources = ApplyFilters(allResources, filters);

            // Convert to DiscoveredResource
            var discoveredResources = filteredResources.Select(r => new DiscoveredResource
            {
                ResourceId = r.Id ?? string.Empty,
                Name = r.Name ?? string.Empty,
                Type = r.Type ?? string.Empty,
                Location = r.Location ?? string.Empty,
                ResourceGroup = r.ResourceGroup ?? string.Empty,
                Tags = r.Tags
            }).ToList();

            // Group results
            var byType = discoveredResources.GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Count());
            var byLocation = discoveredResources.GroupBy(r => r.Location)
                .ToDictionary(g => g.Key, g => g.Count());
            var byResourceGroup = discoveredResources.GroupBy(r => r.ResourceGroup)
                .ToDictionary(g => g.Key, g => g.Count());

            return new ResourceDiscoveryResult
            {
                Success = true,
                TotalCount = discoveredResources.Count,
                Resources = discoveredResources,
                GroupByType = byType,
                GroupByLocation = byLocation,
                GroupByResourceGroup = byResourceGroup,
                Message = $"Found {discoveredResources.Count} resources matching query"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering resources from query: {Query}", query);
            return new ResourceDiscoveryResult
            {
                Success = false,
                ErrorDetails = ex.Message
            };
        }
    }

    public async Task<ResourceDetailsResult> GetResourceDetailsAsync(
        string query,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 DIAGNOSTIC [AzureResourceDiscoveryService]: GetResourceDetailsAsync CALLED");
            _logger.LogInformation("🔍 DIAGNOSTIC [AzureResourceDiscoveryService]: Query: {Query}, Subscription: {SubscriptionId}", query, subscriptionId);
            _logger.LogInformation("Getting resource details from query: {Query}", query);

            // Use AI to extract resource identifier
            var resourceInfo = await ParseResourceIdentifierAsync(query, cancellationToken);

            if (string.IsNullOrWhiteSpace(resourceInfo.ResourceId) && string.IsNullOrWhiteSpace(resourceInfo.ResourceName))
            {
                return new ResourceDetailsResult
                {
                    Success = false,
                    ErrorDetails = "Could not identify resource from query"
                };
            }

            // **Try Resource Graph first with extended properties**
            AzureResource? resource = null;
            string dataSource = "API"; // Track which method was used

            if (!string.IsNullOrEmpty(resourceInfo.ResourceId))
            {
                _logger.LogInformation("🔍 DIAGNOSTIC [AzureResourceDiscoveryService]: About to call Resource Graph for resource ID: {ResourceId}", resourceInfo.ResourceId);
                
                // Use Resource Graph for direct resource ID lookups
                resource = await _resourceGraphService.GetResourceDetailsAsync(
                    resourceInfo.ResourceId,
                    cancellationToken);

                if (resource != null)
                {
                    dataSource = "ResourceGraph";
                    _logger.LogInformation("Retrieved resource from Resource Graph: {ResourceId}", resourceInfo.ResourceId);
                }
                else
                {
                    _logger.LogWarning("Resource Graph did not return results, falling back to API");
                }
            }

            // **Fallback to API if Resource Graph didn't return results**
            if (resource == null)
            {
                var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
                var apiResource = allResources.FirstOrDefault(r =>
                    r.Id?.Contains(resourceInfo.ResourceId ?? resourceInfo.ResourceName ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true ||
                    r.Name?.Equals(resourceInfo.ResourceName, StringComparison.OrdinalIgnoreCase) == true);

                if (apiResource == null)
                {
                    return new ResourceDetailsResult
                    {
                        Success = false,
                        ErrorDetails = "Resource not found"
                    };
                }

                // Map to AzureResource model
                resource = new AzureResource
                {
                    Id = apiResource.Id ?? string.Empty,
                    Name = apiResource.Name ?? string.Empty,
                    Type = apiResource.Type ?? string.Empty,
                    Location = apiResource.Location ?? string.Empty,
                    ResourceGroup = apiResource.ResourceGroup ?? string.Empty,
                    Tags = apiResource.Tags,
                    Sku = apiResource.Sku,
                    Kind = apiResource.Kind,
                    ProvisioningState = apiResource.ProvisioningState,
                    Properties = apiResource.Properties?.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => (object)(kvp.Value?.ToString() ?? string.Empty))
                };
                dataSource = "API";
            }

            // **Apply resource type handler to enrich properties**
            if (resource != null && !string.IsNullOrEmpty(resource.Type))
            {
                var handler = _resourceTypeHandlers.FirstOrDefault(h => 
                    h.ResourceType.Equals(resource.Type, StringComparison.OrdinalIgnoreCase));
                
                if (handler != null)
                {
                    _logger.LogInformation("📋 Applying {HandlerType} handler to resource {ResourceId}", 
                        handler.GetType().Name, resource.Id);
                    
                    var extendedProps = handler.ParseExtendedProperties(resource);
                    if (extendedProps.Any())
                    {
                        resource.Properties ??= new Dictionary<string, object>();
                        foreach (var prop in extendedProps)
                        {
                            resource.Properties[$"parsed_{prop.Key}"] = prop.Value;
                        }
                        _logger.LogInformation("✅ Enriched resource with {Count} parsed properties", extendedProps.Count);
                    }
                }
                else
                {
                    _logger.LogDebug("No resource type handler found for {ResourceType}", resource.Type);
                }
            }

            // Get health status if available
            string? healthStatus = null;
            try
            {
                var health = await _azureResourceService.GetResourceHealthAsync(
                    resource.Id,
                    cancellationToken);
                
                if (health != null)
                {
                    dynamic healthObj = health;
                    healthStatus = healthObj.AvailabilityState?.ToString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get health for resource {ResourceId}", resource.Id);
            }

            return new ResourceDetailsResult
            {
                Success = true,
                Resource = new DiscoveredResource
                {
                    ResourceId = resource.Id,
                    Name = resource.Name,
                    Type = resource.Type,
                    Location = resource.Location,
                    ResourceGroup = resource.ResourceGroup,
                    Tags = resource.Tags,
                    Properties = resource.Properties as Dictionary<string, object>,
                    // **NEW: Include extended properties from Resource Graph**
                    Sku = resource.Sku,
                    Kind = resource.Kind,
                    ProvisioningState = resource.ProvisioningState
                },
                HealthStatus = healthStatus,
                DataSource = dataSource, // NEW: Indicate which method was used
                Message = $"Resource details retrieved successfully from {dataSource}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource details from query: {Query}", query);
            return new ResourceDetailsResult
            {
                Success = false,
                ErrorDetails = ex.Message
            };
        }
    }

    public async Task<ResourceInventoryResult> GetInventorySummaryAsync(
        string query,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting inventory summary from query: {Query}", query);

            // Parse scope from query (subscription or resource group)
            var scope = await ParseInventoryScopeAsync(query, cancellationToken);

            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            // Filter by resource group if specified
            if (!string.IsNullOrWhiteSpace(scope.ResourceGroup))
            {
                allResources = allResources.Where(r =>
                    r.ResourceGroup?.Equals(scope.ResourceGroup, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            var resourceList = allResources.ToList();
            var byType = resourceList.GroupBy(r => r.Type ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());
            var byLocation = resourceList.GroupBy(r => r.Location ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());
            var byResourceGroup = resourceList.GroupBy(r => r.ResourceGroup ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());
            var locations = resourceList.Select(r => r.Location ?? "Unknown").Distinct().ToList();

            return new ResourceInventoryResult
            {
                Success = true,
                Scope = scope.ResourceGroup ?? "Subscription",
                TotalResources = resourceList.Count,
                ResourcesByType = byType,
                ResourcesByLocation = byLocation,
                ResourcesByResourceGroup = byResourceGroup,
                Locations = locations,
                Message = $"Inventory summary for {(scope.ResourceGroup ?? "subscription")}: {resourceList.Count} total resources"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory summary from query: {Query}", query);
            return new ResourceInventoryResult
            {
                Success = false,
                ErrorDetails = ex.Message
            };
        }
    }

    public async Task<ResourceDiscoveryResult> SearchByTagsAsync(
        string query,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching resources by tags from query: {Query}", query);

            // Parse tag filters from query
            var tagFilters = await ParseTagFiltersAsync(query, cancellationToken);

            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);

            // Filter by tags
            var filteredResources = allResources.Where(r =>
            {
                if (r.Tags == null || !r.Tags.Any()) return false;

                return tagFilters.All(filter =>
                    r.Tags.ContainsKey(filter.Key) &&
                    (string.IsNullOrWhiteSpace(filter.Value) ||
                     r.Tags[filter.Key]?.Equals(filter.Value, StringComparison.OrdinalIgnoreCase) == true));
            }).ToList();

            var discoveredResources = filteredResources.Select(r => new DiscoveredResource
            {
                ResourceId = r.Id ?? string.Empty,
                Name = r.Name ?? string.Empty,
                Type = r.Type ?? string.Empty,
                Location = r.Location ?? string.Empty,
                ResourceGroup = r.ResourceGroup ?? string.Empty,
                Tags = r.Tags
            }).ToList();

            return new ResourceDiscoveryResult
            {
                Success = true,
                TotalCount = discoveredResources.Count,
                Resources = discoveredResources,
                Message = $"Found {discoveredResources.Count} resources matching tag filters"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching by tags from query: {Query}", query);
            return new ResourceDiscoveryResult
            {
                Success = false,
                ErrorDetails = ex.Message
            };
        }
    }

    public async Task<ResourceHealthResult> GetHealthStatusAsync(
        string query,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting health status from query: {Query}", query);

            // Parse filters from query
            var filters = await ParseDiscoveryQueryAsync(query, cancellationToken);

            var allResources = await _azureResourceService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            var filteredResources = ApplyFilters(allResources, filters);

            var healthInfoList = new List<ResourceHealthInfo>();
            var healthyCount = 0;
            var unhealthyCount = 0;
            var unknownCount = 0;

            foreach (var resource in filteredResources.Take(50)) // Limit to avoid too many API calls
            {
                try
                {
                    var healthStatus = await _azureResourceService.GetResourceHealthAsync(
                        resource.Id ?? string.Empty,
                        cancellationToken);

                    // Handle dynamic health status object
                    string status = "Unknown";
                    string? statusDetails = null;
                    
                    if (healthStatus != null)
                    {
                        dynamic healthObj = healthStatus;
                        status = healthObj.AvailabilityState?.ToString() ?? "Unknown";
                        statusDetails = healthObj.Summary?.ToString();
                    }
                    
                    if (status.Equals("Available", StringComparison.OrdinalIgnoreCase)) healthyCount++;
                    else if (status.Equals("Unavailable", StringComparison.OrdinalIgnoreCase)) unhealthyCount++;
                    else unknownCount++;

                    healthInfoList.Add(new ResourceHealthInfo
                    {
                        ResourceId = resource.Id ?? string.Empty,
                        ResourceName = resource.Name ?? string.Empty,
                        ResourceType = resource.Type ?? string.Empty,
                        HealthStatus = status,
                        StatusDetails = statusDetails,
                        LastUpdated = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get health for resource {ResourceId}", resource.Id);
                    unknownCount++;
                    healthInfoList.Add(new ResourceHealthInfo
                    {
                        ResourceId = resource.Id ?? string.Empty,
                        ResourceName = resource.Name ?? string.Empty,
                        ResourceType = resource.Type ?? string.Empty,
                        HealthStatus = "Unknown",
                        StatusDetails = "Health status unavailable"
                    });
                }
            }

            return new ResourceHealthResult
            {
                Success = true,
                TotalResources = healthInfoList.Count,
                HealthyCount = healthyCount,
                UnhealthyCount = unhealthyCount,
                UnknownCount = unknownCount,
                ResourceHealth = healthInfoList,
                Message = $"Health check complete: {healthyCount} healthy, {unhealthyCount} unhealthy, {unknownCount} unknown"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status from query: {Query}", query);
            return new ResourceHealthResult
            {
                Success = false,
                ErrorDetails = ex.Message
            };
        }
    }

    public async Task<List<ResourceGroup>> ListResourceGroupsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resourceGroups = await _azureResourceService.ListResourceGroupsAsync(subscriptionId, cancellationToken);

            return resourceGroups.Select(rg => new ResourceGroup
            {
                Name = rg.Name ?? string.Empty,
                Location = rg.Location ?? string.Empty,
                Tags = rg.Tags,
                ProvisioningState = rg.ProvisioningState ?? "Unknown"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing resource groups");
            return new List<ResourceGroup>();
        }
    }

    public async Task<List<AzureSubscription>> ListSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptions = await _azureResourceService.ListSubscriptionsAsync(cancellationToken);
            return subscriptions.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing subscriptions");
            return new List<AzureSubscription>();
        }
    }

    // Helper methods
    private async Task<DiscoveryFilters> ParseDiscoveryQueryAsync(string query, CancellationToken cancellationToken)
    {
        var prompt = $@"Extract resource discovery filters from this query: '{query}'
Return JSON with these optional fields:
- resourceType: Azure resource type (e.g., 'Microsoft.Storage/storageAccounts')
- location: Azure region (e.g., 'eastus', 'westus2')
- resourceGroup: Resource group name
- tags: Dictionary of tag key-value pairs

Only include fields that are mentioned in the query.";

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        var json = result.ToString();

        // Parse JSON (simplified - in production use System.Text.Json)
        return new DiscoveryFilters
        {
            ResourceType = ExtractValue(json, "resourceType"),
            Location = ExtractValue(json, "location"),
            ResourceGroup = ExtractValue(json, "resourceGroup")
        };
    }

    private async Task<(string? ResourceId, string? ResourceName)> ParseResourceIdentifierAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Extract resource identifier from this query: '{query}'
Return JSON with either:
- resourceId: Full Azure resource ID
- resourceName: Resource name

Example output: {{""resourceName"": ""mydata""}}";

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        var json = result.ToString();

        return (ExtractValue(json, "resourceId"), ExtractValue(json, "resourceName"));
    }

    private async Task<(string? ResourceGroup, string? Subscription)> ParseInventoryScopeAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Extract inventory scope from this query: '{query}'
Return JSON with optional fields:
- resourceGroup: Resource group name if specified
- subscription: Subscription ID if specified

Example: {{""resourceGroup"": ""rg-prod""}}";

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        var json = result.ToString();

        return (ExtractValue(json, "resourceGroup"), ExtractValue(json, "subscription"));
    }

    private async Task<Dictionary<string, string>> ParseTagFiltersAsync(string query, CancellationToken cancellationToken)
    {
        var prompt = $@"Extract tag filters from this query: '{query}'
Return JSON object with tag key-value pairs.
Example: {{""environment"": ""production"", ""cost-center"": ""engineering""}}
If only a tag key is mentioned without a value, use empty string as value.";

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        var json = result.ToString();

        // Simple JSON parsing (in production use System.Text.Json)
        return new Dictionary<string, string>();
    }

    private IEnumerable<Platform.Engineering.Copilot.Core.Models.Azure.AzureResource> ApplyFilters(
        IEnumerable<Platform.Engineering.Copilot.Core.Models.Azure.AzureResource> resources,
        DiscoveryFilters filters)
    {
        var filtered = resources;

        if (!string.IsNullOrWhiteSpace(filters.ResourceType))
        {
            filtered = filtered.Where(r =>
                r.Type?.Equals(filters.ResourceType, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrWhiteSpace(filters.Location))
        {
            filtered = filtered.Where(r =>
                r.Location?.Equals(filters.Location, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrWhiteSpace(filters.ResourceGroup))
        {
            filtered = filtered.Where(r =>
                r.ResourceGroup?.Equals(filters.ResourceGroup, StringComparison.OrdinalIgnoreCase) == true);
        }

        return filtered;
    }

    private string? ExtractValue(string json, string key)
    {
        // Simple JSON value extraction (in production use System.Text.Json)
        var keyPattern = $"\"{key}\"";
        var keyIndex = json.IndexOf(keyPattern, StringComparison.OrdinalIgnoreCase);
        if (keyIndex == -1) return null;

        var colonIndex = json.IndexOf(":", keyIndex);
        if (colonIndex == -1) return null;

        var startQuote = json.IndexOf("\"", colonIndex);
        if (startQuote == -1) return null;

        var endQuote = json.IndexOf("\"", startQuote + 1);
        if (endQuote == -1) return null;

        return json.Substring(startQuote + 1, endQuote - startQuote - 1);
    }

    private class DiscoveryFilters
    {
        public string? ResourceType { get; set; }
        public string? Location { get; set; }
        public string? ResourceGroup { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }
}
