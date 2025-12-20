namespace Platform.Engineering.Copilot.Core.Models.TokenManagement;

/// <summary>
/// Strategy for pruning conversation history
/// </summary>
public enum PruningStrategy
{
    /// <summary>
    /// Keep most recent messages (chronological)
    /// </summary>
    RecentMessages,

    /// <summary>
    /// Keep messages with highest relevance scores
    /// </summary>
    RelevanceScoring,

    /// <summary>
    /// Summarize old messages and keep recent ones
    /// </summary>
    Summarization,

    /// <summary>
    /// Remove messages by topic when context switches
    /// </summary>
    TopicBased,

    /// <summary>
    /// Keep important system and user messages, trim assistant responses
    /// </summary>
    CompressAssistantResponses
}

/// <summary>
/// Options for conversation history optimization
/// </summary>
public class ConversationHistoryOptimizationOptions
{
    /// <summary>
    /// Maximum number of messages to keep
    /// </summary>
    public int MaxMessages { get; set; } = 20;

    /// <summary>
    /// Maximum tokens for conversation history
    /// </summary>
    public int MaxTokens { get; set; } = 5000;

    /// <summary>
    /// Minimum number of messages to always keep
    /// </summary>
    public int MinMessages { get; set; } = 3;

    /// <summary>
    /// Strategy to use for pruning
    /// </summary>
    public PruningStrategy Strategy { get; set; } = PruningStrategy.RecentMessages;

    /// <summary>
    /// Model name for token counting
    /// </summary>
    public string ModelName { get; set; } = "gpt-4o";

    /// <summary>
    /// Whether to compress assistant responses (remove redundant text)
    /// </summary>
    public bool CompressResponses { get; set; } = false;

    /// <summary>
    /// Maximum length for compressed assistant response
    /// </summary>
    public int CompressedResponseMaxLength { get; set; } = 200;

    /// <summary>
    /// Whether to use summarization for pruned messages
    /// </summary>
    public bool UseSummarization { get; set; } = false;

    /// <summary>
    /// Number of messages before summarization kicks in
    /// </summary>
    public int SummarizationThreshold { get; set; } = 15;
}

/// <summary>
/// Result of conversation history optimization
/// </summary>
public class OptimizedConversationHistory
{
    /// <summary>
    /// Original number of messages
    /// </summary>
    public int OriginalMessageCount { get; set; }

    /// <summary>
    /// Optimized messages
    /// </summary>
    public List<ConversationMessage> Messages { get; set; } = new();

    /// <summary>
    /// Number of messages removed
    /// </summary>
    public int MessagesRemoved { get; set; }

    /// <summary>
    /// Number of messages summarized
    /// </summary>
    public int MessagesSummarized { get; set; }

    /// <summary>
    /// Summary of pruned messages (if using summarization)
    /// </summary>
    public string? PruningsSummary { get; set; }

    /// <summary>
    /// Original token count
    /// </summary>
    public int OriginalTokenCount { get; set; }

    /// <summary>
    /// Optimized token count
    /// </summary>
    public int OptimizedTokenCount { get; set; }

    /// <summary>
    /// Tokens saved
    /// </summary>
    public int TokensSaved => OriginalTokenCount - OptimizedTokenCount;

    /// <summary>
    /// Optimization percentage
    /// </summary>
    public double OptimizationPercentage => OriginalTokenCount > 0
        ? (TokensSaved * 100.0 / OriginalTokenCount)
        : 0;

    /// <summary>
    /// Strategy applied
    /// </summary>
    public PruningStrategy StrategyApplied { get; set; }

    /// <summary>
    /// Warnings about optimization
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Get summary of optimization
    /// </summary>
    public string GetSummary()
    {
        return $"Conversation History Optimization:\n" +
               $"  Original Messages: {OriginalMessageCount}\n" +
               $"  Messages After Optimization: {Messages.Count}\n" +
               $"  Messages Removed: {MessagesRemoved}\n" +
               $"  Messages Summarized: {MessagesSummarized}\n" +
               $"  Original Tokens: {OriginalTokenCount:N0}\n" +
               $"  Optimized Tokens: {OptimizedTokenCount:N0}\n" +
               $"  Tokens Saved: {TokensSaved:N0} ({OptimizationPercentage:F1}%)\n" +
               $"  Strategy: {StrategyApplied}\n" +
               (Warnings.Any() ? $"  Warnings: {string.Join(", ", Warnings)}\n" : "");
    }
}

/// <summary>
/// Represents a single message in optimized conversation
/// </summary>
public class ConversationMessage
{
    /// <summary>
    /// Role (user, assistant, system)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of message
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Whether message was summarized
    /// </summary>
    public bool WasSummarized { get; set; }

    /// <summary>
    /// Original content before summarization (if applicable)
    /// </summary>
    public string? OriginalContent { get; set; }

    /// <summary>
    /// Token count for this message
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// Relevance score (0.0 to 1.0)
    /// </summary>
    public double RelevanceScore { get; set; } = 1.0;

    /// <summary>
    /// Topic or tags for this message
    /// </summary>
    public List<string> Topics { get; set; } = new();
}

/// <summary>
/// Statistics about conversation health
/// </summary>
public class ConversationHealthMetrics
{
    /// <summary>
    /// Total messages in conversation
    /// </summary>
    public int TotalMessages { get; set; }

    /// <summary>
    /// Average tokens per message
    /// </summary>
    public double AverageTokensPerMessage { get; set; }

    /// <summary>
    /// Conversation age in days
    /// </summary>
    public int ConversationAgeDays { get; set; }

    /// <summary>
    /// Number of topic switches
    /// </summary>
    public int TopicSwitches { get; set; }

    /// <summary>
    /// Estimated token efficiency (0.0 to 1.0)
    /// </summary>
    public double TokenEfficiency { get; set; }

    /// <summary>
    /// Whether conversation needs optimization
    /// </summary>
    public bool NeedsOptimization { get; set; }

    /// <summary>
    /// Reason for optimization need
    /// </summary>
    public string? OptimizationReason { get; set; }

    /// <summary>
    /// Recommended pruning percentage
    /// </summary>
    public double RecommendedPruningPercentage { get; set; }

    /// <summary>
    /// Get health summary
    /// </summary>
    public string GetHealthSummary()
    {
        return $"Conversation Health:\n" +
               $"  Total Messages: {TotalMessages}\n" +
               $"  Average Tokens/Message: {AverageTokensPerMessage:F1}\n" +
               $"  Age: {ConversationAgeDays} days\n" +
               $"  Topic Switches: {TopicSwitches}\n" +
               $"  Token Efficiency: {TokenEfficiency:F1}%\n" +
               $"  Needs Optimization: {NeedsOptimization}\n" +
               (NeedsOptimization ? $"  Reason: {OptimizationReason}\n" : "") +
               (RecommendedPruningPercentage > 0 ? $"  Recommended Pruning: {RecommendedPruningPercentage:F1}%\n" : "");
    }
}
