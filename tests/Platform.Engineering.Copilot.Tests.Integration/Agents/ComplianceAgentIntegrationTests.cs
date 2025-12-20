using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// Integration tests for Compliance Agent subsystem
/// Tests agent coordination, shared memory, and end-to-end workflows
/// Marked with [Trait("Category", "Integration")] for filtering
/// </summary>
[Trait("Category", "Integration")]
public class ComplianceAgentIntegrationTests
{
    private SharedMemory CreateSharedMemory()
    {
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        return new SharedMemory(loggerMock.Object);
    }

    [Fact]
    public void SharedMemory_MultipleAgents_CanCommunicate()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "integration-test-001";

        // Act - Simulate Infrastructure Agent completing deployment
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Infrastructure,
            AgentType.Compliance,
            "Deployment completed: AKS cluster provisioned",
            new Dictionary<string, object>
            {
                ["resourceGroupName"] = "rg-test-aks",
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000001",
                ["resourceType"] = "Microsoft.ContainerService/managedClusters"
            }
        );

        // Simulate Compliance Agent receiving and processing
        var communications = memory.GetAgentCommunications(conversationId);
        var infraMessage = communications.FirstOrDefault(c => c.FromAgent == AgentType.Infrastructure);
        var infraData = infraMessage?.Data as Dictionary<string, object>;

        // Compliance Agent responds
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Compliance,
            AgentType.Orchestrator,
            "Compliance assessment initiated for newly deployed AKS cluster",
            new Dictionary<string, object>
            {
                ["assessmentId"] = Guid.NewGuid().ToString(),
                ["targetResourceGroup"] = infraData?["resourceGroupName"] ?? "unknown"
            }
        );

        // Assert
        var allCommunications = memory.GetAgentCommunications(conversationId);
        allCommunications.Should().HaveCount(2);
        allCommunications.Should().Contain(c => c.FromAgent == AgentType.Infrastructure);
        allCommunications.Should().Contain(c => c.FromAgent == AgentType.Compliance);
    }

    [Fact]
    public void SharedMemory_Context_PersistsAcrossAgentCalls()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "context-test-001";

        // Act - Set context with subscription info
        var context = new ConversationContext
        {
            ConversationId = conversationId,
            UserId = "test-user",
            WorkflowState = new Dictionary<string, object?>
            {
                ["lastSubscriptionId"] = "00000000-0000-0000-0000-000000000002",
                ["lastScanTimestamp"] = DateTime.UtcNow.AddMinutes(-30)
            }
        };
        memory.StoreContext(conversationId, context);

        // Assert - Retrieve context
        var retrievedContext = memory.GetContext(conversationId);
        retrievedContext.Should().NotBeNull();
        retrievedContext!.WorkflowState.Should().ContainKey("lastSubscriptionId");
        retrievedContext.WorkflowState["lastSubscriptionId"].Should().Be("00000000-0000-0000-0000-000000000002");
    }

    [Fact]
    public void AgentTask_WithParameters_CanBeCreatedAndProcessed()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Compliance,
            Description = "Run comprehensive NIST 800-53 compliance assessment",
            Priority = 1,
            IsCritical = true,
            ConversationId = "task-test-001",
            Parameters = new Dictionary<string, object>
            {
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000003",
                ["resourceGroupName"] = "rg-production",
                ["framework"] = "NIST80053",
                ["baseline"] = "FedRAMPHigh"
            }
        };

        // Assert
        task.TaskId.Should().NotBeNullOrEmpty();
        task.AgentType.Should().Be(AgentType.Compliance);
        task.Parameters.Should().HaveCount(4);
        task.Parameters["framework"].Should().Be("NIST80053");
    }

    [Fact]
    public void AgentResponse_WithMetadata_CanStoreComplexResults()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Compliance,
            Success = true,
            Content = "Compliance assessment completed successfully",
            ComplianceScore = 87,
            IsApproved = true,
            ExecutionTimeMs = 15000,
            Errors = new List<string>(),
            Metadata = new Dictionary<string, object>
            {
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000004",
                ["totalFindings"] = 15,
                ["criticalFindings"] = 0,
                ["highFindings"] = 2,
                ["mediumFindings"] = 8,
                ["lowFindings"] = 5,
                ["controlFamiliesAssessed"] = new[] { "AC", "AU", "CM", "IA", "SC", "SI" },
                ["assessmentTimestamp"] = DateTime.UtcNow
            }
        };

        // Assert
        response.Success.Should().BeTrue();
        response.ComplianceScore.Should().Be(87);
        response.IsApproved.Should().BeTrue();
        response.Metadata.Should().ContainKey("totalFindings");
        response.Metadata["totalFindings"].Should().Be(15);
        ((string[])response.Metadata["controlFamiliesAssessed"]).Should().HaveCount(6);
    }

    [Fact]
    public void ComplianceAgentOptions_CanBeConfiguredFromOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Configure options
        services.Configure<ComplianceAgentOptions>(options =>
        {
            options.Temperature = 0.1;
            options.MaxTokens = 8000;
            options.EnableAutomatedRemediation = false;
            options.DefaultFramework = "FedRAMPHigh";
            options.DefaultBaseline = "DoD IL5";
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<ComplianceAgentOptions>>();

        // Assert
        options.Value.Temperature.Should().Be(0.1);
        options.Value.MaxTokens.Should().Be(8000);
        options.Value.EnableAutomatedRemediation.Should().BeFalse();
        options.Value.DefaultFramework.Should().Be("FedRAMPHigh");
        options.Value.DefaultBaseline.Should().Be("DoD IL5");
    }

    [Fact]
    public void SharedMemory_ClearConversation_RemovesAllData()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "clear-test-001";

        // Set up context and communications
        var context = new ConversationContext
        {
            ConversationId = conversationId,
            UserId = "test-user"
        };
        memory.StoreContext(conversationId, context);
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Compliance,
            AgentType.Orchestrator,
            "Test message",
            new Dictionary<string, object>()
        );

        // Act
        memory.ClearConversation(conversationId);

        // Assert - After clearing, GetContext returns new empty context
        memory.HasContext(conversationId).Should().BeFalse();
    }

    [Fact]
    public void AgentCommunication_Timestamp_IsSetAutomatically()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "timestamp-test-001";
        var beforeTime = DateTime.UtcNow;

        // Act
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Compliance,
            AgentType.Orchestrator,
            "Test message",
            new Dictionary<string, object>()
        );

        var afterTime = DateTime.UtcNow;

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().HaveCount(1);
        var comm = communications.First();
        comm.Timestamp.Should().BeOnOrAfter(beforeTime);
        comm.Timestamp.Should().BeOnOrBefore(afterTime);
    }

    [Fact]
    public void ConversationContext_PreviousResults_CanStoreMultipleResponses()
    {
        // Arrange
        var context = new ConversationContext
        {
            ConversationId = "multi-result-test",
            UserId = "test-user",
            PreviousResults = new List<AgentResponse>()
        };

        // Act - Add multiple agent responses
        context.PreviousResults.Add(new AgentResponse
        {
            TaskId = "task-1",
            AgentType = AgentType.Discovery,
            Success = true,
            Content = "Discovered 50 resources"
        });

        context.PreviousResults.Add(new AgentResponse
        {
            TaskId = "task-2",
            AgentType = AgentType.Compliance,
            Success = true,
            Content = "Compliance score: 85%",
            ComplianceScore = 85
        });

        // Assert
        context.PreviousResults.Should().HaveCount(2);
        context.PreviousResults.Should().Contain(r => r.AgentType == AgentType.Discovery);
        context.PreviousResults.Should().Contain(r => r.AgentType == AgentType.Compliance);
    }

    [Theory]
    [InlineData(AgentType.Compliance, AgentType.Orchestrator)]
    [InlineData(AgentType.Compliance, AgentType.Infrastructure)]
    [InlineData(AgentType.Infrastructure, AgentType.Compliance)]
    [InlineData(AgentType.Discovery, AgentType.Compliance)]
    public void SharedMemory_Communication_SupportsDifferentAgentPairs(AgentType from, AgentType to)
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = $"agent-pair-{from}-{to}";

        // Act
        memory.AddAgentCommunication(
            conversationId,
            from,
            to,
            $"Message from {from} to {to}",
            new Dictionary<string, object> { ["testKey"] = "testValue" }
        );

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().HaveCount(1);
        communications.First().FromAgent.Should().Be(from);
        communications.First().ToAgent.Should().Be(to);
    }

    [Fact]
    public void ExecutionPlan_ForCompliance_CanBeConstructed()
    {
        // Arrange & Act
        var plan = new ExecutionPlan
        {
            PrimaryIntent = "Compliance assessment for subscription",
            ExecutionPattern = ExecutionPattern.Sequential,
            Tasks = new List<AgentTask>
            {
                new AgentTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    AgentType = AgentType.Discovery,
                    Description = "Discover resources in subscription",
                    Priority = 1
                },
                new AgentTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    AgentType = AgentType.Compliance,
                    Description = "Run compliance assessment on discovered resources",
                    Priority = 2
                }
            }
        };

        // Assert
        plan.Tasks.Should().HaveCount(2);
        plan.ExecutionPattern.Should().Be(ExecutionPattern.Sequential);
        plan.Tasks[0].AgentType.Should().Be(AgentType.Discovery);
        plan.Tasks[1].AgentType.Should().Be(AgentType.Compliance);
    }

    [Theory]
    [InlineData(ExecutionPattern.Sequential)]
    [InlineData(ExecutionPattern.Parallel)]
    [InlineData(ExecutionPattern.Collaborative)]
    public void ExecutionPattern_AllValuesAreValid(ExecutionPattern pattern)
    {
        // Arrange & Act
        var plan = new ExecutionPlan { ExecutionPattern = pattern };

        // Assert
        plan.ExecutionPattern.Should().Be(pattern);
    }
}
