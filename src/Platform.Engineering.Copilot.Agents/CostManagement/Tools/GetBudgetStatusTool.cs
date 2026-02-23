using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// get_budget_status — Check budget consumption and alerts.
/// Auth required, PIM Read per mcp-tools.md.
/// </summary>
public class GetBudgetStatusTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetBudgetStatusTool(ILogger<GetBudgetStatusTool> logger) : base(logger) { }

    public override string Name => "get_budget_status";
    public override string Description => "Check budget consumption, thresholds, and alerts";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "budgetName": { "type": "string", "description": "Specific budget to check. If omitted, returns all budgets." }
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
        var budgetName = GetOptional<string>(parameters, "budgetName");

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 50,
            Message = "Retrieving budget status..."
        });

        var budgets = GetBudgets();
        if (!string.IsNullOrWhiteSpace(budgetName))
            budgets = budgets.Where(b =>
                b.Name.Equals(budgetName, StringComparison.OrdinalIgnoreCase)).ToList();

        if (budgets.Count == 0)
        {
            sw.Stop();
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    budgets = Array.Empty<object>(),
                    message = string.IsNullOrWhiteSpace(budgetName)
                        ? "No budgets configured."
                        : $"Budget '{budgetName}' not found."
                },
                metadata = BuildMetadata(sw)
            }, JsonOptions));
        }

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = $"Retrieved {budgets.Count} budget(s)."
        });

        sw.Stop();
        var result = new
        {
            budgets = budgets.Select(b => new
            {
                b.Name,
                b.Amount,
                b.Currency,
                b.Period,
                b.CurrentSpend,
                percentConsumed = Math.Round(b.CurrentSpend / b.Amount * 100, 1),
                remaining = Math.Round(b.Amount - b.CurrentSpend, 2),
                forecastedEndOfPeriod = Math.Round(b.ForecastSpend, 2),
                forecastOverBudget = b.ForecastSpend > b.Amount,
                alerts = b.Alerts,
                status = (b.CurrentSpend / b.Amount) switch
                {
                    >= 1.0 => "exceeded",
                    >= 0.9 => "critical",
                    >= 0.75 => "warning",
                    _ => "healthy"
                }
            }).ToArray()
        };

        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static List<Budget> GetBudgets() =>
    [
        new("Monthly-Production", 15000.00, "USD", "Monthly", 11250.00, 14800.00,
        [
            new("75%", true, "2025-01-15"),
            new("90%", false, null),
            new("100%", false, null)
        ]),
        new("Monthly-Development", 5000.00, "USD", "Monthly", 2100.00, 3200.00,
        [
            new("75%", false, null),
            new("90%", false, null)
        ]),
        new("Quarterly-Total", 50000.00, "USD", "Quarterly", 38500.00, 52000.00,
        [
            new("75%", true, "2025-02-10"),
            new("90%", false, null)
        ])
    ];

    private object BuildMetadata(Stopwatch sw) => new
    {
        toolName = Name,
        executionTimeMs = sw.ElapsedMilliseconds,
        timestamp = DateTimeOffset.UtcNow.ToString("o")
    };

    private record Budget(
        string Name, double Amount, string Currency, string Period,
        double CurrentSpend, double ForecastSpend, List<Alert> Alerts);

    private record Alert(string Threshold, bool Triggered, string? TriggeredDate);
}
