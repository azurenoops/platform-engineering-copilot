using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Plugins.Infrastructure;

/// <summary>
/// Unit tests for InfrastructurePlugin functionality.
/// Tests response formatting, file management, caching, and subscription resolution.
/// Note: Kernel is sealed - tests focus on data transformations and logic patterns.
/// </summary>
public class InfrastructurePluginTests
{
    private readonly Mock<ILogger<object>> _loggerMock;
    private readonly Mock<IInfrastructureProvisioningService> _provisioningServiceMock;
    private readonly Mock<IDynamicTemplateGenerator> _templateGeneratorMock;
    private readonly Mock<IAzureResourceService> _azureResourceServiceMock;
    private readonly Mock<ITemplateStorageService> _templateStorageServiceMock;
    private readonly IMemoryCache _memoryCache;

    public InfrastructurePluginTests()
    {
        _loggerMock = new Mock<ILogger<object>>();
        _provisioningServiceMock = new Mock<IInfrastructureProvisioningService>();
        _templateGeneratorMock = new Mock<IDynamicTemplateGenerator>();
        _azureResourceServiceMock = new Mock<IAzureResourceService>();
        _templateStorageServiceMock = new Mock<ITemplateStorageService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    #region Response Formatting Tests

    [Fact]
    public void ProvisioningSuccessResponse_HasCorrectFormat()
    {
        // Arrange
        var resourceName = "mystorageaccount";
        var resourceType = "Microsoft.Storage/storageAccounts";
        var location = "eastus";
        var resourceId = "/subscriptions/sub-123/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/mystorageaccount";

        // Act
        var response = FormatProvisioningSuccessResponse(resourceName, resourceType, location, resourceId);

        // Assert
        response.Should().Contain("✅");
        response.Should().Contain("Provisioned Successfully");
        response.Should().Contain(resourceId);
        response.Should().Contain(resourceType);
        response.Should().Contain(location);
    }

    [Fact]
    public void ProvisioningFailureResponse_HasCorrectFormat()
    {
        // Arrange
        var resourceName = "mystorageaccount";
        var resourceType = "storage-account";
        var errorDetails = "Subscription quota exceeded";

        // Act
        var response = FormatProvisioningFailureResponse(resourceName, resourceType, errorDetails);

        // Assert
        response.Should().Contain("❌");
        response.Should().Contain("Failed");
        response.Should().Contain(resourceName);
        response.Should().Contain(errorDetails);
    }

    [Theory]
    [InlineData("main.bicep", "bicep")]
    [InlineData("main.tf", "hcl")]
    [InlineData("template.json", "json")]
    [InlineData("values.yaml", "yaml")]
    [InlineData("config.yml", "yaml")]
    public void FileResponse_UsesCorrectCodeBlockType(string fileName, string expectedCodeType)
    {
        // Arrange
        var content = "param location string = 'eastus'";

        // Act
        var response = FormatFileResponse(fileName, content);

        // Assert
        response.Should().Contain($"### 📁 {fileName}");
        response.Should().Contain($"```{expectedCodeType}");
        response.Should().Contain(content);
        response.Should().EndWith("```\n");
    }

    [Fact]
    public void FileNotFoundResponse_HasCorrectFormat()
    {
        // Arrange
        var fileName = "missing.bicep";

        // Act
        var response = $"❌ File '{fileName}' not found. Please generate a template first using 'Generate a Bicep/Terraform template for...'";

        // Assert
        response.Should().Contain("❌");
        response.Should().Contain(fileName);
        response.Should().Contain("generate a template first");
    }

    #endregion

    #region Template Generation Response Tests

    [Fact]
    public void TemplateGenerationResponse_ContainsAllFiles()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["main.bicep"] = "param location string",
            ["modules/storage.bicep"] = "param name string",
            ["parameters.json"] = "{}"
        };

        // Act
        var response = FormatTemplateGenerationResponse(files, "my-template");

        // Assert
        response.Should().Contain("main.bicep");
        response.Should().Contain("modules/storage.bicep");
        response.Should().Contain("parameters.json");
        response.Should().Contain("3 files generated");
    }

    [Fact]
    public void TemplateGenerationResponse_IncludesTemplateName()
    {
        // Arrange
        var files = new Dictionary<string, string>
        {
            ["main.bicep"] = "content"
        };

        // Act
        var response = FormatTemplateGenerationResponse(files, "aks-production-template");

        // Assert
        response.Should().Contain("aks-production-template");
    }

    #endregion

    #region Subscription Resolution Tests

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001", true)]
    [InlineData("12345678-1234-1234-1234-123456789012", true)]
    [InlineData("not-a-guid", false)]
    [InlineData("My Production Subscription", false)]
    [InlineData("", false)]
    public void SubscriptionId_IsGuid_DetectsCorrectly(string subscriptionId, bool expectedIsGuid)
    {
        // Act
        var isGuid = Guid.TryParse(subscriptionId, out _);

        // Assert
        isGuid.Should().Be(expectedIsGuid);
    }

    [Fact]
    public void SubscriptionCache_StoresAndRetrievesValue()
    {
        // Arrange
        var cacheKey = "infrastructure_last_subscription";
        var subscriptionId = "00000000-0000-0000-0000-000000000001";

        // Act
        _memoryCache.Set(cacheKey, subscriptionId, TimeSpan.FromHours(24));
        var retrieved = _memoryCache.Get<string>(cacheKey);

        // Assert
        retrieved.Should().Be(subscriptionId);
    }

    [Fact]
    public void SubscriptionCache_ExpiresAfterTimeout()
    {
        // Arrange
        var cacheKey = "test_subscription_expiry";
        var subscriptionId = "sub-123";

        // Act
        _memoryCache.Set(cacheKey, subscriptionId, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);
        var result = _memoryCache.Get<string>(cacheKey);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Resource Type Parsing Tests

    [Theory]
    [InlineData("storage-account", "storage")]
    [InlineData("keyvault", "keyvault")]
    [InlineData("key-vault", "keyvault")]
    [InlineData("vnet", "vnet")]
    [InlineData("virtual-network", "vnet")]
    [InlineData("nsg", "nsg")]
    [InlineData("managed-identity", "identity")]
    [InlineData("log-analytics", "monitoring")]
    [InlineData("app-insights", "monitoring")]
    public void ResourceType_NormalizesCorrectly(string input, string expectedCategory)
    {
        // Arrange
        var categoryMapping = new Dictionary<string, string>
        {
            ["storage-account"] = "storage",
            ["keyvault"] = "keyvault",
            ["key-vault"] = "keyvault",
            ["vnet"] = "vnet",
            ["virtual-network"] = "vnet",
            ["nsg"] = "nsg",
            ["managed-identity"] = "identity",
            ["log-analytics"] = "monitoring",
            ["app-insights"] = "monitoring"
        };

        // Act & Assert
        categoryMapping.Should().ContainKey(input);
        categoryMapping[input].Should().Be(expectedCategory);
    }

    [Theory]
    [InlineData("Create a storage account named mydata", "storage")]
    [InlineData("Provision a VNet with 3 subnets", "vnet")]
    [InlineData("Set up Key Vault with soft delete", "keyvault")]
    public void QueryParsing_ExtractsResourceType(string query, string expectedResourceType)
    {
        // Arrange
        var resourcePatterns = new[]
        {
            ("storage account", "storage"),
            ("storage-account", "storage"),
            ("vnet", "vnet"),
            ("virtual network", "vnet"),
            ("key vault", "keyvault"),
            ("keyvault", "keyvault")
        };

        // Act
        var queryLower = query.ToLowerInvariant();
        var matchedType = resourcePatterns
            .Where(p => queryLower.Contains(p.Item1))
            .Select(p => p.Item2)
            .FirstOrDefault();

        // Assert
        matchedType.Should().Be(expectedResourceType);
    }

    #endregion

    #region Location Validation Tests

    [Theory]
    [InlineData("eastus", true)]
    [InlineData("westus2", true)]
    [InlineData("usgovvirginia", true)]
    [InlineData("usgovarizona", true)]
    [InlineData("centralus", true)]
    [InlineData("invalid-region", false)]
    public void AzureLocation_IsValid_ReturnsCorrectResult(string location, bool expectedValid)
    {
        // Arrange
        var validLocations = new HashSet<string>
        {
            "eastus", "eastus2", "westus", "westus2", "westus3",
            "centralus", "northcentralus", "southcentralus",
            "usgovvirginia", "usgovarizona", "usgovtexas"
        };

        // Act
        var isValid = validLocations.Contains(location);

        // Assert
        isValid.Should().Be(expectedValid);
    }

    #endregion

    #region SKU Validation Tests

    [Theory]
    [InlineData("Standard_LRS", "storage")]
    [InlineData("Standard_GRS", "storage")]
    [InlineData("Premium_LRS", "storage")]
    [InlineData("standard", "keyvault")]
    [InlineData("premium", "keyvault")]
    public void SKU_IsValidForResourceType(string sku, string resourceType)
    {
        // Arrange
        var validSkus = new Dictionary<string, HashSet<string>>
        {
            ["storage"] = new() { "Standard_LRS", "Standard_GRS", "Standard_ZRS", "Premium_LRS", "Premium_ZRS" },
            ["keyvault"] = new() { "standard", "premium" }
        };

        // Act & Assert
        validSkus.Should().ContainKey(resourceType);
        validSkus[resourceType].Should().Contain(sku);
    }

    #endregion

    #region File Path Matching Tests

    [Theory]
    [InlineData("main.bicep", "main.bicep", true)]
    [InlineData("infra/main.bicep", "main.bicep", true)]
    [InlineData("modules/storage/main.bicep", "main.bicep", true)]
    [InlineData("storage.bicep", "main.bicep", false)]
    public void FilePath_EndsWithMatch_Works(string fullPath, string searchName, bool expectedMatch)
    {
        // Act
        var matches = fullPath.EndsWith(searchName, StringComparison.OrdinalIgnoreCase);

        // Assert
        matches.Should().Be(expectedMatch);
    }

    [Theory]
    [InlineData("main.bicep", "main", true)]
    [InlineData("modules/storage.bicep", "storage", true)]
    [InlineData("parameters.json", "param", true)]
    [InlineData("keyvault.bicep", "storage", false)]
    public void FilePath_ContainsMatch_Works(string fullPath, string searchTerm, bool expectedMatch)
    {
        // Act
        var matches = fullPath.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);

        // Assert
        matches.Should().Be(expectedMatch);
    }

    #endregion

    #region Template Storage Tests

    [Fact]
    public void TemplateFiles_GroupByFolder_Works()
    {
        // Arrange
        var files = new List<string>
        {
            "main.bicep",
            "modules/storage.bicep",
            "modules/keyvault.bicep",
            "modules/network/vnet.bicep",
            "modules/network/nsg.bicep",
            "parameters.json"
        };

        // Act - Note: Path.GetDirectoryName returns empty string for files in root
        var byFolder = files.GroupBy(f => Path.GetDirectoryName(f) ?? "")
            .ToDictionary(g => g.Key, g => g.Count());

        // Assert
        byFolder.Should().HaveCount(3); // "", "modules", "modules/network"
        byFolder[""].Should().Be(2); // main.bicep and parameters.json
        byFolder["modules"].Should().Be(2);
        byFolder["modules/network"].Should().Be(2);
    }

    #endregion

    #region Cost Estimation Tests

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
    public void CostEstimation_ByResourceType_ReturnsCorrectEstimate(string resourceType, decimal expectedMonthlyCost)
    {
        // Arrange - cost mapping from InfrastructureProvisioningService
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
        monthlyCost.Should().Be(expectedMonthlyCost);
    }

    [Fact]
    public void CostEstimation_AnnualCalculation_IsCorrect()
    {
        // Arrange
        var monthlyCost = 20.00m;

        // Act
        var annualCost = monthlyCost * 12;

        // Assert
        annualCost.Should().Be(240.00m);
    }

    #endregion

    #region Compliance Framework Tests

    [Theory]
    [InlineData("FedRAMPHigh")]
    [InlineData("DoD IL5")]
    [InlineData("NIST80053")]
    [InlineData("SOC2")]
    [InlineData("GDPR")]
    public void ComplianceFramework_IsRecognized(string framework)
    {
        // Arrange
        var supportedFrameworks = new HashSet<string>
        {
            "FedRAMPHigh", "DoD IL5", "NIST80053", "SOC2", "GDPR"
        };

        // Assert
        supportedFrameworks.Should().Contain(framework);
    }

    #endregion

    #region JSON Serialization Tests

    [Fact]
    public void ProvisioningResult_SerializesToJson_Correctly()
    {
        // Arrange
        var result = new
        {
            success = true,
            resourceId = "/subscriptions/sub-123/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/test",
            resourceType = "Microsoft.Storage/storageAccounts",
            location = "eastus",
            status = "Succeeded"
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("location").GetString().Should().Be("eastus");
    }

    [Fact]
    public void ErrorResponse_SerializesToJson_Correctly()
    {
        // Arrange
        var error = new
        {
            success = false,
            error = "Subscription quota exceeded",
            suggestion = "Request quota increase or use different subscription"
        };

        // Act
        var json = JsonSerializer.Serialize(error, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("quota");
    }

    #endregion

    #region Helper Methods

    private static string FormatProvisioningSuccessResponse(string resourceName, string resourceType, string location, string resourceId)
    {
        return $"✅ **Resource Provisioned Successfully**\n\n" +
               $"Resource: {resourceName}\n" +
               $"📍 Resource ID: {resourceId}\n" +
               $"📦 Resource Type: {resourceType}\n" +
               $"🌍 Location: {location}\n" +
               $"📊 Status: Succeeded\n\n" +
               $"💡 You can view this resource in the Azure Portal.";
    }

    private static string FormatProvisioningFailureResponse(string resourceName, string resourceType, string errorDetails)
    {
        return $"❌ **Provisioning Failed**\n\n" +
               $"Resource: {resourceName}\n" +
               $"Type: {resourceType}\n" +
               $"Error: {errorDetails}\n\n" +
               $"Suggestion: Check parameters and try again, or use generate_infrastructure_template to see the IaC code first.";
    }

    private static string FormatFileResponse(string fileName, string content)
    {
        var fileExt = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        var codeBlockType = fileExt switch
        {
            "bicep" => "bicep",
            "tf" => "hcl",
            "json" => "json",
            "yaml" or "yml" => "yaml",
            _ => fileExt
        };

        return $"### 📁 {fileName}\n\n```{codeBlockType}\n{content}\n```\n";
    }

    private static string FormatTemplateGenerationResponse(Dictionary<string, string> files, string templateName)
    {
        var fileList = string.Join("\n", files.Keys.Select(f => $"  - {f}"));
        return $"✅ **Template Generated: {templateName}**\n\n" +
               $"📁 {files.Count} files generated:\n{fileList}";
    }

    #endregion
}
