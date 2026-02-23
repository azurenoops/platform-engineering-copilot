using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Agents;

/// <summary>
/// Abstract base for all platform tools. Each tool is registered on a
/// <see cref="BaseAgent"/> and exposed through MCP and the orchestrator.
/// <para>
/// Constitution Principle II: All tools derive from this base class.
/// Constitution Principle IV: RequiresAuthentication and PimTierRequired
/// enforce dual-gate auth per FR-008, FR-069.
/// </para>
/// </summary>
public abstract class BaseTool
{
    /// <summary>Tool name used in MCP tools/list and tools/call (e.g. "run_compliance_assessment").</summary>
    public abstract string Name { get; }

    /// <summary>Human-readable description for LLM function-calling and MCP discovery.</summary>
    public abstract string Description { get; }

    /// <summary>
    /// JSON Schema string describing the tool's input parameters.
    /// Used by MCP tools/list and Semantic Kernel function metadata.
    /// Return "{}" for tools with no parameters.
    /// </summary>
    public abstract string Parameters { get; }

    /// <summary>
    /// Whether this tool requires CAC/PIV authentication (FR-008).
    /// Exposed as MCP tool metadata. Server-side enforcement in HTTP mode.
    /// </summary>
    public virtual bool RequiresAuthentication => true;

    /// <summary>
    /// Minimum PIM tier required to execute this tool (FR-069–FR-071).
    /// None = no PIM needed, Read = PIM read activation, Write = PIM write activation.
    /// Exposed as MCP tool metadata.
    /// </summary>
    public virtual PimTier PimTierRequired => PimTier.None;

    /// <summary>Logger for the concrete tool implementation.</summary>
    protected ILogger Logger { get; }

    protected BaseTool(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Execute the tool with the given parameters.
    /// Returns a JSON string matching the ResponseEnvelope schema (FR-079).
    /// </summary>
    /// <param name="parameters">
    /// Key-value parameters parsed from the MCP tools/call request.
    /// Keys correspond to the JSON Schema property names.
    /// </param>
    /// <param name="progress">
    /// Optional progress reporter for long-running operations.
    /// SignalR streaming and MCP progress notifications consume this.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON-serialized ResponseEnvelope.</returns>
    public abstract Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate that required parameters are present and correctly typed.
    /// Override to add custom validation logic.
    /// </summary>
    /// <returns>List of validation error messages (empty if valid).</returns>
    public virtual IReadOnlyList<string> ValidateParameters(Dictionary<string, object?> parameters)
    {
        return [];
    }

    /// <summary>
    /// Get a required parameter value, throwing if missing or null.
    /// </summary>
    protected T GetRequired<T>(Dictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            throw new ArgumentException($"Required parameter '{key}' is missing or null.");
        }

        if (value is T typed)
        {
            return typed;
        }

        // Attempt conversion for common types (JSON deserialization may produce JsonElement)
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            throw new ArgumentException(
                $"Parameter '{key}' has type '{value.GetType().Name}' but expected '{typeof(T).Name}'.");
        }
    }

    /// <summary>
    /// Get an optional parameter value, returning the default if missing.
    /// </summary>
    protected T? GetOptional<T>(Dictionary<string, object?> parameters, string key, T? defaultValue = default)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        if (value is T typed)
        {
            return typed;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}
