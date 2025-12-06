using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// Repository interface for semantic intent tracking operations
/// </summary>
public interface ISemanticIntentRepository
{
    // ==================== SemanticIntent Operations ====================
    
    /// <summary>
    /// Create a new semantic intent record
    /// </summary>
    Task<SemanticIntent> CreateIntentAsync(SemanticIntent intent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a semantic intent by ID
    /// </summary>
    Task<SemanticIntent?> GetIntentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all intents for a specific user
    /// </summary>
    Task<IReadOnlyList<SemanticIntent>> GetIntentsByUserAsync(string userId, int? limit = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all intents by category
    /// </summary>
    Task<IReadOnlyList<SemanticIntent>> GetIntentsByCategoryAsync(string category, int? limit = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get intents within a date range with optional filtering
    /// </summary>
    Task<IReadOnlyList<SemanticIntent>> GetIntentsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? userId = null,
        string? category = null,
        bool? wasSuccessful = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing semantic intent
    /// </summary>
    Task<SemanticIntent> UpdateIntentAsync(SemanticIntent intent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Mark an intent as successful or failed
    /// </summary>
    Task<SemanticIntent> UpdateIntentOutcomeAsync(Guid intentId, bool wasSuccessful, string? errorMessage = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a semantic intent
    /// </summary>
    Task<bool> DeleteIntentAsync(Guid id, CancellationToken cancellationToken = default);
    
    // ==================== IntentFeedback Operations ====================
    
    /// <summary>
    /// Add feedback for an intent
    /// </summary>
    Task<IntentFeedback> AddFeedbackAsync(IntentFeedback feedback, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all feedback for a specific intent
    /// </summary>
    Task<IReadOnlyList<IntentFeedback>> GetFeedbackByIntentAsync(Guid intentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get feedback by type (correct, incorrect, partial)
    /// </summary>
    Task<IReadOnlyList<IntentFeedback>> GetFeedbackByTypeAsync(string feedbackType, int? limit = null, CancellationToken cancellationToken = default);
    
    // ==================== IntentPattern Operations ====================
    
    /// <summary>
    /// Get all active patterns for a category
    /// </summary>
    Task<IReadOnlyList<IntentPattern>> GetPatternsByCategoryAsync(string category, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all active patterns
    /// </summary>
    Task<IReadOnlyList<IntentPattern>> GetActivePatternsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new intent pattern
    /// </summary>
    Task<IntentPattern> CreatePatternAsync(IntentPattern pattern, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update pattern usage statistics
    /// </summary>
    Task<IntentPattern> UpdatePatternStatsAsync(Guid patternId, bool wasSuccessful, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deactivate a pattern
    /// </summary>
    Task<bool> DeactivatePatternAsync(Guid patternId, CancellationToken cancellationToken = default);
    
    // ==================== Analytics Operations ====================
    
    /// <summary>
    /// Get intent count by category within date range
    /// </summary>
    Task<Dictionary<string, int>> GetIntentCountsByCategoryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get success rate by category
    /// </summary>
    Task<Dictionary<string, decimal>> GetSuccessRatesByCategoryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get total intent count
    /// </summary>
    Task<int> GetTotalIntentCountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
}
