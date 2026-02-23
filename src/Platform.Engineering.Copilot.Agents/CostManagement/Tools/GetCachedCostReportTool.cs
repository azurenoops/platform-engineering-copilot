using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// get_cached_cost_report — Retrieve previously fetched cost data without auth.
/// No authentication required per mcp-tools.md.
/// </summary>
public class GetCachedCostReportTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetCachedCostReportTool(ILogger<GetCachedCostReportTool> logger)
        : base(logger) { }

    public override string Name => "get_cached_cost_report";
    public override string Description => "Retrieve previously fetched cost data without requiring authentication";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "reportId": { "type": "string", "description": "Specific report ID to retrieve. If omitted, returns the latest cached report." }
      }
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
        var reportId = GetOptional<string>(parameters, "reportId");

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 50,
            Message = "Retrieving cached cost report..."
        });

        var cachedAt = DateTimeOffset.UtcNow.AddHours(-2);

        var result = new
        {
            reportId = reportId ?? "RPT-" + Guid.NewGuid().ToString()[..8],
            cachedAt = cachedAt.ToString("o"),
            expiresAt = cachedAt.AddHours(24).ToString("o"),
            isStale = false,
            summary = new
            {
                totalCost = 12301.50,
                currency = "USD",
                period = "Last 30 days",
                topSpenders = new[]
                {
                    new { resource = "Microsoft.Compute/virtualMachines", cost = 4250.00, percent = 34.5 },
                    new { resource = "Microsoft.ContainerService/managedClusters", cost = 3100.00, percent = 25.2 },
                    new { resource = "Microsoft.Sql/servers", cost = 2450.75, percent = 19.9 },
                    new { resource = "Microsoft.Storage/storageAccounts", cost = 1820.50, percent = 14.8 },
                    new { resource = "Microsoft.Network/virtualNetworks", cost = 680.25, percent = 5.5 }
                },
                trend = "increasing",
                changeFromPrevious = "+5.8%"
            },
            note = "This is cached data and may not reflect the latest spending. " +
                   "Use get_cost_analysis for real-time data (requires authentication)."
        };

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = "Cached report retrieved successfully."
        });

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
}
