namespace Platform.Engineering.Copilot.Chat.App.Models;

/// <summary>
/// Represents a chat conversation with metadata
/// </summary>
public class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Conversation";
    public string UserId { get; set; } = "default-user";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; } = false;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<ChatMessage> Messages { get; set; } = new();
    public ConversationContext? Context { get; set; }
}

/// <summary>
/// Represents a chat message with enhanced capabilities
/// </summary>
public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MessageRole Role { get; set; } = MessageRole.User;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public MessageStatus Status { get; set; } = MessageStatus.Sent;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<MessageAttachment> Attachments { get; set; } = new();
    public string? ParentMessageId { get; set; }
    public List<string> Tools { get; set; } = new();
    public ToolExecutionResult? ToolResult { get; set; }
}

/// <summary>
/// Represents conversation context for maintaining state
/// </summary>
public class ConversationContext
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "ato_scan", "deployment", "cost_analysis", etc.
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Represents a file attachment to a message
/// </summary>
public class MessageAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MessageId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public AttachmentType Type { get; set; } = AttachmentType.Document;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents the result of a tool execution
/// </summary>
public class ToolExecutionResult
{
    public string ToolName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Chat message roles
/// </summary>
public enum MessageRole
{
    User,
    Assistant,
    System,
    Tool
}

/// <summary>
/// Message status
/// </summary>
public enum MessageStatus
{
    Sending,
    Sent,
    Processing,
    Completed,
    Error,
    Retry
}

/// <summary>
/// Attachment types
/// </summary>
public enum AttachmentType
{
    Document,
    Image,
    Code,
    Configuration,
    Log
}

/// <summary>
/// Chat request model
/// </summary>
public class ChatRequest
{
    public string ConversationId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> AttachmentIds { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Chat response model
/// </summary>
public class ChatResponse
{
    public string MessageId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string Content => Response; // For backward compatibility
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public List<string> SuggestedActions { get; set; } = new();
    public List<ToolInfo> RecommendedTools { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Tool information
/// </summary>
public class ToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string Category { get; set; } = string.Empty;
}

// ============================================================================
// Intelligent Chat API Models
// ============================================================================

/// <summary>
/// Response from the Intelligent Chat API endpoint
/// Mirrors Platform.Engineering.Copilot.API.Models.IntelligentChatApiResponse
/// </summary>
public class IntelligentChatApiResponse
{
    public bool Success { get; set; }
    public IntelligentChatData? Data { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, string>? ErrorDetails { get; set; }
}

/// <summary>
/// Intelligent chat response data
/// Simplified version of Platform.Engineering.Copilot.Core.Models.IntelligentChat.IntelligentChatResponse
/// </summary>
public class IntelligentChatData
{
    public string ConversationId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public IntentClassification Intent { get; set; } = new();
    public string Response { get; set; } = string.Empty;
    public bool ToolExecuted { get; set; }
    public object? ToolResult { get; set; }
    public ToolChainData? ToolChainResult { get; set; }
    public List<ProactiveSuggestionData> Suggestions { get; set; } = new();
    public bool RequiresFollowUp { get; set; }
    public string? FollowUpPrompt { get; set; }
    public ResponseMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Intent classification result
/// </summary>
public class IntentClassification
{
    public string IntentType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? ToolName { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public bool RequiresToolChain { get; set; }
    public string? Reasoning { get; set; }
}

/// <summary>
/// Tool chain execution result
/// </summary>
public class ToolChainData
{
    public string ChainId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public double SuccessRate { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Proactive suggestion from AI
/// </summary>
public class ProactiveSuggestionData
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = "💡";
    public double Confidence { get; set; }
    public string? ToolName { get; set; }
    public string SuggestedPrompt { get; set; } = string.Empty;
    public string ExpectedOutcome { get; set; } = string.Empty;
}

/// <summary>
/// Response metadata
/// </summary>
public class ResponseMetadata
{
    public long ProcessingTimeMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ModelUsed { get; set; }
    public int? TokensUsed { get; set; }
}