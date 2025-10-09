namespace Platform.Engineering.Copilot.Core.Models.IntelligentChat;

/// <summary>
/// Represents a single step in a multi-step tool chain
/// </summary>
public class ToolStep
{
    /// <summary>
    /// Step number in the chain (1-based)
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// Name of the tool to execute in this step
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Action to perform with the tool
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for this tool execution
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>
    /// Description of what this step does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this step depends on the result of the previous step
    /// </summary>
    public bool DependsOnPrevious { get; set; }

    /// <summary>
    /// Status of this step: pending, running, completed, failed
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Result of tool execution (populated after execution)
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Error message if step failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when step completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration of step execution in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }
}
