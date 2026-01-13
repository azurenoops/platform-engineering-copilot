using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.State.Abstractions;
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
    }
}
