using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Repositories;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Service implementation for semantic intent tracking and classification
/// </summary>
public class SemanticIntentService : ISemanticIntentService
{
    private readonly ISemanticIntentRepository _repository;
    private readonly ILogger<SemanticIntentService> _logger;
    
    // Cache patterns to avoid repeated DB calls
    private IReadOnlyList<IntentPattern>? _cachedPatterns;
    private DateTime _patternsCacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _patternsCacheDuration = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _patternsCacheLock = new(1, 1);

    public SemanticIntentService(
        ISemanticIntentRepository repository,
        ILogger<SemanticIntentService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // ==================== Intent Recording ====================

    public async Task<SemanticIntent> RecordIntentAsync(
        string userId,
        string userInput,
        string intentCategory,
        string intentAction,
        decimal confidence,
        string? sessionId = null,
        string? extractedParameters = null,
        string? resolvedToolCall = null,
        CancellationToken cancellationToken = default)
    {
        var intent = new SemanticIntent
        {
            UserId = userId,
            UserInput = userInput.Length > 500 ? userInput[..500] : userInput, // Truncate to fit column
            IntentCategory = intentCategory,
            IntentAction = intentAction,
            Confidence = confidence,
            SessionId = sessionId,
            ExtractedParameters = extractedParameters,
            ResolvedToolCall = resolvedToolCall,
            WasSuccessful = false // Will be updated after execution
        };

        var created = await _repository.CreateIntentAsync(intent, cancellationToken);

        _logger.LogInformation(
            "📊 Recorded intent {IntentId}: [{Category}/{Action}] confidence={Confidence:P2} for user {UserId}",
            created.Id, intentCategory, intentAction, confidence, userId);

        return created;
    }

    public async Task<SemanticIntent> UpdateIntentOutcomeAsync(
        Guid intentId,
        bool wasSuccessful,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var updated = await _repository.UpdateIntentOutcomeAsync(intentId, wasSuccessful, errorMessage, cancellationToken);

        // Also update pattern stats if we know which pattern was used
        if (!string.IsNullOrEmpty(updated.ExtractedParameters))
        {
            try
            {
                var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(updated.ExtractedParameters);
                if (parameters?.TryGetValue("_matchedPatternId", out var patternIdObj) == true 
                    && Guid.TryParse(patternIdObj?.ToString(), out var patternId))
                {
                    await _repository.UpdatePatternStatsAsync(patternId, wasSuccessful, cancellationToken);
                }
            }
            catch
            {
                // Ignore parameter parsing errors
            }
        }

        _logger.LogInformation(
            "📊 Updated intent {IntentId} outcome: {WasSuccessful}{Error}",
            intentId, wasSuccessful ? "✅ Success" : "❌ Failed", 
            errorMessage != null ? $" - {errorMessage}" : "");

        return updated;
    }

    // ==================== Intent Classification ====================

    public async Task<SemanticClassificationResult> ClassifyIntentAsync(
        string userInput,
        CancellationToken cancellationToken = default)
    {
        var patterns = await GetCachedPatternsAsync(cancellationToken);
        var matches = new List<(IntentPattern Pattern, decimal Score, Dictionary<string, string> ExtractedValues)>();

        foreach (var pattern in patterns)
        {
            var (isMatch, score, extractedValues) = TryMatchPattern(userInput, pattern);
            if (isMatch)
            {
                matches.Add((pattern, score, extractedValues));
            }
        }

        if (matches.Count == 0)
        {
            _logger.LogDebug("No pattern matched for input: {Input}", userInput);
            return new SemanticClassificationResult
            {
                Confidence = 0
            };
        }

        // Sort by weighted score (pattern weight * match score * success rate)
        var rankedMatches = matches
            .OrderByDescending(m => m.Pattern.Weight * m.Score * (0.5m + m.Pattern.SuccessRate * 0.5m))
            .ToList();

        var bestMatch = rankedMatches.First();
        var result = new SemanticClassificationResult
        {
            IntentCategory = bestMatch.Pattern.IntentCategory,
            IntentAction = bestMatch.Pattern.IntentAction,
            Confidence = bestMatch.Score * bestMatch.Pattern.Weight,
            MatchedPattern = bestMatch.Pattern,
            ExtractedParameters = SerializeParameters(bestMatch.ExtractedValues, bestMatch.Pattern.Id)
        };

        // Add alternatives
        foreach (var alt in rankedMatches.Skip(1).Take(3))
        {
            result.Alternatives.Add(new AlternativeClassification
            {
                IntentCategory = alt.Pattern.IntentCategory,
                IntentAction = alt.Pattern.IntentAction,
                Confidence = alt.Score * alt.Pattern.Weight
            });
        }

        _logger.LogDebug(
            "Classified input as [{Category}/{Action}] with {Confidence:P2} confidence, {AltCount} alternatives",
            result.IntentCategory, result.IntentAction, result.Confidence, result.Alternatives.Count);

        return result;
    }

    public async Task<IReadOnlyList<PatternMatch>> GetMatchingPatternsAsync(
        string userInput,
        CancellationToken cancellationToken = default)
    {
        var patterns = await GetCachedPatternsAsync(cancellationToken);
        var matches = new List<PatternMatch>();

        foreach (var pattern in patterns)
        {
            var (isMatch, score, extractedValues) = TryMatchPattern(userInput, pattern);
            if (isMatch)
            {
                matches.Add(new PatternMatch
                {
                    Pattern = pattern,
                    MatchScore = score,
                    ExtractedValues = extractedValues
                });
            }
        }

        return matches.OrderByDescending(m => m.MatchScore).ToList();
    }

    // ==================== Feedback ====================

    public async Task<IntentFeedback> SubmitFeedbackAsync(
        Guid intentId,
        string feedbackType,
        string providedBy,
        string? correctCategory = null,
        string? correctAction = null,
        string? correctParameters = null,
        CancellationToken cancellationToken = default)
    {
        // Validate feedback type
        var validTypes = new[] { "correct", "incorrect", "partial" };
        if (!validTypes.Contains(feedbackType.ToLowerInvariant()))
        {
            throw new ArgumentException($"Invalid feedback type. Must be one of: {string.Join(", ", validTypes)}");
        }

        var feedback = new IntentFeedback
        {
            IntentId = intentId,
            FeedbackType = feedbackType.ToLowerInvariant(),
            ProvidedBy = providedBy,
            CorrectIntentCategory = correctCategory,
            CorrectIntentAction = correctAction,
            CorrectParameters = correctParameters
        };

        var created = await _repository.AddFeedbackAsync(feedback, cancellationToken);

        _logger.LogInformation(
            "📝 Feedback submitted for intent {IntentId}: {FeedbackType} by {ProvidedBy}",
            intentId, feedbackType, providedBy);

        // If feedback indicates incorrect classification, consider creating a new pattern
        if (feedbackType == "incorrect" && !string.IsNullOrEmpty(correctCategory))
        {
            _logger.LogInformation(
                "💡 Consider creating new pattern: User expected [{CorrectCategory}/{CorrectAction}]",
                correctCategory, correctAction ?? "unknown");
        }

        return created;
    }

    // ==================== User History ====================

    public async Task<IReadOnlyList<SemanticIntent>> GetUserIntentHistoryAsync(
        string userId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetIntentsByUserAsync(userId, count, cancellationToken);
    }

    public async Task<SemanticIntent?> GetLastUserIntentAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var history = await _repository.GetIntentsByUserAsync(userId, 1, cancellationToken);
        return history.FirstOrDefault();
    }

    // ==================== Analytics ====================

    public async Task<IntentAnalytics> GetAnalyticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        var totalCount = await _repository.GetTotalIntentCountAsync(from, to, cancellationToken);
        var countsByCategory = await _repository.GetIntentCountsByCategoryAsync(from, to, cancellationToken);
        var successRates = await _repository.GetSuccessRatesByCategoryAsync(from, to, cancellationToken);
        
        // Get successful count
        var successfulIntents = await _repository.GetIntentsAsync(from, to, wasSuccessful: true, cancellationToken: cancellationToken);

        // Get top patterns
        var patterns = await _repository.GetActivePatternsAsync(cancellationToken);
        var topPatterns = patterns
            .OrderByDescending(p => p.UsageCount)
            .Take(10)
            .Select(p => new PatternUsage
            {
                PatternId = p.Id,
                Pattern = p.Pattern,
                IntentCategory = p.IntentCategory,
                UsageCount = p.UsageCount,
                SuccessRate = p.SuccessRate
            })
            .ToList();

        return new IntentAnalytics
        {
            FromDate = from,
            ToDate = to,
            TotalIntents = totalCount,
            SuccessfulIntents = successfulIntents.Count,
            OverallSuccessRate = totalCount > 0 ? (decimal)successfulIntents.Count / totalCount : 0,
            IntentsByCategory = countsByCategory,
            SuccessRatesByCategory = successRates,
            TopPatterns = topPatterns
        };
    }

    // ==================== Private Helpers ====================

    private async Task<IReadOnlyList<IntentPattern>> GetCachedPatternsAsync(CancellationToken cancellationToken)
    {
        if (_cachedPatterns != null && DateTime.UtcNow < _patternsCacheExpiry)
        {
            return _cachedPatterns;
        }

        await _patternsCacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedPatterns != null && DateTime.UtcNow < _patternsCacheExpiry)
            {
                return _cachedPatterns;
            }

            _cachedPatterns = await _repository.GetActivePatternsAsync(cancellationToken);
            _patternsCacheExpiry = DateTime.UtcNow.Add(_patternsCacheDuration);

            _logger.LogDebug("Refreshed patterns cache: {Count} active patterns", _cachedPatterns.Count);

            return _cachedPatterns;
        }
        finally
        {
            _patternsCacheLock.Release();
        }
    }

    private (bool IsMatch, decimal Score, Dictionary<string, string> ExtractedValues) TryMatchPattern(
        string userInput, 
        IntentPattern pattern)
    {
        var extractedValues = new Dictionary<string, string>();
        var normalizedInput = userInput.ToLowerInvariant().Trim();
        var patternText = pattern.Pattern.ToLowerInvariant().Trim();

        try
        {
            // Try regex match first
            if (patternText.StartsWith("^") || patternText.Contains("(") || patternText.Contains("["))
            {
                var regex = new Regex(patternText, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
                var match = regex.Match(normalizedInput);
                
                if (match.Success)
                {
                    // Extract named groups
                    foreach (var groupName in regex.GetGroupNames().Where(n => !int.TryParse(n, out _)))
                    {
                        var group = match.Groups[groupName];
                        if (group.Success)
                        {
                            extractedValues[groupName] = group.Value;
                        }
                    }
                    
                    // Calculate score based on match coverage
                    var coverage = (decimal)match.Length / normalizedInput.Length;
                    return (true, Math.Min(1.0m, coverage + 0.2m), extractedValues);
                }
            }

            // Keyword-based matching
            var keywords = patternText.Split(new[] { ' ', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            var matchedKeywords = keywords.Count(k => normalizedInput.Contains(k));
            
            if (matchedKeywords > 0)
            {
                var score = (decimal)matchedKeywords / keywords.Length;
                
                // Require at least 50% keyword match for non-regex patterns
                if (score >= 0.5m)
                {
                    return (true, score, extractedValues);
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.LogWarning("Regex timeout for pattern {PatternId}: {Pattern}", pattern.Id, pattern.Pattern);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid regex pattern {PatternId}: {Error}", pattern.Id, ex.Message);
        }

        return (false, 0, extractedValues);
    }

    private string? SerializeParameters(Dictionary<string, string> extractedValues, Guid patternId)
    {
        if (extractedValues.Count == 0)
        {
            return null;
        }

        // Add pattern ID for tracking
        extractedValues["_matchedPatternId"] = patternId.ToString();

        return JsonSerializer.Serialize(extractedValues);
    }
}
