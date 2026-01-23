using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Orchestration;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Orchestration;

/// <summary>
/// Unit tests for PlatformTerminationStrategy.
/// Tests termination conditions for multi-agent orchestration.
/// </summary>
public class PlatformTerminationStrategyTests
{
    private readonly Mock<ILogger<PlatformTerminationStrategy>> _loggerMock;
    private readonly PlatformTerminationStrategy _sut;

    public PlatformTerminationStrategyTests()
    {
        _loggerMock = new Mock<ILogger<PlatformTerminationStrategy>>();
        _sut = new PlatformTerminationStrategy(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PlatformTerminationStrategy(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Assert
        _sut.MaxConsecutiveResponses.Should().Be(5);
        _sut.MaxTotalResponses.Should().Be(10);
    }

    #endregion

    #region No Responses Tests

    [Fact]
    public async Task ShouldTerminateAsync_WithNoResponses_ReturnsFalse()
    {
        // Arrange
        var responses = new List<AgentResponse>();
        var context = new AgentConversationContext { ConversationId = "test-001" };

        // Act
        var shouldTerminate = await _sut.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeFalse();
    }

    #endregion

    #region Successful Response Tests

    [Fact]
    public async Task ShouldTerminateAsync_WithSuccessfulResponseNoHandoff_ReturnsTrue()
    {
        // Arrange
        var responses = new List<AgentResponse>
        {
            new AgentResponse
            {
                Success = true,
                AgentName = "Compliance Agent",
                Content = "Assessment completed successfully",
                RequiresHandoff = false
            }
        };
        var context = new AgentConversationContext { ConversationId = "test-002" };

        // Act
        var shouldTerminate = await _sut.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldTerminateAsync_WithSuccessfulResponseAndHandoff_ReturnsFalse()
    {
        // Arrange
        var responses = new List<AgentResponse>
        {
            new AgentResponse
            {
                Success = true,
                AgentName = "Infrastructure Agent",
                Content = "Template generated",
                RequiresHandoff = true,
                HandoffTarget = "Compliance Agent"
            }
        };
        var context = new AgentConversationContext { ConversationId = "test-003" };

        // Act
        var shouldTerminate = await _sut.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeFalse();
    }

    #endregion

    #region Max Responses Tests

    [Fact]
    public async Task ShouldTerminateAsync_WhenMaxTotalResponsesReached_ReturnsTrue()
    {
        // Arrange
        _sut.MaxTotalResponses = 3;
        var responses = Enumerable.Range(1, 3)
            .Select(i => new AgentResponse
            {
                Success = true,
                AgentName = $"Agent {i}",
                Content = $"Response {i}",
                RequiresHandoff = true
            })
            .ToList();
        var context = new AgentConversationContext { ConversationId = "test-max" };

        // Act
        var shouldTerminate = await _sut.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldTerminateAsync_WhenSameAgentRespondsConsecutively_ReturnsTrue()
    {
        // Arrange
        _sut.MaxConsecutiveResponses = 3;
        var responses = Enumerable.Range(1, 3)
            .Select(i => new AgentResponse
            {
                Success = true,
                AgentName = "Same Agent", // Same agent each time
                Content = $"Response {i}",
                RequiresHandoff = true
            })
            .ToList();
        var context = new AgentConversationContext { ConversationId = "test-consecutive" };

        // Act
        var shouldTerminate = await _sut.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldTerminateAsync_WhenDifferentAgentsRespond_ReturnsFalse()
    {
        // Arrange
        _sut.MaxConsecutiveResponses = 3;
        var responses = new List<AgentResponse>
        {
            new AgentResponse { Success = true, AgentName = "Agent A", Content = "Response 1", RequiresHandoff = true },
            new AgentResponse { Success = true, AgentName = "Agent B", Content = "Response 2", RequiresHandoff = true },
            new AgentResponse { Success = true, AgentName = "Agent C", Content = "Response 3", RequiresHandoff = true }
        };
        var context = new AgentConversationContext { ConversationId = "test-different" };

        // Act
        var shouldTerminate = await _sut.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeFalse();
    }

    #endregion

    #region All Failed Tests

    [Fact]
    public async Task ShouldTerminateAsync_WhenAllResponsesFailed_ReturnsTrue()
    {
        // Arrange
        var responses = new List<AgentResponse>
        {
            new AgentResponse { Success = false, AgentName = "Agent A", Content = "Error 1" },
            new AgentResponse { Success = false, AgentName = "Agent B", Content = "Error 2" }
        };
        var context = new AgentConversationContext { ConversationId = "test-failed" };

        // Act
        var shouldTerminate = await _sut.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeTrue();
    }

    #endregion

    #region Completion Keywords Tests

    [Theory]
    [InlineData("Task completed successfully")]
    [InlineData("Operation complete")]
    [InlineData("Successfully completed the assessment")]
    public async Task ShouldTerminateAsync_WithCompletionKeyword_ReturnsTrue(string content)
    {
        // Arrange
        var responses = new List<AgentResponse>
        {
            new AgentResponse
            {
                Success = true,
                AgentName = "Test Agent",
                Content = content,
                RequiresHandoff = true // Even with handoff, completion keyword terminates
            }
        };
        var context = new AgentConversationContext { ConversationId = "test-keyword" };

        // Act
        var shouldTerminate = await _sut.ShouldTerminateAsync(responses, context);

        // Assert
        shouldTerminate.Should().BeTrue();
    }

    #endregion
}
