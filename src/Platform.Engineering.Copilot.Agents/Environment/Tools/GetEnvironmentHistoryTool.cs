using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Environment.Tools;

/// <summary>
/// get_environment_history — View change history and deployment timeline.
/// Auth required, PIM Read.
/// </summary>
public class GetEnvironmentHistoryTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetEnvironmentHistoryTool(ILogger<GetEnvironmentHistoryTool> logger) : base(logger) { }

    public override string Name => "get_environment_history";
    public override string Description => "View change history and deployment timeline for an environment";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "environmentName": { "type": "string", "description": "Environment name." },
        "limit": { "type": "integer", "default": 10, "description": "Max history entries to return." }
      },
      "required": ["environmentName"]
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
        var envName = GetRequired<string>(parameters, "environmentName");
        var limit = GetOptional<int?>(parameters, "limit") ?? 10;

        if (string.IsNullOrWhiteSpace(envName))
            return Task.FromResult(BuildError("MISSING_ENVIRONMENT", "Environment name is required.", "Provide environment name.", sw));

        progress?.Report(new ProgressUpdate { PercentComplete = 100, Message = "History retrieved." });

        var response = new
        {
            status = "success",
            data = new
            {
                environmentName = envName,
                historyCount = 4,
                entries = new[]
                {
                    new { timestamp = DateTime.UtcNow.AddDays(-1).ToString("O"), action = "Deployment", version = "v2.4.1", user = "ci-pipeline", status = "Success", details = "Deployed 3 updated resources" },
                    new { timestamp = DateTime.UtcNow.AddDays(-3).ToString("O"), action = "DriftRemediation", version = "v2.4.0", user = "admin@agency.gov", status = "Success", details = "Fixed NSG rule drift on nsg-web-prod" },
                    new { timestamp = DateTime.UtcNow.AddDays(-7).ToString("O"), action = "Deployment", version = "v2.4.0", user = "ci-pipeline", status = "Success", details = "Major release — 8 resources updated" },
                    new { timestamp = DateTime.UtcNow.AddDays(-14).ToString("O"), action = "Promotion", version = "v2.3.5", user = "admin@agency.gov", status = "Success", details = "Promoted from staging" }
                }
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
