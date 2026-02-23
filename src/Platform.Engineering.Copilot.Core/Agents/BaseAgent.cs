using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Agents;

/// <summary>
/// Abstract base for all platform agents. Wraps <see cref="AgentApplication"/>
/// from Microsoft.Agents.Builder and provides standardised agent metadata,
/// tool registration, and system prompt construction.
/// <para>
/// Constitution Principle II: All agents derive from this base class.
/// Constitution Principle V: Structured logging via ILogger.
/// </para>
/// </summary>
public abstract class BaseAgent
{
    /// <summary>Unique agent identifier (e.g. "compliance", "cost-management").</summary>
    public abstract string AgentId { get; }

    /// <summary>Human-friendly name shown in routing explanations.</summary>
    public abstract string AgentName { get; }

    /// <summary>Short description used for LLM-based intent classification.</summary>
    public abstract string Description { get; }

    /// <summary>
    /// Keywords that trigger fast-path routing to this agent (O(1) lookup).
    /// Must be lowercase. The orchestrator indexes these at startup.
    /// </summary>
    public abstract IReadOnlyList<string> Keywords { get; }

    /// <summary>
    /// Minimum PIM tier required to interact with this agent.
    /// Default: None (read-only tools may still enforce per-tool tiers).
    /// </summary>
    public virtual PimTier RequiredPimTier => PimTier.None;

    /// <summary>Logger for the concrete agent implementation.</summary>
    protected ILogger Logger { get; }

    /// <summary>Registered tools for this agent.</summary>
    private readonly List<BaseTool> _tools = [];

    /// <summary>Read-only view of registered tools.</summary>
    public IReadOnlyList<BaseTool> Tools => _tools.AsReadOnly();

    protected BaseAgent(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Returns the system prompt for this agent, used by Semantic Kernel 
    /// for tool-calling and response generation.
    /// </summary>
    public abstract string GetSystemPrompt();

    /// <summary>
    /// Register a tool with this agent. Tools are exposed through MCP
    /// and through the orchestrator's function-calling pipeline.
    /// </summary>
    public void RegisterTool(BaseTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (_tools.Any(t => t.Name.Equals(tool.Name, StringComparison.OrdinalIgnoreCase)))
        {
            Logger.LogWarning("Tool '{ToolName}' already registered on agent '{AgentId}', skipping duplicate",
                tool.Name, AgentId);
            return;
        }

        _tools.Add(tool);
        Logger.LogDebug("Registered tool '{ToolName}' on agent '{AgentId}'", tool.Name, AgentId);
    }

    /// <summary>
    /// Execute a named tool with the given parameters.
    /// Returns the tool's response envelope as a string.
    /// </summary>
    public async Task<string> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tool = _tools.FirstOrDefault(t =>
            t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

        if (tool == null)
        {
            Logger.LogWarning("Tool '{ToolName}' not found on agent '{AgentId}'", toolName, AgentId);
            throw new InvalidOperationException($"Tool '{toolName}' is not registered on agent '{AgentId}'.");
        }

        Logger.LogInformation("Executing tool '{ToolName}' on agent '{AgentId}'", toolName, AgentId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await tool.ExecuteAsync(parameters, progress, cancellationToken);
            stopwatch.Stop();

            Logger.LogInformation("Tool '{ToolName}' completed in {ElapsedMs}ms on agent '{AgentId}'",
                toolName, stopwatch.ElapsedMilliseconds, AgentId);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Tool '{ToolName}' failed after {ElapsedMs}ms on agent '{AgentId}'",
                toolName, stopwatch.ElapsedMilliseconds, AgentId);
            throw;
        }
    }

    /// <summary>
    /// Returns tool metadata for MCP tools/list responses.
    /// </summary>
    public IReadOnlyList<ToolMetadata> GetToolMetadata()
    {
        return _tools.Select(t => new ToolMetadata
        {
            Name = t.Name,
            Description = t.Description,
            Parameters = t.Parameters,
            RequiresAuthentication = t.RequiresAuthentication,
            PimTierRequired = t.PimTierRequired,
            AgentId = AgentId
        }).ToList().AsReadOnly();
    }
}

/// <summary>
/// Metadata about a tool, exposed through MCP tools/list and tool discovery.
/// </summary>
public class ToolMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Parameters { get; set; } = "{}";
    public bool RequiresAuthentication { get; set; }
    public PimTier PimTierRequired { get; set; }
    public string AgentId { get; set; } = string.Empty;
}

/// <summary>
/// Progress update emitted by tools during long-running operations.
/// Used by SignalR streaming and MCP progress notifications.
/// </summary>
public class ProgressUpdate
{
    /// <summary>Percentage complete (0–100), or null if indeterminate.</summary>
    public int? PercentComplete { get; set; }

    /// <summary>Human-readable status message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional structured data for the progress update.</summary>
    public object? Data { get; set; }
}
