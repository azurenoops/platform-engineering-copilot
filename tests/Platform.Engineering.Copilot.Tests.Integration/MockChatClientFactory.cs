using Microsoft.Extensions.AI;
using Moq;

namespace Platform.Engineering.Copilot.Tests.Integration;

/// <summary>
/// Factory for creating mock IChatClient instances for testing.
/// Provides pre-configured behaviors for common scenarios.
/// </summary>
public static class MockChatClientFactory
{
    /// <summary>
    /// Create a mock IChatClient that returns a fixed response for any prompt.
    /// </summary>
    public static Mock<IChatClient> CreateWithResponse(string responseText)
    {
        var mock = new Mock<IChatClient>();

        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        return mock;
    }

    /// <summary>
    /// Create a mock IChatClient that returns different responses based on message content.
    /// </summary>
    public static Mock<IChatClient> CreateWithRoutingResponses(
        Dictionary<string, string> keywordToAgent)
    {
        var mock = new Mock<IChatClient>();

        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                var userMessage = messages.LastOrDefault()?.Text ?? string.Empty;

                foreach (var kvp in keywordToAgent)
                {
                    if (userMessage.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ChatResponse(
                            new ChatMessage(ChatRole.Assistant, kvp.Value));
                    }
                }

                return new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "none"));
            });

        return mock;
    }

    /// <summary>
    /// Create a mock IChatClient that throws on any call.
    /// Useful for testing fallback behavior when LLM is unavailable.
    /// </summary>
    public static Mock<IChatClient> CreateThatThrows(Exception? exception = null)
    {
        var mock = new Mock<IChatClient>();

        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception ?? new InvalidOperationException("LLM unavailable"));

        return mock;
    }

    /// <summary>
    /// Create a mock IChatClient that returns "none" (no routing match).
    /// </summary>
    public static Mock<IChatClient> CreateReturningNone()
    {
        return CreateWithResponse("none");
    }
}
