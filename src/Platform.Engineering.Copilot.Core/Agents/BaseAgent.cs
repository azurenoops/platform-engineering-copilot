using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

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

    /// <summary>Optional AI chat client for LLM-powered responses.</summary>
    protected IChatClient? ChatClient { get; }

    /// <summary>Azure OpenAI configuration options.</summary>
    protected AzureOpenAIOptions AIOptions { get; }

    /// <summary>Registered tools for this agent.</summary>
    private readonly List<BaseTool> _tools = [];

    /// <summary>Read-only view of registered tools.</summary>
    public IReadOnlyList<BaseTool> Tools => _tools.AsReadOnly();

    /// <summary>
    /// Primary constructor with optional AI integration (backward-compatible).
    /// </summary>
    protected BaseAgent(
        ILogger logger,
        IChatClient? chatClient = null,
        IOptions<AzureOpenAIOptions>? aiOptions = null)
    {
        Logger = logger;
        ChatClient = chatClient;
        AIOptions = aiOptions?.Value ?? new AzureOpenAIOptions();
    }

    /// <summary>
    /// Returns the system prompt for this agent, used by Semantic Kernel 
    /// for tool-calling and response generation.
    /// </summary>
    public abstract string GetSystemPrompt();

    // ─── T014: AI-Powered Message Processing ───────────────────────

    /// <summary>
    /// Process a user message through the Azure OpenAI LLM pipeline.
    /// <para>
    /// AI-enabled mode: builds conversation context, wraps tools as AIFunctions,
    /// creates FunctionInvokingChatClient for automatic multi-round tool calling,
    /// streams responses via IProgress, and returns natural-language text.
    /// </para>
    /// <para>
    /// Fallback mode (null client or AgentAIEnabled=false): executes first
    /// registered tool directly and returns raw JSON (pre-feature behavior).
    /// </para>
    /// </summary>
    public async Task<string> ProcessMessageAsync(
        string userMessage,
        IReadOnlyList<SessionMessage>? conversationHistory = null,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Fallback: no AI client or feature disabled
        if (ChatClient is null || !AIOptions.AgentAIEnabled)
        {
            Logger.LogInformation(
                "AI disabled or no client — falling back to direct tool execution on agent '{AgentId}'",
                AgentId);
            return await FallbackDirectToolExecution(progress, cancellationToken);
        }

        try
        {
            return await ExecuteAIPipeline(userMessage, conversationHistory, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            // LLM-failure recovery: catch, log, fall back to direct tool execution
            Logger.LogWarning(ex,
                "LLM call failed on agent '{AgentId}' — falling back to direct tool execution", AgentId);
            return await FallbackDirectToolExecution(progress, cancellationToken);
        }
    }

    /// <summary>
    /// Execute the full AI pipeline: build messages → build tools → invoke LLM with streaming.
    /// </summary>
    private async Task<string> ExecuteAIPipeline(
        string userMessage,
        IReadOnlyList<SessionMessage>? conversationHistory,
        IProgress<ProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // T012: Build chat messages (system prompt + history + user message)
        var messages = BuildChatMessages(userMessage, conversationHistory);
        Logger.LogInformation(
            "Agent '{AgentId}' processing message with {MessageCount} context messages",
            AgentId, messages.Count);

        // T013: Build AI tools from registered BaseTools
        var aiTools = BuildAITools(progress, cancellationToken);

        // Configure chat options
        var options = new ChatOptions
        {
            Temperature = AIOptions.Temperature,
            Tools = aiTools
        };

        // Wrap with FunctionInvokingChatClient for auto tool-call loop
        var functionClient = new ChatClientBuilder(ChatClient)
            .UseFunctionInvocation()
            .Build();

        if (functionClient is FunctionInvokingChatClient fic)
        {
            fic.MaximumIterationsPerRequest = AIOptions.MaxToolCallRounds;
        }

        // Stream response tokens
        var responseBuilder = new System.Text.StringBuilder();
        await foreach (var update in functionClient.GetStreamingResponseAsync(
            messages, options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                responseBuilder.Append(update.Text);
                progress?.Report(new ProgressUpdate
                {
                    Message = update.Text,
                    Data = new { type = "token", content = update.Text }
                });
            }
        }

        stopwatch.Stop();
        var responseText = responseBuilder.ToString();

        Logger.LogInformation(
            "Agent '{AgentId}' AI response completed in {ElapsedMs}ms ({ResponseLength} chars)",
            AgentId, stopwatch.ElapsedMilliseconds, responseText.Length);

        // Handle empty LLM response (T035 edge case)
        if (string.IsNullOrWhiteSpace(responseText))
        {
            Logger.LogWarning("Agent '{AgentId}' received empty response from LLM", AgentId);
            return "I wasn't able to generate a response. Please try rephrasing your request.";
        }

        return responseText;
    }

    /// <summary>
    /// Fallback: execute the first registered tool directly and return raw JSON.
    /// Preserves pre-feature behavior when AI is not available.
    /// </summary>
    private async Task<string> FallbackDirectToolExecution(
        IProgress<ProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        if (_tools.Count == 0)
        {
            return "No tools are available for this agent.";
        }

        var tool = _tools[0];
        return await ExecuteToolAsync(tool.Name, new Dictionary<string, object?>(), progress, cancellationToken);
    }

    // ─── T012: Build Chat Messages ─────────────────────────────────

    /// <summary>
    /// Build the chat message list for LLM invocation:
    /// 1. System prompt → ChatRole.System
    /// 2. Conversation history → ChatRole.User / ChatRole.Assistant (up to 10 messages)
    /// 3. Current user message → ChatRole.User
    /// </summary>
    public List<ChatMessage> BuildChatMessages(
        string userMessage,
        IReadOnlyList<SessionMessage>? conversationHistory = null)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt())
        };

        // Add conversation history (last 10 messages for context retention, SC-006)
        if (conversationHistory is { Count: > 0 })
        {
            var recentHistory = conversationHistory
                .TakeLast(10)
                .ToList();

            foreach (var msg in recentHistory)
            {
                var role = msg.Role.Equals("User", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.User
                    : ChatRole.Assistant;
                messages.Add(new ChatMessage(role, msg.Content));
            }
        }

        // Add current user message
        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        return messages;
    }

    // ─── T013: Build AI Tools ──────────────────────────────────────

    /// <summary>
    /// Convert registered BaseTools to AIFunction instances for LLM tool calling.
    /// Each AIFunction wraps the tool's Name, Description, and a delegate
    /// that calls ExecuteToolAsync with parameters from the LLM.
    /// </summary>
    public IList<AITool> BuildAITools(
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var aiTools = new List<AITool>();

        foreach (var tool in _tools)
        {
            // Capture tool reference for closure
            var capturedTool = tool;

            var aiFunction = AIFunctionFactory.Create(
                async (IDictionary<string, object?> args) =>
                {
                    // T025: Report tool execution progress
                    progress?.Report(new ProgressUpdate
                    {
                        Message = $"Running {capturedTool.Name}...",
                        Data = new { type = "tool_start", toolName = capturedTool.Name }
                    });

                    var parameters = new Dictionary<string, object?>(
                        args ?? new Dictionary<string, object?>());
                    var result = await ExecuteToolAsync(
                        capturedTool.Name, parameters, progress, cancellationToken);

                    progress?.Report(new ProgressUpdate
                    {
                        Message = $"Completed {capturedTool.Name}",
                        Data = new { type = "tool_complete", toolName = capturedTool.Name }
                    });

                    return result;
                },
                capturedTool.Name,
                capturedTool.Description);

            aiTools.Add(aiFunction);
        }

        return aiTools;
    }

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

/// <summary>
/// Conversation message used for session history.
/// Shared between Core (BaseAgent.ProcessMessageAsync) and Chat (ChatHub sessions).
/// </summary>
public class SessionMessage
{
    /// <summary>Message role: "User" or "Assistant".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Message content text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>When the message was sent.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Correlation ID for request tracking.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Which agent generated this message (null for user messages).</summary>
    public string? AgentId { get; set; }
}
