using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// EF Core implementation of semantic intent repository
/// </summary>
public class SemanticIntentRepository : ISemanticIntentRepository
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<SemanticIntentRepository> _logger;

    public SemanticIntentRepository(
        PlatformEngineeringCopilotContext context,
        ILogger<SemanticIntentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ==================== SemanticIntent Operations ====================

    public async Task<SemanticIntent> CreateIntentAsync(SemanticIntent intent, CancellationToken cancellationToken = default)
    {
        intent.Id = intent.Id == Guid.Empty ? Guid.NewGuid() : intent.Id;
        intent.CreatedAt = DateTime.UtcNow;

        _context.SemanticIntents.Add(intent);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created SemanticIntent {IntentId} for user {UserId}, category: {Category}",
            intent.Id, intent.UserId, intent.IntentCategory);

        return intent;
    }

    public async Task<SemanticIntent?> GetIntentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SemanticIntents
            .Include(i => i.Feedback)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SemanticIntent>> GetIntentsByUserAsync(string userId, int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SemanticIntents
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt);

        if (limit.HasValue)
        {
            return await query.Take(limit.Value).ToListAsync(cancellationToken);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SemanticIntent>> GetIntentsByCategoryAsync(string category, int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SemanticIntents
            .Where(i => i.IntentCategory == category)
            .OrderByDescending(i => i.CreatedAt);

        if (limit.HasValue)
        {
            return await query.Take(limit.Value).ToListAsync(cancellationToken);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SemanticIntent>> GetIntentsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? userId = null,
        string? category = null,
        bool? wasSuccessful = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SemanticIntents.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(i => i.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(i => i.CreatedAt <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(i => i.UserId == userId);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(i => i.IntentCategory == category);

        if (wasSuccessful.HasValue)
            query = query.Where(i => i.WasSuccessful == wasSuccessful.Value);

        query = query.OrderByDescending(i => i.CreatedAt);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<SemanticIntent> UpdateIntentAsync(SemanticIntent intent, CancellationToken cancellationToken = default)
    {
        _context.SemanticIntents.Update(intent);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated SemanticIntent {IntentId}", intent.Id);
        return intent;
    }

    public async Task<SemanticIntent> UpdateIntentOutcomeAsync(Guid intentId, bool wasSuccessful, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        var intent = await _context.SemanticIntents.FindAsync(new object[] { intentId }, cancellationToken);
        if (intent == null)
        {
            throw new InvalidOperationException($"SemanticIntent {intentId} not found");
        }

        intent.WasSuccessful = wasSuccessful;
        intent.ErrorMessage = errorMessage;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated SemanticIntent {IntentId} outcome: {WasSuccessful}", intentId, wasSuccessful);
        return intent;
    }

    public async Task<bool> DeleteIntentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var intent = await _context.SemanticIntents.FindAsync(new object[] { id }, cancellationToken);
        if (intent == null)
            return false;

        _context.SemanticIntents.Remove(intent);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted SemanticIntent {IntentId}", id);
        return true;
    }

    // ==================== IntentFeedback Operations ====================

    public async Task<IntentFeedback> AddFeedbackAsync(IntentFeedback feedback, CancellationToken cancellationToken = default)
    {
        feedback.Id = feedback.Id == Guid.Empty ? Guid.NewGuid() : feedback.Id;
        feedback.CreatedAt = DateTime.UtcNow;

        _context.IntentFeedback.Add(feedback);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added IntentFeedback {FeedbackId} for intent {IntentId}, type: {FeedbackType}",
            feedback.Id, feedback.IntentId, feedback.FeedbackType);

        return feedback;
    }

    public async Task<IReadOnlyList<IntentFeedback>> GetFeedbackByIntentAsync(Guid intentId, CancellationToken cancellationToken = default)
    {
        return await _context.IntentFeedback
            .Where(f => f.IntentId == intentId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IntentFeedback>> GetFeedbackByTypeAsync(string feedbackType, int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = _context.IntentFeedback
            .Where(f => f.FeedbackType == feedbackType)
            .OrderByDescending(f => f.CreatedAt);

        if (limit.HasValue)
        {
            return await query.Take(limit.Value).ToListAsync(cancellationToken);
        }

        return await query.ToListAsync(cancellationToken);
    }

    // ==================== IntentPattern Operations ====================

    public async Task<IReadOnlyList<IntentPattern>> GetPatternsByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _context.IntentPatterns
            .Where(p => p.IntentCategory == category && p.IsActive)
            .OrderByDescending(p => p.Weight)
            .ThenByDescending(p => p.SuccessRate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IntentPattern>> GetActivePatternsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.IntentPatterns
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Weight)
            .ThenByDescending(p => p.SuccessRate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IntentPattern> CreatePatternAsync(IntentPattern pattern, CancellationToken cancellationToken = default)
    {
        pattern.Id = pattern.Id == Guid.Empty ? Guid.NewGuid() : pattern.Id;
        pattern.CreatedAt = DateTime.UtcNow;
        pattern.UpdatedAt = DateTime.UtcNow;

        _context.IntentPatterns.Add(pattern);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created IntentPattern {PatternId} for category {Category}",
            pattern.Id, pattern.IntentCategory);

        return pattern;
    }

    public async Task<IntentPattern> UpdatePatternStatsAsync(Guid patternId, bool wasSuccessful, CancellationToken cancellationToken = default)
    {
        var pattern = await _context.IntentPatterns.FindAsync(new object[] { patternId }, cancellationToken);
        if (pattern == null)
        {
            throw new InvalidOperationException($"IntentPattern {patternId} not found");
        }

        pattern.UsageCount++;
        if (wasSuccessful)
        {
            pattern.SuccessCount++;
        }
        pattern.SuccessRate = pattern.UsageCount > 0 
            ? (decimal)pattern.SuccessCount / pattern.UsageCount 
            : 0;
        pattern.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated IntentPattern {PatternId} stats: usage={UsageCount}, success={SuccessCount}, rate={SuccessRate:P2}",
            patternId, pattern.UsageCount, pattern.SuccessCount, pattern.SuccessRate);

        return pattern;
    }

    public async Task<bool> DeactivatePatternAsync(Guid patternId, CancellationToken cancellationToken = default)
    {
        var pattern = await _context.IntentPatterns.FindAsync(new object[] { patternId }, cancellationToken);
        if (pattern == null)
            return false;

        pattern.IsActive = false;
        pattern.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deactivated IntentPattern {PatternId}", patternId);
        return true;
    }

    // ==================== Analytics Operations ====================

    public async Task<Dictionary<string, int>> GetIntentCountsByCategoryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SemanticIntents.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(i => i.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(i => i.CreatedAt <= toDate.Value);

        return await query
            .GroupBy(i => i.IntentCategory)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count, cancellationToken);
    }

    public async Task<Dictionary<string, decimal>> GetSuccessRatesByCategoryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SemanticIntents.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(i => i.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(i => i.CreatedAt <= toDate.Value);

        var grouped = await query
            .GroupBy(i => i.IntentCategory)
            .Select(g => new 
            { 
                Category = g.Key, 
                Total = g.Count(),
                Successful = g.Count(i => i.WasSuccessful)
            })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(
            x => x.Category, 
            x => x.Total > 0 ? (decimal)x.Successful / x.Total : 0m);
    }

    public async Task<int> GetTotalIntentCountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SemanticIntents.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(i => i.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(i => i.CreatedAt <= toDate.Value);

        return await query.CountAsync(cancellationToken);
    }
}
