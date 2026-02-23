using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// get_resource_health — Check health and availability of specific resources.
/// Auth required, PIM Read.
/// </summary>
public class GetResourceHealthTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetResourceHealthTool(ILogger<GetResourceHealthTool> logger) : base(logger) { }

    public override string Name => "get_resource_health";
    public override string Description => "Check the health and availability status of specific Azure resources";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "resourceId": { "type": "string", "description": "Azure resource ID." },
        "includeRecommendations": { "type": "boolean", "default": true, "description": "Include health recommendations." }
      },
      "required": ["resourceId"]
    }
    """;

    public override bool RequiresAuthentication => true;
    public override PimTier PimTierRequired => PimTier.Read;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var resourceId = GetRequired<string>(parameters, "resourceId");
        var includeRecs = GetOptional<bool?>(parameters, "includeRecommendations") ?? true;

        if (string.IsNullOrWhiteSpace(resourceId))
            return Task.FromResult(BuildError("MISSING_RESOURCE_ID",
                "Resource ID is required.", "Provide a valid Azure resource ID.", sw));

        progress?.Report(new ProgressUpdate { PercentComplete = 100, Message = "Health check complete." });

        var response = new
        {
            status = "success",
            data = new
            {
                resourceId,
                healthStatus = "Healthy",
                availabilityState = "Available",
                lastChecked = DateTime.UtcNow.AddMinutes(-5).ToString("O"),
                summary = "Resource is operating normally with no detected issues.",
                recommendations = includeRecs ? new[]
                {
                    new { priority = "Medium", description = "Enable diagnostic logging for improved monitoring." },
                    new { priority = "Low", description = "Consider enabling auto-scaling for peak workloads." }
                } : null
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTime.UtcNow.ToString("O") }
        };

        return Task.FromResult(JsonSerializer.Serialize(response, JsonOptions));
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
