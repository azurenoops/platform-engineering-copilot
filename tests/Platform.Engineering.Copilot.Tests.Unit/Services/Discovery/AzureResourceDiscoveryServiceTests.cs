using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Discovery;

/// <summary>
/// Unit tests for AzureResourceDiscoveryService result models
/// Tests the data structures used for resource discovery results
/// Note: Constructor and service method tests require Kernel which is sealed 
/// and cannot be mocked - those are better suited for integration tests
/// </summary>
public class AzureResourceDiscoveryServiceTests
{
    private readonly Mock<ILogger<object>> _loggerMock;
    private readonly Mock<IAzureResourceService> _azureResourceServiceMock;
    private readonly Mock<IResourceTypeHandler> _resourceTypeHandlerMock;

    public AzureResourceDiscoveryServiceTests()
    {
        _loggerMock = new Mock<ILogger<object>>();
        _azureResourceServiceMock = new Mock<IAzureResourceService>();
        _resourceTypeHandlerMock = new Mock<IResourceTypeHandler>();
    }

    #region ResourceDiscoveryResult Tests

    [Fact]
    public void ResourceDiscoveryResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new ResourceDiscoveryResult();

        // Assert
        result.Success.Should().BeFalse();
        result.TotalCount.Should().Be(0);
        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public void ResourceDiscoveryResult_WithResources_HasCorrectCount()
    {
        // Arrange
        var resources = new List<DiscoveredResource>
        {
            new() { ResourceId = "1", Name = "vm-1", Type = "Microsoft.Compute/virtualMachines" },
            new() { ResourceId = "2", Name = "vm-2", Type = "Microsoft.Compute/virtualMachines" },
            new() { ResourceId = "3", Name = "storage-1", Type = "Microsoft.Storage/storageAccounts" }
        };

        // Act
        var result = new ResourceDiscoveryResult
        {
            Success = true,
            TotalCount = resources.Count,
            Resources = resources
        };

        // Assert
        result.Success.Should().BeTrue();
        result.TotalCount.Should().Be(3);
        result.Resources.Should().HaveCount(3);
    }

    [Fact]
    public void ResourceDiscoveryResult_GroupByType_CalculatesCorrectly()
    {
        // Arrange & Act
        var result = new ResourceDiscoveryResult
        {
            Success = true,
            GroupByType = new Dictionary<string, int>
            {
                ["Microsoft.Compute/virtualMachines"] = 5,
                ["Microsoft.Storage/storageAccounts"] = 3,
                ["Microsoft.KeyVault/vaults"] = 2
            }
        };

        // Assert
        result.GroupByType.Should().HaveCount(3);
        result.GroupByType!["Microsoft.Compute/virtualMachines"].Should().Be(5);
    }

    [Fact]
    public void ResourceDiscoveryResult_GroupByLocation_CalculatesCorrectly()
    {
        // Arrange & Act
        var result = new ResourceDiscoveryResult
        {
            Success = true,
            GroupByLocation = new Dictionary<string, int>
            {
                ["eastus"] = 10,
                ["westus2"] = 5,
                ["centralus"] = 3
            }
        };

        // Assert
        result.GroupByLocation.Should().HaveCount(3);
        result.GroupByLocation!["eastus"].Should().Be(10);
    }

    [Fact]
    public void ResourceDiscoveryResult_GroupByResourceGroup_CalculatesCorrectly()
    {
        // Arrange & Act
        var result = new ResourceDiscoveryResult
        {
            Success = true,
            GroupByResourceGroup = new Dictionary<string, int>
            {
                ["rg-prod"] = 20,
                ["rg-dev"] = 15,
                ["rg-staging"] = 10
            }
        };

        // Assert
        result.GroupByResourceGroup.Should().HaveCount(3);
        result.GroupByResourceGroup!["rg-prod"].Should().Be(20);
    }

    [Fact]
    public void ResourceDiscoveryResult_WithError_ContainsErrorDetails()
    {
        // Arrange & Act
        var result = new ResourceDiscoveryResult
        {
            Success = false,
            ErrorDetails = "Azure Resource Graph query failed"
        };

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorDetails.Should().Contain("Resource Graph");
    }

    #endregion

    #region ResourceDetailsResult Tests

    [Fact]
    public void ResourceDetailsResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new ResourceDetailsResult();

        // Assert
        result.Success.Should().BeFalse();
        result.Resource.Should().BeNull();
        result.Configuration.Should().BeNull();
    }

    [Fact]
    public void ResourceDetailsResult_WithResource_ContainsDetails()
    {
        // Arrange & Act
        var result = new ResourceDetailsResult
        {
            Success = true,
            Resource = new DiscoveredResource
            {
                ResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/storage1",
                Name = "storage1",
                Type = "Microsoft.Storage/storageAccounts",
                Location = "eastus",
                Sku = "Standard_LRS",
                Kind = "StorageV2",
                ProvisioningState = "Succeeded"
            },
            DataSource = "ResourceGraph"
        };

        // Assert
        result.Success.Should().BeTrue();
        result.Resource.Should().NotBeNull();
        result.Resource!.Name.Should().Be("storage1");
        result.Resource.Sku.Should().Be("Standard_LRS");
        result.DataSource.Should().Be("ResourceGraph");
    }

    [Fact]
    public void ResourceDetailsResult_WithConfiguration_ContainsProperties()
    {
        // Arrange & Act
        var result = new ResourceDetailsResult
        {
            Success = true,
            Resource = new DiscoveredResource { Name = "vm-1" },
            Configuration = new Dictionary<string, object>
            {
                ["vmSize"] = "Standard_D2s_v3",
                ["osType"] = "Linux",
                ["adminUsername"] = "azureuser"
            }
        };

        // Assert
        result.Configuration.Should().HaveCount(3);
        result.Configuration!["vmSize"].Should().Be("Standard_D2s_v3");
    }

    [Fact]
    public void ResourceDetailsResult_WithDependencies_ListsDependentResources()
    {
        // Arrange & Act
        var result = new ResourceDetailsResult
        {
            Success = true,
            Resource = new DiscoveredResource { Name = "vm-1" },
            Dependencies = new List<string>
            {
                "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic-1",
                "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/disks/disk-os",
                "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-1"
            }
        };

        // Assert
        result.Dependencies.Should().HaveCount(3);
        result.Dependencies!.Should().Contain(s => s.Contains("networkInterfaces"));
    }

    [Fact]
    public void ResourceDetailsResult_WithHealthStatus_IndicatesResourceHealth()
    {
        // Arrange & Act
        var result = new ResourceDetailsResult
        {
            Success = true,
            Resource = new DiscoveredResource { Name = "vm-1" },
            HealthStatus = "Healthy"
        };

        // Assert
        result.HealthStatus.Should().Be("Healthy");
    }

    #endregion

    #region ResourceInventoryResult Tests

    [Fact]
    public void ResourceInventoryResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new ResourceInventoryResult();

        // Assert
        result.Success.Should().BeFalse();
        result.TotalResources.Should().Be(0);
        result.ResourcesByType.Should().BeEmpty();
    }

    [Fact]
    public void ResourceInventoryResult_WithInventory_ContainsSummary()
    {
        // Arrange & Act
        var result = new ResourceInventoryResult
        {
            Success = true,
            Scope = "Subscription",
            TotalResources = 150,
            ResourcesByType = new Dictionary<string, int>
            {
                ["Microsoft.Compute/virtualMachines"] = 50,
                ["Microsoft.Storage/storageAccounts"] = 30,
                ["Microsoft.Network/virtualNetworks"] = 20
            },
            ResourcesByLocation = new Dictionary<string, int>
            {
                ["eastus"] = 100,
                ["westus2"] = 50
            }
        };

        // Assert
        result.Success.Should().BeTrue();
        result.Scope.Should().Be("Subscription");
        result.TotalResources.Should().Be(150);
        result.ResourcesByType.Should().HaveCount(3);
        result.ResourcesByLocation.Should().HaveCount(2);
    }

    [Fact]
    public void ResourceInventoryResult_ResourcesByResourceGroup_GroupsCorrectly()
    {
        // Arrange & Act
        var result = new ResourceInventoryResult
        {
            Success = true,
            ResourcesByResourceGroup = new Dictionary<string, int>
            {
                ["rg-production"] = 75,
                ["rg-development"] = 50,
                ["rg-staging"] = 25
            }
        };

        // Assert
        result.ResourcesByResourceGroup.Should().HaveCount(3);
        result.ResourcesByResourceGroup["rg-production"].Should().Be(75);
    }

    #endregion

    #region ResourceHealthResult Tests

    [Fact]
    public void ResourceHealthResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new ResourceHealthResult();

        // Assert
        result.Success.Should().BeFalse();
        result.TotalResources.Should().Be(0);
        result.HealthyCount.Should().Be(0);
        result.UnhealthyCount.Should().Be(0);
        result.UnknownCount.Should().Be(0);
        result.ResourceHealth.Should().BeEmpty();
    }

    [Fact]
    public void ResourceHealthResult_WithHealthyResources_ShowsCorrectCounts()
    {
        // Arrange & Act
        var result = new ResourceHealthResult
        {
            Success = true,
            TotalResources = 10,
            HealthyCount = 10,
            UnhealthyCount = 0,
            UnknownCount = 0
        };

        // Assert
        result.HealthyCount.Should().Be(10);
        result.UnhealthyCount.Should().Be(0);
    }

    [Fact]
    public void ResourceHealthResult_WithMixedHealth_ShowsCorrectCounts()
    {
        // Arrange & Act
        var result = new ResourceHealthResult
        {
            Success = true,
            TotalResources = 20,
            HealthyCount = 15,
            UnhealthyCount = 3,
            UnknownCount = 2
        };

        // Assert
        result.TotalResources.Should().Be(20);
        result.HealthyCount.Should().Be(15);
        result.UnhealthyCount.Should().Be(3);
        result.UnknownCount.Should().Be(2);
    }

    [Fact]
    public void ResourceHealthResult_WithResourceHealthList_ContainsDetails()
    {
        // Arrange & Act
        var result = new ResourceHealthResult
        {
            Success = true,
            TotalResources = 3,
            ResourceHealth = new List<ResourceHealthInfo>
            {
                new() { ResourceId = "vm-1", ResourceName = "web-server-01", HealthStatus = "Healthy" },
                new() { ResourceId = "vm-2", ResourceName = "web-server-02", HealthStatus = "Unhealthy", StatusDetails = "High CPU" },
                new() { ResourceId = "vm-3", ResourceName = "db-server-01", HealthStatus = "Unknown" }
            }
        };

        // Assert
        result.ResourceHealth.Should().HaveCount(3);
        result.ResourceHealth.Count(h => h.HealthStatus == "Healthy").Should().Be(1);
        result.ResourceHealth.Count(h => h.HealthStatus == "Unhealthy").Should().Be(1);
    }

    #endregion

    #region ResourceHealthInfo Tests

    [Fact]
    public void ResourceHealthInfo_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var info = new ResourceHealthInfo();

        // Assert
        info.ResourceId.Should().BeEmpty();
        info.ResourceName.Should().BeEmpty();
        info.ResourceType.Should().BeEmpty();
        info.HealthStatus.Should().BeEmpty();
    }

    [Fact]
    public void ResourceHealthInfo_WithValues_ContainsAllDetails()
    {
        // Arrange & Act
        var info = new ResourceHealthInfo
        {
            ResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm-1",
            ResourceName = "vm-web-01",
            ResourceType = "Microsoft.Compute/virtualMachines",
            HealthStatus = "Healthy",
            StatusDetails = "All health checks passed",
            LastUpdated = DateTime.UtcNow
        };

        // Assert
        info.ResourceName.Should().Be("vm-web-01");
        info.HealthStatus.Should().Be("Healthy");
        info.StatusDetails.Should().Contain("health checks");
        info.LastUpdated.Should().NotBeNull();
    }

    #endregion

    #region DiscoveredResource Tests

    [Fact]
    public void DiscoveredResource_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var resource = new DiscoveredResource();

        // Assert
        resource.ResourceId.Should().BeEmpty();
        resource.Name.Should().BeEmpty();
        resource.Type.Should().BeEmpty();
        resource.Location.Should().BeEmpty();
    }

    [Fact]
    public void DiscoveredResource_WithAllProperties_ContainsAllDetails()
    {
        // Arrange & Act
        var resource = new DiscoveredResource
        {
            ResourceId = "/subscriptions/test-sub/resourceGroups/rg-prod/providers/Microsoft.Storage/storageAccounts/storage1",
            Name = "storage1",
            Type = "Microsoft.Storage/storageAccounts",
            Location = "eastus",
            ResourceGroup = "rg-prod",
            Sku = "Standard_LRS",
            Kind = "StorageV2",
            ProvisioningState = "Succeeded",
            Tags = new Dictionary<string, string>
            {
                ["Environment"] = "Production",
                ["Owner"] = "Team A"
            }
        };

        // Assert
        resource.Name.Should().Be("storage1");
        resource.Type.Should().Be("Microsoft.Storage/storageAccounts");
        resource.Sku.Should().Be("Standard_LRS");
        resource.Tags.Should().HaveCount(2);
    }

    [Fact]
    public void DiscoveredResource_WithProperties_ContainsExtendedData()
    {
        // Arrange & Act
        var resource = new DiscoveredResource
        {
            Name = "vm-1",
            Type = "Microsoft.Compute/virtualMachines",
            Properties = new Dictionary<string, object>
            {
                ["vmSize"] = "Standard_D2s_v3",
                ["osType"] = "Linux",
                ["networkProfile"] = new { networkInterfaces = new[] { "nic-1" } }
            }
        };

        // Assert
        resource.Properties.Should().HaveCount(3);
        resource.Properties!["vmSize"].Should().Be("Standard_D2s_v3");
    }

    #endregion

    #region ResourceGroup Tests

    [Fact]
    public void ResourceGroup_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var rg = new ResourceGroup();

        // Assert - default values are empty strings
        rg.Name.Should().BeEmpty();
        rg.Location.Should().BeEmpty();
    }

    [Fact]
    public void ResourceGroup_WithValues_ContainsDetails()
    {
        // Arrange & Act
        var rg = new ResourceGroup
        {
            Name = "rg-production",
            Location = "eastus",
            ResourceCount = 50,
            Tags = new Dictionary<string, string>
            {
                ["Environment"] = "Production"
            }
        };

        // Assert
        rg.Name.Should().Be("rg-production");
        rg.Location.Should().Be("eastus");
        rg.ResourceCount.Should().Be(50);
        rg.Tags.Should().ContainKey("Environment");
    }

    #endregion

    #region AzureSubscription Tests

    [Fact]
    public void AzureSubscription_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var sub = new AzureSubscription();

        // Assert - default values are empty strings
        sub.SubscriptionId.Should().BeEmpty();
        sub.SubscriptionName.Should().BeEmpty();
    }

    [Fact]
    public void AzureSubscription_WithValues_ContainsDetails()
    {
        // Arrange & Act
        var sub = new AzureSubscription
        {
            SubscriptionId = "00000000-0000-0000-0000-000000000001",
            SubscriptionName = "Production Subscription",
            State = "Enabled",
            TenantId = "tenant-id"
        };

        // Assert
        sub.SubscriptionName.Should().Be("Production Subscription");
        sub.State.Should().Be("Enabled");
    }

    #endregion

    #region Filter Parsing Tests

    [Theory]
    [InlineData("Microsoft.Compute/virtualMachines", "Microsoft.Compute", "virtualMachines")]
    [InlineData("Microsoft.Storage/storageAccounts", "Microsoft.Storage", "storageAccounts")]
    [InlineData("Microsoft.KeyVault/vaults", "Microsoft.KeyVault", "vaults")]
    public void ResourceType_CanBeParsed_IntoProviderAndResource(string fullType, string expectedProvider, string expectedResource)
    {
        // Act
        var parts = fullType.Split('/');
        var provider = parts[0];
        var resource = parts[1];

        // Assert
        provider.Should().Be(expectedProvider);
        resource.Should().Be(expectedResource);
    }

    [Theory]
    [InlineData("/subscriptions/sub-id/resourceGroups/rg-name/providers/Microsoft.Compute/virtualMachines/vm-name", "sub-id")]
    [InlineData("/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-prod/providers/Microsoft.Storage/storageAccounts/storage1", "00000000-0000-0000-0000-000000000001")]
    public void ResourceId_CanExtract_SubscriptionId(string resourceId, string expectedSubscriptionId)
    {
        // Act
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var subIndex = Array.IndexOf(parts, "subscriptions");
        var subscriptionId = subIndex >= 0 && subIndex + 1 < parts.Length ? parts[subIndex + 1] : string.Empty;

        // Assert
        subscriptionId.Should().Be(expectedSubscriptionId);
    }

    [Theory]
    [InlineData("/subscriptions/sub-id/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/vm-name", "rg-prod")]
    [InlineData("/subscriptions/sub-id/resourceGroups/rg-development/providers/Microsoft.Storage/storageAccounts/storage1", "rg-development")]
    public void ResourceId_CanExtract_ResourceGroupName(string resourceId, string expectedResourceGroup)
    {
        // Act
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rgIndex = Array.IndexOf(parts, "resourceGroups");
        var resourceGroup = rgIndex >= 0 && rgIndex + 1 < parts.Length ? parts[rgIndex + 1] : string.Empty;

        // Assert
        resourceGroup.Should().Be(expectedResourceGroup);
    }

    #endregion
}
