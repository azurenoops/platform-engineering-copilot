using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for Azure resource discovery and querying
/// </summary>
public class ResourceDiscoveryPlugin : BaseSupervisorPlugin
{
    private readonly IMcpToolHandler _resourceToolHandler;

    public ResourceDiscoveryPlugin(
        IMcpToolHandler resourceToolHandler,
        ILogger<ResourceDiscoveryPlugin> logger,
        Kernel kernel) : base(logger, kernel)
    {
        _resourceToolHandler = resourceToolHandler ?? throw new ArgumentNullException(nameof(resourceToolHandler));
    }

    [KernelFunction("discover_azure_resources")]
    [Description("Discover and list Azure resources across subscriptions, resource groups, and regions. Search for resources by type, name, tags, or location. Use when user wants to: find resources, list resources, discover what exists, search for resources, or inventory Azure environment.")]
    public async Task<string> DiscoverAzureResourcesAsync(
        [Description("Azure subscription ID to search in. If not provided, searches across all accessible subscriptions.")] string? subscriptionId = null,
        [Description("Resource group name to limit search to. Optional - searches entire subscription if not specified.")] string? resourceGroup = null,
        [Description("Resource type to filter by (e.g., 'storage_account', 'key_vault', 'kubernetes'). Optional - returns all types if not specified.")] string? resourceType = null,
        [Description("Location/region to filter by (e.g., 'eastus', 'westus'). Optional - returns resources from all regions if not specified.")] string? location = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = "Discover Azure resources";
            if (!string.IsNullOrEmpty(subscriptionId))
                query += $" in subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(resourceGroup))
                query += $" in resource group {resourceGroup}";
            if (!string.IsNullOrEmpty(resourceType))
                query += $" of type {resourceType}";
            if (!string.IsNullOrEmpty(location))
                query += $" in {location}";

            var toolCall = new McpToolCall
            {
                Name = "resource_discovery",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["resource_group"] = resourceGroup,
                    ["resource_type"] = resourceType,
                    ["location"] = location
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _resourceToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("discover Azure resources", ex);
        }
    }

    [KernelFunction("get_resource_details")]
    [Description("Get detailed information about a specific Azure resource. Shows configuration, properties, status, tags, dependencies, and metadata. Use when user wants to: see resource details, check configuration, view resource properties, or inspect a specific resource.")]
    public async Task<string> GetResourceDetailsAsync(
        [Description("Azure subscription ID containing the resource")] string subscriptionId,
        [Description("Resource group name containing the resource")] string resourceGroup,
        [Description("Resource type (e.g., 'Microsoft.Storage/storageAccounts', 'Microsoft.KeyVault/vaults')")] string resourceType,
        [Description("Resource name to get details for")] string resourceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Get details for {resourceType} named {resourceName} in resource group {resourceGroup}";

            var toolCall = new McpToolCall
            {
                Name = "resource_discovery",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["resourceGroupName"] = resourceGroup,
                    ["resourceType"] = resourceType,
                    ["resourceName"] = resourceName
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _resourceToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("get resource details", ex);
        }
    }

    [KernelFunction("search_resources_by_tag")]
    [Description("Search for Azure resources using tags. Find resources tagged with specific keys or key-value pairs. Use when user wants to: find tagged resources, search by tag, filter by metadata, or discover resources with specific labels.")]
    public async Task<string> SearchResourcesByTagAsync(
        [Description("Tag key to search for (e.g., 'Environment', 'Owner', 'CostCenter')")] string tagKey,
        [Description("Tag value to match. Optional - finds all resources with the tag key if not specified.")] string? tagValue = null,
        [Description("Azure subscription ID to search in. Optional - searches all accessible subscriptions if not specified.")] string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Search for resources tagged with {tagKey}";
            if (!string.IsNullOrEmpty(tagValue))
                query += $" = {tagValue}";
            if (!string.IsNullOrEmpty(subscriptionId))
                query += $" in subscription {subscriptionId}";

            var toolCall = new McpToolCall
            {
                Name = "resource_discovery",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["tag_key"] = tagKey,
                    ["tag_value"] = tagValue
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _resourceToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("search resources by tag", ex);
        }
    }

    [KernelFunction("analyze_resource_dependencies")]
    [Description("Analyze dependencies between Azure resources. Shows which resources depend on each other, network connections, and relationships. Use when user wants to: understand dependencies, see connections, analyze architecture, or plan changes.")]
    public async Task<string> AnalyzeResourceDependenciesAsync(
        [Description("Azure subscription ID to analyze")] string subscriptionId,
        [Description("Resource group to analyze dependencies for. Optional - analyzes entire subscription if not specified.")] string? resourceGroup = null,
        [Description("Specific resource name to analyze dependencies for. Optional - analyzes all resources if not specified.")] string? resourceName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Analyze resource dependencies in subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(resourceGroup))
                query += $" in resource group {resourceGroup}";
            if (!string.IsNullOrEmpty(resourceName))
                query += $" for resource {resourceName}";

            var toolCall = new McpToolCall
            {
                Name = "resource_discovery",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["subscriptionId"] = subscriptionId,
                    ["resource_group"] = resourceGroup,
                    ["resource_name"] = resourceName
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _resourceToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("analyze resource dependencies", ex);
        }
    }
}
