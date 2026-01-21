using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.State.Abstractions;
using Platform.Engineering.Copilot.State.Models;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for BaseAgent base class functionality.
/// Tests common agent behaviors shared across all specialized agents.
/// </summary>
public class BaseAgentTests
{
    private readonly Mock<IChatClient> _chatClientMock;
    private readonly Mock<ILogger> _loggerMock;

    public BaseAgentTests()
    {
        _chatClientMock = new Mock<IChatClient>();
        _loggerMock = new Mock<ILogger>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);

        // Assert
        agent.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullChatClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TestableAgent(null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("chatClient");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TestableAgent(_chatClientMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region AgentId Tests

    [Fact]
    public void AgentId_ReturnsLowercaseNameWithoutAgentSuffix()
    {
        // Arrange
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);

        // Act
        var agentId = agent.AgentId;

        // Assert
        agentId.Should().Be("testable");
    }

    #endregion

    #region AgentName Tests

    [Fact]
    public void AgentName_ReturnsClassName()
    {
        // Arrange
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);

        // Act
        var agentName = agent.AgentName;

        // Assert
        agentName.Should().Be("TestableAgent");
    }

    [Fact]
    public void Name_IsAliasForAgentName()
    {
        // Arrange
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);

        // Assert
        agent.Name.Should().Be(agent.AgentName);
    }

    #endregion

    #region RegisterTool Tests

    [Fact]
    public void RegisterTool_AddsTool_ToRegisteredToolsList()
    {
        // Arrange
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);
        var mockTool = new Mock<BaseTool>(new Mock<ILogger>().Object);
        mockTool.Setup(t => t.Name).Returns("test_tool");

        // Act
        agent.PublicRegisterTool(mockTool.Object);

        // Assert
        agent.GetAITools().Should().ContainSingle();
    }

    #endregion

    #region GetAITools Tests

    [Fact]
    public void GetAITools_WithNoToolsRegistered_ReturnsEmptyCollection()
    {
        // Arrange
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);

        // Act
        var tools = agent.GetAITools();

        // Assert
        tools.Should().BeEmpty();
    }

    #endregion

    #region MaxToolRounds Tests

    [Fact]
    public void MaxToolRounds_DefaultValue_IsFive()
    {
        // Arrange
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);

        // Act
        var maxRounds = agent.GetMaxToolRounds();

        // Assert
        maxRounds.Should().Be(5);
    }

    [Fact]
    public void MaxToolRounds_WithCustomValue_ReturnsCustomValue()
    {
        // Arrange
        var agent = new CustomMaxRoundsAgent(_chatClientMock.Object, _loggerMock.Object, maxRounds: 10);

        // Act
        var maxRounds = agent.GetMaxToolRounds();

        // Assert
        maxRounds.Should().Be(10);
    }

    #endregion

    #region ToolMode Tests

    [Fact]
    public void ToolMode_DefaultValue_IsAuto()
    {
        // Arrange
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);

        // Act
        var toolMode = agent.GetToolMode();

        // Assert
        toolMode.Should().Be(ChatToolMode.Auto);
    }

    [Fact]
    public void ToolMode_WithRequireAny_ReturnsRequireAny()
    {
        // Arrange
        var agent = new RequireToolsAgent(_chatClientMock.Object, _loggerMock.Object);

        // Act
        var toolMode = agent.GetToolMode();

        // Assert
        toolMode.Should().Be(ChatToolMode.RequireAny);
    }

    #endregion

    #region ProcessAsync Multi-Round Tests

    [Fact]
    public async Task ProcessAsync_WithNoToolCalls_ReturnsResponseInOneRound()
    {
        // Arrange
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);
        var context = CreateTestContext("Hello");
        
        var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello there!"));
        _chatClientMock
            .Setup(x => x.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        // Act
        var response = await agent.ProcessAsync(context);

        // Assert
        response.Success.Should().BeTrue();
        response.Content.Should().Be("Hello there!");
        _chatClientMock.Verify(x => x.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var agent = new TestableAgent(_chatClientMock.Object, _loggerMock.Object);
        var context = CreateTestContext("Hello");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => agent.ProcessAsync(context, cts.Token));
    }

    #endregion

    /// <summary>
    /// Testable implementation of BaseAgent for unit testing
    /// </summary>
    private class TestableAgent : BaseAgent
    {
        public override string Description => "Testable agent for unit tests";

        public TestableAgent(IChatClient chatClient, ILogger logger)
            : base(chatClient, logger)
        {
        }

        protected override string GetSystemPrompt() => "You are a test agent for unit testing purposes.";

        public void PublicRegisterTool(BaseTool tool) => RegisterTool(tool);
        
        public int GetMaxToolRounds() => MaxToolRounds;
        
        public ChatToolMode GetToolMode() => ToolMode;
    }

    /// <summary>
    /// Agent with custom MaxToolRounds for testing
    /// </summary>
    private class CustomMaxRoundsAgent : BaseAgent
    {
        private readonly int _maxRounds;
        
        public override string Description => "Agent with custom max rounds";
        protected override int MaxToolRounds => _maxRounds;

        public CustomMaxRoundsAgent(IChatClient chatClient, ILogger logger, int maxRounds)
            : base(chatClient, logger)
        {
            _maxRounds = maxRounds;
        }

        protected override string GetSystemPrompt() => "Test agent";
        
        public int GetMaxToolRounds() => MaxToolRounds;
    }

    /// <summary>
    /// Agent that requires tool usage
    /// </summary>
    private class RequireToolsAgent : BaseAgent
    {
        public override string Description => "Agent that requires tools";
        protected override ChatToolMode ToolMode => ChatToolMode.RequireAny;

        public RequireToolsAgent(IChatClient chatClient, ILogger logger)
            : base(chatClient, logger)
        {
        }

        protected override string GetSystemPrompt() => "Test agent";
        
        public ChatToolMode GetToolMode() => ToolMode;
    }

    private static AgentConversationContext CreateTestContext(string message)
    {
        return new AgentConversationContext
        {
            ConversationId = Guid.NewGuid().ToString(),
            MessageHistory = new List<ConversationMessage>
            {
                new() { Content = message, IsUser = true, Timestamp = DateTime.UtcNow }
            }
        };
    }
}
