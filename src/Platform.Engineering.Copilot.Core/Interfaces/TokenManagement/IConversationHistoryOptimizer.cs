namespace Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;

using Platform.Engineering.Copilot.Core.Models.TokenManagement;

/// <summary>
/// Service for optimizing conversation history and managing context windows
/// </summary>
public interface IConversationHistoryOptimizer
{
    /// <summary>
    /// Optimize conversation history based on token budget
    /// </summary>
    Task<OptimizedConversationHistory> OptimizeHistoryAsync(
        List<ConversationMessage> messages,
        ConversationHistoryOptimizationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluate conversation health and determine if optimization is needed
    /// </summary>
    Task<ConversationHealthMetrics> EvaluateConversationHealthAsync(
        List<ConversationMessage> messages,
        int currentTokenCount,
        int tokenBudget,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compress a long assistant response to fit within token budget
    /// </summary>
    Task<string> CompressResponseAsync(
        string response,
        int maxTokens,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identify and extract important context from conversation history
    /// </summary>
    Task<string> SummarizeConversationAsync(
        List<ConversationMessage> messages,
        int summaryMaxTokens = 500,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate relevance score for each message
    /// </summary>
    Task<Dictionary<int, double>> CalculateMessageRelevanceAsync(
        List<ConversationMessage> messages,
        string currentQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prune messages based on strategy
    /// </summary>
    Task<List<ConversationMessage>> PruneMessagesAsync(
        List<ConversationMessage> messages,
        int targetMessageCount,
        PruningStrategy strategy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract conversation context window for a specific turn
    /// </summary>
    Task<List<ConversationMessage>> GetContextWindowAsync(
        List<ConversationMessage> messages,
        int maxTokens,
        int targetMessageIndex,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect topic switches in conversation
    /// </summary>
    Task<List<(int MessageIndex, string Topic)>> DetectTopicSwitchesAsync(
        List<ConversationMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recommended optimization options for an agent type
    /// </summary>
    ConversationHistoryOptimizationOptions GetRecommendedOptionsForAgent(string agentType);
}
