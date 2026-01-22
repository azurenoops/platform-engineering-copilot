using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.CostManagement.State;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// Tool for managing and monitoring Azure budgets.
/// Consolidates: get_budget_recommendations.
/// </summary>
public class BudgetManagementTool : BaseTool
{
    private readonly CostManagementStateAccessors _stateAccessors;
    private readonly ConfigService _configService;

    public override string Name => "manage_budgets";

    public override string Description =>
        "Monitor budget status, get budget alerts, and receive recommendations for budget configuration. " +
        "If subscriptionId is not provided, uses the configured default subscription. " +
        "Shows current spend vs budget, alert thresholds, and projected overruns.";

    public BudgetManagementTool(
        ILogger<BudgetManagementTool> logger,
        CostManagementStateAccessors stateAccessors,
        ConfigService configService) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Azure subscription ID to check budgets for. If not provided, uses the configured default subscription.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "budgetName",
            description: "Specific budget name to check (optional, returns all budgets if not specified)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "includeRecommendations",
            description: "Include budget configuration recommendations (default: true)",
            required: false,
            type: "boolean"));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var budgetName = GetOptionalString(arguments, "budgetName");
        var includeRecommendations = GetOptionalBool(arguments, "includeRecommendations", defaultValue: true);

        // Auto-fill subscription from configuration if not provided
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                Logger.LogInformation("Using configured default subscription: {SubscriptionId}", subscriptionId);
            }
        }

        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return ToJson(new { 
                success = false, 
                error = "Subscription ID is required. Either provide subscriptionId parameter or set a default using 'Set my subscription to <id>'" 
            });
        }

        Logger.LogInformation("Getting budget status for {SubscriptionId}", subscriptionId);

        try
        {
            // TODO: Integrate with Azure Cost Management Budgets API
            Logger.LogWarning("Budget management requires Azure Cost Management API integration. Returning sample data.");

            var budgets = new List<BudgetStatus>
            {
                new()
                {
                    BudgetName = "Monthly-Production",
                    BudgetAmount = 3000.00m,
                    CurrentSpend = 2450.00m,
                    TimeGrain = "Monthly",
                    StartDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
                    EndDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1).AddDays(-1),
                    Alerts = new List<BudgetAlertSummary>
                    {
                        new() { BudgetName = "Monthly-Production", BudgetAmount = 3000.00m, CurrentSpend = 2450.00m, CurrentPercentage = 81.7m, Severity = "Warning", AlertTriggeredAt = DateTime.UtcNow.AddDays(-2) }
                    }
                },
                new()
                {
                    BudgetName = "Development-Quarterly",
                    BudgetAmount = 5000.00m,
                    CurrentSpend = 3200.00m,
                    TimeGrain = "Quarterly",
                    StartDate = GetQuarterStart(DateTime.UtcNow),
                    EndDate = GetQuarterEnd(DateTime.UtcNow),
                    Alerts = new List<BudgetAlertSummary>()
                },
                new()
                {
                    BudgetName = "AI-Services-Monthly",
                    BudgetAmount = 1000.00m,
                    CurrentSpend = 380.00m,
                    TimeGrain = "Monthly",
                    StartDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
                    EndDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1).AddDays(-1),
                    Alerts = new List<BudgetAlertSummary>()
                }
            };

            // Filter by budget name if specified
            var filteredBudgets = string.IsNullOrWhiteSpace(budgetName)
                ? budgets
                : budgets.Where(b => b.BudgetName.Equals(budgetName, StringComparison.OrdinalIgnoreCase)).ToList();

            // Calculate projections
            foreach (var budget in filteredBudgets)
            {
                var daysInPeriod = (budget.EndDate - budget.StartDate).Days;
                var daysElapsed = (DateTime.UtcNow - budget.StartDate).Days;
                if (daysElapsed > 0 && daysInPeriod > 0)
                {
                    budget.DailyBurnRate = budget.CurrentSpend / daysElapsed;
                    budget.ProjectedSpend = budget.DailyBurnRate * daysInPeriod;
                    budget.ProjectedOverrun = Math.Max(0, budget.ProjectedSpend - budget.BudgetAmount);
                }
            }

            var recommendations = new List<BudgetRecommendation>();
            if (includeRecommendations)
            {
                recommendations = GenerateBudgetRecommendations(filteredBudgets);
            }

            await Task.CompletedTask;

            return ToJson(new
            {
                success = true,
                subscriptionId,
                budgetCount = filteredBudgets.Count,
                budgets = filteredBudgets.Select(b => new
                {
                    b.BudgetName,
                    b.BudgetAmount,
                    b.CurrentSpend,
                    currentPercentage = b.BudgetAmount > 0 ? Math.Round(b.CurrentSpend / b.BudgetAmount * 100, 1) : 0,
                    b.TimeGrain,
                    periodStart = b.StartDate.ToString("yyyy-MM-dd"),
                    periodEnd = b.EndDate.ToString("yyyy-MM-dd"),
                    b.DailyBurnRate,
                    b.ProjectedSpend,
                    b.ProjectedOverrun,
                    alertCount = b.Alerts.Count,
                    alerts = b.Alerts
                }),
                recommendations,
                note = "This is sample data. Integrate with Azure Cost Management API for real budget data."
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting budget status");
            return ToJson(new { success = false, error = $"Failed to get budget status: {ex.Message}" });
        }
    }

    private static DateTime GetQuarterStart(DateTime date)
    {
        var quarter = (date.Month - 1) / 3;
        return new DateTime(date.Year, quarter * 3 + 1, 1);
    }

    private static DateTime GetQuarterEnd(DateTime date)
    {
        return GetQuarterStart(date).AddMonths(3).AddDays(-1);
    }

    private static List<BudgetRecommendation> GenerateBudgetRecommendations(List<BudgetStatus> budgets)
    {
        var recommendations = new List<BudgetRecommendation>();

        foreach (var budget in budgets)
        {
            var percentage = budget.BudgetAmount > 0 ? budget.CurrentSpend / budget.BudgetAmount * 100 : 0;

            if (budget.ProjectedOverrun > 0)
            {
                recommendations.Add(new BudgetRecommendation
                {
                    BudgetName = budget.BudgetName,
                    Type = "Projected Overrun",
                    Description = $"Budget '{budget.BudgetName}' is projected to exceed by ${budget.ProjectedOverrun:N2}. Consider increasing budget or implementing cost controls.",
                    Priority = "High"
                });
            }
            else if (percentage > 80 && percentage < 100)
            {
                recommendations.Add(new BudgetRecommendation
                {
                    BudgetName = budget.BudgetName,
                    Type = "Approaching Limit",
                    Description = $"Budget '{budget.BudgetName}' is at {percentage:N1}% utilization. Monitor closely.",
                    Priority = "Medium"
                });
            }
            else if (percentage < 50 && budget.TimeGrain == "Monthly")
            {
                // Check if past mid-period
                var daysInPeriod = (budget.EndDate - budget.StartDate).Days;
                var daysElapsed = (DateTime.UtcNow - budget.StartDate).Days;
                if (daysElapsed > daysInPeriod / 2)
                {
                    recommendations.Add(new BudgetRecommendation
                    {
                        BudgetName = budget.BudgetName,
                        Type = "Underutilization",
                        Description = $"Budget '{budget.BudgetName}' is only at {percentage:N1}% past mid-period. Consider reallocating funds.",
                        Priority = "Low"
                    });
                }
            }
        }

        return recommendations;
    }

    private class BudgetStatus
    {
        public string BudgetName { get; set; } = string.Empty;
        public decimal BudgetAmount { get; set; }
        public decimal CurrentSpend { get; set; }
        public string TimeGrain { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal DailyBurnRate { get; set; }
        public decimal ProjectedSpend { get; set; }
        public decimal ProjectedOverrun { get; set; }
        public List<BudgetAlertSummary> Alerts { get; set; } = new();
    }

    private class BudgetRecommendation
    {
        public string BudgetName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";
    }
}
