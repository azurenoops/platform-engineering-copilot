using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Orchestration;
using Platform.Engineering.Copilot.State.Models;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Orchestration;

/// <summary>
/// Integration tests for PlatformAgentGroupChat orchestration.
/// Tests multi-agent coordination, selection, and termination strategies.
/// </summary>
[Trait("Category", "Integration")]
public class PlatformAgentGroupChatIntegrationTests
{
    #region Selection Strategy Integration Tests

    [Fact]
    public async Task SelectionStrategy_WithMultipleAgents_SelectsCorrectAgentAsync()
    {
        // Arrange
        var chatClientMock = new Mock<IChatClient>();
        var loggerMock = new Mock<ILogger<PlatformSelectionStrategy>>();
        var strategy = new PlatformSelectionStrategy(chatClientMock.Object, loggerMock.Object);
        
        var agents = CreateAgentCollection();
        var context = new AgentConversationContext
        {
            ConversationId = "integration-test-001",
            MessageHistory = new List<ConversationMessage>
            {
                new ConversationMessage
                {
                    Role = MessageRole.User,
                    Content = "Generate a Bicep template for AKS cluster"
                }
            }
        };

        // Act
        var selectedAgent = await strategy.SelectAgentAsync(
            agents,
            "Generate a Bicep template for AKS cluster",
            context);

        // Assert
        selectedAgent.Should().NotBeNull();
        selectedAgent!.Name.Should().Contain("Infrastructure");
    }

    [Fact]
    public async Task SelectionStrategy_WithHandoffContext_RoutesToHandoffAgentAsync()
    {
        // Arrange
        var chatClientMock = new Mock<IChatClient>();
        var loggerMock = new Mock<ILogger<PlatformSelectionStrategy>>();
        var strategy = new PlatformSelectionStrategy(chatClientMock.Object, loggerMock.Object);
        
        var agents = CreateAgentCollection();
        var context = new AgentConversationContext
        {
            ConversationId = "integration-test-002",
            PreviousResponses = new List<AgentResponse>
            {
                new AgentResponse
                {
                    Success = true,
                    AgentName = "Infrastructure Agent",
                    Content = "Template generated. Please verify compliance.",
                    RequiresHandoff = true,
                    HandoffTarget = "Compliance Agent"
                }
            }
        };

        // Act
        var selectedAgent = await strategy.SelectAgentAsync(
            agents,
            "Proceed with the next step",
            context);

        // Assert
        selectedAgent.Should().NotBeNull();
        selectedAgent!.Name.Should().Contain("Compliance");
    }

    #endregion

    #region Termination Strategy Integration Tests

    [Fact]
    public async Task TerminationStrategy_AfterSuccessfulResponse_TerminatesAsync()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PlatformTerminationStrategy>>();
        var strategy = new PlatformTerminationStrategy(loggerMock.Object);
        
        var responses = new List<AgentResponse>
        {
            new AgentResponse
            {
                Success = true,
                AgentName = "Compliance Agent",
                Content = "Compliance assessment completed successfully. All controls passed.",
                RequiresHandoff = false
            }
        };

        var context = new AgentConversationContext
        {
            ConversationId = "integration-test-003"
        };

        // Act
        var shouldTerminate = await strategy.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeTrue();
    }

    [Fact]
    public async Task TerminationStrategy_WithPendingHandoff_ContinuesOrchestrationAsync()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PlatformTerminationStrategy>>();
        var strategy = new PlatformTerminationStrategy(loggerMock.Object);
        
        var responses = new List<AgentResponse>
        {
            new AgentResponse
            {
                Success = true,
                AgentName = "Infrastructure Agent",
                Content = "Template generated. Handing off to Compliance Agent.",
                RequiresHandoff = true,
                HandoffTarget = "Compliance Agent"
            }
        };

        var context = new AgentConversationContext
        {
            ConversationId = "integration-test-004"
        };

        // Act
        var shouldTerminate = await strategy.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeFalse();
    }

    #endregion

    #region Multi-Agent Workflow Integration Tests

    [Fact]
    public async Task MultiAgentWorkflow_InfrastructureToCompliance_CompletesSuccessfullyAsync()
    {
        // Arrange
        var chatClientMock = new Mock<IChatClient>();
        var selectionLoggerMock = new Mock<ILogger<PlatformSelectionStrategy>>();
        var terminationLoggerMock = new Mock<ILogger<PlatformTerminationStrategy>>();
        
        var selectionStrategy = new PlatformSelectionStrategy(chatClientMock.Object, selectionLoggerMock.Object);
        var terminationStrategy = new PlatformTerminationStrategy(terminationLoggerMock.Object);
        
        var agents = CreateAgentCollection();
        var context = new AgentConversationContext
        {
            ConversationId = "integration-workflow-001",
            MessageHistory = new List<ConversationMessage>(),
            PreviousResponses = new List<AgentResponse>()
        };

        // Step 1: User requests infrastructure template
        var infrastructureAgent = await selectionStrategy.SelectAgentAsync(
            agents,
            "Create a compliant AKS template",
            context);

        infrastructureAgent.Should().NotBeNull();
        infrastructureAgent!.Name.Should().Contain("Infrastructure");

        // Simulate infrastructure response with handoff
        var infrastructureResponse = new AgentResponse
        {
            Success = true,
            AgentName = "Infrastructure Agent",
            Content = "AKS template generated with FedRAMP High settings.",
            RequiresHandoff = true,
            HandoffTarget = "Compliance Agent"
        };
        context.PreviousResponses.Add(infrastructureResponse);

        // Step 2: Check if orchestration should continue
        var shouldContinue = !await terminationStrategy.ShouldTerminateAsync(
            context.PreviousResponses.ToList(),
            context);
        
        shouldContinue.Should().BeTrue();

        // Step 3: Route to Compliance Agent
        var complianceAgent = await selectionStrategy.SelectAgentAsync(
            agents,
            "continue",
            context);

        complianceAgent.Should().NotBeNull();
        complianceAgent!.Name.Should().Contain("Compliance");

        // Simulate compliance response
        var complianceResponse = new AgentResponse
        {
            Success = true,
            AgentName = "Compliance Agent",
            Content = "Template verified. All NIST 800-53 controls are properly configured.",
            RequiresHandoff = false
        };
        context.PreviousResponses.Add(complianceResponse);

        // Step 4: Orchestration should now terminate
        var shouldTerminate = await terminationStrategy.ShouldTerminateAsync(
            context.PreviousResponses.ToList(),
            context);

        shouldTerminate.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static List<BaseAgent> CreateAgentCollection()
    {
        var agents = new List<BaseAgent>();
        
        agents.Add(CreateMockAgent("Infrastructure Agent", "Generates Azure infrastructure templates"));
        agents.Add(CreateMockAgent("Compliance Agent", "Runs NIST 800-53 compliance assessments"));
        agents.Add(CreateMockAgent("Cost Management Agent", "Analyzes Azure costs and budgets"));
        agents.Add(CreateMockAgent("Discovery Agent", "Discovers Azure resources"));
        agents.Add(CreateMockAgent("KnowledgeBase Agent", "Explains compliance controls and frameworks"));
        agents.Add(CreateMockAgent("Configuration Agent", "Manages subscription and environment settings"));

        return agents;
    }

    private static BaseAgent CreateMockAgent(string name, string description)
    {
        var mockAgent = new Mock<BaseAgent>(
            new Mock<IChatClient>().Object,
            new Mock<ILogger>().Object);
        
        // Note: Name property is not virtual (Name => AgentName), so we mock AgentName
        // which is virtual and Name will automatically return the same value
        mockAgent.Setup(a => a.AgentName).Returns(name);
        mockAgent.Setup(a => a.Description).Returns(description);
        
        return mockAgent.Object;
    }

    #endregion
}
