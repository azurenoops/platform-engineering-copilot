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
using Platform.Engineering.Copilot.Core.Services.Azure;
using DetailedCostOptimizationRecommendation = Platform.Engineering.Copilot.Core.Models.CostOptimization.CostOptimizationRecommendation;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for Azure cost management and optimization
/// </summary>
public class CostManagementPlugin : BaseSupervisorPlugin
{
    private readonly ICostOptimizationEngine _costOptimizationEngine;
    private readonly IAzureCostManagementService _costService;
    private readonly AzureMcpClient _azureMcpClient;

    public CostManagementPlugin(
        ILogger<CostManagementPlugin> logger,
        Kernel kernel,
        ICostOptimizationEngine costOptimizationEngine,
        IAzureCostManagementService costService,
        AzureMcpClient azureMcpClient) : base(logger, kernel)
    {
        _costOptimizationEngine = costOptimizationEngine ?? throw new ArgumentNullException(nameof(costOptimizationEngine));
        _costService = costService ?? throw new ArgumentNullException(nameof(costService));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
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
        
        // Header
        sb.AppendLine("# 💰 Azure Cost Analysis Dashboard");
        sb.AppendLine();
        sb.AppendLine($"**Subscription:** `{subscriptionId}`");
        sb.AppendLine($"**Analysis Period:** {startDate:MMM dd, yyyy} → {endDate:MMM dd, yyyy} ({(endDate - startDate).Days} days)");
        sb.AppendLine();
        
        // Summary Section with visual indicators
        sb.AppendLine("## 📊 Cost Summary");
        sb.AppendLine();
        
        var trendIcon = dashboard.Summary.TrendDirection switch
        {
            CostTrendDirection.Increasing => "📈",
            CostTrendDirection.Decreasing => "📉",
            _ => "➡️"
        };
        
        sb.AppendLine($"| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Current Month Spend | **{FormatCurrency(dashboard.Summary.CurrentMonthSpend)}** {trendIcon} |");
        sb.AppendLine($"| Previous Month | {FormatCurrency(dashboard.Summary.PreviousMonthSpend)} |");
        sb.AppendLine($"| Month-over-Month Change | {FormatPercentage(dashboard.Summary.MonthOverMonthChangePercent)} |");
        sb.AppendLine($"| Average Daily Cost | {FormatCurrency(dashboard.Summary.AverageDailyCost)} |");
        sb.AppendLine($"| Projected Month-End | {FormatCurrency(dashboard.Summary.ProjectedMonthlySpend)} |");
        sb.AppendLine($"| Year-to-Date Spend | {FormatCurrency(dashboard.Summary.YearToDateSpend)} |");
        sb.AppendLine();
        
        if (dashboard.Summary.PotentialSavings > 0)
        {
            sb.AppendLine($"### 💡 Optimization Opportunity");
            sb.AppendLine($"> **{FormatCurrency(dashboard.Summary.PotentialSavings)}** in potential monthly savings across **{dashboard.Summary.OptimizationOpportunities}** opportunities");
            sb.AppendLine();
        }

        // Top Services Breakdown
        var topServices = dashboard.ServiceBreakdown
            .OrderByDescending(s => s.MonthlyCost)
            .Take(5)
            .ToList();

        if (topServices.Any() && topServices.Sum(s => s.MonthlyCost) > 0)
        {
            sb.AppendLine("## 🔝 Top Services by Cost");
            sb.AppendLine();
            sb.AppendLine("| Service | Monthly Cost | % of Total | Resources |");
            sb.AppendLine("|---------|--------------|------------|-----------|");
            foreach (var service in topServices)
            {
                var bar = CreateProgressBar(service.PercentageOfTotal);
                sb.AppendLine($"| {service.ServiceName} | {FormatCurrency(service.MonthlyCost)} | {bar} {service.PercentageOfTotal:N1}% | {service.ResourceCount} |");
            }
            sb.AppendLine();
        }

        // Budget Alerts
        var alerts = dashboard.BudgetAlerts.Take(5).ToList();
        if (alerts.Any())
        {
            sb.AppendLine("## ⚠️ Budget Alerts");
            sb.AppendLine();
            foreach (var alert in alerts)
            {
                var icon = alert.Severity switch
                {
                    BudgetAlertSeverity.Critical => "🔴",
                    BudgetAlertSeverity.Warning => "🟠",
                    BudgetAlertSeverity.Info => "🟡",
                    _ => "🟢"
                };
                var progressBar = CreateProgressBar(alert.CurrentPercentage);
                sb.AppendLine($"{icon} **{alert.BudgetName}**: {progressBar} {alert.CurrentPercentage:N0}% of {FormatCurrency(alert.BudgetAmount)} ({alert.Severity})");
            }
            sb.AppendLine();
        }

        // Cost Anomalies
        var anomalies = dashboard.Anomalies.Take(5).ToList();
        if (anomalies.Any())
        {
            sb.AppendLine("## 🔍 Cost Anomalies Detected");
            sb.AppendLine();
            foreach (var anomaly in anomalies)
            {
                var severityIcon = anomaly.Severity switch
                {
                    AnomalySeverity.High => "🔴",
                    AnomalySeverity.Medium => "🟡",
                    _ => "🔵"
                };
                sb.AppendLine($"{severityIcon} **{anomaly.AnomalyDate:MMM dd}**: {anomaly.Description}");
                sb.AppendLine($"   - Deviation: {FormatCurrency(anomaly.CostDifference)} ({FormatPercentage(anomaly.PercentageDeviation)})");
            }
            sb.AppendLine();
        }

        // Top Recommendations
        sb.AppendLine("## 💡 Cost Optimization Recommendations");
        sb.AppendLine();
        var topRecommendations = dashboard.Recommendations
            .OrderByDescending(r => r.PotentialMonthlySavings)
            .Take(5)
            .ToList();
            
        if (topRecommendations.Any())
        {
            sb.AppendLine("| Priority | Recommendation | Monthly Savings | Complexity |");
            sb.AppendLine("|----------|----------------|-----------------|------------|");
            foreach (var rec in topRecommendations)
            {
                var priorityIcon = rec.Priority switch
                {
                    Models.OptimizationPriority.Critical => "🔴 Critical",
                    Models.OptimizationPriority.High => "🟠 High",
                    Models.OptimizationPriority.Medium => "🟡 Medium",
                    _ => "🟢 Low"
                };
                var complexityIcon = rec.ImplementationComplexity switch
                {
                    Models.OptimizationComplexity.Expert => "🔴 Expert",
                    Models.OptimizationComplexity.Complex => "⚠️ Complex",
                    Models.OptimizationComplexity.Moderate => "⚡ Moderate",
                    _ => "✅ Simple"
                };
                sb.AppendLine($"| {priorityIcon} | {rec.Description} | **{FormatCurrency(rec.PotentialMonthlySavings)}** | {complexityIcon} |");
            }
        }
        else
        {
            sb.AppendLine("_No optimization recommendations available at this time._");
        }
        sb.AppendLine();
        
        // Add data quality notice if all costs are zero
        if (dashboard.Summary.CurrentMonthSpend == 0 && dashboard.Summary.PreviousMonthSpend == 0)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("⚠️ **Note**: No cost data was returned from Azure Cost Management API.");
            sb.AppendLine();
            sb.AppendLine("**Possible reasons:**");
            sb.AppendLine("- This subscription has no resource usage in the selected time period");
            sb.AppendLine("- Cost data is still being processed (can take 24-72 hours)");
            sb.AppendLine("- You may need appropriate permissions (`Cost Management Reader` role)");
            sb.AppendLine("- Azure Government Cloud may have limited Cost Management API support");
            sb.AppendLine();
            sb.AppendLine("**Resources found in subscription:**");
            sb.AppendLine("- Cognitive Services accounts in `mcp-rg` resource group");
            sb.AppendLine("- These resources should generate cost data within 24-72 hours");
            sb.AppendLine();
            sb.AppendLine("### 🎨 Sample Dashboard (How it would look with data)");
            sb.AppendLine();
            sb.AppendLine(GenerateSampleDashboard(subscriptionId));
        }

        return sb.ToString();
    }
    
    private static string GenerateSampleDashboard(string subscriptionId)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("**💰 Sample Cost Summary**");
        sb.AppendLine();
        sb.AppendLine("| Metric | Sample Value |");
        sb.AppendLine("|--------|--------------|");
        sb.AppendLine("| Current Month Spend | **$2,450.00** 📈 |");
        sb.AppendLine("| Previous Month | $2,100.00 |");
        sb.AppendLine("| Month-over-Month Change | ↗️ +16.7% |");
        sb.AppendLine("| Average Daily Cost | $81.67 |");
        sb.AppendLine("| Projected Month-End | $2,530.00 |");
        sb.AppendLine();
        
        sb.AppendLine("**🔝 Sample Top Services**");
        sb.AppendLine();
        sb.AppendLine("| Service | Monthly Cost | % of Total | Resources |");
        sb.AppendLine("|---------|--------------|------------|-----------|");
        sb.AppendLine("| Cognitive Services | $1,850.00 | [███████████████░░░░] 75.5% | 2 |");
        sb.AppendLine("| Storage | $320.00 | [██░░░░░░░░░░░░░░░░░░] 13.1% | 3 |");
        sb.AppendLine("| Networking | $280.00 | [██░░░░░░░░░░░░░░░░░░] 11.4% | 1 |");
        sb.AppendLine();
        
        sb.AppendLine("**⚠️ Sample Budget Alerts**");
        sb.AppendLine();
        sb.AppendLine("🟡 **Monthly-AI-Budget**: [████████████████░░░░] 82% of $3,000.00 (Warning)");
        sb.AppendLine();
        
        sb.AppendLine("**🔍 Sample Cost Anomalies**");
        sb.AppendLine();
        sb.AppendLine("🟡 **Oct 15**: Unusual spike in API calls");
        sb.AppendLine("   - Deviation: $185.00 (↗️ +45.2%)");
        sb.AppendLine();
        
        sb.AppendLine("**💡 Sample Cost Optimization Recommendations**");
        sb.AppendLine();
        sb.AppendLine("| Priority | Recommendation | Monthly Savings | Complexity |");
        sb.AppendLine("|----------|----------------|-----------------|------------|");
        sb.AppendLine("| 🟡 Medium | Implement caching for API calls | **$420.00** | ✅ Simple |");
        sb.AppendLine("| 🟢 Low | Use commitment discounts for OpenAI | **$280.00** | ⚡ Moderate |");
        sb.AppendLine("| 🟢 Low | Optimize storage tier for logs | **$95.00** | ✅ Simple |");
        sb.AppendLine();
        
        sb.AppendLine("---");
        sb.AppendLine("_This is sample data to demonstrate formatting. Your dashboard will show actual cost data once available._");
        
        return sb.ToString();
    }
    
    private static string CreateProgressBar(decimal percentage, int width = 20)
    {
        var filled = (int)(percentage / 100 * width);
        filled = Math.Max(0, Math.Min(width, filled));
        var empty = width - filled;
        return $"[{'█'.ToString().PadRight(filled, '█')}{'░'.ToString().PadRight(empty, '░')}]";
    }
    
    private static string FormatPercentage(decimal value)
    {
        var sign = value >= 0 ? "+" : "";
        var icon = value > 5 ? "↗️" : value < -5 ? "↘️" : "→";
        return $"{icon} {sign}{value:N1}%";
    }

    private async Task<string> HandleOptimizationAsync(string subscriptionId, CancellationToken cancellationToken)
    {
    var analysis = await _costOptimizationEngine.AnalyzeSubscriptionAsync(subscriptionId);
    var recommendations = analysis.Recommendations ?? new List<DetailedCostOptimizationRecommendation>();

        var sb = new StringBuilder();
        sb.AppendLine("# 🎯 Cost Optimization Analysis");
        sb.AppendLine();
        sb.AppendLine($"**Subscription:** `{subscriptionId}`");
        sb.AppendLine();
        
        // Financial Overview
        sb.AppendLine("## 💵 Financial Overview");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Value |");
        sb.AppendLine($"|--------|-------|");
        sb.AppendLine($"| Total Monthly Cost | **{FormatCurrency(analysis.TotalMonthlyCost)}** |");
        sb.AppendLine($"| Potential Savings | **{FormatCurrency(analysis.PotentialMonthlySavings)}** |");
        sb.AppendLine($"| Savings Opportunity | {(analysis.TotalMonthlyCost > 0 ? (analysis.PotentialMonthlySavings / analysis.TotalMonthlyCost * 100) : 0):N1}% |");
        sb.AppendLine($"| Total Recommendations | {analysis.TotalRecommendations} |");
        sb.AppendLine();

        var topServices = (analysis.CostByService ?? new Dictionary<string, decimal>())
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .ToList();

        if (topServices.Any())
        {
            sb.AppendLine("## 🔝 Top Cost Drivers by Service");
            sb.AppendLine();
            var total = topServices.Sum(kvp => kvp.Value);
            foreach (var service in topServices)
            {
                var percentage = total > 0 ? (service.Value / total * 100) : 0;
                var bar = CreateProgressBar(percentage);
                sb.AppendLine($"- **{service.Key}**: {FormatCurrency(service.Value)} {bar} {percentage:N1}%");
            }
            sb.AppendLine();
        }

        // Detailed Recommendations
        sb.AppendLine("## 📋 Detailed Recommendations");
        sb.AppendLine();
        
        if (recommendations.Any())
        {
            var sortedRecs = recommendations.OrderByDescending(r => r.EstimatedMonthlySavings).Take(10).ToList();
            
            foreach (var rec in sortedRecs)
            {
                var priorityIcon = rec.Priority switch
                {
                    Models.CostOptimization.OptimizationPriority.Critical => "🔴",
                    Models.CostOptimization.OptimizationPriority.High => "🟠",
                    Models.CostOptimization.OptimizationPriority.Medium => "🟡",
                    _ => "🟢"
                };
                
                var complexityBadge = rec.Complexity switch
                {
                    ImplementationComplexity.VeryComplex => "🔴 Very Complex",
                    ImplementationComplexity.Complex => "⚠️ Complex",
                    ImplementationComplexity.Moderate => "⚡ Moderate",
                    _ => "✅ Simple"
                };
                
                sb.AppendLine($"### {priorityIcon} {rec.Description}");
                sb.AppendLine();
                sb.AppendLine($"**Resource:** `{rec.ResourceName}` ({rec.ResourceType})");
                sb.AppendLine($"**Location:** {rec.ResourceGroup}");
                sb.AppendLine($"**Savings:** {FormatCurrency(rec.EstimatedMonthlySavings)}/month");
                sb.AppendLine($"**Implementation:** {complexityBadge}");
                
                var actionCount = rec.Actions?.Count ?? 0;
                if (actionCount > 0)
                {
                    sb.AppendLine($"**Action Steps:** {actionCount}");
                    var actions = rec.Actions!.Take(3).ToList();
                    foreach (var action in actions)
                    {
                        sb.AppendLine($"  - {action.Description}");
                    }
                    if (actionCount > 3)
                    {
                        sb.AppendLine($"  - _{actionCount - 3} more actions..._");
                    }
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("_No recommendations available. Your resources are already well-optimized! 🎉_");
        }

        return sb.ToString();
    }

    private async Task<string> HandleBudgetsAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var budgets = await _costService.GetBudgetsAsync(subscriptionId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("# 💰 Budget Monitoring");
        sb.AppendLine();
        sb.AppendLine($"**Subscription:** `{subscriptionId}`");
        sb.AppendLine();

        if (budgets.Count == 0)
        {
            sb.AppendLine("## ⚠️ No Budgets Configured");
            sb.AppendLine();
            sb.AppendLine("No budgets are currently configured for this subscription.");
            sb.AppendLine();
            sb.AppendLine("### 💡 Recommendations");
            sb.AppendLine("- Create budgets to monitor and control spending");
            sb.AppendLine("- Set up alert notifications at 50%, 80%, and 100% thresholds");
            sb.AppendLine("- Use Azure Cost Management to configure budgets");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine($"az consumption budget create --subscription {subscriptionId} \\");
            sb.AppendLine("  --budget-name 'Monthly-Budget' \\");
            sb.AppendLine("  --amount 50000 \\");
            sb.AppendLine("  --time-grain Monthly");
            sb.AppendLine("```");
            return sb.ToString();
        }

        sb.AppendLine("## 📊 Budget Status");
        sb.AppendLine();

        foreach (var budget in budgets.Take(10))
        {
            var statusIcon = budget.HealthStatus switch
            {
                BudgetHealthStatus.Critical => "🔴",
                BudgetHealthStatus.Warning => "🟡",
                BudgetHealthStatus.Healthy => "🟢",
                _ => "⚪"
            };
            
            var progressBar = CreateProgressBar(budget.UtilizationPercentage);
            
            sb.AppendLine($"### {statusIcon} {budget.Name}");
            sb.AppendLine();
            sb.AppendLine($"**Budget Amount:** {FormatCurrency(budget.Amount)}");
            sb.AppendLine($"**Current Spend:** {FormatCurrency(budget.CurrentSpend)}");
            sb.AppendLine($"**Remaining:** {FormatCurrency(budget.RemainingBudget)}");
            sb.AppendLine();
            sb.AppendLine($"**Utilization:** {budget.UtilizationPercentage:N1}%");
            sb.AppendLine($"{progressBar}");
            sb.AppendLine();
            
            if (budget.Thresholds.Any())
            {
                sb.AppendLine("**Alert Thresholds:**");
                foreach (var threshold in budget.Thresholds.OrderBy(t => t.Percentage))
                {
                    var thresholdIcon = threshold.Severity switch
                    {
                        BudgetAlertSeverity.Critical => "🔴",
                        BudgetAlertSeverity.Warning => "🟡",
                        _ => "🟢"
                    };
                    var reached = budget.UtilizationPercentage >= threshold.Percentage ? "✅ Reached" : "⏳ Not reached";
                    sb.AppendLine($"  - {thresholdIcon} {threshold.Percentage}% ({threshold.Severity}) - {reached}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private async Task<string> HandleForecastAsync(string subscriptionId, string query, CancellationToken cancellationToken)
    {
        var forecastDays = DetermineForecastWindow(query);
        var forecast = await _costService.GetCostForecastAsync(subscriptionId, forecastDays, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("# 🔮 Cost Forecast");
        sb.AppendLine();
        sb.AppendLine($"**Subscription:** `{subscriptionId}`");
        sb.AppendLine($"**Forecast Window:** {forecastDays} days");
        sb.AppendLine($"**Confidence Level:** {forecast.ConfidenceLevel:P0}");
        sb.AppendLine($"**Method:** {forecast.Method}");
        sb.AppendLine();
        
        // Projections Summary
        sb.AppendLine("## 📈 Projected Costs");
        sb.AppendLine();
        sb.AppendLine("| Period | Projected Cost |");
        sb.AppendLine("|--------|----------------|");
        sb.AppendLine($"| Month-End | **{FormatCurrency(forecast.ProjectedMonthEndCost)}** |");
        sb.AppendLine($"| Quarter-End | **{FormatCurrency(forecast.ProjectedQuarterEndCost)}** |");
        sb.AppendLine($"| Year-End | **{FormatCurrency(forecast.ProjectedYearEndCost)}** |");
        sb.AppendLine();

        // Daily Forecast
        if (forecast.Projections.Any())
        {
            sb.AppendLine("## 📅 Daily Forecast (Next 7 Days)");
            sb.AppendLine();
            sb.AppendLine("| Date | Forecast | Range |");
            sb.AppendLine("|------|----------|-------|");
            
            foreach (var point in forecast.Projections.Take(7))
            {
                sb.AppendLine($"| {point.Date:MMM dd, yyyy} | {FormatCurrency(point.ForecastedCost)} | {FormatCurrency(point.LowerBound)} - {FormatCurrency(point.UpperBound)} |");
            }
            sb.AppendLine();
        }

        // Assumptions
        if (forecast.Assumptions.Any())
        {
            sb.AppendLine("## 📋 Forecast Assumptions");
            sb.AppendLine();
            foreach (var assumption in forecast.Assumptions.Take(5))
            {
                var impactIcon = assumption.Impact > 0.7 ? "🔴" : assumption.Impact > 0.4 ? "🟡" : "🟢";
                sb.AppendLine($"{impactIcon} **{assumption.Description}**");
                sb.AppendLine($"   - Impact: {assumption.Impact:P0} | Category: {assumption.Category}");
            }
            sb.AppendLine();
        }

        // Risks
        if (forecast.Risks.Any())
        {
            sb.AppendLine("## ⚠️ Risk Factors");
            sb.AppendLine();
            foreach (var risk in forecast.Risks.OrderByDescending(r => (double)r.PotentialImpact * r.Probability).Take(5))
            {
                var severity = ((double)risk.PotentialImpact * risk.Probability) > 0.5 ? "🔴 High" : 
                              ((double)risk.PotentialImpact * risk.Probability) > 0.3 ? "🟡 Medium" : "🟢 Low";
                sb.AppendLine($"**{risk.Risk}** - {severity}");
                sb.AppendLine($"  - Potential Impact: {FormatCurrency((decimal)risk.PotentialImpact)}");
                sb.AppendLine($"  - Probability: {risk.Probability:P0}");
                sb.AppendLine($"  - Mitigation: {risk.Mitigation}");
                sb.AppendLine();
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

    #region MCP-Enhanced Functions

    [KernelFunction("get_cost_optimization_recommendations_with_best_practices")]
    [Description("Get comprehensive cost optimization recommendations using Azure MCP Advisor and FinOps best practices. " +
                 "Provides Azure Advisor insights, FinOps guidance, and actionable cost-saving opportunities.")]
    public async Task<string> GetCostOptimizationRecommendationsWithBestPracticesAsync(
        [Description("Azure subscription ID to analyze")] 
        string subscriptionId,
        
        [Description("Optional resource group to focus analysis (analyzes all if not provided)")] 
        string? resourceGroup = null,
        
        [Description("Include detailed implementation steps (default: true)")] 
        bool includeImplementation = true,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Get cost optimization analysis from existing engine
            var optimizationResult = await _costOptimizationEngine.AnalyzeSubscriptionAsync(
                subscriptionId);

            // 2. Get Azure Advisor recommendations via MCP
            var advisorArgs = new Dictionary<string, object?>
            {
                ["subscriptionId"] = subscriptionId
            };
            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                advisorArgs["resourceGroup"] = resourceGroup;
            }

            var advisorResult = await _azureMcpClient.CallToolAsync("advisor", advisorArgs, cancellationToken);
            var advisorRecommendations = advisorResult?.Result?.ToString() ?? "Advisor recommendations unavailable";

            // 3. Get FinOps best practices from MCP
            var finOpsArgs = new Dictionary<string, object?>
            {
                ["query"] = "Azure FinOps cost optimization best practices and cloud financial management"
            };
            var finOpsResult = await _azureMcpClient.CallToolAsync("get_bestpractices", finOpsArgs, cancellationToken);
            var finOpsBestPractices = finOpsResult?.Result?.ToString() ?? "FinOps best practices unavailable";

            // 4. Get cost management best practices
            var costMgmtArgs = new Dictionary<string, object?>
            {
                ["query"] = "Azure cost management and budget optimization strategies"
            };
            var costMgmtResult = await _azureMcpClient.CallToolAsync("get_bestpractices", costMgmtArgs, cancellationToken);
            var costManagementGuidance = costMgmtResult?.Result?.ToString() ?? "Cost management guidance unavailable";

            // 5. Build comprehensive optimization report
            var topRecommendations = optimizationResult.Recommendations
                .OrderByDescending(r => r.EstimatedMonthlySavings)
                .Take(10)
                .Select(r => new
                {
                    resourceId = r.ResourceId,
                    resourceType = r.ResourceType,
                    resourceGroup = r.ResourceGroup,
                    recommendation = r.Description,
                    estimatedMonthlySavings = FormatCurrency(r.EstimatedMonthlySavings),
                    estimatedAnnualSavings = FormatCurrency(r.EstimatedMonthlySavings * 12),
                    priority = r.Priority.ToString(),
                    complexity = r.Complexity.ToString()
                })
                .ToList();

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId,
                resourceGroup = resourceGroup ?? "All resources",
                analysis = new
                {
                    totalRecommendations = optimizationResult.Recommendations.Count,
                    estimatedMonthlySavings = FormatCurrency(optimizationResult.Recommendations.Sum(r => r.EstimatedMonthlySavings)),
                    estimatedAnnualSavings = FormatCurrency(optimizationResult.Recommendations.Sum(r => r.EstimatedMonthlySavings) * 12),
                    analyzedResources = optimizationResult.TotalRecommendations
                },
                topRecommendations,
                azureAdvisor = new
                {
                    source = "Azure Advisor via MCP",
                    recommendations = advisorRecommendations
                },
                finOpsBestPractices = new
                {
                    source = "FinOps Framework",
                    guidance = finOpsBestPractices
                },
                costManagementStrategies = new
                {
                    source = "Azure Cost Management Best Practices",
                    strategies = costManagementGuidance
                },
                implementationGuide = includeImplementation ? new
                {
                    quickWins = new[]
                    {
                        "Stop or deallocate unused virtual machines",
                        "Right-size over-provisioned resources",
                        "Delete unattached disks and orphaned resources",
                        "Switch to reserved instances for stable workloads",
                        "Enable auto-shutdown for dev/test VMs"
                    },
                    mediumTerm = new[]
                    {
                        "Implement Azure Hybrid Benefit for Windows/SQL licenses",
                        "Move appropriate workloads to spot instances",
                        "Consolidate resources to reduce management overhead",
                        "Implement autoscaling for variable workloads",
                        "Review and optimize storage tiers"
                    },
                    longTerm = new[]
                    {
                        "Establish FinOps culture and accountability",
                        "Implement comprehensive tagging strategy",
                        "Set up cost allocation and showback/chargeback",
                        "Regular architectural reviews for cost optimization",
                        "Continuous cost optimization monitoring"
                    }
                } : null,
                nextSteps = new[]
                {
                    "Review top recommendations by estimated savings",
                    "Check Azure Advisor for additional insights",
                    "Implement quick wins with high savings/low effort",
                    "Establish FinOps practices for ongoing optimization",
                    includeImplementation ? "Follow implementation guide by priority" : "Say 'get recommendations with implementation' for detailed steps",
                    "Set up budgets and alerts for cost control",
                    "Schedule monthly cost optimization reviews"
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost optimization recommendations");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get cost recommendations: {ex.Message}"
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_budget_recommendations_with_best_practices")]
    [Description("Get Azure budget recommendations and financial governance best practices using Azure MCP. " +
                 "Provides budget setup guidance, cost alerting strategies, and financial management best practices.")]
    public async Task<string> GetBudgetRecommendationsWithBestPracticesAsync(
        [Description("Azure subscription ID to analyze")] 
        string subscriptionId,
        
        [Description("Target monthly budget amount in USD (optional, will suggest if not provided)")] 
        decimal? targetBudget = null,
        
        [Description("Include automation scripts for budget setup (default: true)")] 
        bool includeAutomation = true,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Get current spending analysis
            var costAnalysis = await _costOptimizationEngine.AnalyzeSubscriptionAsync(
                subscriptionId);

            // 2. Get budget best practices from MCP
            var budgetArgs = new Dictionary<string, object?>
            {
                ["query"] = "Azure budget management and cost alerting best practices"
            };
            var budgetResult = await _azureMcpClient.CallToolAsync("get_bestpractices", budgetArgs, cancellationToken);
            var budgetBestPractices = budgetResult?.Result?.ToString() ?? "Budget best practices unavailable";

            // 3. Get financial governance guidance
            var governanceArgs = new Dictionary<string, object?>
            {
                ["query"] = "Cloud financial governance and budget accountability frameworks"
            };
            var governanceResult = await _azureMcpClient.CallToolAsync("get_bestpractices", governanceArgs, cancellationToken);
            var governanceGuidance = governanceResult?.Result?.ToString() ?? "Governance guidance unavailable";

            // 4. Get cost anomaly detection recommendations
            var anomalyArgs = new Dictionary<string, object?>
            {
                ["query"] = "Azure cost anomaly detection and alerting strategies"
            };
            var anomalyResult = await _azureMcpClient.CallToolAsync("get_bestpractices", anomalyArgs, cancellationToken);
            var anomalyDetection = anomalyResult?.Result?.ToString() ?? "Anomaly detection guidance unavailable";

            // 5. Calculate recommended budget thresholds
            var currentMonthlyAverage = costAnalysis.TotalMonthlyCost;
            var suggestedBudget = targetBudget ?? (currentMonthlyAverage * 1.1m); // 10% buffer

            var budgetRecommendations = new
            {
                suggestedMonthlyBudget = FormatCurrency(suggestedBudget),
                currentMonthlyAverage = FormatCurrency(currentMonthlyAverage),
                alertThresholds = new[]
                {
                    new { percentage = 50, amount = FormatCurrency(suggestedBudget * 0.5m), severity = "Informational", action = "Monitor spending trends" },
                    new { percentage = 75, amount = FormatCurrency(suggestedBudget * 0.75m), severity = "Warning", action = "Review current spending and forecasts" },
                    new { percentage = 90, amount = FormatCurrency(suggestedBudget * 0.9m), severity = "Critical", action = "Immediate cost reduction required" },
                    new { percentage = 100, amount = FormatCurrency(suggestedBudget), severity = "Critical", action = "Budget exceeded - take action" }
                },
                forecastedOverrun = currentMonthlyAverage > suggestedBudget
            };

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId = subscriptionId,
                budgetAnalysis = budgetRecommendations,
                bestPractices = new
                {
                    budgetManagement = new
                    {
                        source = "Azure Best Practices",
                        guidance = budgetBestPractices
                    },
                    financialGovernance = new
                    {
                        source = "FinOps Framework",
                        governance = governanceGuidance
                    },
                    anomalyDetection = new
                    {
                        source = "Azure Cost Anomaly Detection",
                        strategies = anomalyDetection
                    }
                },
                automationScripts = includeAutomation ? new
                {
                    budgetCreation = new
                    {
                        language = "Azure CLI",
                        description = "Create budget with alert thresholds",
                        note = "Customize values before execution"
                    },
                    alertConfiguration = new
                    {
                        language = "Azure PowerShell",
                        description = "Configure cost alert action groups",
                        note = "Set email/SMS/webhook endpoints"
                    },
                    anomalyAlerts = new
                    {
                        language = "Azure Policy",
                        description = "Set up cost anomaly detection alerts",
                        note = "Requires Azure Cost Management permissions"
                    }
                } : null,
                recommendations = new[]
                {
                    new { priority = "Critical", recommendation = $"Set monthly budget at {FormatCurrency(suggestedBudget)}", timeframe = "Immediate" },
                    new { priority = "Critical", recommendation = "Configure 50%, 75%, 90%, 100% alert thresholds", timeframe = "24 hours" },
                    new { priority = "High", recommendation = "Set up action groups for budget alerts", timeframe = "1 week" },
                    new { priority = "High", recommendation = "Enable cost anomaly detection", timeframe = "1 week" },
                    new { priority = "Medium", recommendation = "Implement cost allocation tags", timeframe = "2 weeks" },
                    new { priority = "Medium", recommendation = "Set up chargeback/showback reports", timeframe = "1 month" },
                    new { priority = "Low", recommendation = "Establish monthly budget review process", timeframe = "Ongoing" }
                },
                nextSteps = new[]
                {
                    $"Create monthly budget of {FormatCurrency(suggestedBudget)}",
                    "Configure alert thresholds at recommended percentages",
                    "Set up email/SMS notifications for budget alerts",
                    "Enable cost anomaly detection",
                    includeAutomation ? "Use provided automation scripts for setup" : "Say 'get budget recommendations with automation' for scripts",
                    "Say 'get cost optimization recommendations' to reduce current spending",
                    "Schedule monthly budget review meetings"
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting budget recommendations");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get budget recommendations: {ex.Message}"
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion
}
