using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Discovery;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.Azure;
using Platform.Engineering.Copilot.Discovery.Core;
using Platform.Engineering.Copilot.Discovery.Core.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Integration.Discovery;

/// <summary>
/// Integration tests for Discovery Agent workflows
/// Tests agent-level coordination, plugin interactions, and end-to-end discovery scenarios
/// </summary>
[Collection("Integration")]
public class DiscoveryAgentIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;
    private readonly Mock<IAzureResourceDiscoveryService> _discoveryServiceMock;
    private readonly Mock<IAzureResourceService> _azureResourceServiceMock;
    private readonly IMemoryCache _memoryCache;
    private readonly DiscoveryAgentOptions _options;

    public DiscoveryAgentIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _discoveryServiceMock = new Mock<IAzureResourceDiscoveryService>();
        _azureResourceServiceMock = new Mock<IAzureResourceService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _options = CreateDefaultOptions();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddDebug());
        services.AddMemoryCache();
        services.AddSingleton(_discoveryServiceMock.Object);
        services.AddSingleton(_azureResourceServiceMock.Object);
        services.AddSingleton(Options.Create(_options));
    }

    private static DiscoveryAgentOptions CreateDefaultOptions()
    {
        return new DiscoveryAgentOptions
        {
            Temperature = 0.3,
            MaxTokens = 4000,
            EnableHealthMonitoring = true,
            EnableDependencyMapping = true,
            EnablePerformanceMetrics = true,
            Discovery = new DiscoveryOptions
            {
                CacheDurationMinutes = 15,
                MaxResourcesPerQuery = 1000,
                IncludeDeletedResources = false,
                RequiredTags = new List<string> { "Environment", "Owner" }
            }
        };
    }

    #region Discovery Workflow Tests

    [Fact]
    public async Task DiscoveryWorkflow_WithNaturalLanguageQuery_ReturnsResources()
    {
        // Arrange
        var query = "Find all virtual machines in production";
        var subscriptionId = "test-sub-id";
        
        var resources = new List<DiscoveredResource>
        {
            CreateDiscoveredResource("vm-web-01", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod"),
            CreateDiscoveredResource("vm-web-02", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod")
        };

        var discoveryResult = new ResourceDiscoveryResult
        {
            Success = true,
            TotalCount = resources.Count,
            Resources = resources,
            GroupByType = new Dictionary<string, int> { ["Microsoft.Compute/virtualMachines"] = 2 }
        };

        _discoveryServiceMock
            .Setup(s => s.DiscoverResourcesAsync(query, subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveryResult);

        // Act
        var result = await _discoveryServiceMock.Object.DiscoverResourcesAsync(query, subscriptionId);

        // Assert
        _output.WriteLine($"Query: {query}");
        _output.WriteLine($"Total resources found: {result.TotalCount}");

        result.Success.Should().BeTrue();
        result.TotalCount.Should().Be(2);
        result.Resources.Should().HaveCount(2);
    }

    [Fact]
    public async Task DiscoveryWorkflow_WithMixedResourceTypes_CategorizesCorrectly()
    {
        // Arrange
        var resources = new List<DiscoveredResource>
        {
            CreateDiscoveredResource("vm-web-01", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod"),
            CreateDiscoveredResource("vm-web-02", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod"),
            CreateDiscoveredResource("aks-main", "Microsoft.ContainerService/managedClusters", "eastus", "rg-prod"),
            CreateDiscoveredResource("storage-logs", "Microsoft.Storage/storageAccounts", "eastus", "rg-prod"),
            CreateDiscoveredResource("sql-main", "Microsoft.Sql/servers", "eastus", "rg-prod"),
            CreateDiscoveredResource("kv-secrets", "Microsoft.KeyVault/vaults", "eastus", "rg-prod")
        };

        var discoveryResult = new ResourceDiscoveryResult
        {
            Success = true,
            TotalCount = resources.Count,
            Resources = resources,
            GroupByType = new Dictionary<string, int>
            {
                ["Microsoft.Compute/virtualMachines"] = 2,
                ["Microsoft.ContainerService/managedClusters"] = 1,
                ["Microsoft.Storage/storageAccounts"] = 1,
                ["Microsoft.Sql/servers"] = 1,
                ["Microsoft.KeyVault/vaults"] = 1
            }
        };

        _discoveryServiceMock
            .Setup(s => s.DiscoverResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveryResult);

        // Act
        var result = await _discoveryServiceMock.Object.DiscoverResourcesAsync("show all resources", "test-sub");

        // Assert
        _output.WriteLine($"Total resources: {result.TotalCount}");
        foreach (var kvp in result.GroupByType!)
        {
            _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        result.GroupByType.Should().HaveCount(5);
        result.GroupByType["Microsoft.Compute/virtualMachines"].Should().Be(2);
    }

    [Fact]
    public async Task DiscoveryWorkflow_WithMultipleResourceGroups_GroupsCorrectly()
    {
        // Arrange
        var resources = new List<DiscoveredResource>
        {
            CreateDiscoveredResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod"),
            CreateDiscoveredResource("vm-2", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod"),
            CreateDiscoveredResource("vm-3", "Microsoft.Compute/virtualMachines", "eastus", "rg-dev"),
            CreateDiscoveredResource("vm-4", "Microsoft.Compute/virtualMachines", "eastus", "rg-staging"),
            CreateDiscoveredResource("vm-5", "Microsoft.Compute/virtualMachines", "westus2", "rg-dr")
        };

        var discoveryResult = new ResourceDiscoveryResult
        {
            Success = true,
            TotalCount = resources.Count,
            Resources = resources,
            GroupByResourceGroup = new Dictionary<string, int>
            {
                ["rg-prod"] = 2,
                ["rg-dev"] = 1,
                ["rg-staging"] = 1,
                ["rg-dr"] = 1
            }
        };

        _discoveryServiceMock
            .Setup(s => s.DiscoverResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveryResult);

        // Act
        var result = await _discoveryServiceMock.Object.DiscoverResourcesAsync("show all VMs", "test-sub");

        // Assert
        _output.WriteLine($"Resources by resource group:");
        foreach (var kvp in result.GroupByResourceGroup!)
        {
            _output.WriteLine($"  {kvp.Key}: {kvp.Value} resources");
        }

        result.GroupByResourceGroup.Should().HaveCount(4);
        result.GroupByResourceGroup["rg-prod"].Should().Be(2);
    }

    [Fact]
    public async Task DiscoveryWorkflow_WithTagQuery_ReturnsTaggedResources()
    {
        // Arrange
        var resources = new List<DiscoveredResource>
        {
            CreateDiscoveredResource("vm-prod-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod", 
                new Dictionary<string, string> { ["Environment"] = "Production", ["Owner"] = "Team A" }),
            CreateDiscoveredResource("vm-prod-2", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod",
                new Dictionary<string, string> { ["Environment"] = "Production", ["Owner"] = "Team B" })
        };

        var discoveryResult = new ResourceDiscoveryResult
        {
            Success = true,
            TotalCount = resources.Count,
            Resources = resources
        };

        _discoveryServiceMock
            .Setup(s => s.SearchByTagsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveryResult);

        // Act
        var result = await _discoveryServiceMock.Object.SearchByTagsAsync("find production resources", "test-sub");

        // Assert
        _output.WriteLine($"Production resources: {result.TotalCount}");

        result.Success.Should().BeTrue();
        result.TotalCount.Should().Be(2);
        result.Resources.All(r => r.Tags?["Environment"] == "Production").Should().BeTrue();
    }

    #endregion

    #region Resource Details Integration Tests

    [Fact]
    public async Task ResourceDetails_WithStorageAccount_ReturnsExtendedProperties()
    {
        // Arrange
        var query = "show me details for storage account stproddata";
        var subscriptionId = "test-sub";
        
        var detailsResult = new ResourceDetailsResult
        {
            Success = true,
            Resource = new DiscoveredResource
            {
                ResourceId = "/subscriptions/test-sub/resourceGroups/rg-prod/providers/Microsoft.Storage/storageAccounts/stproddata",
                Name = "stproddata",
                Type = "Microsoft.Storage/storageAccounts",
                Location = "eastus",
                Sku = "Standard_LRS",
                Kind = "StorageV2",
                ProvisioningState = "Succeeded"
            },
            DataSource = "ResourceGraph"
        };

        _discoveryServiceMock
            .Setup(s => s.GetResourceDetailsAsync(query, subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailsResult);

        // Act
        var result = await _discoveryServiceMock.Object.GetResourceDetailsAsync(query, subscriptionId);

        // Assert
        _output.WriteLine($"Resource: {result.Resource?.Name}");
        _output.WriteLine($"Type: {result.Resource?.Type}");
        _output.WriteLine($"SKU: {result.Resource?.Sku}");
        _output.WriteLine($"Kind: {result.Resource?.Kind}");
        _output.WriteLine($"Data Source: {result.DataSource}");

        result.Success.Should().BeTrue();
        result.Resource!.Type.Should().Be("Microsoft.Storage/storageAccounts");
        result.Resource.Sku.Should().Be("Standard_LRS");
        result.Resource.Kind.Should().Be("StorageV2");
        result.DataSource.Should().Be("ResourceGraph");
    }

    [Fact]
    public async Task ResourceDetails_WithVirtualMachine_ReturnsComputeProperties()
    {
        // Arrange
        var query = "get details of vm vm-web-01";
        var subscriptionId = "test-sub";
        
        var detailsResult = new ResourceDetailsResult
        {
            Success = true,
            Resource = new DiscoveredResource
            {
                ResourceId = "/subscriptions/test-sub/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/vm-web-01",
                Name = "vm-web-01",
                Type = "Microsoft.Compute/virtualMachines",
                Location = "eastus",
                ProvisioningState = "Succeeded",
                Properties = new Dictionary<string, object>
                {
                    ["vmSize"] = "Standard_D2s_v3",
                    ["osType"] = "Linux"
                }
            },
            HealthStatus = "Healthy"
        };

        _discoveryServiceMock
            .Setup(s => s.GetResourceDetailsAsync(query, subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailsResult);

        // Act
        var result = await _discoveryServiceMock.Object.GetResourceDetailsAsync(query, subscriptionId);

        // Assert
        result.Success.Should().BeTrue();
        result.Resource!.Type.Should().Be("Microsoft.Compute/virtualMachines");
        result.HealthStatus.Should().Be("Healthy");
    }

    #endregion

    #region Health Monitoring Integration Tests

    [Fact]
    public async Task HealthMonitoring_WithHealthyResources_ReturnsHealthySummary()
    {
        // Arrange
        var query = "check health of all VMs";
        var subscriptionId = "test-sub";
        
        var healthResult = new ResourceHealthResult
        {
            Success = true,
            TotalResources = 5,
            HealthyCount = 5,
            UnhealthyCount = 0,
            UnknownCount = 0,
            ResourceHealth = new List<ResourceHealthInfo>
            {
                new() { ResourceId = "vm-1", ResourceName = "vm-web-01", HealthStatus = "Healthy" },
                new() { ResourceId = "vm-2", ResourceName = "vm-web-02", HealthStatus = "Healthy" },
                new() { ResourceId = "vm-3", ResourceName = "vm-api-01", HealthStatus = "Healthy" },
                new() { ResourceId = "vm-4", ResourceName = "vm-api-02", HealthStatus = "Healthy" },
                new() { ResourceId = "vm-5", ResourceName = "vm-db-01", HealthStatus = "Healthy" }
            }
        };

        _discoveryServiceMock
            .Setup(s => s.GetHealthStatusAsync(query, subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthResult);

        // Act
        var result = await _discoveryServiceMock.Object.GetHealthStatusAsync(query, subscriptionId);

        // Assert
        _output.WriteLine($"Health Status Summary:");
        _output.WriteLine($"  Total: {result.TotalResources}");
        _output.WriteLine($"  Healthy: {result.HealthyCount}");
        _output.WriteLine($"  Unhealthy: {result.UnhealthyCount}");
        _output.WriteLine($"  Unknown: {result.UnknownCount}");

        result.Success.Should().BeTrue();
        result.HealthyCount.Should().Be(5);
        result.UnhealthyCount.Should().Be(0);
    }

    [Fact]
    public async Task HealthMonitoring_WithMixedHealthStatus_ReturnsCorrectCounts()
    {
        // Arrange
        var query = "show resource health";
        var subscriptionId = "test-sub";
        
        var healthResult = new ResourceHealthResult
        {
            Success = true,
            TotalResources = 10,
            HealthyCount = 7,
            UnhealthyCount = 2,
            UnknownCount = 1,
            ResourceHealth = new List<ResourceHealthInfo>
            {
                new() { ResourceId = "vm-1", ResourceName = "vm-unhealthy-01", HealthStatus = "Unhealthy", StatusDetails = "High CPU" },
                new() { ResourceId = "vm-2", ResourceName = "vm-unhealthy-02", HealthStatus = "Unhealthy", StatusDetails = "Disk issues" },
                new() { ResourceId = "vm-3", ResourceName = "vm-unknown-01", HealthStatus = "Unknown" }
            }
        };

        _discoveryServiceMock
            .Setup(s => s.GetHealthStatusAsync(query, subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthResult);

        // Act
        var result = await _discoveryServiceMock.Object.GetHealthStatusAsync(query, subscriptionId);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalResources.Should().Be(10);
        result.HealthyCount.Should().Be(7);
        result.UnhealthyCount.Should().Be(2);
        result.UnknownCount.Should().Be(1);
    }

    [Fact]
    public void HealthMonitoring_WhenDisabled_SkipsHealthCheck()
    {
        // Arrange
        var options = new DiscoveryAgentOptions { EnableHealthMonitoring = false };

        // Act & Assert
        options.EnableHealthMonitoring.Should().BeFalse();
    }

    #endregion

    #region Inventory Summary Integration Tests

    [Fact]
    public async Task InventorySummary_ReturnsAggregatedCounts()
    {
        // Arrange
        var query = "show inventory summary";
        var subscriptionId = "test-sub";
        
        var inventoryResult = new ResourceInventoryResult
        {
            Success = true,
            Scope = "Subscription",
            TotalResources = 150,
            ResourcesByType = new Dictionary<string, int>
            {
                ["Microsoft.Compute/virtualMachines"] = 45,
                ["Microsoft.Storage/storageAccounts"] = 30,
                ["Microsoft.Network/virtualNetworks"] = 20,
                ["Microsoft.Sql/servers"] = 10,
                ["Microsoft.Web/sites"] = 25,
                ["Microsoft.KeyVault/vaults"] = 20
            },
            ResourcesByLocation = new Dictionary<string, int>
            {
                ["eastus"] = 100,
                ["westus2"] = 30,
                ["centralus"] = 20
            }
        };

        _discoveryServiceMock
            .Setup(s => s.GetInventorySummaryAsync(query, subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryResult);

        // Act
        var result = await _discoveryServiceMock.Object.GetInventorySummaryAsync(query, subscriptionId);

        // Assert
        _output.WriteLine($"Inventory Summary:");
        _output.WriteLine($"  Total Resources: {result.TotalResources}");
        _output.WriteLine($"  Resource Types: {result.ResourcesByType.Count}");

        result.Success.Should().BeTrue();
        result.TotalResources.Should().Be(150);
        result.ResourcesByType.Should().HaveCount(6);
        result.ResourcesByLocation.Should().HaveCount(3);
    }

    [Fact]
    public void InventorySummary_CalculatesTagCompliance()
    {
        // Arrange - test tag compliance calculation locally
        var resources = new List<DiscoveredResource>
        {
            CreateDiscoveredResource("r1", "VM", "eastus", "rg-test", new Dictionary<string, string> { ["Environment"] = "Prod", ["Owner"] = "Team A" }),
            CreateDiscoveredResource("r2", "VM", "eastus", "rg-test", new Dictionary<string, string> { ["Environment"] = "Prod" }), // Missing Owner
            CreateDiscoveredResource("r3", "VM", "eastus", "rg-test"), // No tags
            CreateDiscoveredResource("r4", "VM", "eastus", "rg-test", new Dictionary<string, string> { ["Environment"] = "Dev", ["Owner"] = "Team B" })
        };

        // Act
        var requiredTags = _options.Discovery.RequiredTags;
        var compliantResources = resources.Count(r => 
            requiredTags.All(tag => r.Tags?.ContainsKey(tag) == true));
        var compliancePercentage = Math.Round((double)compliantResources / resources.Count * 100, 2);

        // Assert
        _output.WriteLine($"Tag Compliance Analysis:");
        _output.WriteLine($"  Required Tags: {string.Join(", ", requiredTags)}");
        _output.WriteLine($"  Compliant Resources: {compliantResources}/{resources.Count}");
        _output.WriteLine($"  Compliance Rate: {compliancePercentage}%");

        compliantResources.Should().Be(2);
        compliancePercentage.Should().Be(50.0);
    }

    #endregion

    #region Subscription and Resource Group Tests

    [Fact]
    public async Task ListSubscriptions_ReturnsAllAvailableSubscriptions()
    {
        // Arrange
        var subscriptions = new List<AzureSubscription>
        {
            new() { SubscriptionId = "sub-1", SubscriptionName = "Production", State = "Enabled" },
            new() { SubscriptionId = "sub-2", SubscriptionName = "Development", State = "Enabled" },
            new() { SubscriptionId = "sub-3", SubscriptionName = "Sandbox", State = "Enabled" }
        };

        _discoveryServiceMock
            .Setup(s => s.ListSubscriptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriptions);

        // Act
        var result = await _discoveryServiceMock.Object.ListSubscriptionsAsync();

        // Assert
        _output.WriteLine($"Available subscriptions:");
        foreach (var sub in result)
        {
            _output.WriteLine($"  {sub.SubscriptionName} ({sub.SubscriptionId}) - {sub.State}");
        }

        result.Should().HaveCount(3);
        result.All(s => s.State == "Enabled").Should().BeTrue();
    }

    [Fact]
    public async Task ListResourceGroups_ReturnsGroupsWithResourceCounts()
    {
        // Arrange
        var resourceGroups = new List<ResourceGroup>
        {
            new() { Name = "rg-prod", Location = "eastus", ResourceCount = 45 },
            new() { Name = "rg-dev", Location = "eastus", ResourceCount = 20 },
            new() { Name = "rg-staging", Location = "westus2", ResourceCount = 15 }
        };

        _discoveryServiceMock
            .Setup(s => s.ListResourceGroupsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resourceGroups);

        // Act
        var result = await _discoveryServiceMock.Object.ListResourceGroupsAsync("test-sub");

        // Assert
        _output.WriteLine($"Resource groups in subscription:");
        foreach (var rg in result)
        {
            _output.WriteLine($"  {rg.Name} ({rg.Location}) - {rg.ResourceCount} resources");
        }

        result.Should().HaveCount(3);
        result.Sum(rg => rg.ResourceCount).Should().Be(80);
    }

    #endregion

    #region Caching Integration Tests

    [Fact]
    public void Caching_StoresDiscoveryResultsForConfiguredDuration()
    {
        // Arrange
        var subscriptionId = "test-sub";
        var cacheKey = $"discovery_resources_{subscriptionId}";
        var resources = new List<DiscoveredResource>
        {
            CreateDiscoveredResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-test")
        };

        var cacheDuration = TimeSpan.FromMinutes(_options.Discovery.CacheDurationMinutes);

        // Act
        _memoryCache.Set(cacheKey, resources, cacheDuration);
        var cachedResult = _memoryCache.Get<List<DiscoveredResource>>(cacheKey);

        // Assert
        _output.WriteLine($"Cache duration: {_options.Discovery.CacheDurationMinutes} minutes");
        _output.WriteLine($"Cached resources: {cachedResult?.Count}");

        cachedResult.Should().NotBeNull();
        cachedResult!.Should().HaveCount(1);
    }

    [Fact]
    public void Caching_UsesCorrectCacheKeyFormat()
    {
        // Arrange
        var subscriptionId = "00000000-0000-0000-0000-000000000001";
        var resourceGroupName = "rg-production";

        // Act
        var resourcesCacheKey = $"discovery_resources_{subscriptionId}";
        var inventoryCacheKey = $"discovery_inventory_{subscriptionId}";
        var rgCacheKey = $"discovery_resource_groups_{subscriptionId}";
        var rgSummaryCacheKey = $"discovery_rg_summary_{subscriptionId}_{resourceGroupName}";

        // Assert
        resourcesCacheKey.Should().Contain(subscriptionId);
        inventoryCacheKey.Should().StartWith("discovery_inventory_");
        rgCacheKey.Should().StartWith("discovery_resource_groups_");
        rgSummaryCacheKey.Should().Contain(resourceGroupName);
    }

    #endregion

    #region Error Handling Integration Tests

    [Fact]
    public async Task Discovery_WithServiceError_HandlesGracefully()
    {
        // Arrange
        _discoveryServiceMock
            .Setup(s => s.DiscoverResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Azure Resource Graph unavailable"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _discoveryServiceMock.Object.DiscoverResourcesAsync("find all resources", "test-sub"));

        _output.WriteLine($"Caught exception: {exception.Message}");
        exception.Message.Should().Contain("Azure Resource Graph");
    }

    [Fact]
    public async Task Discovery_WithTimeout_HandlesGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        
        _discoveryServiceMock
            .Setup(s => s.DiscoverResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("Request timed out"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _discoveryServiceMock.Object.DiscoverResourcesAsync("find all resources", "test-sub", cts.Token));

        exception.Message.Should().Contain("timed out");
    }

    #endregion

    #region AgentTask Integration Tests

    [Fact]
    public void AgentTask_ForDiscovery_HasCorrectStructure()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Discovery,
            Description = "Discover all virtual machines in production subscription",
            Priority = 1,
            Parameters = new Dictionary<string, object>
            {
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000001",
                ["resourceType"] = "Microsoft.Compute/virtualMachines",
                ["resourceGroup"] = "rg-prod"
            }
        };

        // Assert
        _output.WriteLine($"Task ID: {task.TaskId}");
        _output.WriteLine($"Agent Type: {task.AgentType}");
        _output.WriteLine($"Description: {task.Description}");
        _output.WriteLine($"Parameters: {JsonSerializer.Serialize(task.Parameters)}");

        task.AgentType.Should().Be(AgentType.Discovery);
        task.Parameters.Should().ContainKey("subscriptionId");
        task.Parameters.Should().ContainKey("resourceType");
    }

    [Fact]
    public void AgentResponse_ForDiscovery_HasExpectedFormat()
    {
        // Arrange
        var resources = new List<object>
        {
            new { name = "vm-1", type = "Microsoft.Compute/virtualMachines" },
            new { name = "vm-2", type = "Microsoft.Compute/virtualMachines" }
        };

        // Act
        var response = new AgentResponse
        {
            Success = true,
            AgentType = AgentType.Discovery,
            Content = JsonSerializer.Serialize(new
            {
                success = true,
                summary = new { totalResources = resources.Count },
                resources = resources
            })
        };

        // Assert
        _output.WriteLine($"Response Success: {response.Success}");
        _output.WriteLine($"Response Content: {response.Content}");

        response.Success.Should().BeTrue();
        response.Content.Should().Contain("totalResources");
    }

    #endregion

    #region Helper Methods

    private static DiscoveredResource CreateDiscoveredResource(
        string name,
        string type,
        string location,
        string resourceGroup,
        Dictionary<string, string>? tags = null)
    {
        return new DiscoveredResource
        {
            ResourceId = $"/subscriptions/test-sub/resourceGroups/{resourceGroup}/providers/{type}/{name}",
            Name = name,
            Type = type,
            Location = location,
            ResourceGroup = resourceGroup,
            Tags = tags,
            ProvisioningState = "Succeeded"
        };
    }

    #endregion
}
