using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Orchestration;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Orchestration;

/// <summary>
/// Unit tests for PlatformSelectionStrategy.
/// Tests fast-path agent selection, handoff routing, and LLM fallback.
/// </summary>
public class PlatformSelectionStrategyTests
{
    private readonly Mock<IChatClient> _chatClientMock;
    private readonly Mock<ILogger<PlatformSelectionStrategy>> _loggerMock;
    private readonly PlatformSelectionStrategy _sut;

    public PlatformSelectionStrategyTests()
    {
        _chatClientMock = new Mock<IChatClient>();
        _loggerMock = new Mock<ILogger<PlatformSelectionStrategy>>();
        _sut = new PlatformSelectionStrategy(_chatClientMock.Object, _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullChatClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PlatformSelectionStrategy(null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("chatClient");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PlatformSelectionStrategy(_chatClientMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Fast-Path Selection Tests

    [Theory]
    [InlineData("Generate a Bicep template for AKS", "Infrastructure")]
    [InlineData("Create a Terraform template for storage account", "Infrastructure")]
    [InlineData("Generate infrastructure as code for VM", "Infrastructure")]
    [InlineData("Create an AKS cluster template", "Infrastructure")]
    [InlineData("Generate Kubernetes deployment template", "Infrastructure")]
    [InlineData("Create virtual network with subnets", "Infrastructure")]
    public async Task SelectAgentAsync_WithInfrastructureIntent_SelectsInfrastructureAgent(string message, string expectedAgentKeyword)
    {
        // Arrange
        var agents = CreateMockAgentList();
        var context = new AgentConversationContext { ConversationId = "test-001" };

        // Act
        var selected = await _sut.SelectAgentAsync(agents, message, context);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Contain(expectedAgentKeyword);
    }

    [Theory]
    [InlineData("Run compliance scan for AC controls", "Compliance")]
    [InlineData("Check NIST 800-53 compliance", "Compliance")]
    [InlineData("Generate SSP documentation", "Compliance")]
    [InlineData("Run FedRAMP assessment", "Compliance")]
    [InlineData("Remediate compliance findings", "Compliance")]
    [InlineData("Create ATO package", "Compliance")]
    public async Task SelectAgentAsync_WithComplianceIntent_SelectsComplianceAgent(string message, string expectedAgentKeyword)
    {
        // Arrange
        var agents = CreateMockAgentList();
        var context = new AgentConversationContext { ConversationId = "test-002" };

        // Act
        var selected = await _sut.SelectAgentAsync(agents, message, context);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Contain(expectedAgentKeyword);
    }

    [Theory]
    [InlineData("Show my Azure costs", "Cost")]
    [InlineData("What is my monthly spending?", "Cost")]
    [InlineData("Create a budget for this subscription", "Cost")]
    [InlineData("Show cost breakdown by service", "Cost")]
    [InlineData("Cost optimization recommendations", "Cost")]
    public async Task SelectAgentAsync_WithCostIntent_SelectsCostManagementAgent(string message, string expectedAgentKeyword)
    {
        // Arrange
        var agents = CreateMockAgentList();
        var context = new AgentConversationContext { ConversationId = "test-003" };

        // Act
        var selected = await _sut.SelectAgentAsync(agents, message, context);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Contain(expectedAgentKeyword);
    }

    [Theory]
    [InlineData("List all VMs in my subscription", "Discovery")]
    [InlineData("Find all storage accounts", "Discovery")]
    [InlineData("Show me all resources in resource group", "Discovery")]
    [InlineData("What resources do I have?", "Discovery")]
    [InlineData("Inventory all resources", "Discovery")]
    [InlineData("List my subscriptions", "Discovery")]
    [InlineData("List my Azure subscriptions", "Discovery")]
    [InlineData("Show subscriptions", "Discovery")]
    [InlineData("What subscriptions do I have?", "Discovery")]
    [InlineData("List all my Azure subscriptions", "Discovery")]
    public async Task SelectAgentAsync_WithDiscoveryIntent_SelectsDiscoveryAgent(string message, string expectedAgentKeyword)
    {
        // Arrange
        var agents = CreateMockAgentList();
        var context = new AgentConversationContext { ConversationId = "test-004" };

        // Act
        var selected = await _sut.SelectAgentAsync(agents, message, context);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Contain(expectedAgentKeyword);
    }

    [Theory]
    [InlineData("Explain AC-2 control", "Knowledge")]
    [InlineData("What is NIST control SC-7?", "Knowledge")]
    [InlineData("Tell me about STIG requirements", "Knowledge")]
    [InlineData("Explain the Risk Management Framework", "Knowledge")]
    [InlineData("What does the AC family control?", "Knowledge")]
    public async Task SelectAgentAsync_WithKnowledgeIntent_SelectsKnowledgeBaseAgent(string message, string expectedAgentKeyword)
    {
        // Arrange
        var agents = CreateMockAgentList();
        var context = new AgentConversationContext { ConversationId = "test-005" };

        // Act
        var selected = await _sut.SelectAgentAsync(agents, message, context);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Contain(expectedAgentKeyword);
    }

    [Theory]
    [InlineData("Set my subscription to abc123", "Configuration")]
    [InlineData("Use subscription xyz789", "Configuration")]
    [InlineData("Configure subscription settings", "Configuration")]
    [InlineData("Show my current subscription", "Configuration")]
    public async Task SelectAgentAsync_WithConfigurationIntent_SelectsConfigurationAgent(string message, string expectedAgentKeyword)
    {
        // Arrange
        var agents = CreateMockAgentList();
        var context = new AgentConversationContext { ConversationId = "test-006" };

        // Act
        var selected = await _sut.SelectAgentAsync(agents, message, context);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Contain(expectedAgentKeyword);
    }

    #endregion

    #region Handoff Routing Tests

    [Fact]
    public async Task SelectAgentAsync_WithHandoffTarget_RoutesToHandoffAgent()
    {
        // Arrange
        var agents = CreateMockAgentList();
        var context = new AgentConversationContext
        {
            ConversationId = "test-handoff-001",
            PreviousResponses = new List<AgentResponse>
            {
                new AgentResponse
                {
                    Success = true,
                    AgentName = "Infrastructure Agent",
                    Content = "Template generated. Handing off to Compliance Agent.",
                    RequiresHandoff = true,
                    HandoffTarget = "Compliance Agent"
                }
            }
        };

        // Act - use a generic message that wouldn't normally route to Compliance
        var selected = await _sut.SelectAgentAsync(agents, "continue", context);

        // Assert
        selected.Should().NotBeNull();
        selected!.Name.Should().Contain("Compliance");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SelectAgentAsync_WithEmptyAgentList_ReturnsNull()
    {
        // Arrange
        var emptyAgents = new List<BaseAgent>();
        var context = new AgentConversationContext { ConversationId = "test-empty" };

        // Act
        var selected = await _sut.SelectAgentAsync(emptyAgents, "any message", context);

        // Assert
        selected.Should().BeNull();
    }

    [Fact]
    public async Task SelectAgentAsync_WithAmbiguousMessage_UsesLLMFallback()
    {
        // Arrange
        var agents = CreateMockAgentList();
        var context = new AgentConversationContext { ConversationId = "test-ambiguous" };
        
        // Setup LLM mock to return a specific agent
        var mockResponse = new Mock<ChatResponse>();
        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act - this message doesn't match any fast-path pattern
        var selected = await _sut.SelectAgentAsync(agents, "hello world", context);

        // Assert - should return first agent as fallback
        selected.Should().NotBeNull();
    }

    #endregion

    #region Helpers

    private List<BaseAgent> CreateMockAgentList()
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

    private BaseAgent CreateMockAgent(string name, string description)
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
