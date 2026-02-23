using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Environment.Tools;

/// <summary>
/// list_environments — List all managed environments with status.
/// Auth required, PIM Read.
/// </summary>
public class ListEnvironmentsTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ListEnvironmentsTool(ILogger<ListEnvironmentsTool> logger) : base(logger) { }

    public override string Name => "list_environments";
    public override string Description => "List all managed environments with status and metadata";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "tier": { "type": "string", "enum": ["dev", "staging", "prod", "all"], "default": "all", "description": "Filter by environment tier." }
      }
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
        var tier = GetOptional<string>(parameters, "tier") ?? "all";

        progress?.Report(new ProgressUpdate { PercentComplete = 100, Message = "Environments listed." });

        var allEnvs = new[]
        {
            new { name = "platform-dev", tier = "dev", status = "Healthy", resourceCount = 8, region = "usgovvirginia", driftStatus = "InSync", estimatedCost = 1200.00, lastDeployed = DateTime.UtcNow.AddDays(-1).ToString("O") },
            new { name = "platform-staging", tier = "staging", status = "Healthy", resourceCount = 14, region = "usgovvirginia", driftStatus = "Drifted", estimatedCost = 3200.00, lastDeployed = DateTime.UtcNow.AddDays(-3).ToString("O") },
            new { name = "platform-prod", tier = "prod", status = "Healthy", resourceCount = 24, region = "usgovvirginia", driftStatus = "InSync", estimatedCost = 8500.00, lastDeployed = DateTime.UtcNow.AddDays(-7).ToString("O") },
            new { name = "platform-dr", tier = "prod", status = "Standby", resourceCount = 24, region = "usgovarizona", driftStatus = "InSync", estimatedCost = 4200.00, lastDeployed = DateTime.UtcNow.AddDays(-7).ToString("O") }
        };

        var filtered = tier == "all" ? allEnvs : allEnvs.Where(e => e.tier == tier).ToArray();

        var response = new
        {
            status = "success",
            data = new
            {
                environmentCount = filtered.Length,
                filter = tier,
                environments = filtered,
                totalEstimatedCost = filtered.Sum(e => e.estimatedCost)
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTime.UtcNow.ToString("O") }
        };

        return Task.FromResult(JsonSerializer.Serialize(response, JsonOptions));
    }
}
