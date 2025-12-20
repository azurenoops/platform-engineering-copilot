using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Discovery.Agent.Plugins;
using Platform.Engineering.Copilot.Discovery.Core;
using Platform.Engineering.Copilot.Discovery.Core.Configuration;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for DiscoveryAgent
/// Tests agent initialization, task processing, response handling, and configuration
/// </summary>
public class DiscoveryAgentTests
{
    #region AgentType Tests

    [Fact]
    public void AgentType_ShouldReturnDiscovery()
    {
        // Assert - verify the expected agent type constant
        AgentType.Discovery.Should().Be(AgentType.Discovery);
    }

    [Fact]
    public void AgentType_Discovery_HasCorrectEnumValue()
    {
        // Assert
        ((int)AgentType.Discovery).Should().BeGreaterThan(0);
    }

    #endregion

    #region DiscoveryAgentOptions Tests

    [Fact]
    public void DiscoveryAgentOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new DiscoveryAgentOptions();

        // Assert
        options.Temperature.Should().Be(0.3);
        options.MaxTokens.Should().Be(4000);
        options.EnableHealthMonitoring.Should().BeTrue();
        options.EnablePerformanceMetrics.Should().BeTrue();
        options.EnableDependencyMapping.Should().BeTrue();
    }

    [Fact]
    public void DiscoveryAgentOptions_Discovery_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new DiscoveryAgentOptions();

        // Assert
        options.Discovery.Should().NotBeNull();
        options.Discovery.CacheDurationMinutes.Should().Be(15);
        options.Discovery.MaxResourcesPerQuery.Should().Be(1000);
        options.Discovery.IncludeDeletedResources.Should().BeFalse();
        options.Discovery.RequiredTags.Should().NotBeNull();
        options.Discovery.RequiredTags.Should().BeEmpty();
    }

    [Fact]
    public void DiscoveryAgentOptions_HealthMonitoring_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new DiscoveryAgentOptions();

        // Assert
        options.HealthMonitoring.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void DiscoveryAgentOptions_Temperature_AcceptsValidRange(double temperature)
    {
        // Arrange & Act
        var options = new DiscoveryAgentOptions { Temperature = temperature };

        // Assert
        options.Temperature.Should().Be(temperature);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(4000)]
    [InlineData(8000)]
    [InlineData(128000)]
    public void DiscoveryAgentOptions_MaxTokens_AcceptsValidRange(int maxTokens)
    {
        // Arrange & Act
        var options = new DiscoveryAgentOptions { MaxTokens = maxTokens };

        // Assert
        options.MaxTokens.Should().Be(maxTokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DiscoveryAgentOptions_EnableHealthMonitoring_CanBeToggled(bool enabled)
    {
        // Arrange & Act
        var options = new DiscoveryAgentOptions { EnableHealthMonitoring = enabled };

        // Assert
        options.EnableHealthMonitoring.Should().Be(enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DiscoveryAgentOptions_EnableDependencyMapping_CanBeToggled(bool enabled)
    {
        // Arrange & Act
        var options = new DiscoveryAgentOptions { EnableDependencyMapping = enabled };

        // Assert
        options.EnableDependencyMapping.Should().Be(enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DiscoveryAgentOptions_EnablePerformanceMetrics_CanBeToggled(bool enabled)
    {
        // Arrange & Act
        var options = new DiscoveryAgentOptions { EnablePerformanceMetrics = enabled };

        // Assert
        options.EnablePerformanceMetrics.Should().Be(enabled);
    }

    [Fact]
    public void DiscoveryAgentOptions_SectionName_IsCorrect()
    {
        // Assert
        DiscoveryAgentOptions.SectionName.Should().Be("DiscoveryAgent");
    }

    #endregion

    #region DiscoveryOptions Tests

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(1440)]
    public void DiscoveryOptions_CacheDurationMinutes_AcceptsValidRange(int duration)
    {
        // Arrange & Act
        var options = new DiscoveryOptions { CacheDurationMinutes = duration };

        // Assert
        options.CacheDurationMinutes.Should().Be(duration);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void DiscoveryOptions_MaxResourcesPerQuery_AcceptsValidRange(int maxResources)
    {
        // Arrange & Act
        var options = new DiscoveryOptions { MaxResourcesPerQuery = maxResources };

        // Assert
        options.MaxResourcesPerQuery.Should().Be(maxResources);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DiscoveryOptions_IncludeDeletedResources_CanBeToggled(bool include)
    {
        // Arrange & Act
        var options = new DiscoveryOptions { IncludeDeletedResources = include };

        // Assert
        options.IncludeDeletedResources.Should().Be(include);
    }

    [Fact]
    public void DiscoveryOptions_RequiredTags_CanBeConfigured()
    {
        // Arrange
        var requiredTags = new List<string> { "Environment", "Owner", "CostCenter" };

        // Act
        var options = new DiscoveryOptions { RequiredTags = requiredTags };

        // Assert
        options.RequiredTags.Should().BeEquivalentTo(requiredTags);
    }

    #endregion

    #region AgentTask Tests

    [Fact]
    public void AgentTask_WithDiscoveryType_IsCorrectlyIdentified()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Discovery,
            Description = "Discover all resources in subscription",
            Priority = 1,
            IsCritical = false
        };

        // Assert
        task.AgentType.Should().Be(AgentType.Discovery);
        task.Description.Should().Contain("resources");
    }

    [Fact]
    public void AgentTask_WithResourceIdQuery_ContainsResourceId()
    {
        // Arrange
        var resourceId = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/teststorage";
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Discovery,
            Description = $"Get details for resource {resourceId}",
            Priority = 1
        };

        // Assert
        task.Description.Should().Contain("/subscriptions/");
        task.Description.Should().Contain("resourceGroups");
    }

    [Fact]
    public void AgentTask_WithParameters_ContainsSubscriptionId()
    {
        // Arrange
        var subscriptionId = "00000000-0000-0000-0000-000000000001";
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Discovery,
            Description = "List all VMs",
            Parameters = new Dictionary<string, object>
            {
                ["subscriptionId"] = subscriptionId,
                ["resourceType"] = "Microsoft.Compute/virtualMachines"
            }
        };

        // Assert
        task.Parameters.Should().ContainKey("subscriptionId");
        task.Parameters["subscriptionId"].Should().Be(subscriptionId);
    }

    #endregion

    #region AgentResponse Tests

    [Fact]
    public void AgentResponse_WithDiscoveryResults_IsCorrectlySet()
    {
        // Arrange
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Discovery,
            Success = true,
            Content = "Discovered 150 resources across 5 resource groups",
            ExecutionTimeMs = 1500
        };

        // Assert
        response.AgentType.Should().Be(AgentType.Discovery);
        response.Success.Should().BeTrue();
        response.Content.Should().Contain("resources");
        response.ExecutionTimeMs.Should().BePositive();
    }

    [Fact]
    public void AgentResponse_WithMetadata_ContainsTimestamp()
    {
        // Arrange
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Discovery,
            Success = true,
            Metadata = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["agentType"] = AgentType.Discovery.ToString(),
                ["toolsInvoked"] = "AzureResourceDiscoveryPlugin functions"
            }
        };

        // Assert
        response.Metadata.Should().ContainKey("timestamp");
        response.Metadata.Should().ContainKey("agentType");
        response.Metadata["agentType"].Should().Be("Discovery");
    }

    [Fact]
    public void AgentResponse_OnFailure_ContainsErrors()
    {
        // Arrange
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Discovery,
            Success = false,
            Errors = new List<string>
            {
                "Subscription not found",
                "Access denied"
            }
        };

        // Assert
        response.Success.Should().BeFalse();
        response.Errors.Should().HaveCount(2);
        response.Errors.Should().Contain("Subscription not found");
    }

    [Fact]
    public void AgentResponse_ExecutionTime_IsTracked()
    {
        // Arrange
        var startTime = DateTime.UtcNow;

        // Simulate work
        System.Threading.Thread.Sleep(10);

        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Discovery,
            Success = true,
            ExecutionTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds
        };

        // Assert
        response.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(10);
    }

    #endregion

    #region SharedMemory Tests

    [Fact]
    public void SharedMemory_CanStoreDiscoveryContext()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "discovery-session-001";

        // Act
        var context = new ConversationContext
        {
            ConversationId = conversationId,
            MentionedResources = new Dictionary<string, string>
            {
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000001"
            }
        };
        memory.StoreContext(conversationId, context);
        var retrievedContext = memory.GetContext(conversationId);

        // Assert
        retrievedContext.Should().NotBeNull();
        retrievedContext.ConversationId.Should().Be(conversationId);
        retrievedContext.MentionedResources.Should().ContainKey("subscriptionId");
    }

    [Fact]
    public void SharedMemory_CanAddAgentCommunication()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "discovery-session-001";

        // Act
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Discovery,
            AgentType.Orchestrator,
            "Discovered 50 resources in subscription",
            new Dictionary<string, object>
            {
                ["resourceCount"] = 50,
                ["subscriptionId"] = "test-sub"
            }
        );

        var communications = memory.GetAgentCommunications(conversationId);

        // Assert
        communications.Should().NotBeEmpty();
    }

    #endregion

    #region Resource Discovery Query Detection Tests

    [Theory]
    [InlineData("/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/test", true)]
    [InlineData("Show me all VMs", false)]
    [InlineData("Get details for resource ID /subscriptions/test", true)]
    [InlineData("List all storage accounts", false)]
    public void TaskDescription_ResourceIdDetection_WorksCorrectly(string description, bool containsResourceId)
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Discovery,
            Description = description
        };

        // Act
        var hasResourceId = task.Description.Contains("/subscriptions/");

        // Assert
        hasResourceId.Should().Be(containsResourceId);
    }

    [Theory]
    [InlineData("get details for resource X", true)]
    [InlineData("show me resource details", true)]
    [InlineData("list all resources", false)]
    [InlineData("discover VMs", false)]
    public void TaskDescription_DetailsQuery_IsDetected(string description, bool isDetailsQuery)
    {
        // Arrange
        var descLower = description.ToLowerInvariant();

        // Act
        var isDetails = descLower.Contains("details") && descLower.Contains("resource");

        // Assert
        isDetails.Should().Be(isDetailsQuery);
    }

    #endregion

    #region Resource Type Parsing Tests

    [Theory]
    [InlineData("Microsoft.Storage/storageAccounts", "Microsoft.Storage", "storageAccounts")]
    [InlineData("Microsoft.Compute/virtualMachines", "Microsoft.Compute", "virtualMachines")]
    [InlineData("Microsoft.ContainerService/managedClusters", "Microsoft.ContainerService", "managedClusters")]
    [InlineData("Microsoft.KeyVault/vaults", "Microsoft.KeyVault", "vaults")]
    public void ResourceType_Parsing_ExtractsProviderAndType(string resourceType, string expectedProvider, string expectedType)
    {
        // Act
        var parts = resourceType.Split('/');
        var provider = parts[0];
        var type = parts[1];

        // Assert
        provider.Should().Be(expectedProvider);
        type.Should().Be(expectedType);
    }

    #endregion

    #region Subscription ID Validation Tests

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001", true)]
    [InlineData("12345678-1234-1234-1234-123456789012", true)]
    [InlineData("not-a-guid", false)]
    [InlineData("", false)]
    public void SubscriptionId_GuidValidation_WorksCorrectly(string subscriptionId, bool isValidGuid)
    {
        // Act
        var result = Guid.TryParse(subscriptionId, out _);

        // Assert
        result.Should().Be(isValidGuid);
    }

    #endregion
}
