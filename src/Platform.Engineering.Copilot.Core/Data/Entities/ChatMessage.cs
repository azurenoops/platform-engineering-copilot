using System.ComponentModel.DataAnnotations;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Individual message in a conversation.
/// Context maintained across at least 10 sequential follow-up messages (SC-006).
/// </summary>
public class ChatMessage
{
    [Key]
    public Guid MessageId { get; set; }

    [Required]
    public Guid ConversationId { get; set; }

    [Required]
    public MessageRole Role { get; set; }

    /// <summary>Which agent responded.</summary>
    [MaxLength(50)]
    public string? AgentId { get; set; }

    /// <summary>Markdown content.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Links to AuditLogEntry.</summary>
    public Guid? CorrelationId { get; set; }

    [Required]
    public DateTimeOffset Timestamp { get; set; }

    // Navigation
    public Conversation Conversation { get; set; } = null!;
}
