using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Common;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Common;

/// <summary>
/// Unit tests for BaseTool base class functionality.
/// Tests common tool behaviors shared across all tools.
/// </summary>
public class BaseToolTests
{
    private readonly Mock<ILogger> _loggerMock;

    public BaseToolTests()
    {
        _loggerMock = new Mock<ILogger>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var tool = new TestableTool(_loggerMock.Object);

        // Assert
        tool.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TestableTool(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Name and Description Tests

    [Fact]
    public void Name_ReturnsExpectedToolName()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);

        // Assert
        tool.Name.Should().Be("test_tool");
    }

    [Fact]
    public void Description_ReturnsExpectedDescription()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);

        // Assert
        tool.Description.Should().Be("A test tool for unit testing");
    }

    #endregion

    #region Parameters Tests

    [Fact]
    public void Parameters_WhenAdded_AreAccessible()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);

        // Assert
        tool.Parameters.Should().HaveCount(2);
        tool.Parameters.Should().Contain(p => p.Name == "required_param" && p.Required);
        tool.Parameters.Should().Contain(p => p.Name == "optional_param" && !p.Required);
    }

    #endregion

    #region AsAITool Tests

    [Fact]
    public void AsAITool_ReturnsAIToolWithCorrectName()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);

        // Act
        var aiTool = tool.AsAITool();

        // Assert
        aiTool.Should().NotBeNull();
        aiTool.Name.Should().Be("test_tool");
    }

    [Fact]
    public void AsAITool_ReturnsAIToolWithCorrectDescription()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);

        // Act
        var aiTool = tool.AsAITool();

        // Assert
        aiTool.Description.Should().Be("A test tool for unit testing");
    }

    #endregion

    #region Parameter Helper Tests

    [Fact]
    public async Task ExecuteAsync_WithRequiredParameter_ReturnsSuccess()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);
        var arguments = new Dictionary<string, object?>
        {
            ["required_param"] = "test-value"
        };

        // Act
        var result = await tool.ExecuteAsync(arguments);

        // Assert
        result.Should().Contain("test-value");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingRequiredParameter_ReturnsError()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);
        var arguments = new Dictionary<string, object?>();

        // Act
        var result = await tool.ExecuteAsync(arguments);

        // Assert
        result.Should().Contain("error");
        result.Should().Contain("required_param");
    }

    [Fact]
    public async Task ExecuteAsync_WithOptionalParameter_UsesDefaultWhenMissing()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);
        var arguments = new Dictionary<string, object?>
        {
            ["required_param"] = "test-value"
            // optional_param not provided
        };

        // Act
        var result = await tool.ExecuteAsync(arguments);

        // Assert
        result.Should().Contain("optional_value");
    }

    [Fact]
    public async Task ExecuteAsync_WithBoolParameter_ParsesCorrectly()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);
        var arguments = new Dictionary<string, object?>
        {
            ["required_param"] = "test",
            ["bool_param"] = "true"
        };

        // Act
        var result = await tool.ExecuteAsync(arguments);

        // Assert
        result.Should().Contain("\"boolValue\":true");
    }

    [Fact]
    public async Task ExecuteAsync_WithIntParameter_ParsesCorrectly()
    {
        // Arrange
        var tool = new TestableTool(_loggerMock.Object);
        var arguments = new Dictionary<string, object?>
        {
            ["required_param"] = "test",
            ["int_param"] = 42
        };

        // Act
        var result = await tool.ExecuteAsync(arguments);

        // Assert
        result.Should().Contain("\"intValue\":42");
    }

    #endregion

    /// <summary>
    /// Testable implementation of BaseTool for unit testing
    /// </summary>
    private class TestableTool : BaseTool
    {
        public override string Name => "test_tool";
        public override string Description => "A test tool for unit testing";

        public TestableTool(ILogger logger) : base(logger)
        {
            Parameters.Add(new ToolParameter("required_param", "A required string parameter", true));
            Parameters.Add(new ToolParameter("optional_param", "An optional string parameter", false));
        }

        public override Task<string> ExecuteAsync(
            IDictionary<string, object?> arguments,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var required = GetRequiredString(arguments, "required_param");
                var optional = GetOptionalString(arguments, "optional_param") ?? "optional_value";
                var boolValue = GetOptionalBool(arguments, "bool_param", false);
                var intValue = GetOptionalInt(arguments, "int_param");

                return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = true,
                    required,
                    optional,
                    boolValue,
                    intValue
                }));
            }
            catch (ArgumentException ex)
            {
                return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = ex.Message
                }));
            }
        }
    }
}
