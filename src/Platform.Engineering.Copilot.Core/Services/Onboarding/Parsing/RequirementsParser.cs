using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Platform.Engineering.Copilot.Core.Services.Onboarding.Parsing;

/// <summary>
/// Multi-strategy parser for onboarding requirements.
/// Supports JSON, bullet lists, comma-separated values, key-value pairs, and natural language.
/// </summary>
public class RequirementsParser
{
    private readonly ILogger<RequirementsParser> _logger;
    private readonly Kernel? _kernel;

    public RequirementsParser(ILogger<RequirementsParser> logger, Kernel? kernel = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kernel = kernel;
    }

    /// <summary>
    /// Parse requirements from any supported format.
    /// Tries multiple strategies in order: JSON → Bullet List → Key-Value → Comma-Separated → LLM
    /// </summary>
    public async Task<Dictionary<string, object?>> ParseAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new Dictionary<string, object?>();
        }

        _logger.LogDebug("Attempting to parse requirements: {Input}", input.Substring(0, Math.Min(100, input.Length)));

        // Strategy 1: Try JSON parsing first
        var jsonResult = TryParseJson(input);
        if (jsonResult != null && jsonResult.Count > 0)
        {
            _logger.LogInformation("Successfully parsed requirements using JSON strategy");
            return jsonResult;
        }

        // Strategy 2: Try bullet list parsing
        var bulletResult = TryParseBulletList(input);
        if (bulletResult != null && bulletResult.Count > 0)
        {
            _logger.LogInformation("Successfully parsed requirements using bullet list strategy");
            return bulletResult;
        }

        // Strategy 3: Try key-value pattern matching
        var kvResult = TryParseKeyValue(input);
        if (kvResult != null && kvResult.Count > 0)
        {
            _logger.LogInformation("Successfully parsed requirements using key-value strategy");
            return kvResult;
        }

        // Strategy 4: Try comma-separated parsing
        var commaResult = TryParseCommaSeparated(input);
        if (commaResult != null && commaResult.Count > 0)
        {
            _logger.LogInformation("Successfully parsed requirements using comma-separated strategy");
            return commaResult;
        }

        // Strategy 5: Use LLM extraction as fallback (if kernel available)
        if (_kernel != null)
        {
            var llmResult = await TryExtractWithLLMAsync(input);
            if (llmResult != null && llmResult.Count > 0)
            {
                _logger.LogInformation("Successfully parsed requirements using LLM strategy");
                return llmResult;
            }
        }

        _logger.LogWarning("All parsing strategies failed for input: {Input}", input.Substring(0, Math.Min(100, input.Length)));
        return new Dictionary<string, object?>();
    }

    /// <summary>
    /// Strategy 1: Parse JSON format
    /// Example: {"classificationLevel": "Secret", "environmentType": "Production"}
    /// </summary>
    private Dictionary<string, object?>? TryParseJson(string input)
    {
        try
        {
            var trimmed = input.Trim();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                return null; // Not JSON format
            }

            var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(trimmed);
            if (jsonDoc == null)
            {
                return null;
            }

            var result = new Dictionary<string, object?>();
            foreach (var kvp in jsonDoc)
            {
                var normalizedKey = NormalizeKey(kvp.Key);
                result[normalizedKey] = kvp.Value.ValueKind == JsonValueKind.String
                    ? kvp.Value.GetString()
                    : kvp.Value.ToString();
            }

            return result.Count > 0 ? result : null;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "JSON parsing failed");
            return null;
        }
    }

    /// <summary>
    /// Strategy 2: Parse bullet list format
    /// Example:
    /// - Classification: Secret
    /// - Environment: Production
    /// * Region: US Gov Virginia
    /// </summary>
    private Dictionary<string, object?>? TryParseBulletList(string input)
    {
        var result = new Dictionary<string, object?>();
        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Match patterns like:
            // - Classification: Secret
            // * Environment: Production
            // • Region: US Gov Virginia
            var bulletMatch = Regex.Match(trimmed, @"^[-*•]\s*(.+?):\s*(.+)$", RegexOptions.IgnoreCase);
            if (bulletMatch.Success)
            {
                var key = NormalizeKey(bulletMatch.Groups[1].Value);
                var value = bulletMatch.Groups[2].Value.Trim();
                result[key] = value;
            }
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Strategy 3: Parse key-value patterns in natural language
    /// Example: "Classification is Secret" or "Environment: Production"
    /// </summary>
    private Dictionary<string, object?>? TryParseKeyValue(string input)
    {
        var result = new Dictionary<string, object?>();

        // Define patterns for common key-value formats
        var patterns = new[]
        {
            // "Classification is Secret" or "Classification: Secret"
            (@"(classification|classification level)\s+(?:is|:|are)\s+(\w+)", "classificationLevel"),
            
            // "Environment is Production" or "Environment type: Production"
            (@"(environment|environment type|env type)\s+(?:is|:|are)\s+(development|staging|production|dev|prod|test)", "environmentType"),
            
            // "Region is US Gov Virginia" or "Location: East US"
            (@"(region|location|azure region)\s+(?:is|in|:|are)\s+([\w\s]+?)(?:,|\.|$|and|with)", "region"),
            
            // "Services: AKS cluster, Azure SQL" or "Required services are AKS, SQL"
            (@"(services|required services)\s+(?:is|:|are)\s+([\w\s,]+?)(?:\.|$|and|with)", "requiredServices"),
            
            // "Network requirements: VNet isolation"
            (@"(network|network requirements|networking)\s+(?:is|:|are|requirements?)\s*([\w\s,]+?)(?:\.|$|and|with)", "networkRequirements"),
            
            // "Compute: 4 vCPUs, 16GB RAM"
            (@"(compute|compute requirements|compute resources)\s+(?:is|:|are|requirements?)\s*([\w\s,]+?)(?:\.|$|and|with)", "computeRequirements"),
            
            // "Database: Azure SQL"
            (@"(database|database requirements|db)\s+(?:is|:|are|requirements?)\s*([\w\s,]+?)(?:\.|$|and|with)", "databaseRequirements"),
            
            // "Compliance: FedRAMP High, NIST 800-53"
            (@"(compliance|compliance frameworks|frameworks)\s+(?:is|:|are|frameworks?)\s*([\w\s,\-]+?)(?:\.|$|and|with)", "complianceFrameworks"),
            
            // "Security controls: MFA, encryption"
            (@"(security|security controls|sec controls)\s+(?:is|:|are|controls?)\s*([\w\s,]+?)(?:\.|$|and|with)", "securityControls"),
            
            // "Target deployment: 2025-11-01" or "Deployment date is November 1st"
            (@"(target deployment|deployment date|target deploy)\s+(?:is|:|are|date)\s*([\w\s,\-]+?)(?:\.|$|and|with)", "targetDeploymentDate"),
            
            // "Go-live: December 2025"
            (@"(go-live|golive|go live|expected go-live)\s+(?:is|:|are|date)\s*([\w\s,\-]+?)(?:\.|$|and|with)", "expectedGoLiveDate")
        };

        foreach (var (pattern, fieldName) in patterns)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 2)
            {
                var value = match.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result[fieldName] = value;
                }
            }
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Strategy 4: Parse comma-separated format
    /// Example: "Classification is Secret, environment is Production, region is US Gov Virginia"
    /// </summary>
    private Dictionary<string, object?>? TryParseCommaSeparated(string input)
    {
        var result = new Dictionary<string, object?>();
        var segments = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();

            // Try to extract key-value from each segment
            // Matches: "Classification is Secret" or "environment: Production"
            var kvMatch = Regex.Match(trimmed,
                @"(.+?)\s+(?:is|are|:)\s+(.+)",
                RegexOptions.IgnoreCase);

            if (kvMatch.Success)
            {
                var key = NormalizeKey(kvMatch.Groups[1].Value);
                var value = kvMatch.Groups[2].Value.Trim();
                result[key] = value;
            }
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Strategy 5: Use LLM to extract structured data from natural language
    /// Example: "We need a Secret classification production environment in East US with AKS cluster"
    /// </summary>
    private async Task<Dictionary<string, object?>?> TryExtractWithLLMAsync(string input)
    {
        if (_kernel == null)
        {
            return null;
        }

        try
        {
            var prompt = $@"Extract onboarding requirements from this text: '{input}'

Return ONLY valid JSON with these fields (only include fields you can confidently extract from the text):
- classificationLevel (e.g., ""Unclassified"", ""Secret"", ""Top Secret"")
- environmentType (e.g., ""Development"", ""Staging"", ""Production"")
- region (e.g., ""US Gov Virginia"", ""East US"")
- requiredServices (e.g., ""AKS cluster, Azure SQL"")
- networkRequirements (e.g., ""VNet isolation, private endpoints"")
- computeRequirements (e.g., ""4 vCPUs, 16GB RAM"")
- databaseRequirements (e.g., ""Azure SQL, geo-redundant"")
- complianceFrameworks (e.g., ""FedRAMP High, NIST 800-53"")
- securityControls (e.g., ""MFA, encryption at rest"")
- targetDeploymentDate (e.g., ""2025-11-01"")
- expectedGoLiveDate (e.g., ""2025-12-01"")

Return only the JSON object, no additional text or explanation.";

            // Retry logic for rate limiting (HTTP 429) with exponential backoff
            FunctionResult? result = null;
            int maxRetries = 3;
            int retryDelayMs = 1000; // Start with 1 second
            
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    result = await _kernel.InvokePromptAsync(prompt);
                    break; // Success - exit retry loop
                }
                catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (attempt < maxRetries)
                    {
                        var delay = retryDelayMs * (int)Math.Pow(2, attempt); // Exponential backoff: 1s, 2s, 4s
                        _logger.LogWarning("⚠️ Rate limit hit in RequirementsParser LLM fallback. Retrying in {DelayMs}ms (attempt {Attempt}/{MaxRetries})", 
                            delay, attempt + 1, maxRetries);
                        await Task.Delay(delay);
                    }
                    else
                    {
                        _logger.LogError(ex, "❌ Rate limit exceeded in RequirementsParser after {MaxRetries} retries", maxRetries);
                        throw; // Re-throw after max retries
                    }
                }
            }
            
            if (result == null)
            {
                _logger.LogWarning("LLM fallback failed to get result after retries");
                return null;
            }
            
            var resultString = result.ToString().Trim();

            // Clean up common LLM response patterns
            if (resultString.StartsWith("```json"))
            {
                resultString = resultString.Substring(7);
            }
            if (resultString.StartsWith("```"))
            {
                resultString = resultString.Substring(3);
            }
            if (resultString.EndsWith("```"))
            {
                resultString = resultString.Substring(0, resultString.Length - 3);
            }
            resultString = resultString.Trim();

            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resultString);
            if (parsed == null)
            {
                return null;
            }

            var finalResult = new Dictionary<string, object?>();
            foreach (var kvp in parsed)
            {
                var normalizedKey = NormalizeKey(kvp.Key);
                finalResult[normalizedKey] = kvp.Value.ValueKind == JsonValueKind.String
                    ? kvp.Value.GetString()
                    : kvp.Value.ToString();
            }

            return finalResult.Count > 0 ? finalResult : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM extraction failed");
            return null;
        }
    }

    /// <summary>
    /// Normalize field names to match expected keys
    /// Example: "Classification" → "classificationLevel"
    /// </summary>
    private string NormalizeKey(string input)
    {
        var normalized = input.Trim().ToLowerInvariant()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("_", "");

        return normalized switch
        {
            "classification" or "classificationlevel" => "classificationLevel",
            "environment" or "envtype" or "environmenttype" or "env" => "environmentType",
            "region" or "location" or "azureregion" => "region",
            "services" or "requiredservices" => "requiredServices",
            "network" or "networkrequirements" or "networking" => "networkRequirements",
            "compute" or "computerequirements" or "computeresources" => "computeRequirements",
            "database" or "databaserequirements" or "db" => "databaseRequirements",
            "compliance" or "complianceframeworks" or "frameworks" => "complianceFrameworks",
            "security" or "securitycontrols" or "seccontrols" => "securityControls",
            "targetdeployment" or "deploymentdate" or "targetdeploy" or "targetdeploymentdate" => "targetDeploymentDate",
            "golive" or "golive" or "expectedgolive" or "expectedgolivedate" => "expectedGoLiveDate",
            _ => normalized
        };
    }
}
