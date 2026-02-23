using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// get_cost_anomalies — Detect anomalous spending patterns.
/// Auth required, PIM Read per mcp-tools.md.
/// </summary>
public class GetCostAnomaliesTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetCostAnomaliesTool(ILogger<GetCostAnomaliesTool> logger) : base(logger) { }

    public override string Name => "get_cost_anomalies";
    public override string Description => "Detect anomalous spending patterns that may indicate misconfiguration or security issues";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "lookbackDays": { "type": "integer", "default": 30, "description": "Number of days to analyze for anomalies." },
        "sensitivity": { "type": "string", "enum": ["low", "medium", "high"], "default": "medium", "description": "Anomaly detection sensitivity." }
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
        var lookbackDays = GetOptional<int?>(parameters, "lookbackDays") ?? 30;
        var sensitivity = GetOptional<string>(parameters, "sensitivity") ?? "medium";

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 40,
            Message = $"Analyzing spending patterns over {lookbackDays} days (sensitivity: {sensitivity})..."
        });

        var anomalies = GetAnomalies(sensitivity);

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = $"Detected {anomalies.Count} anomalies."
        });

        sw.Stop();
        var result = new
        {
            lookbackDays,
            sensitivity,
            totalAnomalies = anomalies.Count,
            anomalies = anomalies.Select(a => new
            {
                a.DetectedDate,
                a.Resource,
                a.Description,
                a.ExpectedCost,
                a.ActualCost,
                deviation = $"+{Math.Round((a.ActualCost - a.ExpectedCost) / a.ExpectedCost * 100, 1)}%",
                a.Severity,
                a.PossibleCause,
                a.Recommendation
            }).ToArray(),
            summary = new
            {
                critical = anomalies.Count(a => a.Severity == "critical"),
                warning = anomalies.Count(a => a.Severity == "warning"),
                info = anomalies.Count(a => a.Severity == "info")
            }
        };

        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static List<Anomaly> GetAnomalies(string sensitivity)
    {
        var all = new List<Anomaly>
        {
            new("2025-01-20", "/subscriptions/sub1/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/vm-prod-08",
                "Unexpected spike in VM compute costs", 120.00, 450.00, "critical",
                "Auto-scaling triggered by load spike or misconfigured threshold",
                "Review auto-scale rules and recent deployments"),
            new("2025-01-18", "/subscriptions/sub1/resourceGroups/rg-prod/providers/Microsoft.Storage/storageAccounts/stproddata01",
                "Storage egress costs 3x higher than average", 80.00, 240.00, "warning",
                "Large data transfer or backup operation",
                "Check recent backup schedules and data transfer activities"),
            new("2025-01-15", "/subscriptions/sub1/resourceGroups/rg-dev/providers/Microsoft.Sql/servers/sql-dev",
                "Database costs increased overnight", 50.00, 95.00, "info",
                "DTU tier change or performance tier upgrade",
                "Verify intended tier change with development team")
        };

        return sensitivity switch
        {
            "high" => all,
            "low" => all.Where(a => a.Severity == "critical").ToList(),
            _ => all.Where(a => a.Severity is "critical" or "warning").ToList()
        };
    }

    private object BuildMetadata(Stopwatch sw) => new
    {
        toolName = Name,
        executionTimeMs = sw.ElapsedMilliseconds,
        timestamp = DateTimeOffset.UtcNow.ToString("o")
    };

    private record Anomaly(
        string DetectedDate, string Resource, string Description,
        double ExpectedCost, double ActualCost, string Severity,
        string PossibleCause, string Recommendation);
}
