using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Natural language template matching using weighted keyword overlap scoring.
/// Extracts keywords from user descriptions and matches against template metadata (keywords, useCases, aiSelectionHints, categories, descriptions).
/// </summary>
public class NaturalLanguageTemplateMatchingService : INaturalLanguageTemplateMatchingService
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<NaturalLanguageTemplateMatchingService> _logger;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "shall", "should",
        "may", "might", "must", "can", "could", "i", "we", "you", "he", "she", "it",
        "they", "me", "us", "him", "her", "them", "my", "our", "your", "his", "its",
        "their", "this", "that", "these", "those", "and", "but", "or", "nor", "not",
        "so", "if", "then", "than", "too", "very", "just", "about", "above", "after",
        "again", "all", "also", "any", "because", "before", "between", "both",
        "each", "for", "from", "how", "in", "into", "more", "most", "no", "of",
        "on", "only", "other", "out", "over", "own", "same", "some", "such",
        "to", "up", "want", "need", "with", "what", "when", "where", "which", "while", "who"
    };

    // Weight factors for different match sources
    private const double KeywordWeight = 1.0;
    private const double UseCaseWeight = 0.9;
    private const double AiHintWeight = 0.85;
    private const double CategoryWeight = 0.7;
    private const double DescriptionWeight = 0.5;
    private const double NameWeight = 0.6;

    public NaturalLanguageTemplateMatchingService(PlatformEngineeringCopilotContext context,
        ILogger<NaturalLanguageTemplateMatchingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<object> MatchTemplatesAsync(string description, double minScore = 0.3, int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var queryTokens = Tokenize(description);
        if (queryTokens.Count == 0)
        {
            return new { matches = Array.Empty<object>(), totalCandidates = 0, queryTokens = Array.Empty<string>() };
        }

        var templates = await _context.ServiceTemplates
            .Where(t => t.Status == TemplateStatus.Published)
            .ToListAsync(cancellationToken);

        var matches = new List<(ServiceTemplate Template, double Score, Dictionary<string, double> Factors)>();

        foreach (var template in templates)
        {
            var factors = new Dictionary<string, double>();
            double totalScore = 0;
            int factorCount = 0;

            // Match against keywords
            var keywordScore = CalculateOverlap(queryTokens, Tokenize(template.Keywords ?? ""));
            if (keywordScore > 0) { factors["keywords"] = keywordScore * KeywordWeight; totalScore += factors["keywords"]; factorCount++; }

            // Match against use cases
            var useCaseScore = CalculateOverlap(queryTokens, Tokenize(template.UseCases ?? ""));
            if (useCaseScore > 0) { factors["useCases"] = useCaseScore * UseCaseWeight; totalScore += factors["useCases"]; factorCount++; }

            // Match against AI selection hints
            var hintScore = CalculateOverlap(queryTokens, Tokenize(template.AiSelectionHints ?? ""));
            if (hintScore > 0) { factors["aiHints"] = hintScore * AiHintWeight; totalScore += factors["aiHints"]; factorCount++; }

            // Match against category
            var categoryScore = CalculateOverlap(queryTokens, Tokenize(template.Category ?? ""));
            if (categoryScore > 0) { factors["category"] = categoryScore * CategoryWeight; totalScore += factors["category"]; factorCount++; }

            // Match against description
            var descScore = CalculateOverlap(queryTokens, Tokenize(template.Description ?? ""));
            if (descScore > 0) { factors["description"] = descScore * DescriptionWeight; totalScore += factors["description"]; factorCount++; }

            // Match against name
            var nameScore = CalculateOverlap(queryTokens, Tokenize(template.Name));
            if (nameScore > 0) { factors["name"] = nameScore * NameWeight; totalScore += factors["name"]; factorCount++; }

            var finalScore = factorCount > 0 ? totalScore / factorCount : 0;
            if (finalScore >= minScore)
            {
                matches.Add((template, finalScore, factors));
            }
        }

        var ranked = matches
            .OrderByDescending(m => m.Score)
            .Take(maxResults)
            .Select(m => new
            {
                templateId = m.Template.TemplateId,
                name = m.Template.Name,
                displayName = m.Template.DisplayName,
                category = m.Template.Category,
                description = m.Template.Description,
                score = Math.Round(m.Score, 4),
                matchingFactors = m.Factors
            })
            .ToList();

        _logger.LogInformation("NL match for '{Description}': {MatchCount} results from {TotalCandidates} candidates",
            description, ranked.Count, templates.Count);

        return new { matches = ranked, totalCandidates = templates.Count, queryTokens = queryTokens.ToArray() };
    }

    public async Task<object> ExtractParametersAsync(Guid templateId, string description,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        var parameters = new List<object>();

        // Parse template parameters from ParametersJson
        if (!string.IsNullOrWhiteSpace(template.ParametersJson))
        {
            try
            {
                var paramsDoc = System.Text.Json.JsonDocument.Parse(template.ParametersJson);
                var descTokens = description.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var prop in paramsDoc.RootElement.EnumerateObject())
                {
                    string? extractedValue = null;
                    double confidence = 0;

                    // Try to extract values from description based on parameter name
                    var paramName = prop.Name.ToLowerInvariant();
                    for (int i = 0; i < descTokens.Length; i++)
                    {
                        if (descTokens[i].Contains(paramName) && i + 1 < descTokens.Length)
                        {
                            extractedValue = descTokens[i + 1];
                            confidence = 0.6;
                            break;
                        }
                    }

                    parameters.Add(new
                    {
                        name = prop.Name,
                        extractedValue,
                        confidence,
                        source = extractedValue is not null ? "description" : "not_found"
                    });
                }
            }
            catch (System.Text.Json.JsonException)
            {
                _logger.LogWarning("Failed to parse ParametersJson for template {TemplateId}", templateId);
            }
        }

        return new
        {
            templateId,
            templateName = template.Name,
            description,
            parameters,
            extractedCount = parameters.Count(p => ((dynamic)p).extractedValue is not null)
        };
    }

    public async Task<object> ExplainMatchAsync(Guid templateId, string description,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        var queryTokens = Tokenize(description);
        var explanations = new List<object>();

        void AddExplanation(string source, string? content, double weight)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            var tokens = Tokenize(content);
            var overlap = CalculateOverlap(queryTokens, tokens);
            var matchedTokens = queryTokens.Intersect(tokens, StringComparer.OrdinalIgnoreCase).ToList();
            if (overlap > 0)
            {
                explanations.Add(new { source, score = Math.Round(overlap * weight, 4), matchedTokens, weight });
            }
        }

        AddExplanation("keywords", template.Keywords, KeywordWeight);
        AddExplanation("useCases", template.UseCases, UseCaseWeight);
        AddExplanation("aiSelectionHints", template.AiSelectionHints, AiHintWeight);
        AddExplanation("category", template.Category, CategoryWeight);
        AddExplanation("description", template.Description, DescriptionWeight);
        AddExplanation("name", template.Name, NameWeight);

        var totalScore = explanations.Count > 0
            ? explanations.Average(e => (double)((dynamic)e).score)
            : 0;

        return new
        {
            templateId,
            templateName = template.Name,
            description,
            queryTokens = queryTokens.ToArray(),
            overallScore = Math.Round(totalScore, 4),
            factors = explanations,
            recommendation = totalScore >= 0.3 ? "Good match" : "Weak match"
        };
    }

    private static List<string> Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ';', ':', '-', '_', '/', '\\', '(', ')', '[', ']', '{', '}', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .Distinct()
            .ToList();
    }

    private static double CalculateOverlap(List<string> queryTokens, List<string> targetTokens)
    {
        if (queryTokens.Count == 0 || targetTokens.Count == 0) return 0;

        var matchCount = queryTokens.Count(qt =>
            targetTokens.Any(tt => tt.Contains(qt) || qt.Contains(tt)));

        return (double)matchCount / queryTokens.Count;
    }
}
