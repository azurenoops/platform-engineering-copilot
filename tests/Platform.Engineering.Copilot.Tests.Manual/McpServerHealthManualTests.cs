using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual;

/// <summary>
/// Manual tests for MCP Server health, connectivity, and basic functionality.
/// These tests verify the MCP HTTP API is running and responding correctly.
/// 
/// Prerequisites:
/// - MCP server running in HTTP mode: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5100
/// </summary>
public class McpServerHealthManualTests : McpHttpTestBase
{

    public McpServerHealthManualTests(ITestOutputHelper output) : base(output) { }

    #region Health Checks

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task HealthCheck_ShouldReturnHealthyStatus()
    {
        // Act
        var health = await CheckHealthAsync();

        // Assert
        health.Should().NotBeNull();
        health.Status.Should().Be("healthy");
        health.Server.Should().Contain("Platform Engineering Copilot");
        health.Mode.Should().NotBeNullOrEmpty();
        health.Version.Should().NotBeNullOrEmpty();

        Output.WriteLine($"Server: {health.Server}");
        Output.WriteLine($"Mode: {health.Mode}");
        Output.WriteLine($"Version: {health.Version}");
        Output.WriteLine($"Status: {health.Status}");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task HealthCheck_ShouldRespondWithinTimeout()
    {
        // Arrange
        var startTime = DateTime.UtcNow;

        // Act
        var health = await CheckHealthAsync();
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        health.Status.Should().Be("healthy");
        elapsed.TotalMilliseconds.Should().BeLessThan(5000, "Health check should respond within 5 seconds");

        Output.WriteLine($"Health check response time: {elapsed.TotalMilliseconds:F0}ms");
    }

    #endregion

    #region Basic Chat Functionality

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task ChatEndpoint_SimpleMessage_ShouldReturnResponse()
    {
        // Arrange
        var message = "Hello, how can you help me with Azure?";

        // Act
        var response = await SendChatRequestAsync(message, "health-chat-001");

        // Assert
        response.Success.Should().BeTrue();
        response.Response.Should().NotBeNullOrEmpty();
        response.ConversationId.Should().NotBeNullOrEmpty();
        response.ProcessingTimeMs.Should().BeGreaterThan(0);

        Output.WriteLine($"Response length: {response.Response?.Length ?? 0} chars");
        Output.WriteLine($"Processing time: {response.ProcessingTimeMs}ms");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task ChatEndpoint_WithNewConversationId_ShouldCreateSession()
    {
        // Arrange
        var uniqueConversationId = $"new-session-{Guid.NewGuid():N}";
        var message = "Start a new conversation about infrastructure";

        // Act
        var response = await SendChatRequestAsync(message, uniqueConversationId);

        // Assert
        response.Success.Should().BeTrue();
        response.ConversationId.Should().Be(uniqueConversationId);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task ChatEndpoint_WithoutConversationId_ShouldGenerateOne()
    {
        // Arrange
        var request = new McpChatRequest { Message = "Test message without conversation ID" };

        // Act
        var response = await SendChatRequestAsync(request.Message);

        // Assert
        response.Success.Should().BeTrue();
        response.ConversationId.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Response Structure Validation

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task ChatResponse_ShouldContainRequiredFields()
    {
        // Arrange
        var message = "List the available agents";

        // Act
        var response = await SendChatRequestAsync(message, "health-fields-001");

        // Assert
        response.Should().NotBeNull();
        
        // Required fields
        response.Success.Should().BeTrue();
        response.Response.Should().NotBeNull();
        response.ConversationId.Should().NotBeNullOrEmpty();
        
        // Optional but expected fields
        response.ProcessingTimeMs.Should().BeGreaterOrEqualTo(0);
        
        Output.WriteLine($"Success: {response.Success}");
        Output.WriteLine($"IntentType: {response.IntentType}");
        Output.WriteLine($"Confidence: {response.Confidence}");
        Output.WriteLine($"ToolExecuted: {response.ToolExecuted}");
        Output.WriteLine($"RequiresFollowUp: {response.RequiresFollowUp}");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task ChatResponse_ShouldIncludeSuggestions()
    {
        // Arrange
        var message = "Show me my Azure resources";

        // Act
        var response = await SendChatRequestAsync(message, "health-suggestions-001");

        // Assert
        response.Success.Should().BeTrue();
        
        if (response.Suggestions?.Any() == true)
        {
            Output.WriteLine($"Found {response.Suggestions.Count} suggestions:");
            foreach (var suggestion in response.Suggestions)
            {
                Output.WriteLine($"  - {suggestion.Title}: {suggestion.Description}");
            }
        }
        else
        {
            Output.WriteLine("No suggestions provided (this may be expected)");
        }
    }

    #endregion

    #region Performance Tests

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task Performance_SimpleRequest_ShouldRespondQuickly()
    {
        // Arrange
        var message = "What time is it?";
        var maxExpectedMs = 10000; // 10 seconds for simple request

        // Act
        var response = await SendChatRequestAsync(message, "perf-simple-001");

        // Assert
        response.Success.Should().BeTrue();
        response.ProcessingTimeMs.Should().BeLessThan(maxExpectedMs,
            $"Simple request should complete within {maxExpectedMs}ms");

        Output.WriteLine($"Simple request processing time: {response.ProcessingTimeMs}ms");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task Performance_MultipleSequentialRequests_ShouldMaintainPerformance()
    {
        // Arrange
        var messages = new[]
        {
            "List storage accounts",
            "Show virtual machines",
            "Check compliance status"
        };
        var processingTimes = new List<long>();

        // Act
        foreach (var message in messages)
        {
            var response = await SendChatRequestAsync(message, $"perf-seq-{Guid.NewGuid():N}");
            response.Success.Should().BeTrue();
            processingTimes.Add(response.ProcessingTimeMs);
        }

        // Assert
        var avgTime = processingTimes.Average();
        var maxTime = processingTimes.Max();
        
        Output.WriteLine($"Processing times: {string.Join(", ", processingTimes.Select(t => $"{t}ms"))}");
        Output.WriteLine($"Average: {avgTime:F0}ms, Max: {maxTime}ms");
        
        maxTime.Should().BeLessThan(60000, "No request should take more than 60 seconds");
    }

    #endregion

    #region Error Handling

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task ErrorHandling_EmptyMessage_ShouldHandleGracefully()
    {
        // Arrange
        var message = "";

        // Act & Assert
        try
        {
            var response = await SendChatRequestAsync(message, "error-empty-001");
            
            // Server might accept empty message or return error
            Output.WriteLine($"Server accepted empty message. Success: {response.Success}");
        }
        catch (HttpRequestException ex)
        {
            Output.WriteLine($"Server rejected empty message: {ex.Message}");
            // This is acceptable behavior
        }
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task ErrorHandling_VeryLongMessage_ShouldHandleGracefully()
    {
        // Arrange
        var longMessage = new string('x', 50000); // 50KB message

        // Act
        try
        {
            var response = await SendChatRequestAsync(longMessage, "error-long-001");
            Output.WriteLine($"Server handled long message. Success: {response.Success}");
        }
        catch (HttpRequestException ex)
        {
            Output.WriteLine($"Server rejected long message (expected): {ex.Message}");
        }
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Component", "McpServer")]
    public async Task ErrorHandling_SpecialCharacters_ShouldHandleGracefully()
    {
        // Arrange
        var messageWithSpecialChars = "Test with special chars: <script>alert('xss')</script> & \"quotes\" 'apostrophes' \n newlines \t tabs";

        // Act
        var response = await SendChatRequestAsync(messageWithSpecialChars, "error-special-001");

        // Assert
        response.Success.Should().BeTrue();
        Output.WriteLine("Server handled special characters correctly");
    }

    #endregion
}
