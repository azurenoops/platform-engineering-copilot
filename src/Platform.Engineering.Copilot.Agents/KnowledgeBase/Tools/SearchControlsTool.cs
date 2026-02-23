using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;

/// <summary>
/// search_controls — Full-text search across all NIST 800-53 controls.
/// Searches titles and descriptions. No auth required per SC-008.
/// </summary>
public class SearchControlsTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly INistService _nistService;

    public SearchControlsTool(INistService nistService, ILogger<SearchControlsTool> logger)
        : base(logger) => _nistService = nistService;

    public override string Name => "search_controls";
    public override string Description => "Search across all NIST 800-53 controls by keyword";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "query": { "type": "string", "description": "Search keyword or phrase." },
        "maxResults": { "type": "integer", "default": 25, "description": "Maximum results to return." }
      },
      "required": ["query"]
    }
    """;

    public override bool RequiresAuthentication => false;
    public override PimTier PimTierRequired => PimTier.None;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var query = GetRequired<string>(parameters, "query");
        var maxResults = GetOptional<int>(parameters, "maxResults");
        if (maxResults <= 0) maxResults = 25;
        if (maxResults > 100) maxResults = 100;

        if (string.IsNullOrWhiteSpace(query))
        {
            sw.Stop();
            return Task.FromResult(BuildError("MISSING_QUERY",
                "Search query is required.", "Provide a keyword like 'access control' or 'encryption'", sw));
        }

        var results = _nistService.SearchControls(query, maxResults);

        var result = new
        {
            query,
            totalResults = results.Count,
            controls = results.Select(c => new
            {
                controlId = c.ControlId,
                title = c.Title,
                family = c.FamilyName,
                familyCode = c.Family,
                priority = c.Priority,
                baselines = new
                {
                    high = c.Baselines.High,
                    moderate = c.Baselines.Moderate,
                    low = c.Baselines.Low
                }
            }).ToArray()
        };

        sw.Stop();
        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private object BuildMetadata(Stopwatch sw) => new
    {
        toolName = Name,
        executionTimeMs = sw.ElapsedMilliseconds,
        timestamp = DateTimeOffset.UtcNow.ToString("o")
    };

    private string BuildError(string code, string message, string suggestion, Stopwatch sw)
    {
        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            status = "error",
            error = new { errorCode = code, message, suggestion },
            metadata = BuildMetadata(sw)
        }, JsonOptions);
    }
}
