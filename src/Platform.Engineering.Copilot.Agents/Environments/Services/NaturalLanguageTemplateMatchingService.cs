using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;
using Platform.Engineering.Copilot.Core.Models.TemplateMatching;

namespace Platform.Engineering.Copilot.Agents.Environments.Services;

/// <summary>
/// Service for matching natural language requests to service templates using LLM.
/// Provides semantic understanding of user requests and intelligent template recommendations.
/// </summary>
public class NaturalLanguageTemplateMatchingService : INaturalLanguageTemplateMatchingService
{
    private readonly ILogger<NaturalLanguageTemplateMatchingService> _logger;
    private readonly IServiceTemplateCatalogService _catalogService;
    private readonly IChatCompletionService? _chatService;
    private readonly Kernel? _kernel;

    // System prompt for template matching
    private const string TemplateMatchingSystemPrompt = @"You are an expert Azure infrastructure architect helping match user requests to pre-approved infrastructure templates.

Your task is to:
1. Understand what the user needs (web app, database, storage, networking, etc.)
2. Match their requirements to the available templates
3. Rank templates by relevance (1.0 = perfect match, 0.0 = no match)
4. Extract any parameters mentioned in the user's request
5. Explain why each template is a good or poor match

Be precise and only recommend templates that actually match the user's needs.
Consider:
- Security requirements (FedRAMP, NIST, etc.)
- Scale requirements (dev/test vs production)
- Technology stack preferences
- Compliance requirements
- Cost considerations";

    public NaturalLanguageTemplateMatchingService(
        ILogger<NaturalLanguageTemplateMatchingService> logger,
        IServiceTemplateCatalogService catalogService,
        Kernel? kernel = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _kernel = kernel;
        _chatService = kernel?.GetRequiredService<IChatCompletionService>();
    }

    /// <summary>
    /// Match a natural language request to available templates using LLM.
    /// </summary>
    public async Task<TemplateMatchResult> MatchTemplatesAsync(
        string userRequest,
        TemplateMatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new TemplateMatchOptions();

        _logger.LogInformation("🔍 Matching templates for request: {Request}", 
            userRequest.Length > 100 ? userRequest[..100] + "..." : userRequest);

        // Get available templates
        var templates = await _catalogService.GetPublishedTemplatesAsync(cancellationToken);
        
        if (templates.Count == 0)
        {
            return new TemplateMatchResult
            {
                Success = false,
                UserRequest = userRequest,
                Message = "No published templates available in the catalog.",
                Matches = new List<TemplateMatch>()
            };
        }

        // If LLM is available, use semantic matching
        if (_chatService != null)
        {
            return await MatchWithLlmAsync(userRequest, templates, options, cancellationToken);
        }

        // Fallback to keyword-based matching
        return await MatchWithKeywordsAsync(userRequest, templates, options, cancellationToken);
    }

    /// <summary>
    /// Extract suggested parameter values from natural language request.
    /// </summary>
    public async Task<ParameterExtractionResult> ExtractParametersAsync(
        string userRequest,
        ServiceTemplate template,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📝 Extracting parameters for template {Template} from request", template.Name);

        var result = new ParameterExtractionResult
        {
            TemplateId = template.Id,
            TemplateName = template.Name,
            ExtractedParameters = new Dictionary<string, ExtractedParameter>()
        };

        if (_chatService == null || template.Parameters.Count == 0)
        {
            // Return defaults if no LLM or no parameters
            foreach (var param in template.Parameters)
            {
                result.ExtractedParameters[param.Name] = new ExtractedParameter
                {
                    ParameterName = param.Name,
                    SuggestedValue = param.DefaultValue,
                    Confidence = 0.5,
                    Source = "default",
                    Reasoning = "Using template default value"
                };
            }
            return result;
        }

        try
        {
            var parametersDescription = BuildParametersDescription(template.Parameters);
            
            var prompt = $@"Based on the user's request, extract parameter values for the following template parameters.
Return a JSON object with parameter names as keys and objects containing 'value', 'confidence' (0.0-1.0), and 'reasoning'.

Template: {template.DisplayName}
Description: {template.Description}

Available Parameters:
{parametersDescription}

User Request: ""{userRequest}""

Return ONLY valid JSON in this format:
{{
    ""parameterName"": {{
        ""value"": ""extracted or default value"",
        ""confidence"": 0.8,
        ""reasoning"": ""why this value was chosen""
    }}
}}";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a parameter extraction assistant. Extract parameter values from user requests. Return only valid JSON.");
            chatHistory.AddUserMessage(prompt);

            var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            var responseText = response.Content ?? "{}";

            // Parse the JSON response
            var extracted = ParseParameterExtractionResponse(responseText, template.Parameters);
            result.ExtractedParameters = extracted;
            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract parameters with LLM, using defaults");
            
            foreach (var param in template.Parameters)
            {
                result.ExtractedParameters[param.Name] = new ExtractedParameter
                {
                    ParameterName = param.Name,
                    SuggestedValue = param.DefaultValue,
                    Confidence = 0.3,
                    Source = "default",
                    Reasoning = "LLM extraction failed, using default"
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Explain why a template matches (or doesn't match) a user request.
    /// </summary>
    public async Task<string> ExplainMatchAsync(
        string userRequest,
        ServiceTemplate template,
        CancellationToken cancellationToken = default)
    {
        if (_chatService == null)
        {
            return $"Template '{template.DisplayName}' provides {template.Description}";
        }

        try
        {
            var prompt = $@"Explain concisely (2-3 sentences) why the template '{template.DisplayName}' is or isn't a good match for the following user request.

Template:
- Name: {template.DisplayName}
- Category: {template.Category}
- Description: {template.Description}
- Use Cases: {string.Join(", ", template.UseCases)}
- Compliance: {string.Join(", ", template.ComplianceFrameworks)}

User Request: ""{userRequest}""

Provide a brief, helpful explanation.";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a helpful infrastructure advisor. Be concise and specific.");
            chatHistory.AddUserMessage(prompt);

            var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            return response.Content ?? template.Description;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate explanation with LLM");
            return template.Description;
        }
    }

    #region Private Methods

    private async Task<TemplateMatchResult> MatchWithLlmAsync(
        string userRequest,
        List<ServiceTemplate> templates,
        TemplateMatchOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var templatesCatalog = BuildTemplatesCatalog(templates);

            var prompt = $@"Match the user's request to the available templates. Return a JSON array of matches.

Available Templates:
{templatesCatalog}

User Request: ""{userRequest}""

Return ONLY a JSON array in this exact format (no markdown, no explanation):
[
    {{
        ""templateId"": ""template-id"",
        ""score"": 0.95,
        ""reasoning"": ""Why this template matches"",
        ""suggestedParameters"": {{
            ""paramName"": ""suggestedValue""
        }}
    }}
]

Rules:
- Score from 0.0 (no match) to 1.0 (perfect match)
- Only include templates with score >= {options.MinimumScore}
- Order by score descending
- Maximum {options.MaxResults} results
- Be conservative with scores - only high scores for actual matches";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(TemplateMatchingSystemPrompt);
            chatHistory.AddUserMessage(prompt);

            var response = await _chatService!.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            var responseText = response.Content ?? "[]";

            var matches = ParseMatchResponse(responseText, templates, options);

            _logger.LogInformation("✅ LLM matched {Count} templates for request", matches.Count);

            return new TemplateMatchResult
            {
                Success = true,
                UserRequest = userRequest,
                Message = matches.Count > 0 
                    ? $"Found {matches.Count} matching template(s)" 
                    : "No templates matched your request",
                Matches = matches,
                UsedLlm = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM matching failed, falling back to keyword matching");
            return await MatchWithKeywordsAsync(userRequest, templates, options, cancellationToken);
        }
    }

    private Task<TemplateMatchResult> MatchWithKeywordsAsync(
        string userRequest,
        List<ServiceTemplate> templates,
        TemplateMatchOptions options,
        CancellationToken cancellationToken)
    {
        var requestWords = Tokenize(userRequest.ToLowerInvariant());
        var matches = new List<TemplateMatch>();

        foreach (var template in templates)
        {
            var score = CalculateKeywordScore(requestWords, template);
            
            if (score >= options.MinimumScore)
            {
                matches.Add(new TemplateMatch
                {
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    DisplayName = template.DisplayName,
                    Category = template.Category,
                    Score = score,
                    Reasoning = $"Matched keywords in template name, description, and use cases",
                    SuggestedParameters = new Dictionary<string, object?>()
                });
            }
        }

        var orderedMatches = matches
            .OrderByDescending(m => m.Score)
            .Take(options.MaxResults)
            .ToList();

        _logger.LogInformation("✅ Keyword matched {Count} templates for request", orderedMatches.Count);

        return Task.FromResult(new TemplateMatchResult
        {
            Success = true,
            UserRequest = userRequest,
            Message = orderedMatches.Count > 0 
                ? $"Found {orderedMatches.Count} matching template(s) (keyword-based)" 
                : "No templates matched your request",
            Matches = orderedMatches,
            UsedLlm = false
        });
    }

    private double CalculateKeywordScore(HashSet<string> requestWords, ServiceTemplate template)
    {
        var templateWords = new HashSet<string>();
        
        // Collect all template searchable text
        templateWords.UnionWith(Tokenize(template.Name.ToLowerInvariant()));
        templateWords.UnionWith(Tokenize(template.DisplayName.ToLowerInvariant()));
        templateWords.UnionWith(Tokenize(template.Description.ToLowerInvariant()));
        templateWords.UnionWith(Tokenize(template.Category.ToLowerInvariant()));
        
        foreach (var keyword in template.Keywords)
            templateWords.UnionWith(Tokenize(keyword.ToLowerInvariant()));
        
        foreach (var useCase in template.UseCases)
            templateWords.UnionWith(Tokenize(useCase.ToLowerInvariant()));

        // Calculate Jaccard similarity with boosting for important matches
        var intersection = requestWords.Intersect(templateWords).Count();
        var union = requestWords.Union(templateWords).Count();
        
        if (union == 0) return 0.0;

        var baseScore = (double)intersection / union;

        // Boost for category match
        if (requestWords.Any(w => template.Category.ToLowerInvariant().Contains(w)))
            baseScore = Math.Min(1.0, baseScore + 0.15);

        // Boost for name match
        if (requestWords.Any(w => template.Name.ToLowerInvariant().Contains(w)))
            baseScore = Math.Min(1.0, baseScore + 0.2);

        return Math.Round(baseScore, 2);
    }

    private HashSet<string> Tokenize(string text)
    {
        // Remove punctuation and split on whitespace
        var cleaned = Regex.Replace(text, @"[^\w\s]", " ");
        var words = cleaned.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Filter out common stop words
        var stopWords = new HashSet<string> { "a", "an", "the", "is", "are", "was", "were", "be", "been", 
            "being", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", 
            "may", "might", "must", "shall", "can", "need", "to", "of", "in", "for", "on", "with", 
            "at", "by", "from", "as", "into", "through", "during", "before", "after", "above", 
            "below", "between", "under", "again", "further", "then", "once", "here", "there", 
            "when", "where", "why", "how", "all", "each", "few", "more", "most", "other", "some", 
            "such", "no", "nor", "not", "only", "own", "same", "so", "than", "too", "very", "just", 
            "i", "me", "my", "we", "our", "you", "your", "it", "its", "and", "or", "but" };

        return words
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToHashSet();
    }

    private string BuildTemplatesCatalog(List<ServiceTemplate> templates)
    {
        var sb = new StringBuilder();
        
        foreach (var template in templates)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"ID: {template.Id}");
            sb.AppendLine($"Name: {template.DisplayName}");
            sb.AppendLine($"Category: {template.Category}");
            sb.AppendLine($"Description: {template.Description}");
            sb.AppendLine($"Keywords: {string.Join(", ", template.Keywords)}");
            sb.AppendLine($"Use Cases: {string.Join(", ", template.UseCases)}");
            sb.AppendLine($"Compliance: {string.Join(", ", template.ComplianceFrameworks)}");
            
            if (template.Parameters.Any())
            {
                sb.AppendLine($"Parameters:");
                foreach (var param in template.Parameters.Take(5))
                {
                    sb.AppendLine($"  - {param.Name}: {param.Description} (default: {param.DefaultValue})");
                }
            }
        }
        
        return sb.ToString();
    }

    private string BuildParametersDescription(List<TemplateParameter> parameters)
    {
        var sb = new StringBuilder();
        
        foreach (var param in parameters)
        {
            sb.AppendLine($"- {param.Name} ({param.Type}): {param.Description}");
            if (param.DefaultValue != null)
                sb.AppendLine($"  Default: {param.DefaultValue}");
            if (param.AllowedValues.Any())
                sb.AppendLine($"  Allowed values: {string.Join(", ", param.AllowedValues)}");
        }
        
        return sb.ToString();
    }

    private List<TemplateMatch> ParseMatchResponse(string responseText, List<ServiceTemplate> templates, TemplateMatchOptions options)
    {
        var matches = new List<TemplateMatch>();
        
        try
        {
            // Extract JSON from response (in case of markdown wrapping)
            var jsonMatch = Regex.Match(responseText, @"\[[\s\S]*\]");
            if (!jsonMatch.Success)
            {
                _logger.LogWarning("Could not find JSON array in LLM response");
                return matches;
            }

            var jsonArray = JsonSerializer.Deserialize<JsonElement>(jsonMatch.Value);
            
            foreach (var item in jsonArray.EnumerateArray())
            {
                var templateId = item.GetProperty("templateId").GetString();
                var score = item.GetProperty("score").GetDouble();
                var reasoning = item.TryGetProperty("reasoning", out var r) ? r.GetString() : null;
                
                var template = templates.FirstOrDefault(t => t.Id == templateId);
                if (template == null) continue;

                if (score < options.MinimumScore) continue;

                var suggestedParams = new Dictionary<string, object?>();
                if (item.TryGetProperty("suggestedParameters", out var paramsElement))
                {
                    foreach (var prop in paramsElement.EnumerateObject())
                    {
                        suggestedParams[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString()
                        };
                    }
                }

                matches.Add(new TemplateMatch
                {
                    TemplateId = templateId!,
                    TemplateName = template.Name,
                    DisplayName = template.DisplayName,
                    Category = template.Category,
                    Score = Math.Round(score, 2),
                    Reasoning = reasoning ?? template.Description,
                    SuggestedParameters = suggestedParams
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM match response: {Response}", 
                responseText.Length > 200 ? responseText[..200] : responseText);
        }

        return matches.OrderByDescending(m => m.Score).Take(options.MaxResults).ToList();
    }

    private Dictionary<string, ExtractedParameter> ParseParameterExtractionResponse(
        string responseText, 
        List<TemplateParameter> templateParams)
    {
        var result = new Dictionary<string, ExtractedParameter>();
        
        try
        {
            // Extract JSON from response
            var jsonMatch = Regex.Match(responseText, @"\{[\s\S]*\}");
            if (!jsonMatch.Success)
            {
                return result;
            }

            var jsonObj = JsonSerializer.Deserialize<JsonElement>(jsonMatch.Value);
            
            foreach (var param in templateParams)
            {
                if (jsonObj.TryGetProperty(param.Name, out var paramElement))
                {
                    var value = paramElement.TryGetProperty("value", out var v) 
                        ? GetJsonValue(v) 
                        : param.DefaultValue;
                    
                    var confidence = paramElement.TryGetProperty("confidence", out var c) 
                        ? c.GetDouble() 
                        : 0.5;
                    
                    var reasoning = paramElement.TryGetProperty("reasoning", out var r) 
                        ? r.GetString() 
                        : null;

                    result[param.Name] = new ExtractedParameter
                    {
                        ParameterName = param.Name,
                        SuggestedValue = value,
                        Confidence = confidence,
                        Source = "llm",
                        Reasoning = reasoning ?? "Extracted from user request"
                    };
                }
                else
                {
                    result[param.Name] = new ExtractedParameter
                    {
                        ParameterName = param.Name,
                        SuggestedValue = param.DefaultValue,
                        Confidence = 0.3,
                        Source = "default",
                        Reasoning = "Parameter not found in request, using default"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse parameter extraction response");
        }

        return result;
    }

    private object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    #endregion
}
