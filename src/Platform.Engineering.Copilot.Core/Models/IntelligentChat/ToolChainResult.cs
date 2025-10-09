namespace Platform.Engineering.Copilot.Core.Models.IntelligentChat;

/// <summary>
/// Result of executing a multi-step tool chain
/// </summary>
public class ToolChainResult
{
    /// <summary>
    /// Unique identifier for this tool chain execution
    /// </summary>
    public string ChainId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Overall status: running, completed, partial_success, failed
    /// </summary>
    public string Status { get; set; } = "running";

    /// <summary>
    /// All steps in the chain with their results
    /// </summary>
    public List<ToolStep> Steps { get; set; } = new();

    /// <summary>
    /// Current step being executed (1-based)
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Total number of steps
    /// </summary>
    public int TotalSteps => Steps.Count;

    /// <summary>
    /// Number of completed steps
    /// </summary>
    public int CompletedSteps => Steps.Count(s => s.Status == "completed");

    /// <summary>
    /// Number of failed steps
    /// </summary>
    public int FailedSteps => Steps.Count(s => s.Status == "failed");

    /// <summary>
    /// Overall success rate (0.0 to 1.0)
    /// </summary>
    public double SuccessRate => TotalSteps > 0 
        ? (double)CompletedSteps / TotalSteps 
        : 0.0;

    /// <summary>
    /// Total execution time for entire chain
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// When chain execution started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When chain execution completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Summary message for user
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Final result combining all step outputs
    /// </summary>
    public object? FinalResult { get; set; }

    /// <summary>
    /// Errors encountered during chain execution
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Context passed between steps
    /// </summary>
    public Dictionary<string, object?> Context { get; set; } = new();
}
