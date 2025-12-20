namespace Platform.Engineering.Copilot.Core.Models.TokenManagement;

/// <summary>
/// Tracks cost metrics for a specific agent's operations
/// Used for cost tracking, optimization effectiveness, and billing
/// </summary>
public class AgentCostMetrics
{
    /// <summary>
    /// Unique identifier for this metric record
    /// </summary>
    public string MetricId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Agent type this metric belongs to
    /// </summary>
    public string AgentType { get; set; } = string.Empty;

    /// <summary>
    /// Task ID that triggered this cost tracking
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Conversation ID for multi-turn tracking
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this cost was incurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total tokens used in the prompt (before optimization)
    /// </summary>
    public int OriginalPromptTokens { get; set; }

    /// <summary>
    /// Total tokens used in the prompt (after optimization)
    /// </summary>
    public int OptimizedPromptTokens { get; set; }

    /// <summary>
    /// Tokens saved through prompt optimization
    /// </summary>
    public int TokensSaved { get; set; }

    /// <summary>
    /// Percentage of tokens saved
    /// </summary>
    public double OptimizationPercentage { get; set; }

    /// <summary>
    /// Completion tokens used (LLM output)
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Total tokens including completion
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Estimated cost in USD
    /// </summary>
    public double EstimatedCost { get; set; }

    /// <summary>
    /// Cost saved through optimization
    /// </summary>
    public double CostSaved { get; set; }

    /// <summary>
    /// Model used for this operation (e.g., gpt-4o)
    /// </summary>
    public string Model { get; set; } = "gpt-4o";

    /// <summary>
    /// Optimization strategy applied (if any)
    /// </summary>
    public string OptimizationStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Whether optimization was applied
    /// </summary>
    public bool WasOptimized { get; set; }

    /// <summary>
    /// Number of RAG context items included
    /// </summary>
    public int RagContextItems { get; set; }

    /// <summary>
    /// Number of RAG context items after optimization
    /// </summary>
    public int RagContextItemsAfterOptimization { get; set; }

    /// <summary>
    /// Number of conversation history messages included
    /// </summary>
    public int ConversationHistoryMessages { get; set; }

    /// <summary>
    /// Additional metadata for analysis
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Calculate total cost with optimization savings
    /// </summary>
    public double GetTotalCostWithSavings()
    {
        return EstimatedCost - CostSaved;
    }

    /// <summary>
    /// Get a summary of this cost metric
    /// </summary>
    public string GetSummary()
    {
        var summary = $"Agent: {AgentType}\n" +
                     $"Timestamp: {Timestamp:O}\n" +
                     $"Original Tokens: {OriginalPromptTokens:N0}\n" +
                     $"Optimized Tokens: {OptimizedPromptTokens:N0}\n" +
                     $"Tokens Saved: {TokensSaved:N0} ({OptimizationPercentage:F1}%)\n" +
                     $"Completion Tokens: {CompletionTokens:N0}\n" +
                     $"Total Tokens: {TotalTokens:N0}\n" +
                     $"Estimated Cost: ${EstimatedCost:F4}\n" +
                     $"Cost Saved: ${CostSaved:F4}\n";

        if (WasOptimized)
        {
            summary += $"Optimization Strategy: {OptimizationStrategy}\n";
        }

        return summary;
    }
}

/// <summary>
/// Aggregated cost metrics for tracking optimization effectiveness
/// </summary>
public class PromptOptimizationMetrics
{
    /// <summary>
    /// Total prompts processed
    /// </summary>
    public int TotalPromptsProcessed { get; set; }

    /// <summary>
    /// Number of prompts that required optimization
    /// </summary>
    public int PromptsOptimized { get; set; }

    /// <summary>
    /// Total tokens processed (all prompts combined)
    /// </summary>
    public long TotalTokensProcessed { get; set; }

    /// <summary>
    /// Total tokens after optimization
    /// </summary>
    public long TotalTokensAfterOptimization { get; set; }

    /// <summary>
    /// Overall tokens saved across all prompts
    /// </summary>
    public long TotalTokensSaved { get; set; }

    /// <summary>
    /// Average optimization effectiveness (percentage)
    /// </summary>
    public double AverageOptimizationPercentage { get; set; }

    /// <summary>
    /// Total cost saved (USD)
    /// </summary>
    public double TotalCostSaved { get; set; }

    /// <summary>
    /// Total cost incurred (USD)
    /// </summary>
    public double TotalCostIncurred { get; set; }

    /// <summary>
    /// Period covered by these metrics
    /// </summary>
    public DateTime? PeriodStart { get; set; }

    /// <summary>
    /// Period covered by these metrics
    /// </summary>
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// Per-agent breakdown of metrics
    /// </summary>
    public Dictionary<string, AgentOptimizationStats> AgentStats { get; set; } = new();

    /// <summary>
    /// Most common optimization strategy applied
    /// </summary>
    public string MostCommonStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Get overall optimization ROI
    /// </summary>
    public double GetOptimizationROI()
    {
        if (TotalCostIncurred == 0)
            return 0;

        return (TotalCostSaved / TotalCostIncurred) * 100;
    }

    /// <summary>
    /// Get summary of metrics
    /// </summary>
    public string GetSummary()
    {
        return $"Prompt Optimization Metrics Summary:\n" +
               $"  Total Prompts Processed: {TotalPromptsProcessed:N0}\n" +
               $"  Prompts Optimized: {PromptsOptimized:N0} ({(TotalPromptsProcessed > 0 ? (PromptsOptimized * 100.0 / TotalPromptsProcessed):0):F1}%)\n" +
               $"  Total Tokens Processed: {TotalTokensProcessed:N0}\n" +
               $"  Tokens After Optimization: {TotalTokensAfterOptimization:N0}\n" +
               $"  Total Tokens Saved: {TotalTokensSaved:N0}\n" +
               $"  Average Optimization: {AverageOptimizationPercentage:F1}%\n" +
               $"  Total Cost Saved: ${TotalCostSaved:F4}\n" +
               $"  Total Cost Incurred: ${TotalCostIncurred:F4}\n" +
               $"  Optimization ROI: {GetOptimizationROI():F1}%\n";
    }
}

/// <summary>
/// Per-agent optimization statistics
/// </summary>
public class AgentOptimizationStats
{
    /// <summary>
    /// Agent type
    /// </summary>
    public string AgentType { get; set; } = string.Empty;

    /// <summary>
    /// Number of operations for this agent
    /// </summary>
    public int OperationCount { get; set; }

    /// <summary>
    /// Number of operations that were optimized
    /// </summary>
    public int OptimizedOperationCount { get; set; }

    /// <summary>
    /// Total tokens saved for this agent
    /// </summary>
    public long TokensSaved { get; set; }

    /// <summary>
    /// Total cost saved for this agent
    /// </summary>
    public double CostSaved { get; set; }

    /// <summary>
    /// Average optimization percentage for this agent
    /// </summary>
    public double AverageOptimizationPercentage { get; set; }

    /// <summary>
    /// Most commonly used optimization strategy
    /// </summary>
    public string PreferredStrategy { get; set; } = string.Empty;
}

/// <summary>
/// Historical record of token usage for trend analysis
/// </summary>
public class TokenUsageRecord
{
    /// <summary>
    /// Unique identifier for this record
    /// </summary>
    public string RecordId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Agent type
    /// </summary>
    public string AgentType { get; set; } = string.Empty;

    /// <summary>
    /// Date of the usage
    /// </summary>
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total tokens used on this date
    /// </summary>
    public long TotalTokensUsed { get; set; }

    /// <summary>
    /// Total tokens saved through optimization
    /// </summary>
    public long TotalTokensSaved { get; set; }

    /// <summary>
    /// Number of operations performed
    /// </summary>
    public int OperationCount { get; set; }

    /// <summary>
    /// Average tokens per operation
    /// </summary>
    public double AverageTokensPerOperation => OperationCount > 0 ? (double)TotalTokensUsed / OperationCount : 0;

    /// <summary>
    /// Total estimated cost for the day
    /// </summary>
    public double DailyEstimatedCost { get; set; }

    /// <summary>
    /// Total cost saved for the day
    /// </summary>
    public double DailyCostSaved { get; set; }

    /// <summary>
    /// Percentage of prompts that required optimization
    /// </summary>
    public double OptimizationRate { get; set; }

    /// <summary>
    /// Get daily summary
    /// </summary>
    public string GetDailySummary()
    {
        return $"{Date:yyyy-MM-dd} {AgentType}:\n" +
               $"  Operations: {OperationCount:N0}\n" +
               $"  Avg Tokens/Op: {AverageTokensPerOperation:N0}\n" +
               $"  Total Tokens: {TotalTokensUsed:N0}\n" +
               $"  Tokens Saved: {TotalTokensSaved:N0}\n" +
               $"  Optimization Rate: {OptimizationRate:F1}%\n" +
               $"  Estimated Cost: ${DailyEstimatedCost:F4}\n" +
               $"  Cost Saved: ${DailyCostSaved:F4}\n";
    }
}
