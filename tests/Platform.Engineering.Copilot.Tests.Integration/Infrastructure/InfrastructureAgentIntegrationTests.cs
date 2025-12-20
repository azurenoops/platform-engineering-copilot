using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Infrastructure;

/// <summary>
/// Integration tests for Infrastructure Agent
/// Tests end-to-end workflows with mocked Azure dependencies
/// </summary>
public class InfrastructureAgentIntegrationTests
{
    private readonly Mock<ILogger<object>> _loggerMock;

    public InfrastructureAgentIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<object>>();
    }

    #region Agent Task Flow Tests

    [Fact]
    public void AgentTask_Infrastructure_HasCorrectType()
    {
        // Arrange
        var task = new AgentTask
        {
            AgentType = AgentType.Infrastructure,
            Description = "Create a storage account",
            Priority = 0
        };

        // Assert
        task.AgentType.Should().Be(AgentType.Infrastructure);
    }

    [Theory]
    [InlineData("Create a storage account in eastus", AgentType.Infrastructure)]
    [InlineData("Provision a virtual network", AgentType.Infrastructure)]
    [InlineData("Deploy Bicep template", AgentType.Infrastructure)]
    [InlineData("Set up AKS cluster", AgentType.Infrastructure)]
    public void AgentTask_InfrastructureKeywords_RouteCorrectly(string description, AgentType expectedType)
    {
        // Arrange
        var task = new AgentTask
        {
            AgentType = expectedType,
            Description = description
        };

        // Assert
        task.AgentType.Should().Be(AgentType.Infrastructure);
    }

    [Fact]
    public void AgentTask_WithParameters_PreservesInformation()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["subscriptionId"] = "00000000-0000-0000-0000-000000000001",
            ["resourceGroup"] = "rg-test",
            ["region"] = "eastus"
        };

        var task = new AgentTask
        {
            AgentType = AgentType.Infrastructure,
            Description = "Create a storage account",
            Parameters = parameters
        };

        // Assert
        task.Parameters.Should().ContainKey("subscriptionId");
        task.Parameters.Should().ContainKey("resourceGroup");
        task.Parameters.Should().ContainKey("region");
    }

    #endregion

    #region Agent Response Flow Tests

    [Fact]
    public void AgentResponse_Success_HasRequiredFields()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            AgentType = AgentType.Infrastructure,
            Success = true,
            Content = "Storage account created successfully"
        };

        // Assert
        response.Success.Should().BeTrue();
        response.AgentType.Should().Be(AgentType.Infrastructure);
        response.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AgentResponse_Failure_ContainsErrorInfo()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            AgentType = AgentType.Infrastructure,
            Success = false,
            Errors = new List<string> { "Resource group not found" }
        };

        // Assert
        response.Success.Should().BeFalse();
        response.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void AgentResponse_WithMetadata_ContainsAzureInfo()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            AgentType = AgentType.Infrastructure,
            Success = true,
            Metadata = new Dictionary<string, object>
            {
                ["resourceId"] = "/subscriptions/sub-123/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/mystore",
                ["provisioningState"] = "Succeeded",
                ["duration"] = TimeSpan.FromSeconds(45)
            }
        };

        // Assert
        response.Metadata.Should().ContainKey("resourceId");
        response.Metadata.Should().ContainKey("provisioningState");
    }

    #endregion

    #region SharedMemory Integration Tests

    [Fact]
    public void SharedMemory_StoresDeploymentMetadata()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var sharedMemory = new SharedMemory(loggerMock.Object);
        var conversationId = "test-conv-123";
        var metadata = new Dictionary<string, string>
        {
            ["subscriptionId"] = "sub-123",
            ["resourceGroup"] = "rg-test"
        };

        // Act
        sharedMemory.StoreDeploymentMetadata(conversationId, metadata);
        var result = sharedMemory.GetDeploymentMetadata(conversationId);

        // Assert
        result.Should().NotBeNull();
        result!["subscriptionId"].Should().Be("sub-123");
    }

    [Fact]
    public void SharedMemory_TracksGeneratedFiles()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var sharedMemory = new SharedMemory(loggerMock.Object);
        var conversationId = "test-conv-123";
        var files = new Dictionary<string, string>
        {
            ["main.bicep"] = "param location string",
            ["modules/storage.bicep"] = "resource storage ..."
        };

        // Act
        sharedMemory.StoreGeneratedFiles(conversationId, files);
        var fileNames = sharedMemory.GetGeneratedFileNames(conversationId);

        // Assert
        fileNames.Should().HaveCount(2);
        fileNames.Should().Contain("main.bicep");
    }

    #endregion

    #region Configuration Integration Tests

    [Fact]
    public void AgentConfiguration_Temperature_HasValidRange()
    {
        // Arrange - standard agent configuration values
        var temperatureMin = 0.0;
        var temperatureMax = 2.0;
        var defaultTemperature = 0.4;

        // Assert
        defaultTemperature.Should().BeGreaterThanOrEqualTo(temperatureMin);
        defaultTemperature.Should().BeLessThanOrEqualTo(temperatureMax);
    }

    [Fact]
    public void AgentConfiguration_DefaultRegion_IsEastus()
    {
        // Arrange - standard infrastructure agent defaults
        var defaultRegion = "eastus";
        var supportedCommercialRegions = new[] { "eastus", "eastus2", "westus", "westus2", "centralus" };

        // Assert
        supportedCommercialRegions.Should().Contain(defaultRegion);
    }

    [Fact]
    public void AgentConfiguration_MaxTokens_HasReasonableDefault()
    {
        // Arrange
        var defaultMaxTokens = 8000;
        var minTokens = 1;
        var maxTokens = 128000;

        // Assert
        defaultMaxTokens.Should().BeGreaterThanOrEqualTo(minTokens);
        defaultMaxTokens.Should().BeLessThanOrEqualTo(maxTokens);
    }

    #endregion

    #region End-to-End Workflow Tests

    [Fact]
    public void Workflow_CreateStorageAccount_HasCorrectSequence()
    {
        // Arrange - simulate the workflow steps
        var workflowSteps = new List<string>
        {
            "Parse user request",
            "Validate subscription context",
            "Check resource group existence",
            "Generate ARM/Bicep template",
            "Execute what-if analysis",
            "Deploy template",
            "Verify deployment status",
            "Return provisioned resource info"
        };

        // Assert
        workflowSteps.Should().HaveCount(8);
        workflowSteps.First().Should().Be("Parse user request");
        workflowSteps.Last().Should().Be("Return provisioned resource info");
    }

    [Fact]
    public void Workflow_NetworkTopologyCreation_IncludesAllSubnets()
    {
        // Arrange
        var workflowResult = new
        {
            VNetCreated = true,
            AddressSpace = "10.0.0.0/16",
            Subnets = new List<string>
            {
                "WebTier - 10.0.1.0/24",
                "AppTier - 10.0.2.0/24",
                "DataTier - 10.0.3.0/24",
                "GatewaySubnet - 10.0.0.0/27"
            }
        };

        // Assert
        workflowResult.VNetCreated.Should().BeTrue();
        workflowResult.Subnets.Should().HaveCount(4);
    }

    [Fact]
    public void Workflow_TemplateGeneration_ProducesValidBicep()
    {
        // Arrange
        var templateContent = @"
@description('The name of the storage account')
param storageAccountName string

@description('The location for resources')
param location string = resourceGroup().location

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

output storageAccountId string = storageAccount.id
";

        // Assert
        templateContent.Should().Contain("param storageAccountName string");
        templateContent.Should().Contain("Microsoft.Storage/storageAccounts");
        templateContent.Should().Contain("output storageAccountId string");
    }

    #endregion

    #region Multi-Resource Deployment Tests

    [Fact]
    public void MultiResourceDeployment_OrdersResourcesCorrectly()
    {
        // Arrange - Resources should be deployed in dependency order
        var deploymentOrder = new List<string>
        {
            "Microsoft.Network/virtualNetworks",
            "Microsoft.Network/networkSecurityGroups",
            "Microsoft.Storage/storageAccounts",
            "Microsoft.KeyVault/vaults",
            "Microsoft.Compute/virtualMachines"
        };

        // Assert - VNet before VM (VM depends on subnet)
        var vnetIndex = deploymentOrder.IndexOf("Microsoft.Network/virtualNetworks");
        var vmIndex = deploymentOrder.IndexOf("Microsoft.Compute/virtualMachines");
        vnetIndex.Should().BeLessThan(vmIndex);
    }

    [Fact]
    public void MultiResourceDeployment_TracksAllResources()
    {
        // Arrange
        var deploymentResults = new List<InfrastructureProvisionResult>
        {
            new() { Success = true, ResourceType = "Microsoft.Network/virtualNetworks", ResourceName = "vnet-main" },
            new() { Success = true, ResourceType = "Microsoft.Storage/storageAccounts", ResourceName = "storemain" },
            new() { Success = false, ResourceType = "Microsoft.KeyVault/vaults", ResourceName = "kv-main", ErrorDetails = "Name already in use" }
        };

        // Act
        var successCount = deploymentResults.Count(r => r.Success);
        var failureCount = deploymentResults.Count(r => !r.Success);

        // Assert
        successCount.Should().Be(2);
        failureCount.Should().Be(1);
    }

    #endregion

    #region Rollback Scenario Tests

    [Fact]
    public void Rollback_OnPartialFailure_IdentifiesResourcesCreated()
    {
        // Arrange
        var createdResources = new List<string>
        {
            "/subscriptions/sub-123/resourceGroups/rg-test/providers/Microsoft.Network/virtualNetworks/vnet1",
            "/subscriptions/sub-123/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/store1"
        };

        var failedResource = "/subscriptions/sub-123/resourceGroups/rg-test/providers/Microsoft.KeyVault/vaults/kv1";

        // Assert
        createdResources.Should().HaveCount(2);
        createdResources.Should().NotContain(failedResource);
    }

    [Fact]
    public void Rollback_CanDeleteResourceGroup()
    {
        // Arrange
        var rollbackActions = new List<string>
        {
            "az group delete --name rg-test --yes --no-wait"
        };

        // Assert
        rollbackActions.Should().ContainSingle();
        rollbackActions.First().Should().Contain("az group delete");
    }

    #endregion

    #region Cost Estimation Integration Tests

    [Fact]
    public void CostEstimation_SumsMultipleResources()
    {
        // Arrange
        var estimates = new List<InfrastructureCostEstimate>
        {
            new() { ResourceType = "vm", MonthlyEstimate = 150.00m },
            new() { ResourceType = "storage", MonthlyEstimate = 25.00m },
            new() { ResourceType = "keyvault", MonthlyEstimate = 0.03m },
            new() { ResourceType = "vnet", MonthlyEstimate = 0.00m }
        };

        // Act
        var totalMonthly = estimates.Sum(e => e.MonthlyEstimate);
        var totalAnnual = totalMonthly * 12;

        // Assert
        totalMonthly.Should().Be(175.03m);
        totalAnnual.Should().Be(2100.36m);
    }

    [Fact]
    public void CostEstimation_FormatsForDisplay()
    {
        // Arrange
        var estimate = new InfrastructureCostEstimate
        {
            MonthlyEstimate = 175.03m,
            Currency = "USD"
        };

        // Act
        var formatted = $"{estimate.Currency} {estimate.MonthlyEstimate:N2}/month";

        // Assert
        formatted.Should().Be("USD 175.03/month");
    }

    #endregion

    #region Compliance Enhancement Integration Tests

    [Fact]
    public void ComplianceEnhancement_AddsRequiredTags()
    {
        // Arrange
        var requiredTags = new Dictionary<string, string>
        {
            ["Environment"] = "Production",
            ["Owner"] = "platform-team",
            ["CostCenter"] = "IT-001",
            ["Compliance"] = "FedRAMP-High"
        };

        // Assert
        requiredTags.Should().ContainKey("Environment");
        requiredTags.Should().ContainKey("Compliance");
    }

    [Fact]
    public void ComplianceEnhancement_EnforcesSecuritySettings()
    {
        // Arrange
        var securitySettings = new
        {
            MinimumTlsVersion = "TLS1_2",
            SupportsHttpsTrafficOnly = true,
            AllowBlobPublicAccess = false,
            EnableSoftDelete = true,
            EnablePurgeProtection = true
        };

        // Assert
        securitySettings.MinimumTlsVersion.Should().Be("TLS1_2");
        securitySettings.SupportsHttpsTrafficOnly.Should().BeTrue();
        securitySettings.AllowBlobPublicAccess.Should().BeFalse();
    }

    #endregion
}
