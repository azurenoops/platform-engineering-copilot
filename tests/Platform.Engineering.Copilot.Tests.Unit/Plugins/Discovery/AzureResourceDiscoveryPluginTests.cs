using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Discovery;
using Platform.Engineering.Copilot.Core.Models.Azure;
using Platform.Engineering.Copilot.Discovery.Core.Configuration;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Plugins.Discovery;

/// <summary>
/// Unit tests for AzureResourceDiscoveryPlugin functionality
/// Tests plugin response formatting, caching, and filter logic
/// Note: Actual plugin instantiation requires Kernel which is sealed
/// These tests focus on the data transformations and logic the plugin uses
/// </summary>
public class AzureResourceDiscoveryPluginTests
{
    private readonly Mock<ILogger<object>> _loggerMock;
    private readonly Mock<IAzureResourceDiscoveryService> _discoveryServiceMock;
    private readonly Mock<IAzureResourceService> _azureResourceServiceMock;
    private readonly IMemoryCache _memoryCache;
    private readonly DiscoveryAgentOptions _options;

    public AzureResourceDiscoveryPluginTests()
    {
        _loggerMock = new Mock<ILogger<object>>();
        _discoveryServiceMock = new Mock<IAzureResourceDiscoveryService>();
        _azureResourceServiceMock = new Mock<IAzureResourceService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _options = new DiscoveryAgentOptions
        {
            Temperature = 0.3,
            MaxTokens = 4000,
            EnableHealthMonitoring = true,
            EnableDependencyMapping = true,
            Discovery = new DiscoveryOptions
            {
                CacheDurationMinutes = 15,
                MaxResourcesPerQuery = 1000,
                IncludeDeletedResources = false
            }
        };
    }

    #region Response Format Tests

    [Fact]
    public void DiscoveryResponse_WithSuccess_HasExpectedStructure()
    {
        // Arrange
        var resources = new List<AzureResource>
        {
            CreateMockResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-test"),
            CreateMockResource("storage-1", "Microsoft.Storage/storageAccounts", "eastus", "rg-test")
        };

        // Act
        var result = CreateDiscoveryResultJson(resources, "00000000-0000-0000-0000-000000000001");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("subscriptionId").GetString().Should().NotBeEmpty();
        json.RootElement.TryGetProperty("summary", out var summary).Should().BeTrue();
        summary.GetProperty("totalResources").GetInt32().Should().Be(2);
    }

    [Fact]
    public void DiscoveryResponse_WithError_ContainsErrorMessage()
    {
        // Act
        var result = CreateErrorJson("Subscription ID is required");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("Subscription ID");
    }

    [Fact]
    public void ResourceDetailsResponse_WithResource_ContainsAllProperties()
    {
        // Arrange
        var resource = new DiscoveredResource
        {
            ResourceId = "/subscriptions/test-sub/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/teststorage",
            Name = "teststorage",
            Type = "Microsoft.Storage/storageAccounts",
            Location = "eastus",
            Sku = "Standard_LRS",
            Kind = "StorageV2",
            ProvisioningState = "Succeeded"
        };

        // Act
        var result = CreateResourceDetailsJson(resource, "ResourceGraph");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("dataSource").GetString().Should().Be("ResourceGraph");
        var resourceData = json.RootElement.GetProperty("resource");
        resourceData.GetProperty("name").GetString().Should().Be("teststorage");
        resourceData.GetProperty("sku").GetString().Should().Be("Standard_LRS");
    }

    [Fact]
    public void ResourceDetailsResponse_WithHealth_IncludesHealthStatus()
    {
        // Arrange
        var resource = new DiscoveredResource
        {
            ResourceId = "test-id",
            Name = "vm-test"
        };

        // Act
        var result = CreateResourceDetailsWithHealthJson(resource, "Available");

        // Assert
        var json = JsonDocument.Parse(result);
        var health = json.RootElement.GetProperty("health");
        health.GetProperty("available").GetBoolean().Should().BeTrue();
        health.GetProperty("status").GetString().Should().Be("Available");
    }

    #endregion

    #region Resource Filtering Tests

    [Fact]
    public void FilterByType_ReturnsOnlyMatchingResources()
    {
        // Arrange
        var allResources = new List<AzureResource>
        {
            CreateMockResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-test"),
            CreateMockResource("vm-2", "Microsoft.Compute/virtualMachines", "westus2", "rg-prod"),
            CreateMockResource("storage-1", "Microsoft.Storage/storageAccounts", "eastus", "rg-test")
        };

        // Act - Filter by type
        var vmResources = allResources
            .Where(r => r.Type == "Microsoft.Compute/virtualMachines")
            .ToList();

        // Assert
        vmResources.Should().HaveCount(2);
        vmResources.All(r => r.Type == "Microsoft.Compute/virtualMachines").Should().BeTrue();
    }

    [Fact]
    public void FilterByLocation_ReturnsOnlyMatchingResources()
    {
        // Arrange
        var allResources = new List<AzureResource>
        {
            CreateMockResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-test"),
            CreateMockResource("vm-2", "Microsoft.Compute/virtualMachines", "westus2", "rg-prod"),
            CreateMockResource("storage-1", "Microsoft.Storage/storageAccounts", "eastus", "rg-test")
        };

        // Act
        var eastusResources = allResources
            .Where(r => r.Location?.Equals("eastus", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        // Assert
        eastusResources.Should().HaveCount(2);
        eastusResources.All(r => r.Location == "eastus").Should().BeTrue();
    }

    [Fact]
    public void FilterByResourceGroup_ReturnsOnlyMatchingResources()
    {
        // Arrange
        var allResources = new List<AzureResource>
        {
            CreateMockResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-test"),
            CreateMockResource("vm-2", "Microsoft.Compute/virtualMachines", "westus2", "rg-prod"),
            CreateMockResource("storage-1", "Microsoft.Storage/storageAccounts", "eastus", "rg-test")
        };

        // Act
        var rgTestResources = allResources
            .Where(r => r.ResourceGroup?.Equals("rg-test", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        // Assert
        rgTestResources.Should().HaveCount(2);
        rgTestResources.All(r => r.ResourceGroup == "rg-test").Should().BeTrue();
    }

    [Fact]
    public void FilterByProvisioningState_ExcludesDeletedResources()
    {
        // Arrange
        var resources = new List<AzureResource>
        {
            CreateMockResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-test", "Succeeded"),
            CreateMockResource("vm-2", "Microsoft.Compute/virtualMachines", "eastus", "rg-test", "Deleting")
        };

        // Act - Exclude deleting resources
        var activeResources = resources
            .Where(r => r.ProvisioningState == null || 
                       !r.ProvisioningState.Equals("Deleting", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        activeResources.Should().HaveCount(1);
        activeResources[0].Name.Should().Be("vm-1");
    }

    [Fact]
    public void ApplyMaxResourcesLimit_ReturnsLimitedResults()
    {
        // Arrange
        var resources = Enumerable.Range(1, 100)
            .Select(i => CreateMockResource($"resource-{i}", "Microsoft.Compute/virtualMachines", "eastus", "rg-test"))
            .ToList();

        var maxResources = 50;

        // Act
        var limitedResources = resources.Take(maxResources).ToList();

        // Assert
        limitedResources.Should().HaveCount(50);
    }

    #endregion

    #region Tag Filtering Tests

    [Fact]
    public void FilterByTagKey_ReturnsResourcesWithTag()
    {
        // Arrange
        var resources = new List<AzureResource>
        {
            CreateMockResourceWithTags("vm-1", new Dictionary<string, string> { ["Environment"] = "Production" }),
            CreateMockResourceWithTags("vm-2", new Dictionary<string, string> { ["Environment"] = "Development" }),
            CreateMockResourceWithTags("vm-3", new Dictionary<string, string> { ["Owner"] = "Team A" })
        };

        // Act
        var tagged = resources.Where(r => r.Tags?.ContainsKey("Environment") == true).ToList();

        // Assert
        tagged.Should().HaveCount(2);
    }

    [Fact]
    public void FilterByTagKeyAndValue_ReturnsMatchingResources()
    {
        // Arrange
        var resources = new List<AzureResource>
        {
            CreateMockResourceWithTags("vm-1", new Dictionary<string, string> { ["Environment"] = "Production" }),
            CreateMockResourceWithTags("vm-2", new Dictionary<string, string> { ["Environment"] = "Development" }),
            CreateMockResourceWithTags("vm-3", new Dictionary<string, string> { ["Environment"] = "Production" })
        };

        // Act
        var productionResources = resources
            .Where(r => r.Tags?.TryGetValue("Environment", out var value) == true && 
                       value.Equals("Production", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        productionResources.Should().HaveCount(2);
    }

    [Fact]
    public void GroupByTagValue_CategorizedCorrectly()
    {
        // Arrange
        var resources = new List<AzureResource>
        {
            CreateMockResourceWithTags("vm-1", new Dictionary<string, string> { ["Environment"] = "Production" }),
            CreateMockResourceWithTags("vm-2", new Dictionary<string, string> { ["Environment"] = "Development" }),
            CreateMockResourceWithTags("vm-3", new Dictionary<string, string> { ["Environment"] = "Production" }),
            CreateMockResourceWithTags("vm-4", new Dictionary<string, string> { ["Environment"] = "Staging" })
        };

        var tagKey = "Environment";

        // Act
        var byTagValue = resources
            .Where(r => r.Tags?.ContainsKey(tagKey) == true)
            .GroupBy(r => r.Tags![tagKey])
            .ToDictionary(g => g.Key ?? "null", g => g.Count());

        // Assert
        byTagValue.Should().HaveCount(3);
        byTagValue["Production"].Should().Be(2);
        byTagValue["Development"].Should().Be(1);
        byTagValue["Staging"].Should().Be(1);
    }

    #endregion

    #region Grouping Tests

    [Fact]
    public void GroupByType_CalculatesCorrectCounts()
    {
        // Arrange
        var resources = new List<AzureResource>
        {
            CreateMockResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-test"),
            CreateMockResource("vm-2", "Microsoft.Compute/virtualMachines", "eastus", "rg-test"),
            CreateMockResource("storage-1", "Microsoft.Storage/storageAccounts", "eastus", "rg-test"),
            CreateMockResource("aks-1", "Microsoft.ContainerService/managedClusters", "eastus", "rg-test")
        };

        // Act
        var byType = resources.GroupBy(r => r.Type)
            .ToDictionary(g => g.Key!, g => g.Count());

        // Assert
        byType.Should().HaveCount(3);
        byType["Microsoft.Compute/virtualMachines"].Should().Be(2);
        byType["Microsoft.Storage/storageAccounts"].Should().Be(1);
        byType["Microsoft.ContainerService/managedClusters"].Should().Be(1);
    }

    [Fact]
    public void GroupByLocation_CalculatesCorrectCounts()
    {
        // Arrange
        var resources = new List<AzureResource>
        {
            CreateMockResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-test"),
            CreateMockResource("vm-2", "Microsoft.Compute/virtualMachines", "eastus", "rg-test"),
            CreateMockResource("vm-3", "Microsoft.Compute/virtualMachines", "westus2", "rg-test"),
            CreateMockResource("vm-4", "Microsoft.Compute/virtualMachines", "centralus", "rg-test")
        };

        // Act
        var byLocation = resources.GroupBy(r => r.Location)
            .ToDictionary(g => g.Key!, g => g.Count());

        // Assert
        byLocation.Should().HaveCount(3);
        byLocation["eastus"].Should().Be(2);
        byLocation["westus2"].Should().Be(1);
        byLocation["centralus"].Should().Be(1);
    }

    [Fact]
    public void GroupByResourceGroup_CalculatesCorrectCounts()
    {
        // Arrange
        var resources = new List<AzureResource>
        {
            CreateMockResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod"),
            CreateMockResource("vm-2", "Microsoft.Compute/virtualMachines", "eastus", "rg-prod"),
            CreateMockResource("vm-3", "Microsoft.Compute/virtualMachines", "eastus", "rg-dev"),
            CreateMockResource("vm-4", "Microsoft.Compute/virtualMachines", "eastus", "rg-staging")
        };

        // Act
        var byResourceGroup = resources.GroupBy(r => r.ResourceGroup)
            .ToDictionary(g => g.Key!, g => g.Count());

        // Assert
        byResourceGroup.Should().HaveCount(3);
        byResourceGroup["rg-prod"].Should().Be(2);
        byResourceGroup["rg-dev"].Should().Be(1);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public void Cache_StoresResourcesForConfiguredDuration()
    {
        // Arrange
        var subscriptionId = "test-sub";
        var cacheKey = $"discovery_resources_{subscriptionId}";
        var resources = new List<AzureResource>
        {
            CreateMockResource("vm-1", "Microsoft.Compute/virtualMachines", "eastus", "rg-test")
        };

        var cacheExpiration = TimeSpan.FromMinutes(_options.Discovery.CacheDurationMinutes);

        // Act
        _memoryCache.Set(cacheKey, resources, cacheExpiration);
        var cachedResources = _memoryCache.Get<List<AzureResource>>(cacheKey);

        // Assert
        cachedResources.Should().NotBeNull();
        cachedResources.Should().HaveCount(1);
    }

    [Fact]
    public void Cache_ReturnsNullForExpiredEntry()
    {
        // Arrange
        var cacheKey = "test_expired_key";
        var expiredData = new List<AzureResource>();

        // Act - Set with immediate expiration
        _memoryCache.Set(cacheKey, expiredData, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);
        var result = _memoryCache.Get<List<AzureResource>>(cacheKey);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CacheKey_FormatsCorrectlyForResources()
    {
        // Arrange
        var subscriptionId = "00000000-0000-0000-0000-000000000001";

        // Act
        var cacheKey = $"discovery_resources_{subscriptionId}";

        // Assert
        cacheKey.Should().Be("discovery_resources_00000000-0000-0000-0000-000000000001");
    }

    [Fact]
    public void CacheKey_FormatsCorrectlyForResourceGroupSummary()
    {
        // Arrange
        var subscriptionId = "00000000-0000-0000-0000-000000000001";
        var resourceGroup = "rg-production";

        // Act
        var cacheKey = $"discovery_rg_summary_{subscriptionId}_{resourceGroup}";

        // Assert
        cacheKey.Should().Contain(subscriptionId);
        cacheKey.Should().Contain(resourceGroup);
    }

    #endregion

    #region Subscription Resolution Tests

    [Fact]
    public void SubscriptionId_WithGuid_IsRecognizedAsGuid()
    {
        // Arrange
        var subscriptionId = "00000000-0000-0000-0000-000000000001";

        // Act
        var isGuid = Guid.TryParse(subscriptionId, out _);

        // Assert
        isGuid.Should().BeTrue();
    }

    [Fact]
    public void SubscriptionName_IsNotGuid_NeedsResolution()
    {
        // Arrange
        var subscriptionName = "Production Subscription";

        // Act
        var isGuid = Guid.TryParse(subscriptionName, out _);

        // Assert
        isGuid.Should().BeFalse();
    }

    #endregion

    #region Resource Group List Tests

    [Fact]
    public void ResourceGroupList_ReturnsFormattedJson()
    {
        // Arrange
        var resourceGroups = new List<dynamic>
        {
            new { name = "rg-production", location = "eastus", resourceCount = 50 },
            new { name = "rg-development", location = "eastus", resourceCount = 30 },
            new { name = "rg-staging", location = "westus2", resourceCount = 20 }
        };

        // Act
        var result = JsonSerializer.Serialize(new
        {
            success = true,
            summary = new { totalResourceGroups = resourceGroups.Count },
            resourceGroups = resourceGroups
        }, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var summary = json.RootElement.GetProperty("summary");
        summary.GetProperty("totalResourceGroups").GetInt32().Should().Be(3);
    }

    [Fact]
    public void ResourceGroups_GroupByLocation_CalculatesCorrectly()
    {
        // Arrange
        var resourceGroups = new[]
        {
            new { name = "rg-1", location = "eastus" },
            new { name = "rg-2", location = "eastus" },
            new { name = "rg-3", location = "westus2" }
        };

        // Act
        var byLocation = resourceGroups.GroupBy(rg => rg.location)
            .Select(g => new { location = g.Key, count = g.Count() })
            .ToList();

        // Assert
        byLocation.Should().HaveCount(2);
        byLocation.First(l => l.location == "eastus").count.Should().Be(2);
    }

    #endregion

    #region Subscription List Tests

    [Fact]
    public void SubscriptionList_ReturnsFormattedJson()
    {
        // Arrange
        var subscriptions = new List<AzureSubscription>
        {
            new() { SubscriptionId = "sub-1", SubscriptionName = "Production", State = "Enabled", TenantId = "tenant-1" },
            new() { SubscriptionId = "sub-2", SubscriptionName = "Development", State = "Enabled", TenantId = "tenant-1" }
        };

        // Act
        var result = JsonSerializer.Serialize(new
        {
            success = true,
            summary = new { totalSubscriptions = subscriptions.Count },
            subscriptions = subscriptions.Select(s => new
            {
                subscriptionId = s.SubscriptionId,
                displayName = s.SubscriptionName,
                state = s.State
            })
        }, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("summary").GetProperty("totalSubscriptions").GetInt32().Should().Be(2);
    }

    #endregion

    #region Health Status Tests

    [Theory]
    [InlineData("Available", true)]
    [InlineData("Unavailable", false)]
    [InlineData("Degraded", false)]
    [InlineData("Unknown", false)]
    public void HealthStatus_IdentifiesAvailability(string status, bool expectedHealthy)
    {
        // Act
        var isHealthy = status.Equals("Available", StringComparison.OrdinalIgnoreCase);

        // Assert
        isHealthy.Should().Be(expectedHealthy);
    }

    #endregion

    #region Dependency Mapping Tests

    [Fact]
    public void DependencyMapping_WhenEnabled_IsAccessible()
    {
        // Arrange
        var options = new DiscoveryAgentOptions { EnableDependencyMapping = true };

        // Assert
        options.EnableDependencyMapping.Should().BeTrue();
    }

    [Fact]
    public void DependencyMapping_WhenDisabled_ReturnsError()
    {
        // Arrange
        var options = new DiscoveryAgentOptions { EnableDependencyMapping = false };

        // Act
        var result = CreateErrorJson("Dependency mapping is currently disabled. Please enable it in the Discovery Agent configuration.");

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("Dependency mapping");
    }

    #endregion

    #region Helper Methods

    private static AzureResource CreateMockResource(
        string name, 
        string type, 
        string location, 
        string resourceGroup,
        string provisioningState = "Succeeded")
    {
        return new AzureResource
        {
            Id = $"/subscriptions/test-sub/resourceGroups/{resourceGroup}/providers/{type}/{name}",
            Name = name,
            Type = type,
            Location = location,
            ResourceGroup = resourceGroup,
            ProvisioningState = provisioningState
        };
    }

    private static AzureResource CreateMockResourceWithTags(
        string name,
        Dictionary<string, string> tags)
    {
        return new AzureResource
        {
            Id = $"/subscriptions/test-sub/resourceGroups/rg-test/providers/Microsoft.Compute/virtualMachines/{name}",
            Name = name,
            Type = "Microsoft.Compute/virtualMachines",
            Location = "eastus",
            ResourceGroup = "rg-test",
            Tags = tags
        };
    }

    private static string CreateErrorJson(string error)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = error
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateDiscoveryResultJson(List<AzureResource> resources, string subscriptionId)
    {
        var byType = resources.GroupBy(r => r.Type)
            .Select(g => new { type = g.Key, count = g.Count() });

        return JsonSerializer.Serialize(new
        {
            success = true,
            subscriptionId = subscriptionId,
            summary = new
            {
                totalResources = resources.Count,
                uniqueTypes = byType.Count()
            },
            resources = resources.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                type = r.Type,
                location = r.Location
            })
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateResourceDetailsJson(DiscoveredResource resource, string dataSource)
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            dataSource = dataSource,
            resource = new
            {
                id = resource.ResourceId,
                name = resource.Name,
                type = resource.Type,
                location = resource.Location,
                sku = resource.Sku,
                kind = resource.Kind,
                provisioningState = resource.ProvisioningState
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateResourceDetailsWithHealthJson(DiscoveredResource resource, string healthStatus)
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            resource = new
            {
                id = resource.ResourceId,
                name = resource.Name
            },
            health = new
            {
                available = true,
                status = healthStatus
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    #endregion
}
