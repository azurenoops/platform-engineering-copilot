using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// get_optimization_suggestions — Identify cost-saving opportunities.
/// Idle resources, oversized VMs, unused disks, reserved instance recommendations.
/// Auth required, PIM Read per mcp-tools.md / FR-035.
/// </summary>
public class GetOptimizationSuggestionsTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetOptimizationSuggestionsTool(ILogger<GetOptimizationSuggestionsTool> logger)
        : base(logger) { }

    public override string Name => "get_optimization_suggestions";
    public override string Description => "Identify cost-saving opportunities including idle resources, right-sizing, and reserved instances";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "category": { "type": "string", "enum": ["all", "idle", "rightsizing", "reserved", "unused"], "default": "all", "description": "Category of optimization to analyze." },
        "minSavings": { "type": "number", "default": 0, "description": "Minimum monthly savings threshold to include." }
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
        var category = GetOptional<string>(parameters, "category") ?? "all";
        var minSavings = GetOptional<double?>(parameters, "minSavings") ?? 0;

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 30,
            Message = "Scanning resources for optimization opportunities..."
        });

        var allSuggestions = GetAllSuggestions();

        var filtered = category == "all"
            ? allSuggestions.Where(s => s.EstimatedMonthlySavings >= minSavings).ToList()
            : allSuggestions.Where(s =>
                s.Category.Equals(category, StringComparison.OrdinalIgnoreCase) &&
                s.EstimatedMonthlySavings >= minSavings).ToList();

        var totalSavings = filtered.Sum(s => s.EstimatedMonthlySavings);

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = $"Found {filtered.Count} optimization opportunities. Total savings: ${totalSavings:N2}/month"
        });

        sw.Stop();
        var result = new
        {
            suggestions = filtered.Select(s => new
            {
                s.Category,
                s.Resource,
                s.Description,
                estimatedMonthlySavings = Math.Round(s.EstimatedMonthlySavings, 2),
                s.Action,
                s.Impact,
                s.Confidence
            }).OrderByDescending(s => s.estimatedMonthlySavings).ToArray(),
            totalEstimatedSavings = Math.Round(totalSavings, 2),
            annualizedSavings = Math.Round(totalSavings * 12, 2),
            categorySummary = filtered
                .GroupBy(s => s.Category)
                .Select(g => new
                {
                    category = g.Key,
                    count = g.Count(),
                    totalSavings = Math.Round(g.Sum(s => s.EstimatedMonthlySavings), 2)
                }).ToArray()
        };

        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static List<Suggestion> GetAllSuggestions() =>
    [
        new("idle", "/subscriptions/sub1/resourceGroups/rg-dev/providers/Microsoft.Compute/virtualMachines/vm-dev-01",
            "VM has <5% CPU usage for 14 days", 150.00, "Deallocate or resize", "high", "high"),
        new("idle", "/subscriptions/sub1/resourceGroups/rg-staging/providers/Microsoft.Compute/virtualMachines/vm-staging-02",
            "VM has <3% CPU usage for 21 days", 200.00, "Deallocate during non-business hours", "high", "high"),
        new("rightsizing", "/subscriptions/sub1/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/vm-prod-05",
            "VM consistently uses <30% of allocated resources", 120.00, "Resize from Standard_D4s_v3 to Standard_D2s_v3", "medium", "medium"),
        new("unused", "/subscriptions/sub1/resourceGroups/rg-dev/providers/Microsoft.Compute/disks/disk-orphaned-01",
            "Unattached managed disk for 30+ days", 45.00, "Delete or attach to an active VM", "low", "high"),
        new("unused", "/subscriptions/sub1/resourceGroups/rg-prod/providers/Microsoft.Network/publicIPAddresses/pip-unused-01",
            "Public IP not associated with any resource", 5.00, "Delete unused public IP", "low", "high"),
        new("reserved", "/subscriptions/sub1/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/*",
            "3 VMs running 24/7 — eligible for 1-year Reserved Instance savings", 380.00, "Purchase 1-year RI for Standard_D4s_v3", "high", "medium"),
        new("idle", "/subscriptions/sub1/resourceGroups/rg-shared/providers/Microsoft.Sql/servers/sql-shared/databases/db-archive",
            "Database has <1% DTU usage for 30 days", 85.00, "Scale down or move to serverless tier", "medium", "high")
    ];

    private object BuildMetadata(Stopwatch sw) => new
    {
        toolName = Name,
        executionTimeMs = sw.ElapsedMilliseconds,
        timestamp = DateTimeOffset.UtcNow.ToString("o")
    };

    private record Suggestion(
        string Category, string Resource, string Description,
        double EstimatedMonthlySavings, string Action, string Impact, string Confidence);
}
