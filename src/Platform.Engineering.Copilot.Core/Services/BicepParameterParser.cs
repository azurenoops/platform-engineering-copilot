using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Parses Bicep parameter files and extracts parameter definitions.
/// </summary>
public class BicepParameterParser
{
    private readonly ILogger<BicepParameterParser> _logger;

    public BicepParameterParser(ILogger<BicepParameterParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a template's content and parameters JSON.
    /// </summary>
    public Task<object> ValidateAsync(string content, string? parametersJson = null, string format = "Bicep",
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add("Template content is required.");
        }

        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                JsonDocument.Parse(parametersJson);
            }
            catch (JsonException ex)
            {
                errors.Add($"Invalid parameters JSON: {ex.Message}");
            }
        }

        // Basic Bicep content validation
        if (format.Equals("Bicep", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(content))
        {
            if (!content.Contains("resource") && !content.Contains("param") && !content.Contains("module"))
            {
                warnings.Add("Template content does not appear to contain Bicep resource, param, or module declarations.");
            }
        }

        _logger.LogDebug("Template validation completed: {ErrorCount} errors, {WarningCount} warnings", errors.Count, warnings.Count);

        return Task.FromResult<object>(new
        {
            isValid = errors.Count == 0,
            errors,
            warnings,
            format
        });
    }

    /// <summary>
    /// Parses Bicep parameters from content string.
    /// </summary>
    public Task<object> ParseParametersAsync(string content, CancellationToken cancellationToken = default)
    {
        var parameters = new List<object>();

        // Simple parse: look for lines matching "param <name> <type>"
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("param ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    parameters.Add(new
                    {
                        name = parts[1],
                        type = parts[2],
                        required = !trimmed.Contains("="),
                        defaultValue = trimmed.Contains("=")
                            ? trimmed[(trimmed.IndexOf('=') + 1)..].Trim().Trim('\'', '"')
                            : (string?)null
                    });
                }
            }
        }

        _logger.LogDebug("Parsed {Count} parameters from content", parameters.Count);

        return Task.FromResult<object>(new
        {
            parameters,
            count = parameters.Count
        });
    }

    /// <summary>
    /// Parses Bicep parameters from a Git URL (fetches content first).
    /// </summary>
    public Task<object> ParseFromGitAsync(string gitRepoUrl, string? branch = null, string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing Bicep parameters from Git: {Url}, branch={Branch}, path={Path}",
            gitRepoUrl, branch ?? "main", filePath);

        // Stub: In production, would clone/fetch the repo and parse
        return Task.FromResult<object>(new
        {
            gitRepoUrl,
            branch = branch ?? "main",
            filePath,
            parameters = Array.Empty<object>(),
            count = 0,
            message = "Git parsing is a stub. Connect to Git provider for real parsing."
        });
    }
}
