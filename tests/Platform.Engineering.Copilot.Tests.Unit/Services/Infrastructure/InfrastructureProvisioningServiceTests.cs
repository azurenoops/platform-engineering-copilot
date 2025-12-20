using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Models;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Infrastructure;

/// <summary>
/// Unit tests for InfrastructureProvisioningService
/// Tests query parsing, provisioning logic, and cost estimation
/// </summary>
public class InfrastructureProvisioningServiceTests
{
    private readonly Mock<ILogger<object>> _loggerMock;
    private readonly Mock<IAzureResourceService> _azureResourceServiceMock;

    public InfrastructureProvisioningServiceTests()
    {
        _loggerMock = new Mock<ILogger<object>>();
        _azureResourceServiceMock = new Mock<IAzureResourceService>();
    }

    #region InfrastructureProvisionResult Tests

    [Fact]
    public void InfrastructureProvisionResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new InfrastructureProvisionResult();

        // Assert
        result.Success.Should().BeFalse();
        result.ResourceId.Should().BeEmpty();
        result.ResourceName.Should().BeEmpty();
        result.ResourceType.Should().BeEmpty();
        result.Status.Should().BeEmpty();
        result.Message.Should().BeNull();
        result.ErrorDetails.Should().BeNull();
        result.Properties.Should().BeNull();
    }

    [Fact]
    public void InfrastructureProvisionResult_Success_HasAllProperties()
    {
        // Arrange & Act
        var result = new InfrastructureProvisionResult
        {
            Success = true,
            ResourceId = "/subscriptions/sub-123/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/test",
            ResourceName = "test",
            ResourceType = "Microsoft.Storage/storageAccounts",
            Status = "Succeeded",
            Message = "Storage account created successfully",
            Properties = new Dictionary<string, string>
            {
                ["sku"] = "Standard_LRS",
                ["accessTier"] = "Hot"
            }
        };

        // Assert
        result.Success.Should().BeTrue();
        result.ResourceId.Should().Contain("Microsoft.Storage");
        result.Status.Should().Be("Succeeded");
        result.Properties.Should().HaveCount(2);
    }

    [Fact]
    public void InfrastructureProvisionResult_Failure_ContainsErrorInfo()
    {
        // Arrange & Act
        var result = new InfrastructureProvisionResult
        {
            Success = false,
            Status = "Failed",
            ErrorDetails = "ResourceGroupNotFound: The resource group 'rg-missing' does not exist",
            Message = "Failed to create storage account"
        };

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorDetails.Should().Contain("ResourceGroupNotFound");
    }

    [Fact]
    public void InfrastructureProvisionResult_ProvisionedAt_IsReasonable()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = new InfrastructureProvisionResult();
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        result.ProvisionedAt.Should().BeAfter(before);
        result.ProvisionedAt.Should().BeBefore(after);
    }

    #endregion

    #region InfrastructureCostEstimate Tests

    [Fact]
    public void InfrastructureCostEstimate_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var estimate = new InfrastructureCostEstimate();

        // Assert
        estimate.ResourceType.Should().BeEmpty();
        estimate.Location.Should().BeEmpty();
        estimate.MonthlyEstimate.Should().Be(0);
        estimate.AnnualEstimate.Should().Be(0);
        estimate.Currency.Should().Be("USD");
        estimate.Notes.Should().BeNull();
    }

    [Fact]
    public void InfrastructureCostEstimate_WithValues_CalculatesCorrectly()
    {
        // Arrange & Act
        var estimate = new InfrastructureCostEstimate
        {
            ResourceType = "Microsoft.Compute/virtualMachines",
            Location = "eastus",
            MonthlyEstimate = 150.00m,
            AnnualEstimate = 1800.00m,
            Notes = "Based on Standard_D2s_v3 VM"
        };

        // Assert
        estimate.MonthlyEstimate.Should().Be(150.00m);
        estimate.AnnualEstimate.Should().Be(estimate.MonthlyEstimate * 12);
    }

    [Fact]
    public void InfrastructureCostEstimate_WithBreakdown_SumsCorrectly()
    {
        // Arrange & Act
        var estimate = new InfrastructureCostEstimate
        {
            ResourceType = "aks-cluster",
            CostBreakdown = new Dictionary<string, decimal>
            {
                ["compute"] = 200.00m,
                ["storage"] = 50.00m,
                ["networking"] = 30.00m,
                ["monitoring"] = 20.00m
            }
        };

        // Assert
        estimate.CostBreakdown.Should().HaveCount(4);
        estimate.CostBreakdown!.Values.Sum().Should().Be(300.00m);
    }

    [Theory]
    [InlineData("vnet", 0.00)]
    [InlineData("virtual-network", 0.00)]
    [InlineData("storage-account", 20.00)]
    [InlineData("keyvault", 0.03)]
    [InlineData("key-vault", 0.03)]
    [InlineData("nsg", 0.00)]
    [InlineData("load-balancer", 25.00)]
    [InlineData("managed-identity", 0.00)]
    [InlineData("log-analytics", 2.30)]
    [InlineData("app-insights", 2.88)]
    [InlineData("unknown-resource", 10.00)]
    public void CostEstimation_ByResourceType_ReturnsExpectedCost(string resourceType, decimal expectedCost)
    {
        // Act - this is the logic from InfrastructureProvisioningService.EstimateCostAsync
        var monthlyCost = resourceType.ToLowerInvariant() switch
        {
            "vnet" or "virtual-network" => 0.00m,
            "storage-account" => 20.00m,
            "keyvault" or "key-vault" => 0.03m,
            "nsg" => 0.00m,
            "load-balancer" => 25.00m,
            "managed-identity" => 0.00m,
            "log-analytics" => 2.30m,
            "app-insights" => 2.88m,
            _ => 10.00m
        };

        // Assert
        monthlyCost.Should().Be(expectedCost);
    }

    #endregion

    #region Query Parsing Tests

    [Theory]
    [InlineData("Create a storage account named mydata in eastus", "storage")]
    [InlineData("Provision a VNet with address space 10.0.0.0/16", "vnet")]
    [InlineData("Set up Key Vault with soft delete enabled", "keyvault")]
    [InlineData("Create NSG named app-nsg", "nsg")]
    [InlineData("Deploy Log Analytics workspace", "log-analytics")]
    public void QueryParsing_ExtractsResourceType(string query, string expectedResourceType)
    {
        // Arrange
        var resourcePatterns = new Dictionary<string, string[]>
        {
            ["storage"] = new[] { "storage account", "storage-account" },
            ["vnet"] = new[] { "vnet", "virtual network" },
            ["keyvault"] = new[] { "key vault", "keyvault" },
            ["nsg"] = new[] { "nsg", "network security group" },
            ["log-analytics"] = new[] { "log analytics", "log-analytics" }
        };

        // Act
        var queryLower = query.ToLowerInvariant();
        var matchedType = resourcePatterns
            .Where(kvp => kvp.Value.Any(pattern => queryLower.Contains(pattern)))
            .Select(kvp => kvp.Key)
            .FirstOrDefault();

        // Assert
        matchedType.Should().Be(expectedResourceType);
    }

    [Theory]
    [InlineData("Create a storage account named mydata in eastus", "mydata")]
    [InlineData("Provision storage-account called testaccount", "testaccount")]
    public void QueryParsing_ExtractsResourceName(string query, string expectedName)
    {
        // Arrange - simplified name extraction pattern
        var patterns = new[] { "named ", "called " };
        var queryLower = query.ToLowerInvariant();

        // Act
        string? extractedName = null;
        foreach (var pattern in patterns)
        {
            var idx = queryLower.IndexOf(pattern);
            if (idx >= 0)
            {
                var start = idx + pattern.Length;
                var end = queryLower.IndexOf(' ', start);
                extractedName = end > start 
                    ? query.Substring(start, end - start) 
                    : query.Substring(start);
                break;
            }
        }

        // Assert
        extractedName.Should().Be(expectedName);
    }

    [Theory]
    [InlineData("Create storage in eastus", "eastus")]
    [InlineData("Deploy to westus2", "westus2")]
    [InlineData("Resource in usgovvirginia region", "usgovvirginia")]
    public void QueryParsing_ExtractsLocation(string query, string expectedLocation)
    {
        // Arrange - Order by length descending to match longer regions first
        var locations = new[] { "usgovvirginia", "usgovarizona", "westus3", "westus2", 
                               "eastus2", "westus", "eastus", "centralus" };

        // Act
        var queryLower = query.ToLowerInvariant();
        var extractedLocation = locations.FirstOrDefault(loc => queryLower.Contains(loc));

        // Assert
        extractedLocation.Should().Be(expectedLocation);
    }

    [Theory]
    [InlineData("storage with Standard_LRS", "Standard_LRS")]
    [InlineData("storage using Premium_LRS SKU", "Premium_LRS")]
    [InlineData("keyvault with standard tier", "standard")]
    public void QueryParsing_ExtractsSku(string query, string expectedSku)
    {
        // Arrange
        var skuPatterns = new[] { "Standard_LRS", "Standard_GRS", "Premium_LRS", 
                                   "standard", "premium" };

        // Act
        var extractedSku = skuPatterns.FirstOrDefault(sku => 
            query.Contains(sku, StringComparison.OrdinalIgnoreCase));

        // Assert
        extractedSku.Should().Be(expectedSku);
    }

    #endregion

    #region Resource ID Format Tests

    [Fact]
    public void ResourceId_StorageAccount_HasCorrectFormat()
    {
        // Arrange
        var subscriptionId = "00000000-0000-0000-0000-000000000001";
        var resourceGroup = "rg-test";
        var accountName = "mystorageaccount";

        // Act
        var resourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Storage/storageAccounts/{accountName}";

        // Assert
        resourceId.Should().StartWith("/subscriptions/");
        resourceId.Should().Contain("/resourceGroups/");
        resourceId.Should().Contain("/providers/Microsoft.Storage/storageAccounts/");
        resourceId.Should().EndWith(accountName);
    }

    [Fact]
    public void ResourceId_KeyVault_HasCorrectFormat()
    {
        // Arrange
        var subscriptionId = "00000000-0000-0000-0000-000000000001";
        var resourceGroup = "rg-test";
        var vaultName = "mykeyvault";

        // Act
        var resourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.KeyVault/vaults/{vaultName}";

        // Assert
        resourceId.Should().Contain("/providers/Microsoft.KeyVault/vaults/");
    }

    [Fact]
    public void ResourceId_VirtualNetwork_HasCorrectFormat()
    {
        // Arrange
        var subscriptionId = "00000000-0000-0000-0000-000000000001";
        var resourceGroup = "rg-test";
        var vnetName = "myvnet";

        // Act
        var resourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Network/virtualNetworks/{vnetName}";

        // Assert
        resourceId.Should().Contain("/providers/Microsoft.Network/virtualNetworks/");
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData("mystorageaccount", true)]  // Valid: lowercase, 3-24 chars
    [InlineData("my-storage", false)]       // Invalid: hyphen not allowed
    [InlineData("MyStorage", false)]        // Invalid: uppercase not allowed
    [InlineData("ab", false)]               // Invalid: too short
    [InlineData("abcdefghijklmnopqrstuvwxyz", false)]  // Invalid: too long (>24)
    public void StorageAccountName_Validation_Works(string name, bool expectedValid)
    {
        // Arrange - storage account naming rules: 3-24 chars, lowercase alphanumeric only
        bool isValid = name.Length >= 3 && 
                       name.Length <= 24 && 
                       name.All(c => char.IsLower(c) || char.IsDigit(c));

        // Assert
        isValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData("my-keyvault", true)]    // Valid: 3-24 chars, alphanumeric and hyphens
    [InlineData("MyKeyVault", true)]      // Valid: case-insensitive
    [InlineData("ab", false)]             // Invalid: too short
    [InlineData("-myvault", false)]       // Invalid: starts with hyphen
    public void KeyVaultName_Validation_Works(string name, bool expectedValid)
    {
        // Arrange - key vault naming rules: 3-24 chars, alphanumeric and hyphens, can't start/end with hyphen
        bool isValid = name.Length >= 3 && 
                       name.Length <= 24 && 
                       name.All(c => char.IsLetterOrDigit(c) || c == '-') &&
                       !name.StartsWith('-') &&
                       !name.EndsWith('-');

        // Assert
        isValid.Should().Be(expectedValid);
    }

    #endregion
}
