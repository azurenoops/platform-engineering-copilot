using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;

namespace Platform.Engineering.Copilot.Mcp.Tools;

/// <summary>
/// MCP tool that exposes the Platform Engineering Copilot's multi-agent orchestrator.
/// This is the primary interface for AI assistants to interact with the platform.
/// Preserves full multi-agent intelligence - the orchestrator decides which agents to use.
/// </summary>
public class PlatformEngineeringCopilotTools
{
    private readonly IIntelligentChatService _intelligentChatService;
    private readonly ILogger<PlatformEngineeringCopilotTools> _logger;

    public PlatformEngineeringCopilotTools(
        IIntelligentChatService intelligentChatService,
        ILogger<PlatformEngineeringCopilotTools> logger)
    {
        _intelligentChatService = intelligentChatService;
        _logger = logger;
    }

    /// <summary>
    /// Process a platform engineering request through the multi-agent orchestrator.
    /// The orchestrator will automatically:
    /// 1. Analyze user intent
    /// 2. Determine which specialized agents to use (Infrastructure, Compliance, Cost, etc.)
    /// 3. Coordinate agent execution (sequential, parallel, or collaborative)
    /// 4. Synthesize a comprehensive response
    /// </summary>
    public async Task<ChatMcpResult> ProcessRequestAsync(
        string message,
        string? conversationId = null,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        conversationId ??= Guid.NewGuid().ToString();

        _logger.LogInformation("📨 MCP Chat Tool processing request for conversation: {ConversationId}", conversationId);

        try
        {
            // Get or create conversation context
            var conversationContext = await _intelligentChatService.GetOrCreateContextAsync(
                conversationId,
                userId: "mcp-user",
                cancellationToken: cancellationToken);

            // Add any additional context from MCP client
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    conversationContext.WorkflowState[kvp.Key] = kvp.Value;
                }
            }

            // Process message through multi-agent orchestrator
            var response = await _intelligentChatService.ProcessMessageAsync(
                message,
                conversationId,
                conversationContext,
                cancellationToken);

            _logger.LogInformation(
                "✅ MCP Chat processed successfully | Intent: {Intent} | Time: {TimeMs}ms",
                response.Intent.IntentType,
                response.Metadata.ProcessingTimeMs);

            return new ChatMcpResult
            {
                Success = true,  // IntelligentChatResponse doesn't have Success, assume true if no exception
                Response = response.Response,
                ConversationId = conversationId,
                IntentType = response.Intent.IntentType,
                Confidence = response.Intent.Confidence,
                ToolExecuted = response.ToolExecuted,
                ToolResult = response.ToolResult,
                AgentsInvoked = new List<string>(),  // Not in current model
                ExecutionPattern = null,  // Not in current model
                ProcessingTimeMs = response.Metadata.ProcessingTimeMs,
                Suggestions = response.Suggestions?.Select(s => new SuggestionInfo
                {
                    Title = s.Title,
                    Description = s.Description,
                    Priority = s.Priority,  // Already a string
                    Category = s.Category,
                    SuggestedPrompt = s.SuggestedPrompt
                }).ToList() ?? new List<SuggestionInfo>(),
                RequiresFollowUp = response.RequiresFollowUp,
                FollowUpPrompt = response.FollowUpPrompt,
                Errors = new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing MCP chat request");

            return new ChatMcpResult
            {
                Success = false,
                Response = $"I encountered an error processing your request: {ex.Message}",
                ConversationId = conversationId,
                IntentType = "error",
                Confidence = 0,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Get conversation history for a specific conversation
    /// </summary>
    public async Task<ConversationHistoryResult> GetConversationHistoryAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await _intelligentChatService.GetOrCreateContextAsync(
                conversationId,
                cancellationToken: cancellationToken);

            return new ConversationHistoryResult
            {
                Success = true,
                ConversationId = conversationId,
                MessageCount = context.MessageCount,
                Messages = context.MessageHistory.Select(m => new MessageInfo
                {
                    Role = m.Role,
                    Content = m.Content,
                    Timestamp = m.Timestamp
                }).ToList(),
                UsedTools = context.UsedTools
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history");
            return new ConversationHistoryResult
            {
                Success = false,
                ConversationId = conversationId,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Generate proactive suggestions based on conversation context
    /// </summary>
    public async Task<ProactiveSuggestionsResult> GetProactiveSuggestionsAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await _intelligentChatService.GetOrCreateContextAsync(
                conversationId,
                cancellationToken: cancellationToken);

            var suggestions = await _intelligentChatService.GenerateProactiveSuggestionsAsync(
                conversationId,
                context,
                cancellationToken);

            return new ProactiveSuggestionsResult
            {
                Success = true,
                ConversationId = conversationId,
                Suggestions = suggestions.Select(s => new SuggestionInfo
                {
                    Title = s.Title,
                    Description = s.Description,
                    Priority = s.Priority,
                    Category = s.Category,
                    SuggestedPrompt = s.SuggestedPrompt
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating proactive suggestions");
            return new ProactiveSuggestionsResult
            {
                Success = false,
                ConversationId = conversationId,
                Errors = new List<string> { ex.Message }
            };
        }
    }
}

/// <summary>
/// Result from processing a chat request through MCP
/// </summary>
public class ChatMcpResult
{
    public bool Success { get; set; }
    public string Response { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string IntentType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public bool ToolExecuted { get; set; }
    public object? ToolResult { get; set; }
    public List<string> AgentsInvoked { get; set; } = new();
    public string? ExecutionPattern { get; set; }
    public double ProcessingTimeMs { get; set; }
    public List<SuggestionInfo> Suggestions { get; set; } = new();
    public bool RequiresFollowUp { get; set; }
    public string? FollowUpPrompt { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ConversationHistoryResult
{
    public bool Success { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public List<MessageInfo> Messages { get; set; } = new();
    public List<string> UsedTools { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class ProactiveSuggestionsResult
{
    public bool Success { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public List<SuggestionInfo> Suggestions { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class MessageInfo
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class SuggestionInfo
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SuggestedPrompt { get; set; }
}
