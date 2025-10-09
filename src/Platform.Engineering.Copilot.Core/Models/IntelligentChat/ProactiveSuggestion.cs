namespace Platform.Engineering.Copilot.Core.Models.IntelligentChat;

/// <summary>
/// AI-generated proactive suggestion for next user action
/// </summary>
public class ProactiveSuggestion
{
    /// <summary>
    /// Unique identifier for this suggestion
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display title for the suggestion
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what this action does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Priority level: high, medium, low
    /// </summary>
    public string Priority { get; set; } = "medium";

    /// <summary>
    /// Category of suggestion: optimization, security, compliance, workflow, learning
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Icon or emoji for visual representation
    /// </summary>
    public string Icon { get; set; } = "💡";

    /// <summary>
    /// Confidence score for suggestion relevance (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Tool that would be executed if suggestion is accepted
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Action to perform with the tool
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Pre-filled parameters for tool execution
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>
    /// User-friendly prompt to execute this suggestion
    /// </summary>
    public string SuggestedPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Expected benefit or outcome
    /// </summary>
    public string ExpectedOutcome { get; set; } = string.Empty;

    /// <summary>
    /// Estimated time to complete
    /// </summary>
    public string? EstimatedTime { get; set; }

    /// <summary>
    /// Whether this is time-sensitive
    /// </summary>
    public bool IsUrgent { get; set; }

    /// <summary>
    /// Context that triggered this suggestion
    /// </summary>
    public string? TriggerContext { get; set; }

    /// <summary>
    /// Timestamp when suggestion was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether suggestion has been dismissed
    /// </summary>
    public bool IsDismissed { get; set; }

    /// <summary>
    /// Whether suggestion was accepted and executed
    /// </summary>
    public bool WasExecuted { get; set; }
}
