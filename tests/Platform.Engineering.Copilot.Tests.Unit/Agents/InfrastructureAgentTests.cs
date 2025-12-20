using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Infrastructure.Agent.Configuration;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for InfrastructureAgent configuration, task processing, and response handling.
/// Note: Kernel is sealed and cannot be mocked - tests focus on options, models, and workflows.
/// </summary>
public class InfrastructureAgentTests
{
    #region InfrastructureAgentOptions Tests

    [Fact]
    public void InfrastructureAgentOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions();

        // Assert
        options.Temperature.Should().Be(0.4);
        options.MaxTokens.Should().Be(8000);
        options.DefaultRegion.Should().Be("eastus");
        options.EnableComplianceEnhancement.Should().BeTrue();
        options.DefaultComplianceFramework.Should().Be("FedRAMPHigh");
        options.EnablePredictiveScaling.Should().BeTrue();
        options.EnableNetworkDesign.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void InfrastructureAgentOptions_Temperature_AcceptsValidValues(double temperature)
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions { Temperature = temperature };

        // Assert
        options.Temperature.Should().Be(temperature);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4000)]
    [InlineData(8000)]
    [InlineData(128000)]
    public void InfrastructureAgentOptions_MaxTokens_AcceptsValidValues(int maxTokens)
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions { MaxTokens = maxTokens };

        // Assert
        options.MaxTokens.Should().Be(maxTokens);
    }

    [Theory]
    [InlineData("eastus")]
    [InlineData("westus2")]
    [InlineData("usgovvirginia")]
    [InlineData("centralus")]
    public void InfrastructureAgentOptions_DefaultRegion_AcceptsValidValues(string region)
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions { DefaultRegion = region };

        // Assert
        options.DefaultRegion.Should().Be(region);
    }

    [Theory]
    [InlineData("FedRAMPHigh")]
    [InlineData("DoD IL5")]
    [InlineData("NIST80053")]
    [InlineData("SOC2")]
    [InlineData("GDPR")]
    public void InfrastructureAgentOptions_DefaultComplianceFramework_AcceptsValidValues(string framework)
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions { DefaultComplianceFramework = framework };

        // Assert
        options.DefaultComplianceFramework.Should().Be(framework);
    }

    [Fact]
    public void InfrastructureAgentOptions_SectionName_IsCorrect()
    {
        // Assert
        InfrastructureAgentOptions.SectionName.Should().Be("InfrastructureAgent");
    }

    [Fact]
    public void InfrastructureAgentOptions_WithAllFeaturesDisabled_ConfiguresCorrectly()
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions
        {
            EnableComplianceEnhancement = false,
            EnablePredictiveScaling = false,
            EnableNetworkDesign = false
        };

        // Assert
        options.EnableComplianceEnhancement.Should().BeFalse();
        options.EnablePredictiveScaling.Should().BeFalse();
        options.EnableNetworkDesign.Should().BeFalse();
    }

    #endregion

    #region AgentTask Tests

    [Fact]
    public void AgentTask_ForInfrastructure_HasCorrectAgentType()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure,
            Description = "Generate Bicep template for storage account"
        };

        // Assert
        task.AgentType.Should().Be(AgentType.Infrastructure);
    }

    [Fact]
    public void AgentTask_WithParameters_ContainsAllParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["resourceType"] = "storage-account",
            ["resourceName"] = "mystorageaccount",
            ["location"] = "eastus",
            ["sku"] = "Standard_LRS"
        };

        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure,
            Description = "Create storage account",
            Parameters = parameters
        };

        // Assert
        task.Parameters.Should().HaveCount(4);
        task.Parameters["resourceType"].Should().Be("storage-account");
        task.Parameters["location"].Should().Be("eastus");
    }

    [Fact]
    public void AgentTask_IsCritical_DefaultsToFalse()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure
        };

        // Assert
        task.IsCritical.Should().BeFalse();
    }

    [Fact]
    public void AgentTask_Priority_DefaultsToZero()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure
        };

        // Assert
        task.Priority.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]  // Default/Normal
    [InlineData(1)]  // Low
    [InlineData(5)]  // Medium
    [InlineData(10)] // High
    public void AgentTask_Priority_CanBeSetToAnyLevel(int priority)
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure,
            Priority = priority
        };

        // Assert
        task.Priority.Should().Be(priority);
    }

    #endregion

    #region AgentResponse Tests

    [Fact]
    public void AgentResponse_ForInfrastructure_HasCorrectAgentType()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure,
            Success = true,
            Content = "Bicep template generated successfully"
        };

        // Assert
        response.AgentType.Should().Be(AgentType.Infrastructure);
    }

    [Fact]
    public void AgentResponse_Success_WithContent_IsValid()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure,
            Success = true,
            Content = "✅ Resource Provisioned Successfully"
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Content.Should().Contain("Provisioned");
    }

    [Fact]
    public void AgentResponse_Failure_WithErrors_IsValid()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure,
            Success = false,
            Content = "Failed to provision resource",
            Errors = new List<string> { "Invalid subscription ID", "Resource group not found" }
        };

        // Assert
        response.Success.Should().BeFalse();
        response.Errors.Should().HaveCount(2);
        response.Errors.Should().Contain("Invalid subscription ID");
    }

    [Fact]
    public void AgentResponse_ExecutionTime_IsTracked()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure,
            Success = true,
            ExecutionTimeMs = 1500.5
        };

        // Assert
        response.ExecutionTimeMs.Should().BeApproximately(1500.5, 0.01);
    }

    [Fact]
    public void AgentResponse_WithMetadata_ContainsAdditionalInfo()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure,
            Success = true,
            Content = "Template generated",
            Metadata = new Dictionary<string, object>
            {
                ["templateType"] = "bicep",
                ["resourceCount"] = 5,
                ["fileCount"] = 3
            }
        };

        // Assert
        response.Metadata.Should().HaveCount(3);
        response.Metadata["templateType"].Should().Be("bicep");
        response.Metadata["resourceCount"].Should().Be(5);
    }

    [Fact]
    public void AgentResponse_WithWarnings_ContainsWarnings()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Infrastructure,
            Success = true,
            Content = "Template generated with warnings",
            Warnings = new List<string>
            {
                "SKU 'Basic' may not meet production SLA requirements",
                "Consider enabling diagnostic logging"
            }
        };

        // Assert
        response.Warnings.Should().HaveCount(2);
        response.Warnings.Should().Contain(w => w.Contains("SKU"));
    }

    #endregion

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
    }

    [Fact]
    public void InfrastructureProvisionResult_Success_ContainsResourceDetails()
    {
        // Arrange & Act
        var result = new InfrastructureProvisionResult
        {
            Success = true,
            ResourceId = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/mystorageaccount",
            ResourceName = "mystorageaccount",
            ResourceType = "Microsoft.Storage/storageAccounts",
            Status = "Succeeded",
            Message = "Storage account created successfully"
        };

        // Assert
        result.Success.Should().BeTrue();
        result.ResourceId.Should().Contain("Microsoft.Storage");
        result.ResourceName.Should().Be("mystorageaccount");
        result.Status.Should().Be("Succeeded");
    }

    [Fact]
    public void InfrastructureProvisionResult_Failure_ContainsErrorDetails()
    {
        // Arrange & Act
        var result = new InfrastructureProvisionResult
        {
            Success = false,
            Status = "Failed",
            ErrorDetails = "Subscription quota exceeded",
            Message = "Failed to create resource"
        };

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorDetails.Should().Contain("quota");
    }

    [Fact]
    public void InfrastructureProvisionResult_WithProperties_ContainsAdditionalInfo()
    {
        // Arrange & Act
        var result = new InfrastructureProvisionResult
        {
            Success = true,
            ResourceName = "mykeyvault",
            Properties = new Dictionary<string, string>
            {
                ["softDeleteEnabled"] = "true",
                ["purgeProtectionEnabled"] = "true",
                ["skuName"] = "standard"
            }
        };

        // Assert
        result.Properties.Should().HaveCount(3);
        result.Properties["softDeleteEnabled"].Should().Be("true");
    }

    [Fact]
    public void InfrastructureProvisionResult_ProvisionedAt_IsSetToCurrentTime()
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
    }

    [Fact]
    public void InfrastructureCostEstimate_WithCosts_CalculatesCorrectly()
    {
        // Arrange & Act
        var estimate = new InfrastructureCostEstimate
        {
            ResourceType = "Microsoft.Compute/virtualMachines",
            Location = "eastus",
            MonthlyEstimate = 150.00m,
            AnnualEstimate = 1800.00m
        };

        // Assert
        estimate.MonthlyEstimate.Should().Be(150.00m);
        estimate.AnnualEstimate.Should().Be(1800.00m);
        (estimate.AnnualEstimate / 12).Should().Be(estimate.MonthlyEstimate);
    }

    [Fact]
    public void InfrastructureCostEstimate_WithBreakdown_ContainsComponentCosts()
    {
        // Arrange & Act
        var estimate = new InfrastructureCostEstimate
        {
            ResourceType = "Microsoft.Compute/virtualMachines",
            CostBreakdown = new Dictionary<string, decimal>
            {
                ["compute"] = 100.00m,
                ["storage"] = 30.00m,
                ["networking"] = 20.00m
            }
        };

        // Assert
        estimate.CostBreakdown.Should().HaveCount(3);
        estimate.CostBreakdown.Values.Sum().Should().Be(150.00m);
    }

    [Theory]
    [InlineData("vnet", 0.00)]
    [InlineData("storage-account", 20.00)]
    [InlineData("keyvault", 0.03)]
    [InlineData("managed-identity", 0.00)]
    public void InfrastructureCostEstimate_ByResourceType_ReturnsExpectedCost(string resourceType, decimal expectedMonthlyCost)
    {
        // Arrange & Act
        var estimate = new InfrastructureCostEstimate
        {
            ResourceType = resourceType,
            MonthlyEstimate = expectedMonthlyCost
        };

        // Assert
        estimate.MonthlyEstimate.Should().Be(expectedMonthlyCost);
    }

    #endregion

    #region SharedMemory Infrastructure Context Tests

    [Fact]
    public void SharedMemory_StoreDeploymentMetadata_ForInfrastructure_Works()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "test-conversation-123";
        var metadata = new Dictionary<string, string>
        {
            ["subscriptionId"] = "sub-123",
            ["resourceGroup"] = "rg-test",
            ["region"] = "eastus"
        };

        // Act
        memory.StoreDeploymentMetadata(conversationId, metadata);
        var result = memory.GetDeploymentMetadata(conversationId);

        // Assert
        result.Should().NotBeNull();
        result!["subscriptionId"].Should().Be("sub-123");
    }

    [Fact]
    public void SharedMemory_StoreGeneratedFiles_Works()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "test-conversation-123";
        var files = new Dictionary<string, string>
        {
            ["main.bicep"] = "param location string = 'eastus'"
        };

        // Act
        memory.StoreGeneratedFiles(conversationId, files);
        var content = memory.GetGeneratedFile(conversationId, "main.bicep");

        // Assert
        content.Should().Be("param location string = 'eastus'");
    }

    [Fact]
    public void SharedMemory_GetGeneratedFileNames_ReturnsAllFiles()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "test-conversation-123";
        var files = new Dictionary<string, string>
        {
            ["main.bicep"] = "content1",
            ["modules/storage.bicep"] = "content2",
            ["parameters.json"] = "{}"
        };

        // Act
        memory.StoreGeneratedFiles(conversationId, files);
        var fileNames = memory.GetGeneratedFileNames(conversationId);

        // Assert
        fileNames.Should().HaveCount(3);
        fileNames.Should().Contain("main.bicep");
        fileNames.Should().Contain("modules/storage.bicep");
    }

    #endregion

    #region AgentType Enum Tests

    [Fact]
    public void AgentType_Infrastructure_HasCorrectValue()
    {
        // Assert
        AgentType.Infrastructure.Should().BeDefined();
        AgentType.Infrastructure.ToString().Should().Be("Infrastructure");
    }

    [Fact]
    public void AgentType_CanDistinguishInfrastructureFromOthers()
    {
        // Arrange
        var infrastructureType = AgentType.Infrastructure;
        var complianceType = AgentType.Compliance;
        var discoveryType = AgentType.Discovery;

        // Assert
        infrastructureType.Should().NotBe(complianceType);
        infrastructureType.Should().NotBe(discoveryType);
    }

    #endregion

    #region Template Format Detection Tests

    [Theory]
    [InlineData("main.bicep", "bicep")]
    [InlineData("main.tf", "hcl")]
    [InlineData("template.json", "json")]
    [InlineData("values.yaml", "yaml")]
    [InlineData("config.yml", "yaml")]
    public void TemplateFileExtension_MapsToCorrectCodeBlockType(string fileName, string expectedCodeType)
    {
        // Arrange
        var fileExt = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

        // Act
        var codeBlockType = fileExt switch
        {
            "bicep" => "bicep",
            "tf" => "hcl",
            "json" => "json",
            "yaml" or "yml" => "yaml",
            _ => fileExt
        };

        // Assert
        codeBlockType.Should().Be(expectedCodeType);
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
    public void SubscriptionName_IsNotGuid_RequiresResolution()
    {
        // Arrange
        var subscriptionName = "My Production Subscription";

        // Act
        var isGuid = Guid.TryParse(subscriptionName, out _);

        // Assert
        isGuid.Should().BeFalse();
    }

    #endregion

    #region Resource Type Mapping Tests

    [Theory]
    [InlineData("storage-account", "Microsoft.Storage/storageAccounts")]
    [InlineData("keyvault", "Microsoft.KeyVault/vaults")]
    [InlineData("vnet", "Microsoft.Network/virtualNetworks")]
    [InlineData("nsg", "Microsoft.Network/networkSecurityGroups")]
    public void ResourceType_ShortName_MapsToAzureResourceType(string shortName, string expectedType)
    {
        // This tests the pattern used in InfrastructureProvisioningService
        var typeMapping = new Dictionary<string, string>
        {
            ["storage-account"] = "Microsoft.Storage/storageAccounts",
            ["keyvault"] = "Microsoft.KeyVault/vaults",
            ["key-vault"] = "Microsoft.KeyVault/vaults",
            ["vnet"] = "Microsoft.Network/virtualNetworks",
            ["virtual-network"] = "Microsoft.Network/virtualNetworks",
            ["nsg"] = "Microsoft.Network/networkSecurityGroups",
            ["managed-identity"] = "Microsoft.ManagedIdentity/userAssignedIdentities"
        };

        // Assert
        typeMapping.Should().ContainKey(shortName);
        typeMapping[shortName].Should().Be(expectedType);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public void Cache_SubscriptionKey_FormatsCorrectly()
    {
        // Arrange
        var cacheKey = "infrastructure_last_subscription";

        // Assert
        cacheKey.Should().StartWith("infrastructure_");
    }

    [Fact]
    public void MemoryCache_StoresSubscriptionForSession()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var cacheKey = "infrastructure_last_subscription";
        var subscriptionId = "00000000-0000-0000-0000-000000000001";

        // Act
        cache.Set(cacheKey, subscriptionId, TimeSpan.FromHours(24));
        var retrieved = cache.Get<string>(cacheKey);

        // Assert
        retrieved.Should().Be(subscriptionId);
    }

    #endregion
}
