namespace Platform.Engineering.Copilot.Core.Models.IntelligentChat;

/// <summary>
/// Context for maintaining conversation state across messages
/// </summary>
public class ConversationContext
{
    /// <summary>
    /// Unique conversation identifier
    /// </summary>
    public string ConversationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User identifier
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Conversation history (recent messages)
    /// </summary>
    public List<MessageSnapshot> MessageHistory { get; set; } = new();

    /// <summary>
    /// Current topic or domain being discussed
    /// </summary>
    public string? CurrentTopic { get; set; }

    /// <summary>
    /// Tools that have been used in this conversation
    /// </summary>
    public List<string> UsedTools { get; set; } = new();

    /// <summary>
    /// Resources mentioned in conversation (resource groups, services, etc.)
    /// </summary>
    public Dictionary<string, string> MentionedResources { get; set; } = new();

    /// <summary>
    /// Active workflow or multi-step process
    /// </summary>
    public string? ActiveWorkflow { get; set; }

    /// <summary>
    /// State of active workflow
    /// </summary>
    public Dictionary<string, object?> WorkflowState { get; set; } = new();

    /// <summary>
    /// User preferences inferred from conversation
    /// </summary>
    public Dictionary<string, string> UserPreferences { get; set; } = new();

    /// <summary>
    /// When conversation started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of messages in conversation
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Session metadata
    /// </summary>
    public Dictionary<string, object?> SessionMetadata { get; set; } = new();

    /// <summary>
    /// Whether conversation is still active
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Snapshot of a single message in conversation history
/// </summary>
public class MessageSnapshot
{
    /// <summary>
    /// Message identifier
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Role: user or assistant
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Intent type if classified
    /// </summary>
    public string? IntentType { get; set; }

    /// <summary>
    /// Tool executed if any
    /// </summary>
    public string? ToolExecuted { get; set; }

    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this message was part of a tool chain
    /// </summary>
    public bool PartOfChain { get; set; }
}
