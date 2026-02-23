using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// A chat session with context.
/// </summary>
public class Conversation
{
    [Key]
    public Guid ConversationId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    /// <summary>Auto-generated from first message.</summary>
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>Currently active agent.</summary>
    [MaxLength(50)]
    public string? ActiveAgentId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsArchived { get; set; }

    // Navigation
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    public ICollection<ConversationContext> ContextEntries { get; set; } = new List<ConversationContext>();
}
