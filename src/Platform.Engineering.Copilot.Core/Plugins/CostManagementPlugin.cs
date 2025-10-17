using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.CostOptimization;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Azure.Cost;
using DetailedCostOptimizationRecommendation = Platform.Engineering.Copilot.Core.Models.CostOptimization.CostOptimizationRecommendation;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for Azure cost management and optimization
/// </summary>
public class CostManagementPlugin : BaseSupervisorPlugin
{
    private readonly ICostOptimizationEngine _costOptimizationEngine;
    private readonly IAzureCostManagementService _costService;

    public CostManagementPlugin(
        ILogger<CostManagementPlugin> logger,
        Kernel kernel,
        ICostOptimizationEngine costOptimizationEngine,
        IAzureCostManagementService costService) : base(logger, kernel)
    {
        _costOptimizationEngine = costOptimizationEngine ?? throw new ArgumentNullException(nameof(costOptimizationEngine));
        _costService = costService ?? throw new ArgumentNullException(nameof(costService));
    }

    [KernelFunction("process_cost_management_query")]
    [Description("Process any Azure cost management query using natural language. Handles cost analysis, optimization recommendations, budget monitoring, forecasting, and reporting. Use this for ANY cost-related request such as 'Analyze costs for subscription abc-123', 'Recommend cost savings', 'Show budget status', 'Forecast next month's spend', or 'Export a resource cost summary'.")]
    public async Task<string> ProcessCostManagementQueryAsync(
        [Description("Natural language cost management query (e.g., 'Analyze last month's spend for subscription 1234', 'Find savings opportunities').")] string query,
        [Description("Azure subscription ID to analyze. Optional if included in the query text.")] string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing cost management query: {Query}", query);

            var normalizedQuery = query.ToLowerInvariant();
            subscriptionId ??= ExtractSubscriptionId(query);

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return "Unable to identify the subscription to analyze. Please specify the Azure subscription ID in the query or as a parameter.";
            }

            var intent = DetermineIntent(normalizedQuery);
            return intent switch
            {
                CostIntent.Optimization => await HandleOptimizationAsync(subscriptionId, cancellationToken),
                CostIntent.Budget => await HandleBudgetsAsync(subscriptionId, cancellationToken),
                CostIntent.Forecast => await HandleForecastAsync(subscriptionId, normalizedQuery, cancellationToken),
                CostIntent.Export => await HandleExportAsync(subscriptionId, cancellationToken),
                _ => await HandleDashboardAsync(subscriptionId, normalizedQuery, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("process cost management query", ex);
        }
    }

    private async Task<string> HandleDashboardAsync(string subscriptionId, string query, CancellationToken cancellationToken)
    {
        var endDate = DateTimeOffset.UtcNow;
        var startDate = endDate.AddDays(-DetermineLookbackWindow(query));

        var dashboard = await _costService.GetCostDashboardAsync(subscriptionId, startDate, endDate, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Cost Optimization for subscription {subscriptionId}");
        sb.AppendLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        sb.AppendLine($"Current spend: {FormatCurrency(dashboard.Summary.CurrentMonthSpend)} ({dashboard.Summary.TrendDirection})");
        sb.AppendLine($"Potential savings: {FormatCurrency(dashboard.Summary.PotentialSavings)} across {dashboard.Summary.OptimizationOpportunities} opportunities");
        sb.AppendLine($"Average daily cost: {FormatCurrency(dashboard.Summary.AverageDailyCost)}");

        var topServices = dashboard.ServiceBreakdown
            .OrderByDescending(s => s.MonthlyCost)
            .Take(3)
            .Select(s => $"{s.ServiceName}: {FormatCurrency(s.MonthlyCost)} ({s.PercentageOfTotal:N1}% of total)");

        if (topServices.Any())
        {
            sb.AppendLine("Top services: ");
            foreach (var service in topServices)
            {
                sb.AppendLine($"  - {service}");
            }
        }

        var alerts = dashboard.BudgetAlerts.Take(3).ToList();
        if (alerts.Any())
        {
            sb.AppendLine("Active budget alerts:");
            foreach (var alert in alerts)
            {
                sb.AppendLine($"  - {alert.BudgetName} at {alert.CurrentPercentage:N0}% of {FormatCurrency(alert.BudgetAmount)} ({alert.Severity})");
            }
        }

        var anomalies = dashboard.Anomalies.Take(3).ToList();
        if (anomalies.Any())
        {
            sb.AppendLine("Recent anomalies:");
            foreach (var anomaly in anomalies)
            {
                sb.AppendLine($"  - {anomaly.Description} (deviation {FormatCurrency(anomaly.CostDifference)} on {anomaly.DetectedAt:yyyy-MM-dd})");
            }
        }

        sb.AppendLine("Key recommendations:");
        foreach (var recommendation in dashboard.Recommendations.Take(5))
        {
            sb.AppendLine($"  - {recommendation.Description} | Savings: {FormatCurrency(recommendation.PotentialMonthlySavings)} | Priority: {recommendation.Priority}");
        }

        return sb.ToString();
    }

    private async Task<string> HandleOptimizationAsync(string subscriptionId, CancellationToken cancellationToken)
    {
    var analysis = await _costOptimizationEngine.AnalyzeSubscriptionAsync(subscriptionId);
    var recommendations = analysis.Recommendations ?? new List<DetailedCostOptimizationRecommendation>();

        var sb = new StringBuilder();
        sb.AppendLine($"Cost optimization analysis for subscription {subscriptionId}");
        sb.AppendLine($"Total monthly cost: {FormatCurrency(analysis.TotalMonthlyCost)}");
        sb.AppendLine($"Potential monthly savings: {FormatCurrency(analysis.PotentialMonthlySavings)} across {analysis.TotalRecommendations} recommendations");

        var topServices = (analysis.CostByService ?? new Dictionary<string, decimal>())
            .OrderByDescending(kvp => kvp.Value)
            .Take(3)
            .Select(kvp => $"{kvp.Key}: {FormatCurrency(kvp.Value)}");

        if (topServices.Any())
        {
            sb.AppendLine("Top cost drivers by service:");
            foreach (var service in topServices)
            {
                sb.AppendLine($"  - {service}");
            }
        }

        foreach (var recommendation in recommendations.OrderByDescending(r => r.EstimatedMonthlySavings).Take(5))
        {
            sb.AppendLine($"Recommendation: {recommendation.Description}");
            sb.AppendLine($"  Resource: {recommendation.ResourceName} ({recommendation.ResourceType}) in {recommendation.ResourceGroup}");
            sb.AppendLine($"  Estimated savings: {FormatCurrency(recommendation.EstimatedMonthlySavings)} | Priority: {recommendation.Priority}");
            var actionCount = recommendation.Actions?.Count ?? 0;
            sb.AppendLine($"  Complexity: {recommendation.Complexity} | Suggested actions: {actionCount}");
        }

        return sb.ToString();
    }

    private async Task<string> HandleBudgetsAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var budgets = await _costService.GetBudgetsAsync(subscriptionId, cancellationToken);

        if (budgets.Count == 0)
        {
            return $"No budgets are configured for subscription {subscriptionId}. Consider creating budgets to monitor spend.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Budget overview for subscription {subscriptionId}");

        foreach (var budget in budgets.Take(5))
        {
            sb.AppendLine($"Budget: {budget.Name} ({FormatCurrency(budget.Amount)})");
            sb.AppendLine($"  Utilization: {budget.UtilizationPercentage:N1}% | Current spend: {FormatCurrency(budget.CurrentSpend)}");
            sb.AppendLine($"  Remaining: {FormatCurrency(budget.RemainingBudget)} | Status: {budget.HealthStatus}");
            if (budget.Thresholds.Any())
            {
                var thresholds = string.Join(", ", budget.Thresholds.Select(t => $"{t.Percentage}% ({t.Severity})"));
                sb.AppendLine($"  Alerts: {thresholds}");
            }
        }

        return sb.ToString();
    }

    private async Task<string> HandleForecastAsync(string subscriptionId, string query, CancellationToken cancellationToken)
    {
        var forecastDays = DetermineForecastWindow(query);
        var forecast = await _costService.GetCostForecastAsync(subscriptionId, forecastDays, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Cost forecast for subscription {subscriptionId}");
        sb.AppendLine($"Forecast window: {forecastDays} days | Confidence: {forecast.ConfidenceLevel:P0}");
        sb.AppendLine($"Projected month-end cost: {FormatCurrency(forecast.ProjectedMonthEndCost)}");
        sb.AppendLine($"Projected quarter-end cost: {FormatCurrency(forecast.ProjectedQuarterEndCost)}");
        sb.AppendLine($"Projected year-end cost: {FormatCurrency(forecast.ProjectedYearEndCost)}");

        foreach (var point in forecast.Projections.Take(5))
        {
            sb.AppendLine($"  {point.Date:yyyy-MM-dd}: {FormatCurrency(point.ForecastedCost)} (range {FormatCurrency(point.LowerBound)} - {FormatCurrency(point.UpperBound)})");
        }

        if (forecast.Assumptions.Any())
        {
            sb.AppendLine("Assumptions considered:");
            foreach (var assumption in forecast.Assumptions.Take(3))
            {
                sb.AppendLine($"  - {assumption.Description} (impact {assumption.Impact:N1})");
            }
        }

        if (forecast.Risks.Any())
        {
            sb.AppendLine("Risks to monitor:");
            foreach (var risk in forecast.Risks.Take(3))
            {
                sb.AppendLine($"  - {risk.Risk} (impact {risk.PotentialImpact:N1}, probability {risk.Probability:P0})");
            }
        }

        return sb.ToString();
    }

    private async Task<string> HandleExportAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var breakdown = await _costService.GetResourceCostBreakdownAsync(
            subscriptionId,
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow,
            cancellationToken);

        if (breakdown.Count == 0)
        {
            return $"No cost data available to export for subscription {subscriptionId} in the selected window.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Export-ready resource cost summary for subscription {subscriptionId}");
        sb.AppendLine("Top resources by monthly spend:");

        foreach (var resource in breakdown.OrderByDescending(r => r.MonthlyCost).Take(10))
        {
            sb.AppendLine($"  - {resource.ResourceName} ({resource.ResourceType}) | {FormatCurrency(resource.MonthlyCost)} this month | Trend {resource.CostTrend:N1}%");
        }

        sb.AppendLine("Use Azure Cost Management exports or APIs to pull full CSV/Parquet detail based on these identifiers.");
        return sb.ToString();
    }

    private static CostIntent DetermineIntent(string normalizedQuery)
    {
        if (normalizedQuery.Contains("optimize") || normalizedQuery.Contains("saving") || normalizedQuery.Contains("recommend"))
        {
            return CostIntent.Optimization;
        }

        if (normalizedQuery.Contains("budget") || normalizedQuery.Contains("alert"))
        {
            return CostIntent.Budget;
        }

        if (normalizedQuery.Contains("forecast") || normalizedQuery.Contains("predict") || normalizedQuery.Contains("projection"))
        {
            return CostIntent.Forecast;
        }

        if (normalizedQuery.Contains("export") || normalizedQuery.Contains("download") || normalizedQuery.Contains("report"))
        {
            return CostIntent.Export;
        }

        return CostIntent.Dashboard;
    }

    private static string? ExtractSubscriptionId(string query)
    {
        var match = Regex.Match(query, "(?i)subscription[\\s:]+([0-9a-f-]{8}-[0-9a-f-]{4}-[0-9a-f-]{4}-[0-9a-f-]{4}-[0-9a-f-]{12})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int DetermineLookbackWindow(string query)
    {
        if (query.Contains("quarter")) return 90;
        if (query.Contains("year")) return 365;
        if (query.Contains("week")) return 7;
        return 30;
    }

    private static int DetermineForecastWindow(string query)
    {
        if (query.Contains("quarter")) return 90;
        if (query.Contains("year")) return 365;
        if (query.Contains("week")) return 7;
        if (query.Contains("6 month")) return 180;
        return 30;
    }

    private static string FormatCurrency(decimal amount)
    {
        return string.Format(CultureInfo.InvariantCulture, "${0:N2}", amount);
    }

    private enum CostIntent
    {
        Dashboard,
        Optimization,
        Budget,
        Forecast,
        Export
    }
}
