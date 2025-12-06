using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Azure.Management.ResourceGraph;
using Microsoft.Azure.Management.ResourceGraph.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.Azure.Graph;

/// <summary>
/// Service for executing Azure Resource Graph queries to retrieve resource information with extended properties.
/// Uses KQL (Kusto Query Language) for efficient bulk resource queries.
/// </summary>
public class AzureResourceGraphService
{
    private readonly ILogger<AzureResourceGraphService> _logger;
    private readonly IAzureClientFactory _clientFactory;
    private ResourceGraphClient? _resourceGraphClient;
    private readonly string _azureManagementScope;
    private const int MaxResultsPerPage = 1000; // Azure Resource Graph limit
    private readonly object _initLock = new object();

    public AzureResourceGraphService(
        ILogger<AzureResourceGraphService> logger,
        IAzureClientFactory clientFactory)
    {
        _logger = logger;
        _clientFactory = clientFactory;

        // Get management scope from factory
        _azureManagementScope = _clientFactory.GetManagementScope();
        
        _logger.LogInformation("Resource Graph Service configured for {CloudEnvironment} with scope {Scope} (lazy initialization)", 
            _clientFactory.CloudEnvironment, _azureManagementScope);

        // LAZY INITIALIZATION: Don't create client here
        // Will be created on first use to avoid blocking during service registration
    }

    /// <summary>
    /// Lazy initialization of Resource Graph client (only when first needed)
    /// </summary>
    private void EnsureClientInitialized()
    {
        if (_resourceGraphClient != null) return;

        lock (_initLock)
        {
            if (_resourceGraphClient != null) return;

            _logger.LogInformation("Initializing Resource Graph client for first use...");
            
            // Get credential from factory (centralized credential management)
            var credential = _clientFactory.GetCredential();
            
            // Create Resource Graph client using legacy SDK
            // Note: Azure.ResourceManager.ResourceGraph is not yet GA, using Microsoft.Azure.Management.ResourceGraph
            var tokenCredentials = new TokenCredentials(new AzureCredentialAdapter(credential, _azureManagementScope));
            _resourceGraphClient = new ResourceGraphClient(tokenCredentials);
            
            _logger.LogInformation("✅ Resource Graph client initialized successfully using {CloudEnvironment}", 
                _clientFactory.CloudEnvironment);
        }
    }

    /// <summary>
    /// Execute a KQL query against Azure Resource Graph
    /// </summary>
    public async Task<ResourceGraphQueryResult> ExecuteQueryAsync(
        string query,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        EnsureClientInitialized(); // Lazy initialization on first use
        
        try
        {
            _logger.LogInformation("Executing Resource Graph query: {Query}", query);

            var queryRequest = new QueryRequest
            {
                Subscriptions = new List<string> { subscriptionId },
                Query = query,
                Options = new QueryRequestOptions
                {
                    ResultFormat = ResultFormat.ObjectArray,
                    Top = MaxResultsPerPage
                }
            };

            var response = await _resourceGraphClient.ResourcesAsync(queryRequest, cancellationToken);

            var results = new List<Dictionary<string, object>>();
            
            if (response.Data is System.Collections.IEnumerable data)
            {
                foreach (var item in data)
                {
                    var json = JsonSerializer.Serialize(item);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null)
                    {
                        results.Add(dict);
                    }
                }
            }

            _logger.LogInformation("Resource Graph query returned {Count} results", results.Count);

            return new ResourceGraphQueryResult
            {
                Success = true,
                TotalRecords = response.TotalRecords,
                Results = results,
                SkipToken = response.SkipToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Resource Graph query: {Query}", query);
            return new ResourceGraphQueryResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get all resources with extended properties for a specific resource type
    /// </summary>
    public async Task<List<AzureResource>> GetResourcesWithExtendedPropertiesAsync(
        string subscriptionId,
        string? resourceType = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildExtendedPropertiesQuery(resourceType);
        var result = await ExecuteQueryAsync(query, subscriptionId, cancellationToken);

        if (!result.Success || result.Results == null)
        {
            _logger.LogWarning("Resource Graph query failed or returned no results");
            return new List<AzureResource>();
        }

        return result.Results.Select(MapToAzureResource).ToList();
    }

    /// <summary>
    /// Get details for a specific resource by resource ID
    /// </summary>
    public async Task<AzureResource?> GetResourceDetailsAsync(
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        // Extract subscription ID from resource ID
        var subscriptionId = ExtractSubscriptionId(resourceId);
        if (string.IsNullOrEmpty(subscriptionId))
        {
            _logger.LogError("Could not extract subscription ID from resource ID: {ResourceId}", resourceId);
            return null;
        }

        // Parse resource ID to get resource type
        var resourceType = ExtractResourceType(resourceId);
        
        var query = $@"
Resources
| where id =~ '{resourceId}'
{GetExtendedPropertiesProjection(resourceType)}
| take 1";

        var result = await ExecuteQueryAsync(query, subscriptionId, cancellationToken);

        if (!result.Success || result.Results == null || !result.Results.Any())
        {
            _logger.LogWarning("Resource not found or query failed for ID: {ResourceId}", resourceId);
            return null;
        }

        return MapToAzureResource(result.Results.First());
    }

    /// <summary>
    /// Build KQL query with extended properties based on resource type
    /// </summary>
    private string BuildExtendedPropertiesQuery(string? resourceType)
    {
        var baseQuery = "Resources";
        
        if (!string.IsNullOrEmpty(resourceType))
        {
            baseQuery += $" | where type =~ '{resourceType}'";
        }

        baseQuery += GetExtendedPropertiesProjection(resourceType);
        
        return baseQuery;
    }

    /// <summary>
    /// Get extended properties projection based on resource type
    /// </summary>
    private string GetExtendedPropertiesProjection(string? resourceType)
    {
        // Return common extended properties that work across all resource types
        // Use coalesce to try multiple possible locations for SKU
        var projection = @"
| extend sku = coalesce(sku.name, properties.sku.name, sku, tostring(properties.sku))
| extend kind = kind
| extend provisioningState = coalesce(properties.provisioningState, properties.status)
| extend extendedProperties = properties";

        // Add resource-type-specific properties
        if (!string.IsNullOrEmpty(resourceType))
        {
            var typeSpecificProps = ResourcePropertyMaps.GetExtendedPropertiesForType(resourceType);
            if (typeSpecificProps.Any())
            {
                foreach (var prop in typeSpecificProps)
                {
                    var propName = prop.Split('.').Last();
                    projection += $"\n| extend {propName} = {prop}";
                }
            }
        }

        projection += @"
| project id, name, type, location, resourceGroup, tags, sku, kind, provisioningState, extendedProperties";

        return projection;
    }

    /// <summary>
    /// Map Resource Graph result to AzureResource model
    /// </summary>
    private AzureResource MapToAzureResource(Dictionary<string, object> result)
    {
        var resource = new AzureResource
        {
            Id = GetStringValue(result, "id"),
            Name = GetStringValue(result, "name"),
            Type = GetStringValue(result, "type"),
            Location = GetStringValue(result, "location"),
            ResourceGroup = GetStringValue(result, "resourceGroup"),
            Sku = GetStringValue(result, "sku"),
            Kind = GetStringValue(result, "kind"),
            ProvisioningState = GetStringValue(result, "provisioningState"),
            Tags = GetDictionaryValue(result, "tags"),
            Properties = GetObjectDictionaryValue(result, "extendedProperties")
        };

        return resource;
    }

    private string GetStringValue(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value != null)
        {
            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
            {
                return jsonElement.GetString() ?? string.Empty;
            }
            return value.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    private Dictionary<string, string>? GetDictionaryValue(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private Dictionary<string, object>? GetObjectDictionaryValue(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private string ExtractSubscriptionId(string resourceId)
    {
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var subIndex = Array.IndexOf(parts, "subscriptions");
        if (subIndex >= 0 && subIndex + 1 < parts.Length)
        {
            return parts[subIndex + 1];
        }
        return string.Empty;
    }

    private string ExtractResourceType(string resourceId)
    {
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var providerIndex = Array.IndexOf(parts, "providers");
        if (providerIndex >= 0 && providerIndex + 2 < parts.Length)
        {
            return $"{parts[providerIndex + 1]}/{parts[providerIndex + 2]}";
        }
        return string.Empty;
    }

    /// <summary>
    /// Adapter to convert Azure.Identity TokenCredential to Microsoft.Rest.TokenCredentials
    /// </summary>
    private class AzureCredentialAdapter : Microsoft.Rest.ITokenProvider
    {
        private readonly TokenCredential _credential;
        private readonly string _scope;

        public AzureCredentialAdapter(TokenCredential credential, string scope)
        {
            _credential = credential;
            _scope = scope;
        }

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            try
            {
                var token = await _credential.GetTokenAsync(
                    new TokenRequestContext(new[] { _scope }),
                    cancellationToken);

                // Log token acquisition success (first 20 chars only for security)
                Console.WriteLine($"🔑 Token acquired for scope {_scope}: {token.Token.Substring(0, Math.Min(20, token.Token.Length))}... (ExpiresOn: {token.ExpiresOn})");

                return new AuthenticationHeaderValue("Bearer", token.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Token acquisition failed for scope {_scope}: {ex.Message}");
                throw;
            }
        }
    }
}

/// <summary>
/// Result of a Resource Graph query
/// </summary>
public class ResourceGraphQueryResult
{
    public bool Success { get; set; }
    public long TotalRecords { get; set; }
    public List<Dictionary<string, object>>? Results { get; set; }
    public string? SkipToken { get; set; }
    public string? ErrorMessage { get; set; }
}
