namespace Platform.Engineering.Copilot.Core.Models.IntelligentChat;

/// <summary>
/// Complete response from intelligent chat service
/// </summary>
public class IntelligentChatResponse
{
    /// <summary>
    /// Unique identifier for this conversation
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Message ID for tracking
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Intent classification result
    /// </summary>
    public IntentClassificationResult Intent { get; set; } = new();

    /// <summary>
    /// AI-generated response message
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Whether a tool was executed
    /// </summary>
    public bool ToolExecuted { get; set; }

    /// <summary>
    /// Result of tool execution if tool was called
    /// </summary>
    public object? ToolResult { get; set; }

    /// <summary>
    /// Tool chain result if multi-step workflow was executed
    /// </summary>
    public ToolChainResult? ToolChainResult { get; set; }

    /// <summary>
    /// Proactive suggestions for next actions
    /// </summary>
    public List<ProactiveSuggestion> Suggestions { get; set; } = new();

    /// <summary>
    /// Whether follow-up is needed from user
    /// </summary>
    public bool RequiresFollowUp { get; set; }

    /// <summary>
    /// Follow-up prompt for user
    /// </summary>
    public string? FollowUpPrompt { get; set; }

    /// <summary>
    /// Metadata about the response
    /// </summary>
    public ResponseMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Context for continuing the conversation
    /// </summary>
    public ConversationContext Context { get; set; } = new();
}

/// <summary>
/// Metadata about the response generation
/// </summary>
public class ResponseMetadata
{
    /// <summary>
    /// Time taken to process the message (milliseconds)
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Timestamp of response generation
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Model used for AI classification
    /// </summary>
    public string? ModelUsed { get; set; }

    /// <summary>
    /// Version of intelligent chat service
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Tokens used by AI model
    /// </summary>
    public int? TokensUsed { get; set; }

    /// <summary>
    /// Cost of AI processing (if applicable)
    /// </summary>
    public decimal? Cost { get; set; }
}
