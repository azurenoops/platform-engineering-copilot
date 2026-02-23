using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Cached assessment results and state for multi-turn conversations.
/// </summary>
public class ConversationContext
{
    [Key]
    public Guid ContextId { get; set; }

    [Required]
    public Guid ConversationId { get; set; }

    /// <summary>e.g. "last_assessment_id".</summary>
    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    /// <summary>JSON-serialized context data.</summary>
    [Required]
    public string Value { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Conversation Conversation { get; set; } = null!;
}
