namespace Platform.Engineering.Copilot.Core.Services.TokenManagement;

using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;
using Platform.Engineering.Copilot.Core.Models.TokenManagement;

/// <summary>
/// Service for optimizing conversation history and managing context windows
/// </summary>
public class ConversationHistoryOptimizer : IConversationHistoryOptimizer
{
    private readonly ITokenCounter _tokenCounter;
    private readonly IPromptOptimizer _promptOptimizer;
    private readonly ILogger<ConversationHistoryOptimizer> _logger;

    public ConversationHistoryOptimizer(
        ITokenCounter tokenCounter,
        IPromptOptimizer promptOptimizer,
        ILogger<ConversationHistoryOptimizer> logger)
    {
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _promptOptimizer = promptOptimizer ?? throw new ArgumentNullException(nameof(promptOptimizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OptimizedConversationHistory> OptimizeHistoryAsync(
        List<ConversationMessage> messages,
        ConversationHistoryOptimizationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (messages == null || messages.Count == 0)
        {
            return new OptimizedConversationHistory
            {
                OriginalMessageCount = 0,
                Messages = new(),
                StrategyApplied = options?.Strategy ?? PruningStrategy.RecentMessages
            };
        }

        options ??= new ConversationHistoryOptimizationOptions();
        var result = new OptimizedConversationHistory
        {
            OriginalMessageCount = messages.Count,
            StrategyApplied = options.Strategy
        };

        // Calculate original token count
        result.OriginalTokenCount = CalculateHistoryTokens(messages, options.ModelName);

        // If already within budget, no optimization needed
        if (result.OriginalTokenCount <= options.MaxTokens && messages.Count <= options.MaxMessages)
        {
            result.Messages = messages;
            result.OptimizedTokenCount = result.OriginalTokenCount;
            _logger.LogInformation("Conversation history already optimized: {Messages} messages, {Tokens} tokens",
                messages.Count, result.OriginalTokenCount);
            return result;
        }

        // Prune messages based on strategy
        var relevanceScores = await CalculateMessageRelevanceAsync(messages, string.Empty, cancellationToken);
        var targetCount = Math.Min(options.MaxMessages, Math.Max(options.MinMessages, messages.Count / 2));

        result.Messages = await PruneMessagesAsync(messages, targetCount, options.Strategy, cancellationToken);
        result.MessagesRemoved = messages.Count - result.Messages.Count;

        // Compress responses if needed
        if (options.CompressResponses)
        {
            result.Messages = await CompressAssistantResponsesAsync(result.Messages, options, cancellationToken);
        }

        // Calculate optimized token count
        result.OptimizedTokenCount = CalculateHistoryTokens(result.Messages, options.ModelName);

        // Verify budget compliance
        if (result.OptimizedTokenCount > options.MaxTokens)
        {
            result.Warnings.Add($"Optimized history still exceeds token budget: {result.OptimizedTokenCount} > {options.MaxTokens}");
        }

        _logger.LogInformation("Conversation history optimized:\n{Summary}",
            result.GetSummary());

        return result;
    }

    public async Task<ConversationHealthMetrics> EvaluateConversationHealthAsync(
        List<ConversationMessage> messages,
        int currentTokenCount,
        int tokenBudget,
        CancellationToken cancellationToken = default)
    {
        var metrics = new ConversationHealthMetrics
        {
            TotalMessages = messages.Count,
            AverageTokensPerMessage = messages.Count > 0 ? currentTokenCount / (double)messages.Count : 0,
            ConversationAgeDays = messages.Count > 0 ? (int)(DateTime.UtcNow - messages.Min(m => m.Timestamp)).TotalDays : 0
        };

        // Detect topic switches
        var topicSwitches = await DetectTopicSwitchesAsync(messages, cancellationToken);
        metrics.TopicSwitches = topicSwitches.Count;

        // Calculate token efficiency
        var maxRecommendedTokens = tokenBudget * 0.7; // Use 70% of budget for history
        metrics.TokenEfficiency = Math.Min(1.0, currentTokenCount / maxRecommendedTokens);

        // Determine if optimization is needed
        if (currentTokenCount > tokenBudget * 0.8)
        {
            metrics.NeedsOptimization = true;
            metrics.OptimizationReason = $"Token usage {currentTokenCount}/{tokenBudget} exceeds 80% threshold";
            metrics.RecommendedPruningPercentage = ((currentTokenCount - (tokenBudget * 0.6)) / (double)currentTokenCount) * 100;
        }
        else if (messages.Count > 30)
        {
            metrics.NeedsOptimization = true;
            metrics.OptimizationReason = $"Conversation has {messages.Count} messages (recommended max 25)";
            metrics.RecommendedPruningPercentage = 30;
        }
        else if (metrics.TopicSwitches > 5)
        {
            metrics.NeedsOptimization = true;
            metrics.OptimizationReason = $"High topic switch count ({metrics.TopicSwitches}) may indicate scattered context";
            metrics.RecommendedPruningPercentage = 20;
        }

        _logger.LogInformation("Conversation health evaluated:\n{Summary}", metrics.GetHealthSummary());
        return metrics;
    }

    public async Task<string> CompressResponseAsync(
        string response,
        int maxTokens,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        var responseTokens = _tokenCounter.CountTokens(response, "gpt-4o");

        if (responseTokens <= maxTokens)
            return response;

        // Find the best truncation point
        var lines = response.Split(new[] { "\n", ". " }, StringSplitOptions.None);
        var compressed = new List<string>();
        var currentTokens = 0;

        foreach (var line in lines)
        {
            var lineTokens = _tokenCounter.CountTokens(line, "gpt-4o");
            if (currentTokens + lineTokens > maxTokens)
                break;

            compressed.Add(line);
            currentTokens += lineTokens;
        }

        var result = string.Join(" ", compressed);
        if (!result.EndsWith(".") && !result.EndsWith("..."))
            result += "...";

        _logger.LogDebug("Response compressed: {OriginalTokens} → {CompressedTokens} tokens",
            responseTokens, _tokenCounter.CountTokens(result, "gpt-4o"));

        return result;
    }

    public async Task<string> SummarizeConversationAsync(
        List<ConversationMessage> messages,
        int summaryMaxTokens = 500,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
            return "No conversation history to summarize.";

        // Group messages by role and extract key points
        var userMessages = messages.Where(m => m.Role == "user").ToList();
        var importantPoints = new List<string>();

        foreach (var msg in userMessages.TakeLast(Math.Min(5, userMessages.Count)))
        {
            if (msg.Content.Length > 50)
            {
                importantPoints.Add($"User asked: {msg.Content.Substring(0, Math.Min(100, msg.Content.Length))}...");
            }
        }

        var summary = $"Conversation Summary ({messages.Count} messages):\n" +
                      string.Join("\n", importantPoints.Take(3));

        var summaryTokens = _tokenCounter.CountTokens(summary, "gpt-4o");
        if (summaryTokens > summaryMaxTokens)
        {
            summary = await CompressResponseAsync(summary, summaryMaxTokens, cancellationToken);
        }

        return summary;
    }

    public async Task<Dictionary<int, double>> CalculateMessageRelevanceAsync(
        List<ConversationMessage> messages,
        string currentQuery,
        CancellationToken cancellationToken = default)
    {
        var scores = new Dictionary<int, double>();

        // Recent messages have higher relevance
        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            double score = 0.5; // Base score

            // Recency boost
            var ageHours = (DateTime.UtcNow - msg.Timestamp).TotalHours;
            var recencyBoost = Math.Max(0, (1.0 - (ageHours / 168.0))); // Decay over a week
            score += recencyBoost * 0.3;

            // Content importance (longer context messages are often more important)
            var contentBoost = Math.Min(0.2, msg.Content.Length / 1000.0);
            score += contentBoost;

            // Query relevance if provided
            if (!string.IsNullOrWhiteSpace(currentQuery))
            {
                var queryWords = currentQuery.Split(new[] { ' ', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
                var matchingWords = queryWords.Count(w => msg.Content.Contains(w, StringComparison.OrdinalIgnoreCase));
                var queryBoost = matchingWords > 0 ? (matchingWords / (double)queryWords.Length) * 0.2 : 0;
                score += queryBoost;
            }

            // System messages always high priority
            if (msg.Role == "system")
                score = Math.Min(1.0, score + 0.3);

            scores[i] = Math.Min(1.0, score);
        }

        return scores;
    }

    public async Task<List<ConversationMessage>> PruneMessagesAsync(
        List<ConversationMessage> messages,
        int targetMessageCount,
        PruningStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count <= targetMessageCount)
            return messages;

        List<ConversationMessage> pruned = strategy switch
        {
            PruningStrategy.RecentMessages => PruneByRecentMessages(messages, targetMessageCount),
            PruningStrategy.RelevanceScoring => await PruneByRelevanceAsync(messages, targetMessageCount, cancellationToken),
            PruningStrategy.TopicBased => PruneByTopic(messages, targetMessageCount),
            PruningStrategy.CompressAssistantResponses => await CompressAssistantResponsesAsync(messages, new ConversationHistoryOptimizationOptions(), cancellationToken),
            _ => PruneByRecentMessages(messages, targetMessageCount)
        };

        _logger.LogInformation("Pruned messages using {Strategy}: {OriginalCount} → {PrunedCount}",
            strategy, messages.Count, pruned.Count);

        return pruned;
    }

    public async Task<List<ConversationMessage>> GetContextWindowAsync(
        List<ConversationMessage> messages,
        int maxTokens,
        int targetMessageIndex,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0 || targetMessageIndex >= messages.Count)
            return messages;

        // Start with system message if present
        var window = new List<ConversationMessage>();
        var systemMsg = messages.FirstOrDefault(m => m.Role == "system");
        if (systemMsg != null)
            window.Add(systemMsg);

        // Add messages backwards from target index
        var currentTokens = CalculateHistoryTokens(window, "gpt-4o");
        for (int i = targetMessageIndex; i >= 0 && currentTokens < maxTokens; i--)
        {
            if (window.Contains(messages[i]))
                continue;

            var messageTokens = _tokenCounter.CountTokens(messages[i].Content, "gpt-4o");
            if (currentTokens + messageTokens > maxTokens)
                break;

            window.Insert(systemMsg != null ? 1 : 0, messages[i]);
            currentTokens += messageTokens;
        }

        return window;
    }

    public async Task<List<(int MessageIndex, string Topic)>> DetectTopicSwitchesAsync(
        List<ConversationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var switches = new List<(int, string)>();

        if (messages.Count < 2)
            return switches;

        // Simple topic detection based on keywords
        var keywordGroups = new Dictionary<string, List<string>>
        {
            { "infrastructure", new() { "vm", "resource", "deploy", "cluster", "network", "storage" } },
            { "compliance", new() { "nist", "audit", "policy", "secure", "compliance", "regulation" } },
            { "cost", new() { "cost", "budget", "savings", "optimize", "expense", "pricing" } },
            { "discovery", new() { "discovery", "scan", "detect", "identify", "find", "search" } }
        };

        string? currentTopic = null;

        for (int i = 0; i < messages.Count; i++)
        {
            var content = messages[i].Content.ToLower();
            string? detectedTopic = null;

            foreach (var (topic, keywords) in keywordGroups)
            {
                if (keywords.Any(k => content.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    detectedTopic = topic;
                    break;
                }
            }

            if (detectedTopic != null && detectedTopic != currentTopic && currentTopic != null)
            {
                switches.Add((i, detectedTopic));
            }

            if (detectedTopic != null)
                currentTopic = detectedTopic;
        }

        return switches;
    }

    public ConversationHistoryOptimizationOptions GetRecommendedOptionsForAgent(string agentType)
    {
        return agentType.ToLower() switch
        {
            "infrastructure" => new ConversationHistoryOptimizationOptions
            {
                MaxMessages = 25,
                MaxTokens = 6000,
                MinMessages = 3,
                Strategy = PruningStrategy.RelevanceScoring,
                CompressResponses = true,
                CompressedResponseMaxLength = 250,
                UseSummarization = false
            },
            "compliance" => new ConversationHistoryOptimizationOptions
            {
                MaxMessages = 20,
                MaxTokens = 5000,
                MinMessages = 2,
                Strategy = PruningStrategy.RecentMessages,
                CompressResponses = true,
                CompressedResponseMaxLength = 200,
                UseSummarization = true,
                SummarizationThreshold = 12
            },
            "costmanagement" => new ConversationHistoryOptimizationOptions
            {
                MaxMessages = 30,
                MaxTokens = 7000,
                MinMessages = 4,
                Strategy = PruningStrategy.RelevanceScoring,
                CompressResponses = true,
                CompressedResponseMaxLength = 300,
                UseSummarization = false
            },
            "discovery" => new ConversationHistoryOptimizationOptions
            {
                MaxMessages = 15,
                MaxTokens = 4000,
                MinMessages = 2,
                Strategy = PruningStrategy.RecentMessages,
                CompressResponses = false,
                UseSummarization = true,
                SummarizationThreshold = 10
            },
            "knowledgebase" => new ConversationHistoryOptimizationOptions
            {
                MaxMessages = 20,
                MaxTokens = 5000,
                MinMessages = 2,
                Strategy = PruningStrategy.RelevanceScoring,
                CompressResponses = true,
                CompressedResponseMaxLength = 250,
                UseSummarization = true,
                SummarizationThreshold = 12
            },
            "environment" => new ConversationHistoryOptimizationOptions
            {
                MaxMessages = 25,
                MaxTokens = 5500,
                MinMessages = 3,
                Strategy = PruningStrategy.TopicBased,
                CompressResponses = true,
                CompressedResponseMaxLength = 200,
                UseSummarization = false
            },
            _ => new ConversationHistoryOptimizationOptions()
        };
    }

    #region Private Helper Methods

    private int CalculateHistoryTokens(
        List<ConversationMessage> messages,
        string modelName)
    {
        var totalTokens = 0;
        foreach (var msg in messages)
        {
            var tokens = _tokenCounter.CountTokens(msg.Content, modelName);
            totalTokens += tokens;
            msg.TokenCount = tokens;
        }
        return totalTokens;
    }

    private List<ConversationMessage> PruneByRecentMessages(List<ConversationMessage> messages, int targetCount)
    {
        // Keep system message and most recent messages
        var systemMsg = messages.FirstOrDefault(m => m.Role == "system");
        var recentMessages = messages.Where(m => m.Role != "system").TakeLast(targetCount - (systemMsg != null ? 1 : 0)).ToList();

        var result = new List<ConversationMessage>();
        if (systemMsg != null)
            result.Add(systemMsg);
        result.AddRange(recentMessages);

        return result;
    }

    private async Task<List<ConversationMessage>> PruneByRelevanceAsync(
        List<ConversationMessage> messages,
        int targetCount,
        CancellationToken cancellationToken)
    {
        var scores = await CalculateMessageRelevanceAsync(messages, string.Empty, cancellationToken);
        var systemMsg = messages.FirstOrDefault(m => m.Role == "system");

        var scored = messages.Select((m, i) => (Message: m, Score: scores[i], Index: i)).ToList();
        var topMessages = scored.OrderByDescending(s => s.Score).Take(targetCount).OrderBy(s => s.Index).Select(s => s.Message).ToList();

        if (systemMsg != null && !topMessages.Contains(systemMsg))
        {
            topMessages.Insert(0, systemMsg);
            if (topMessages.Count > targetCount)
                topMessages.RemoveAt(topMessages.Count - 1);
        }

        return topMessages;
    }

    private List<ConversationMessage> PruneByTopic(List<ConversationMessage> messages, int targetCount)
    {
        // Keep first, last, and evenly distributed messages
        if (messages.Count <= targetCount)
            return messages;

        var result = new List<ConversationMessage> { messages[0] };
        var step = messages.Count / targetCount;

        for (int i = step; i < messages.Count - 1; i += step)
        {
            if (result.Count < targetCount)
                result.Add(messages[i]);
        }

        result.Add(messages[messages.Count - 1]);
        return result.Take(targetCount).ToList();
    }

    private async Task<List<ConversationMessage>> CompressAssistantResponsesAsync(
        List<ConversationMessage> messages,
        ConversationHistoryOptimizationOptions options,
        CancellationToken cancellationToken)
    {
        var compressed = new List<ConversationMessage>();

        foreach (var msg in messages)
        {
            if (msg.Role == "assistant" && msg.Content.Length > options.CompressedResponseMaxLength)
            {
                var compressedContent = await CompressResponseAsync(msg.Content, options.CompressedResponseMaxLength, cancellationToken);
                var compressedMsg = new ConversationMessage
                {
                    Role = msg.Role,
                    Content = compressedContent,
                    Timestamp = msg.Timestamp,
                    WasSummarized = true,
                    OriginalContent = msg.Content,
                    RelevanceScore = msg.RelevanceScore,
                    Topics = msg.Topics
                };
                compressed.Add(compressedMsg);
            }
            else
            {
                compressed.Add(msg);
            }
        }

        return compressed;
    }

    #endregion
}
