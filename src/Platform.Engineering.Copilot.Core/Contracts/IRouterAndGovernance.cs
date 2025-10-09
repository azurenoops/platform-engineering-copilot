using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Contracts;

/// <summary>
/// Contract for routing tool calls to appropriate handlers
/// NOTE: This interface is obsolete. Use Semantic Kernel plugins with auto-calling instead.
/// See IntelligentChatService_v2 for the new pattern.
/// </summary>
[Obsolete("Use Semantic Kernel plugins with ToolCallBehavior.AutoInvokeKernelFunctions instead. See IntelligentChatService_v2.")]
public interface IToolRouter
{
    /// <summary>
    /// Route a tool call to the appropriate handler based on natural language understanding
    /// </summary>
    Task<McpToolResult> RouteToolCallAsync(string naturalLanguageQuery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Route a specific tool call to the appropriate handler
    /// </summary>
    Task<McpToolResult> RouteToolCallAsync(McpToolCall toolCall, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available tools from all registered handlers
    /// </summary>
    Task<IEnumerable<McpTool>> GetAvailableToolsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Contract for governance and compliance checking
/// </summary>
public interface IGovernanceService
{
    /// <summary>
    /// Check if a tool call is allowed by governance policies
    /// </summary>
    Task<GovernanceResult> CheckPolicyAsync(McpToolCall toolCall, CancellationToken cancellationToken = default);

    /// <summary>
    /// Request approval for a tool call that requires human intervention
    /// </summary>
    Task<ApprovalResult> RequestApprovalAsync(McpToolCall toolCall, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a governance policy check
/// </summary>
public record GovernanceResult
{
    public required bool IsAllowed { get; init; }
    public required string[] Violations { get; init; }
    public required bool RequiresApproval { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Result of an approval request
/// </summary>
public record ApprovalResult
{
    public required bool IsApproved { get; init; }
    public required string ApprovalId { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? Comments { get; init; }
}