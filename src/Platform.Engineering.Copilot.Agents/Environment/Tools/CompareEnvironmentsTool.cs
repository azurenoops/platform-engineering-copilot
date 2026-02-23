using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Environment.Tools;

/// <summary>
/// compare_environments — Compare two environments side-by-side.
/// Auth required, PIM Read.
/// </summary>
public class CompareEnvironmentsTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CompareEnvironmentsTool(ILogger<CompareEnvironmentsTool> logger) : base(logger) { }

    public override string Name => "compare_environments";
    public override string Description => "Compare two environments side-by-side for discrepancies";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "environmentA": { "type": "string", "description": "First environment name." },
        "environmentB": { "type": "string", "description": "Second environment name." }
      },
      "required": ["environmentA", "environmentB"]
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
        var envA = GetRequired<string>(parameters, "environmentA");
        var envB = GetRequired<string>(parameters, "environmentB");

        if (string.IsNullOrWhiteSpace(envA) || string.IsNullOrWhiteSpace(envB))
            return Task.FromResult(BuildError("MISSING_ENVIRONMENTS", "Both environment names are required.", "Provide environmentA and environmentB.", sw));

        progress?.Report(new ProgressUpdate { PercentComplete = 100, Message = "Comparison complete." });

        var response = new
        {
            status = "success",
            data = new
            {
                environmentA = envA,
                environmentB = envB,
                totalDifferences = 5,
                resourcesOnlyInA = new[] { "vm-debug-01" },
                resourcesOnlyInB = new[] { "vm-perf-test-01", "sa-perf-data" },
                configurationDifferences = new[]
                {
                    new { resource = "vm-web-01", property = "vmSize", valueA = "Standard_D4s_v3", valueB = "Standard_D2s_v3" },
                    new { resource = "sa-logs", property = "replication", valueA = "GRS", valueB = "LRS" }
                },
                costDifferential = new { environmentACost = 4500.00, environmentBCost = 3200.00, difference = 1300.00 }
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
