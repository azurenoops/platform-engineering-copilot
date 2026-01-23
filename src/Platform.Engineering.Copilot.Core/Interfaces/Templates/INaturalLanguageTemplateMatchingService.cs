using Platform.Engineering.Copilot.Core.Models.TemplateMatching;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Core.Interfaces.Templates;

/// <summary>
/// Interface for natural language template matching service.
/// Uses LLM to semantically match user requests to available templates.
/// </summary>
public interface INaturalLanguageTemplateMatchingService
{
    /// <summary>
    /// Match a natural language request to available templates.
    /// Uses LLM for semantic understanding when available, falls back to keyword matching.
    /// </summary>
    /// <param name="userRequest">Natural language description of what the user needs</param>
    /// <param name="options">Optional matching configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ranked list of matching templates with scores and reasoning</returns>
    Task<TemplateMatchResult> MatchTemplatesAsync(
        string userRequest,
        TemplateMatchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract suggested parameter values from a natural language request.
    /// </summary>
    /// <param name="userRequest">Natural language description containing parameter hints</param>
    /// <param name="template">The template to extract parameters for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted parameters with confidence scores</returns>
    Task<ParameterExtractionResult> ExtractParametersAsync(
        string userRequest,
        ServiceTemplate template,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate an explanation for why a template matches (or doesn't match) a request.
    /// </summary>
    /// <param name="userRequest">The user's original request</param>
    /// <param name="template">The template to explain</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Human-readable explanation</returns>
    Task<string> ExplainMatchAsync(
        string userRequest,
        ServiceTemplate template,
        CancellationToken cancellationToken = default);
}
