namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Natural language template matching using weighted keyword overlap scoring.
/// </summary>
public interface INaturalLanguageTemplateMatchingService
{
    Task<object> MatchTemplatesAsync(string description, double minScore = 0.3, int maxResults = 5,
        CancellationToken cancellationToken = default);

    Task<object> ExtractParametersAsync(Guid templateId, string description,
        CancellationToken cancellationToken = default);

    Task<object> ExplainMatchAsync(Guid templateId, string description,
        CancellationToken cancellationToken = default);
}
