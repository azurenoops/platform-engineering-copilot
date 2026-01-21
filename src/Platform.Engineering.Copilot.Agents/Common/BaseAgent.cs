using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.State.Abstractions;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.Common;

/// <summary>
/// Base agent class that all platform agents inherit from.
/// Uses Microsoft.Extensions.AI for chat completions.
/// </summary>
public abstract class BaseAgent
{
    protected readonly IChatClient ChatClient;
    protected readonly ILogger Logger;
    protected readonly IAgentStateManager? StateManager;
    protected readonly ISharedMemory? SharedMemory;
    protected readonly List<BaseTool> RegisteredTools = new();

    /// <summary>
    /// Unique identifier for this agent
    /// </summary>
    public virtual string AgentId => GetType().Name.Replace("Agent", "").ToLowerInvariant();

    /// <summary>
    /// Display name for this agent
    /// </summary>
    public virtual string AgentName => GetType().Name;

    /// <summary>
    /// Name alias for compatibility
    /// </summary>
    public string Name => AgentName;

    /// <summary>
    /// Description of the agent's capabilities
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Temperature for LLM responses (0.0 - 2.0). Lower = more focused, higher = more creative.
    /// Override in derived agents to use configured values.
    /// </summary>
    protected virtual float Temperature => 0.4f;

    /// <summary>
    /// Maximum tokens for LLM responses.
    /// Override in derived agents to use configured values.
    /// </summary>
    protected virtual int MaxTokens => 4000;

    protected BaseAgent(IChatClient chatClient, ILogger logger)
        : this(chatClient, logger, null, null)
    {
    }

    protected BaseAgent(
        IChatClient chatClient, 
        ILogger logger,
        IAgentStateManager? stateManager,
        ISharedMemory? sharedMemory)
    {
        ChatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        StateManager = stateManager;
        SharedMemory = sharedMemory;
    }

    /// <summary>
    /// Register a tool for this agent
    /// </summary>
    protected void RegisterTool(BaseTool tool)
    {
        RegisteredTools.Add(tool);
        Logger.LogDebug("Registered tool: {ToolName} for agent: {AgentName}", tool.Name, AgentName);
    }

    /// <summary>
    /// Get all registered tools as AITool instances
    /// </summary>
    public IEnumerable<AITool> GetAITools()
    {
        return RegisteredTools.Select(t => t.AsAITool());
    }

    /// <summary>
    /// Maximum number of tool execution rounds before forcing a response.
    /// Override in derived agents if more rounds are needed.
    /// </summary>
    protected virtual int MaxToolRounds => 5;

    /// <summary>
    /// Tool mode for the agent. Override to change behavior.
    /// Auto = let AI decide, RequireAny = force tool use, None = disable tools
    /// </summary>
    protected virtual ChatToolMode ToolMode => ChatToolMode.Auto;

    /// <summary>
    /// Process a conversation context and return a response
    /// </summary>
    public virtual async Task<AgentResponse> ProcessAsync(
        AgentConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var userMessage = context.MessageHistory.LastOrDefault(m => m.IsUser)?.Content ?? "";
        
        Logger.LogInformation("🤖 {AgentName} processing: {Message}",
            AgentName, userMessage.Length > 50 ? userMessage[..50] + "..." : userMessage);

        try
        {
            // Load agent state if state manager is available
            var agentState = await LoadAgentStateAsync(context.ConversationId, cancellationToken);
            
            var messages = BuildChatMessages(context);

            // Configure options with Temperature, MaxTokens, tools, and tool mode
            var options = new ChatOptions
            {
                Temperature = Temperature,
                MaxOutputTokens = MaxTokens,
                ToolMode = ToolMode
            };
            if (RegisteredTools.Any())
            {
                options.Tools = RegisteredTools.Select(t => t.AsAITool()).ToList();
            }

            Logger.LogDebug("LLM options: Temperature={Temperature}, MaxTokens={MaxTokens}, Tools={ToolCount}, ToolMode={ToolMode}",
                Temperature, MaxTokens, RegisteredTools.Count, ToolMode);

            // Multi-round tool execution loop
            var toolsExecuted = new List<ToolExecutionResult>();
            var responseText = "";
            var round = 0;

            while (round < MaxToolRounds)
            {
                round++;
                cancellationToken.ThrowIfCancellationRequested();

                // Get completion
                var response = await ChatClient.GetResponseAsync(messages, options, cancellationToken);
                responseText = response.Text ?? "";

                // Check for tool calls in the response
                var lastMessage = response.Messages.LastOrDefault();
                if (lastMessage?.Contents == null)
                    break;

                var hasToolCalls = lastMessage.Contents.OfType<FunctionCallContent>().Any();
                if (!hasToolCalls)
                {
                    // No more tool calls, we have the final response
                    break;
                }

                Logger.LogDebug("Tool execution round {Round}/{MaxRounds}", round, MaxToolRounds);

                var toolCallResults = await ExecuteToolCallsAsync(lastMessage, context.ConversationId, toolsExecuted, cancellationToken);
                if (!toolCallResults.Any())
                    break;

                // Add assistant message with tool calls and tool results to continue the conversation
                messages.AddRange(response.Messages);
                messages.AddRange(toolCallResults);

                // After max rounds, disable tools to force a final response
                if (round >= MaxToolRounds - 1)
                {
                    Logger.LogWarning("Reached max tool rounds ({MaxRounds}), forcing final response", MaxToolRounds);
                    options.Tools = null;
                    options.ToolMode = ChatToolMode.None;
                }
            }

            // Save agent state if state manager is available
            await SaveAgentStateAsync(context.ConversationId, agentState, cancellationToken);

            // Publish event to shared memory for other agents
            await PublishEventAsync(context.ConversationId, "agent_response", new
            {
                agentName = AgentName,
                messagePreview = responseText.Length > 100 ? responseText[..100] : responseText,
                toolsUsed = toolsExecuted.Select(t => t.ToolName).ToList(),
                toolRounds = round
            }, cancellationToken);

            return new AgentResponse
            {
                AgentId = AgentId,
                AgentName = AgentName,
                Content = responseText,
                Success = true,
                ToolsExecuted = toolsExecuted
            };
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Agent processing cancelled: {AgentName}", AgentName);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Agent processing failed: {AgentName}", AgentName);
            return new AgentResponse
            {
                AgentId = AgentId,
                AgentName = AgentName,
                Content = $"Error: {ex.Message}",
                Success = false
            };
        }
    }

    /// <summary>
    /// Get the system prompt for this agent
    /// </summary>
    protected abstract string GetSystemPrompt();

    /// <summary>
    /// Build chat messages from conversation context
    /// </summary>
    protected virtual List<ChatMessage> BuildChatMessages(AgentConversationContext context)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt())
        };

        // Add conversation history
        foreach (var msg in context.MessageHistory.TakeLast(20))
        {
            var role = msg.IsUser ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, msg.Content));
        }

        return messages;
    }

    /// <summary>
    /// Execute tool calls from AI response
    /// </summary>
    protected async Task<List<ChatMessage>> ExecuteToolCallsAsync(
        ChatMessage message,
        string conversationId,
        List<ToolExecutionResult> toolsExecuted,
        CancellationToken cancellationToken)
    {
        var results = new List<ChatMessage>();

        if (message.Contents == null)
            return results;

        foreach (var content in message.Contents)
        {
            if (content is FunctionCallContent functionCall)
            {
                Logger.LogInformation("🔧 Executing tool: {ToolName}", functionCall.Name);
                var startTime = DateTime.UtcNow;

                try
                {
                    var tool = RegisteredTools.FirstOrDefault(t => t.Name == functionCall.Name);
                    if (tool == null)
                    {
                        var errorResult = new FunctionResultContent(functionCall.CallId, $"Error: Tool '{functionCall.Name}' not found");
                        results.Add(new ChatMessage(ChatRole.Tool, [errorResult]));
                        toolsExecuted.Add(new ToolExecutionResult
                        {
                            ToolName = functionCall.Name,
                            Success = false,
                            Result = $"Tool '{functionCall.Name}' not found",
                            ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
                        });
                        continue;
                    }

                    var args = functionCall.Arguments ?? new Dictionary<string, object?>();
                    var toolResult = await tool.ExecuteAsync(args, cancellationToken);

                    // Store tool result in agent state
                    await StoreToolResultAsync(conversationId, functionCall.Name, toolResult, cancellationToken);

                    var successResult = new FunctionResultContent(functionCall.CallId, toolResult);
                    results.Add(new ChatMessage(ChatRole.Tool, [successResult]));
                    
                    toolsExecuted.Add(new ToolExecutionResult
                    {
                        ToolName = functionCall.Name,
                        Success = true,
                        Result = toolResult,
                        ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Tool execution failed: {ToolName}", functionCall.Name);
                    var errorResult = new FunctionResultContent(functionCall.CallId, $"Error: {ex.Message}");
                    results.Add(new ChatMessage(ChatRole.Tool, [errorResult]));
                    
                    toolsExecuted.Add(new ToolExecutionResult
                    {
                        ToolName = functionCall.Name,
                        Success = false,
                        Result = ex.Message,
                        ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
                    });
                }
            }
        }

        return results;
    }

    #region State Management Helpers

    /// <summary>
    /// Load agent state from state manager
    /// </summary>
    protected async Task<State.Models.AgentState?> LoadAgentStateAsync(
        string conversationId, 
        CancellationToken cancellationToken)
    {
        if (StateManager == null)
            return null;

        try
        {
            return await StateManager.GetAgentStateAsync(conversationId, AgentId, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load agent state for {AgentId}", AgentId);
            return null;
        }
    }

    /// <summary>
    /// Save agent state to state manager
    /// </summary>
    protected async Task SaveAgentStateAsync(
        string conversationId,
        State.Models.AgentState? state,
        CancellationToken cancellationToken)
    {
        if (StateManager == null || state == null)
            return;

        try
        {
            await StateManager.SaveAgentStateAsync(conversationId, AgentId, state, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save agent state for {AgentId}", AgentId);
        }
    }

    /// <summary>
    /// Store a tool result in agent state
    /// </summary>
    protected async Task StoreToolResultAsync(
        string conversationId,
        string toolName,
        object result,
        CancellationToken cancellationToken)
    {
        if (StateManager == null)
            return;

        try
        {
            await StateManager.SetToolResultAsync(conversationId, AgentId, toolName, result, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to store tool result for {ToolName}", toolName);
        }
    }

    /// <summary>
    /// Get data from shared memory
    /// </summary>
    protected async Task<T?> GetSharedDataAsync<T>(
        string conversationId,
        string key,
        CancellationToken cancellationToken) where T : class
    {
        if (SharedMemory == null)
            return null;

        try
        {
            return await SharedMemory.GetAsync<T>(conversationId, key, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to get shared data for key {Key}", key);
            return null;
        }
    }

    /// <summary>
    /// Set data in shared memory for other agents to access
    /// </summary>
    protected async Task SetSharedDataAsync<T>(
        string conversationId,
        string key,
        T value,
        CancellationToken cancellationToken) where T : class
    {
        if (SharedMemory == null)
            return;

        try
        {
            await SharedMemory.SetAsync(conversationId, key, value, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to set shared data for key {Key}", key);
        }
    }

    /// <summary>
    /// Publish an event to shared memory for agent coordination
    /// </summary>
    protected async Task PublishEventAsync(
        string conversationId,
        string eventType,
        object data,
        CancellationToken cancellationToken)
    {
        if (SharedMemory == null)
            return;

        try
        {
            await SharedMemory.PublishEventAsync(conversationId, eventType, data, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to publish event {EventType}", eventType);
        }
    }

    #endregion
}
