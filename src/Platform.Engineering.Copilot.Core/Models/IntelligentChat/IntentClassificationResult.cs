namespace Platform.Engineering.Copilot.Core.Models.IntelligentChat;

/// <summary>
/// Result of AI-powered intent classification
/// </summary>
public class IntentClassificationResult
{
    /// <summary>
    /// Type of intent: tool_execution, information_request, or conversational
    /// </summary>
    public string IntentType { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Category of tool if tool execution is required
    /// </summary>
    public string? ToolCategory { get; set; }

    /// <summary>
    /// Specific tool name to execute
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Extracted parameters for tool execution
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>
    /// Whether this requires a multi-step tool chain
    /// </summary>
    public bool RequiresToolChain { get; set; }

    /// <summary>
    /// Steps in the tool chain if multi-step workflow
    /// </summary>
    public List<ToolStep> ToolChain { get; set; } = new();

    /// <summary>
    /// Whether follow-up information is needed from user
    /// </summary>
    public bool RequiresFollowUp { get; set; }

    /// <summary>
    /// Question to ask user for follow-up
    /// </summary>
    public string? FollowUpPrompt { get; set; }

    /// <summary>
    /// AI reasoning for classification
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// Whether a tool needs to be executed
    /// </summary>
    public bool RequiresTool => !string.IsNullOrEmpty(ToolName);

    /// <summary>
    /// Create MCP tool call if tool execution required
    /// </summary>
    public McpToolCall? ToolCall => RequiresTool 
        ? new McpToolCall { Name = ToolName!, Arguments = Parameters }
        : null;
}
