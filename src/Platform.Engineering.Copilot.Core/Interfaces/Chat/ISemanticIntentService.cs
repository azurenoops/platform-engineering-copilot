using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Service interface for semantic intent tracking and classification
/// </summary>
public interface ISemanticIntentService
{
    // ==================== Intent Recording ====================
    
    /// <summary>
    /// Record a new user intent from natural language input
    /// This is the main entry point for tracking user requests
    /// </summary>
    /// <param name="userId">The user who made the request</param>
    /// <param name="userInput">The raw natural language input</param>
    /// <param name="intentCategory">The classified category (infrastructure, compliance, cost, etc.)</param>
    /// <param name="intentAction">The specific action (create, delete, scale, list, etc.)</param>
    /// <param name="confidence">Classification confidence score (0.0 - 1.0)</param>
    /// <param name="sessionId">Optional session identifier</param>
    /// <param name="extractedParameters">Optional JSON of extracted parameters</param>
    /// <param name="resolvedToolCall">Optional JSON of the resolved MCP tool call</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created semantic intent</returns>
    Task<SemanticIntent> RecordIntentAsync(
        string userId,
        string userInput,
        string intentCategory,
        string intentAction,
        decimal confidence,
        string? sessionId = null,
        string? extractedParameters = null,
        string? resolvedToolCall = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update the outcome of a previously recorded intent
    /// Call this after agent execution to track success/failure
    /// </summary>
    Task<SemanticIntent> UpdateIntentOutcomeAsync(
        Guid intentId,
        bool wasSuccessful,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
    
    // ==================== Intent Classification ====================
    
    /// <summary>
    /// Classify user input against known patterns
    /// Returns the best matching category and action with confidence score
    /// </summary>
    Task<SemanticClassificationResult> ClassifyIntentAsync(
        string userInput,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get patterns that match a specific input
    /// </summary>
    Task<IReadOnlyList<PatternMatch>> GetMatchingPatternsAsync(
        string userInput,
        CancellationToken cancellationToken = default);
    
    // ==================== Feedback ====================
    
    /// <summary>
    /// Submit feedback on an intent classification
    /// Used to improve future classifications
    /// </summary>
    /// <param name="intentId">The intent being rated</param>
    /// <param name="feedbackType">correct, incorrect, or partial</param>
    /// <param name="providedBy">User providing the feedback</param>
    /// <param name="correctCategory">The correct category if classification was wrong</param>
    /// <param name="correctAction">The correct action if classification was wrong</param>
    /// <param name="correctParameters">The correct parameters as JSON</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IntentFeedback> SubmitFeedbackAsync(
        Guid intentId,
        string feedbackType,
        string providedBy,
        string? correctCategory = null,
        string? correctAction = null,
        string? correctParameters = null,
        CancellationToken cancellationToken = default);
    
    // ==================== User History ====================
    
    /// <summary>
    /// Get recent intent history for a user
    /// Useful for personalization and context
    /// </summary>
    Task<IReadOnlyList<SemanticIntent>> GetUserIntentHistoryAsync(
        string userId,
        int count = 10,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the most recent intent for a user
    /// </summary>
    Task<SemanticIntent?> GetLastUserIntentAsync(
        string userId,
        CancellationToken cancellationToken = default);
    
    // ==================== Analytics ====================
    
    /// <summary>
    /// Get aggregated intent analytics
    /// </summary>
    Task<IntentAnalytics> GetAnalyticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of semantic intent classification (for pattern-based matching)
/// </summary>
public class SemanticClassificationResult
{
    /// <summary>
    /// The classified intent category (infrastructure, compliance, cost, etc.)
    /// </summary>
    public string IntentCategory { get; set; } = string.Empty;
    
    /// <summary>
    /// The classified action (create, delete, scale, list, etc.)
    /// </summary>
    public string IntentAction { get; set; } = string.Empty;
    
    /// <summary>
    /// Confidence score from 0.0 to 1.0
    /// </summary>
    public decimal Confidence { get; set; }
    
    /// <summary>
    /// The pattern that matched (if any)
    /// </summary>
    public IntentPattern? MatchedPattern { get; set; }
    
    /// <summary>
    /// Extracted parameters as JSON
    /// </summary>
    public string? ExtractedParameters { get; set; }
    
    /// <summary>
    /// Whether classification was successful
    /// </summary>
    public bool IsClassified => !string.IsNullOrEmpty(IntentCategory);
    
    /// <summary>
    /// Alternative classifications with lower confidence
    /// </summary>
    public List<AlternativeClassification> Alternatives { get; set; } = new();
}

/// <summary>
/// Alternative classification suggestion
/// </summary>
public class AlternativeClassification
{
    public string IntentCategory { get; set; } = string.Empty;
    public string IntentAction { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
}

/// <summary>
/// Pattern match result
/// </summary>
public class PatternMatch
{
    public IntentPattern Pattern { get; set; } = null!;
    public decimal MatchScore { get; set; }
    public Dictionary<string, string> ExtractedValues { get; set; } = new();
}

/// <summary>
/// Aggregated intent analytics
/// </summary>
public class IntentAnalytics
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    
    /// <summary>
    /// Total number of intents recorded
    /// </summary>
    public int TotalIntents { get; set; }
    
    /// <summary>
    /// Number of successful intents
    /// </summary>
    public int SuccessfulIntents { get; set; }
    
    /// <summary>
    /// Overall success rate
    /// </summary>
    public decimal OverallSuccessRate { get; set; }
    
    /// <summary>
    /// Intent counts by category
    /// </summary>
    public Dictionary<string, int> IntentsByCategory { get; set; } = new();
    
    /// <summary>
    /// Success rates by category
    /// </summary>
    public Dictionary<string, decimal> SuccessRatesByCategory { get; set; } = new();
    
    /// <summary>
    /// Top patterns by usage
    /// </summary>
    public List<PatternUsage> TopPatterns { get; set; } = new();
}

/// <summary>
/// Pattern usage statistics
/// </summary>
public class PatternUsage
{
    public Guid PatternId { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string IntentCategory { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public decimal SuccessRate { get; set; }
}
