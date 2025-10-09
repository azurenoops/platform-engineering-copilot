using Platform.Engineering.Copilot.Core.Models.IntelligentChat;

namespace Platform.Engineering.Copilot.API.Models;

/// <summary>
/// Request model for intelligent chat query
/// </summary>
public class IntelligentChatRequest
{
    /// <summary>
    /// User's message text
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Unique conversation identifier
    /// </summary>
    public required string ConversationId { get; set; }

    /// <summary>
    /// Optional conversation context for maintaining state
    /// </summary>
    public ConversationContext? Context { get; set; }

    /// <summary>
    /// Optional user identifier
    /// </summary>
    public string? UserId { get; set; }
}

/// <summary>
/// Response wrapper for API consumers
/// </summary>
public class IntelligentChatApiResponse
{
    /// <summary>
    /// Whether the request was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The intelligent chat response
    /// </summary>
    public IntelligentChatResponse? Data { get; set; }

    /// <summary>
    /// Error message if request failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Additional error details
    /// </summary>
    public Dictionary<string, string>? ErrorDetails { get; set; }
}

/// <summary>
/// Request to get conversation context
/// </summary>
public class GetContextRequest
{
    /// <summary>
    /// Conversation identifier
    /// </summary>
    public required string ConversationId { get; set; }

    /// <summary>
    /// Optional user identifier
    /// </summary>
    public string? UserId { get; set; }
}
