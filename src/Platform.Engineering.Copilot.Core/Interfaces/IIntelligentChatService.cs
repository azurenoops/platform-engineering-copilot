using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// AI-powered intelligent chat service using Azure OpenAI and Semantic Kernel
/// Handles intent classification, tool chaining, and proactive suggestions
/// </summary>
public interface IIntelligentChatService
{
    /// <summary>
    /// Process a user message with AI-powered intent classification
    /// </summary>
    Task<IntelligentChatResponse> ProcessMessageAsync(
        string message,
        string conversationId,
        ConversationContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Classify user intent using Azure OpenAI
    /// </summary>
    Task<IntentClassificationResult> ClassifyIntentAsync(
        string message,
        ConversationContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a multi-step tool chain
    /// </summary>
    Task<ToolChainResult> ExecuteToolChainAsync(
        List<ToolStep> steps,
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate proactive suggestions based on conversation context
    /// </summary>
    Task<List<ProactiveSuggestion>> GenerateProactiveSuggestionsAsync(
        string conversationId,
        ConversationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get or create conversation context
    /// </summary>
    Task<ConversationContext> GetOrCreateContextAsync(
        string conversationId,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update conversation context with new message
    /// </summary>
    Task UpdateContextAsync(
        ConversationContext context,
        MessageSnapshot message,
        CancellationToken cancellationToken = default);
}
