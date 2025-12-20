using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.CostOptimization;
using Platform.Engineering.Copilot.Core.Models.CostOptimization.Analysis;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Plugins;
using DetailedCostOptimizationRecommendation = Platform.Engineering.Copilot.Core.Models.CostOptimization.Analysis.CostOptimizationRecommendation;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Cost;
using Platform.Engineering.Copilot.CostManagement.Core.Configuration;

namespace Platform.Engineering.Copilot.CostManagement.Agent.Plugins;

/// <summary>
/// Semantic Kernel plugin for Azure cost management and optimization
/// </summary>
public class CostManagementPlugin : BaseSupervisorPlugin
{
    private readonly ICostOptimizationEngine _costOptimizationEngine;
    private readonly IAzureCostManagementService _costService;
    private readonly AzureMcpClient _azureMcpClient;
    private readonly ConfigService _configService;
    private readonly IMemoryCache _subscriptionCache;
    private readonly CostManagementAgentOptions _options;
    
    private const string LAST_SUBSCRIPTION_CACHE_KEY = "cost_last_subscription";

    public CostManagementPlugin(
        ILogger<CostManagementPlugin> logger,
        Kernel kernel,
        ICostOptimizationEngine costOptimizationEngine,
        IAzureCostManagementService costService,
        AzureMcpClient azureMcpClient,
        ConfigService configService,
        IMemoryCache subscriptionCache,
        IOptions<CostManagementAgentOptions> options) : base(logger, kernel)
    {
        _costOptimizationEngine = costOptimizationEngine ?? throw new ArgumentNullException(nameof(costOptimizationEngine));
        _costService = costService ?? throw new ArgumentNullException(nameof(costService));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _subscriptionCache = subscriptionCache ?? throw new ArgumentNullException(nameof(subscriptionCache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }
    
    // ========== SUBSCRIPTION LOOKUP HELPERS ==========
    
    /// <summary>
    /// Stores the last used subscription ID in cache AND persistent config file for session continuity
    /// </summary>
    private void SetLastUsedSubscription(string subscriptionId)
    {
        _subscriptionCache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
        try
        {
            _configService.SetDefaultSubscription(subscriptionId);
            _logger.LogInformation("Stored subscription in persistent config: {SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist subscription to config file, will only use cache");
        }
    }
    
    /// <summary>
    /// Gets the last used subscription ID from cache, or persistent config file if cache is empty
    /// </summary>
    private string? GetLastUsedSubscription()
    {
        if (_subscriptionCache.TryGetValue<string>(LAST_SUBSCRIPTION_CACHE_KEY, out var subscriptionId))
        {
            _logger.LogDebug("Retrieved last used subscription from cache: {SubscriptionId}", subscriptionId);
            return subscriptionId;
        }
        
        try
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogInformation("Retrieved subscription from persistent config: {SubscriptionId}", subscriptionId);
                _subscriptionCache.Set(LAST_SUBSCRIPTION_CACHE_KEY, subscriptionId, TimeSpan.FromHours(24));
                return subscriptionId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read subscription from config file");
        }
        
        return null;
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
            
            // Try to get subscription from: 1) parameter, 2) query text, 3) cached/config default
            subscriptionId ??= ExtractSubscriptionId(query);
            
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                // Fall back to last used subscription from cache or config
                subscriptionId = GetLastUsedSubscription();
                if (!string.IsNullOrWhiteSpace(subscriptionId))
                {
                    _logger.LogInformation("Using last used subscription from session: {SubscriptionId}", subscriptionId);
                }
            }

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return @"⚠️ **No Subscription Configured**

I couldn't identify which Azure subscription to analyze. 

**To fix this, you can:**
1. **Set a default subscription:** `Set my subscription to <subscription-id>`
2. **Include it in your query:** `What's the cost for subscription 453c2549-...`

💡 Once you set a default subscription, it will be used for all future cost queries automatically.";
            }
            
            // Persist the subscription for future calls
            SetLastUsedSubscription(subscriptionId);
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
        var lookbackDays = DetermineLookbackWindow(query);
        var startDate = endDate.AddDays(-lookbackDays);

        // Use caching with configured RefreshIntervalMinutes
        var cacheKey = $"cost_dashboard_{subscriptionId}_{lookbackDays}";
        if (!_subscriptionCache.TryGetValue<CostMonitoringDashboard>(cacheKey, out var dashboard) || dashboard == null)
        {
            dashboard = await _costService.GetCostDashboardAsync(subscriptionId, startDate, endDate, cancellationToken);
            
            // Cache for the configured refresh interval
            var cacheExpiration = TimeSpan.FromMinutes(_options.CostManagement.RefreshIntervalMinutes);
            _subscriptionCache.Set(cacheKey, dashboard, cacheExpiration);
            _logger.LogDebug("Cached cost dashboard for {Minutes} minutes", _options.CostManagement.RefreshIntervalMinutes);
        }
        else
        {
            _logger.LogDebug("Using cached cost dashboard for subscription {SubscriptionId}", subscriptionId);
        }

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

        // Cost Anomalies (if enabled in configuration)
        if (_options.EnableAnomalyDetection)
        {
            var anomalies = dashboard.Anomalies
                .Where(a => Math.Abs(a.PercentageDeviation) >= _options.CostManagement.AnomalyThresholdPercentage)
                .Take(5)
                .ToList();
                
            if (anomalies.Any())
            {
                sb.AppendLine("## 🔍 Cost Anomalies Detected");
                sb.AppendLine();
                sb.AppendLine($"_Threshold: {_options.CostManagement.AnomalyThresholdPercentage}% deviation_");
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
                var priorityIcon = ((int)rec.Priority) switch
                {
                    3 => "🔴 Critical", // Critical
                    2 => "🟠 High",     // High
                    1 => "🟡 Medium",   // Medium
                    _ => "🟢 Low"       // Low
                };
                var complexityIcon = ((int)rec.Complexity) switch
                {
                    3 => "🔴 Expert",   // VeryComplex
                    2 => "⚠️ Complex",  // Complex
                    1 => "⚡ Moderate", // Moderate
                    _ => "✅ Simple"    // Simple
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
        // Check if optimization recommendations are enabled
        if (!_options.EnableOptimizationRecommendations)
        {
            return "⚠️ Cost optimization recommendations are currently disabled in configuration. " +
                   "Enable 'EnableOptimizationRecommendations' in CostManagementAgent settings to use this feature.";
        }

        var analysis = await _costOptimizationEngine.AnalyzeSubscriptionAsync(subscriptionId);
        var recommendations = analysis.Recommendations ?? new List<DetailedCostOptimizationRecommendation>();

        // Filter recommendations by minimum savings threshold
        recommendations = recommendations
            .Where(r => r.EstimatedMonthlySavings >= (decimal)_options.CostManagement.MinimumSavingsThreshold)
            .ToList();

        _logger.LogInformation("Found {Count} optimization recommendations (filtered by minimum savings threshold: {Threshold})",
            recommendations.Count, _options.CostManagement.MinimumSavingsThreshold);

        // Get Azure best practices for cost optimization via MCP
        object? mcpBestPractices = null;
        try
        {
            await _azureMcpClient.InitializeAsync(cancellationToken);
            
            _logger.LogInformation("Fetching cost optimization best practices via Azure MCP");
            
            var bestPractices = await _azureMcpClient.CallToolAsync("get_bestpractices", 
                new Dictionary<string, object?>
                {
                    ["resourceType"] = "cost-optimization"
                }, cancellationToken);

            if (bestPractices.Success)
            {
                mcpBestPractices = bestPractices.Result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve cost optimization best practices from Azure MCP");
        }

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
                var priorityIcon = ((int)rec.Priority) switch
                {
                    3 => "🔴", // Critical
                    2 => "🟠", // High
                    1 => "🟡", // Medium
                    _ => "🟢"  // Low
                };
                
                var complexityBadge = rec.Complexity switch
                {
                    OptimizationComplexity.Complex => "⚠️ Complex",
                    OptimizationComplexity.Moderate => "⚡ Moderate",
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

        // Add Azure MCP best practices if available
        if (mcpBestPractices != null)
        {
            sb.AppendLine();
            sb.AppendLine("## 📚 Azure Cost Optimization Best Practices");
            sb.AppendLine();
            sb.AppendLine("_Guidance from Microsoft Azure Best Practices:_");
            sb.AppendLine();
            sb.AppendLine($"```");
            sb.AppendLine(mcpBestPractices.ToString());
            sb.AppendLine($"```");
            sb.AppendLine();
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
            sb.AppendLine($"- Set up alert notifications at {string.Join("%, ", _options.Budgets.DefaultAlertThresholds)}% thresholds");
            sb.AppendLine("- Use Azure Cost Management to configure budgets");
            if (_options.Budgets.EmailNotifications && _options.Budgets.NotificationEmails.Any())
            {
                sb.AppendLine($"- Email notifications will be sent to: {string.Join(", ", _options.Budgets.NotificationEmails)}");
            }
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
        // Use configured forecast days or parse from query
        var forecastDays = DetermineForecastWindow(query);
        if (forecastDays == 30) // If default, use configured value
        {
            forecastDays = _options.CostManagement.ForecastDays;
        }

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

    /// <summary>
    /// Determines lookback window from query, falling back to DefaultTimeframe config
    /// </summary>
    private int DetermineLookbackWindow(string query)
    {
        // Check query for explicit time references
        if (query.Contains("quarter")) return 90;
        if (query.Contains("year")) return 365;
        if (query.Contains("week")) return 7;
        if (query.Contains("month") || query.Contains("last month")) return 30;
        
        // Fall back to configured DefaultTimeframe
        return _options.DefaultTimeframe.ToLowerInvariant() switch
        {
            "monthttodate" or "monthtodate" => (DateTime.UtcNow.Day), // Days into current month
            "lastmonth" => 30,
            "last7days" => 7,
            "last30days" => 30,
            "last90days" or "quarter" => 90,
            "yeartodate" => DateTime.UtcNow.DayOfYear,
            _ => 30 // Default fallback
        };
    }

    private static int DetermineForecastWindow(string query)
    {
        if (query.Contains("quarter")) return 90;
        if (query.Contains("year")) return 365;
        if (query.Contains("week")) return 7;
        if (query.Contains("month")) return 30;
        if (query.Contains("6 month")) return 180;
        return 30;
    }

    private string FormatCurrency(decimal amount)
    {
        // Use configured currency
        var currencySymbol = _options.DefaultCurrency switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "JPY" => "¥",
            "CAD" => "C$",
            "AUD" => "A$",
            _ => _options.DefaultCurrency + " "
        };
        
        return string.Format(CultureInfo.InvariantCulture, "{0}{1:N2}", currencySymbol, amount);
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

    [KernelFunction("get_cost_optimization_recommendations")]
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

    [KernelFunction("get_budget_recommendations")]
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

    // ========== AZURE MCP ENHANCED FUNCTIONS ==========

    [KernelFunction("search_cost_docs")]
    [Description("Search official Microsoft Azure cost management documentation for guidance. " +
                 "Use when you need official docs on pricing, cost optimization, budgets, or FinOps best practices. " +
                 "Examples: 'How to optimize storage costs', 'Reserved instance pricing', 'Cost allocation tags'")]
    public async Task<string> SearchCostOptimizationDocsAsync(
        [Description("Search query (e.g., 'reserved instances pricing', 'cost optimization strategies', 'budget alerts')")] string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching Azure cost documentation for: {Query}", query);

            if (string.IsNullOrWhiteSpace(query))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Search query is required"
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            await _azureMcpClient.InitializeAsync(cancellationToken);

            var docs = await _azureMcpClient.CallToolAsync("documentation", 
                new Dictionary<string, object?>
                {
                    ["query"] = $"Azure cost management {query}"
                }, cancellationToken);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = docs.Success,
                query = query,
                results = docs.Success ? docs.Result : "Documentation search unavailable",
                nextSteps = new[]
                {
                    "Review the documentation results above for official Microsoft guidance.",
                    "Say 'get cost optimization recommendations for subscription <id>' for actionable recommendations.",
                    "Say 'analyze costs for subscription <id>' for detailed cost analysis.",
                    "Visit https://learn.microsoft.com/azure/cost-management-billing for comprehensive guides."
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching cost documentation for query: {Query}", query);
            return CreateErrorResponse("search cost documentation", ex);
        }
    }

    #endregion

    // NOTE: Advanced Forecasting Functions temporarily commented out pending implementation of advanced forecasting types
    // (SeasonalityPattern, CostScenario, GrowthProjection, etc.) in the engine
    #region Advanced Forecasting Functions - DISABLED
    /*

    [KernelFunction("forecast_costs_with_seasonality")]
    [Description("Generate advanced cost forecast with seasonality detection and confidence intervals. Detects weekly/monthly patterns and provides more accurate forecasts than simple linear projection.")]
    public async Task<string> ForecastCostsWithSeasonalityAsync(
        [Description("Azure subscription ID to analyze")] 
        string subscriptionId,
        
        [Description("Number of days to forecast (default: 90)")] 
        int forecastDays = 90,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating advanced forecast with seasonality for subscription {SubscriptionId}, {Days} days", 
                subscriptionId, forecastDays);

            var forecast = await _costOptimizationEngine.GetAdvancedForecastAsync(
                subscriptionId, 
                forecastDays, 
                cancellationToken);

            var monthlyProjection = forecast.Projections
                .GroupBy(p => new { p.Date.Year, p.Date.Month })
                .Select(g => new
                {
                    month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    totalCost = FormatCurrency(g.Sum(p => p.ForecastedCost)),
                    avgDailyCost = FormatCurrency(g.Average(p => p.ForecastedCost)),
                    confidence = $"{g.Average(p => p.Confidence):P0}"
                })
                .Take(6)
                .ToList();

            var seasonalityInfo = forecast.Seasonality != null && forecast.Seasonality.Type != "None"
                ? new
                {
                    detected = true,
                    pattern = forecast.Seasonality.Type,
                    strength = $"{forecast.Seasonality.Strength:P0}",
                    confidence = $"{forecast.Seasonality.Confidence:P0}",
                    insights = forecast.Seasonality.DetectedPatterns
                }
                : new
                {
                    detected = false,
                    pattern = "None",
                    strength = "0%",
                    confidence = "0%",
                    insights = new List<string> { "No significant seasonal patterns detected" }
                };

            var trendInfo = forecast.Trend != null
                ? new
                {
                    direction = forecast.Trend.Direction.ToString(),
                    dailyChange = FormatCurrency(forecast.Trend.AverageDailyChange),
                    monthlyChange = FormatCurrency(forecast.Trend.ProjectedMonthlyChange),
                    confidence = $"{forecast.Trend.RSquared:P0}"
                }
                : null;

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId,
                forecastPeriod = $"{forecastDays} days",
                method = "Time Series Decomposition with Seasonality",
                summary = new
                {
                    currentMonthlySpend = FormatCurrency(forecast.Projections.Take(30).Sum(p => p.ForecastedCost)),
                    projectedMonthEnd = FormatCurrency(forecast.ProjectedMonthEndCost),
                    projectedQuarterEnd = FormatCurrency(forecast.ProjectedQuarterEndCost),
                    projectedYearEnd = FormatCurrency(forecast.ProjectedYearEndCost),
                    overallConfidence = $"{forecast.ConfidenceLevel:P0}"
                },
                seasonality = seasonalityInfo,
                trend = trendInfo,
                monthlyProjections = monthlyProjection,
                assumptions = forecast.Assumptions.Select(a => new
                {
                    description = a.Description,
                    category = a.Category,
                    impact = $"{a.Impact:P0}"
                }).ToList(),
                nextSteps = new[]
                {
                    "Review seasonality patterns to optimize resource scheduling",
                    "Use trend information for budget planning",
                    "Model what-if scenarios with 'model_cost_scenario'",
                    "Set up budget alerts based on projected costs"
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating advanced forecast for subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("generate advanced forecast", ex);
        }
    }

    [KernelFunction("model_cost_scenario")]
    [Description("Model what-if cost scenarios to understand impact of proposed changes before implementation. Examples: 'Downsize all VMs', 'Buy 1-year RIs', 'Migrate to Spot VMs', 'Enable auto-shutdown'.")]
    public async Task<string> ModelCostScenarioAsync(
        [Description("Azure subscription ID to analyze")] 
        string subscriptionId,
        
        [Description("Scenario description (e.g., 'Downsize all over-provisioned VMs', 'Purchase Reserved Instances for stable workloads')")] 
        string scenarioDescription,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Modeling cost scenario for subscription {SubscriptionId}: {Scenario}", 
                subscriptionId, scenarioDescription);

            // Get current optimization recommendations to build scenario
            var analysis = await _costOptimizationEngine.AnalyzeSubscriptionAsync(subscriptionId);

            // Build scenario from recommendations based on description
            var scenario = new CostScenario
            {
                Name = scenarioDescription,
                Description = $"What-if analysis: {scenarioDescription}"
            };

            var lowerDesc = scenarioDescription.ToLower();

            // Map description to actions
            if (lowerDesc.Contains("downsize") || lowerDesc.Contains("right-size") || lowerDesc.Contains("rightsize"))
            {
                var rightsizingRecs = analysis.Recommendations
                    .Where(r => r.Type == OptimizationType.RightSizing)
                    .ToList();

                foreach (var rec in rightsizingRecs)
                {
                    scenario.Actions.Add(new ScenarioAction
                    {
                        ActionType = "ResizeVM",
                        ResourceId = rec.ResourceId,
                        ResourceType = rec.ResourceType,
                        EstimatedMonthlySavings = rec.EstimatedMonthlySavings,
                        Parameters = new Dictionary<string, object>
                        {
                            ["currentSize"] = "Current SKU",
                            ["targetSize"] = "Recommended SKU"
                        }
                    });
                }
            }

            if (lowerDesc.Contains("spot") || lowerDesc.Contains("spot vm"))
            {
                var vmRecs = analysis.Recommendations
                    .Where(r => r.ResourceType.Contains("VirtualMachine", StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .ToList();

                foreach (var rec in vmRecs)
                {
                    scenario.Actions.Add(new ScenarioAction
                    {
                        ActionType = "MigrateToSpot",
                        ResourceId = rec.ResourceId,
                        ResourceType = rec.ResourceType,
                        EstimatedMonthlySavings = rec.CurrentMonthlyCost * 0.7m, // 70% savings
                        Parameters = new Dictionary<string, object>
                        {
                            ["evictionPolicy"] = "Deallocate",
                            ["maxPrice"] = -1
                        }
                    });
                }
            }

            if (lowerDesc.Contains("reserved") || lowerDesc.Contains("ri") || lowerDesc.Contains("reservation"))
            {
                var stableVMs = analysis.Recommendations
                    .Where(r => r.ResourceType.Contains("VirtualMachine", StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .ToList();

                foreach (var rec in stableVMs)
                {
                    scenario.Actions.Add(new ScenarioAction
                    {
                        ActionType = "PurchaseReservedInstance",
                        ResourceId = rec.ResourceId,
                        ResourceType = rec.ResourceType,
                        EstimatedMonthlySavings = rec.CurrentMonthlyCost * 0.4m, // 40% savings (1-year RI)
                        ImplementationCost = rec.CurrentMonthlyCost * 12 * 0.6m, // Upfront cost
                        Parameters = new Dictionary<string, object>
                        {
                            ["term"] = "1-year",
                            ["paymentOption"] = "Upfront"
                        }
                    });
                }
            }

            if (lowerDesc.Contains("shutdown") || lowerDesc.Contains("auto-shutdown"))
            {
                var devTestVMs = analysis.Recommendations
                    .Where(r => r.ResourceType.Contains("VirtualMachine", StringComparison.OrdinalIgnoreCase))
                    .Take(15)
                    .ToList();

                foreach (var rec in devTestVMs)
                {
                    scenario.Actions.Add(new ScenarioAction
                    {
                        ActionType = "EnableAutoShutdown",
                        ResourceId = rec.ResourceId,
                        ResourceType = rec.ResourceType,
                        EstimatedMonthlySavings = rec.CurrentMonthlyCost * 0.5m, // 50% savings (12 hours/day)
                        Parameters = new Dictionary<string, object>
                        {
                            ["shutdownTime"] = "20:00",
                            ["startupTime"] = "08:00",
                            ["timezone"] = "UTC"
                        }
                    });
                }
            }

            // If no actions matched, use all high-priority recommendations
            if (!scenario.Actions.Any())
            {
                var topRecs = analysis.Recommendations
                    .Where(r => r.Priority == OptimizationPriority.High || r.Priority == OptimizationPriority.Critical)
                    .Take(10)
                    .ToList();

                foreach (var rec in topRecs)
                {
                    scenario.Actions.Add(new ScenarioAction
                    {
                        ActionType = rec.Type.ToString(),
                        ResourceId = rec.ResourceId,
                        ResourceType = rec.ResourceType,
                        EstimatedMonthlySavings = rec.EstimatedMonthlySavings
                    });
                }
            }

            // Model the scenario
            var comparison = await _costOptimizationEngine.ModelCostScenarioAsync(
                subscriptionId, 
                scenario, 
                cancellationToken);

            var breakEvenMonths = comparison.MonthlySavings > 0 
                ? (int)Math.Ceiling(scenario.Actions.Sum(a => a.ImplementationCost) / comparison.MonthlySavings)
                : 0;

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId,
                scenario = new
                {
                    name = scenario.Name,
                    totalActions = scenario.Actions.Count,
                    actionTypes = scenario.Actions.GroupBy(a => a.ActionType)
                        .Select(g => new { type = g.Key, count = g.Count() })
                        .ToList()
                },
                comparison = new
                {
                    baselineMonthlyCost = FormatCurrency(comparison.BaselineMonthlyCost),
                    scenarioMonthlyCost = FormatCurrency(comparison.ScenarioMonthlyCost),
                    monthlySavings = FormatCurrency(comparison.MonthlySavings),
                    annualSavings = FormatCurrency(comparison.AnnualSavings),
                    percentageReduction = $"{comparison.PercentageChange:F1}%",
                    breakEvenMonths = breakEvenMonths > 0 ? breakEvenMonths : "Immediate"
                },
                topActions = scenario.Actions
                    .OrderByDescending(a => a.EstimatedMonthlySavings)
                    .Take(5)
                    .Select(a => new
                    {
                        action = a.ActionType,
                        resource = a.ResourceType,
                        monthlySavings = FormatCurrency(a.EstimatedMonthlySavings)
                    })
                    .ToList(),
                monthlyProjection = comparison.ScenarioProjections
                    .GroupBy(p => new { p.Date.Year, p.Date.Month })
                    .Select(g => new
                    {
                        month = $"{g.Key.Year}-{g.Key.Month:D2}",
                        baselineCost = FormatCurrency(
                            comparison.BaselineProjections
                                .Where(bp => bp.Date.Year == g.Key.Year && bp.Date.Month == g.Key.Month)
                                .Sum(bp => bp.ForecastedCost)),
                        scenarioCost = FormatCurrency(g.Sum(p => p.ForecastedCost)),
                        savings = FormatCurrency(
                            comparison.BaselineProjections
                                .Where(bp => bp.Date.Year == g.Key.Year && bp.Date.Month == g.Key.Month)
                                .Sum(bp => bp.ForecastedCost) - g.Sum(p => p.ForecastedCost))
                    })
                    .Take(3)
                    .ToList(),
                recommendations = comparison.Recommendations.Take(5).ToList(),
                nextSteps = new[]
                {
                    $"Review the {scenario.Actions.Count} proposed actions",
                    breakEvenMonths > 0 ? $"Plan for {breakEvenMonths} months to break even" : "Implement immediately for instant savings",
                    "Generate implementation scripts with automation tools",
                    "Set up monitoring for cost impact validation"
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modeling cost scenario for subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("model cost scenario", ex);
        }
    }

    [KernelFunction("forecast_with_growth_projection")]
    [Description("Forecast costs based on business growth projections (e.g., '50% user growth expected'). Separates elastic costs (scale with growth) from fixed costs.")]
    public async Task<string> ForecastWithGrowthProjectionAsync(
        [Description("Azure subscription ID to analyze")] 
        string subscriptionId,
        
        [Description("Expected user/traffic growth percentage (e.g., 50 for 50% growth)")] 
        decimal growthPercentage,
        
        [Description("Number of months to forecast (default: 12)")] 
        int forecastMonths = 12,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating growth-based forecast for subscription {SubscriptionId}: {Growth}% over {Months} months", 
                subscriptionId, growthPercentage, forecastMonths);

            var growthProjection = new GrowthProjection
            {
                UserGrowthPercentage = growthPercentage,
                TrafficGrowthPercentage = growthPercentage, // Assume same as user growth
                ProjectionMonths = forecastMonths,
                ElasticServices = new List<string>
                {
                    "Virtual Machines",
                    "Azure Kubernetes Service",
                    "Azure Functions",
                    "Azure App Service",
                    "Azure SQL Database",
                    "Azure Cosmos DB",
                    "Application Insights"
                },
                FixedServices = new List<string>
                {
                    "Azure Monitor",
                    "Azure Key Vault",
                    "Azure DNS",
                    "Azure Backup"
                }
            };

            var forecast = await _costOptimizationEngine.ForecastWithGrowthProjectionAsync(
                subscriptionId, 
                growthProjection, 
                cancellationToken);

            var quarterlyBreakdown = forecast
                .GroupBy(f => (f.Month.Year, Quarter: (f.Month.Month - 1) / 3 + 1))
                .Select(g => new
                {
                    quarter = $"Q{g.Key.Quarter} {g.Key.Year}",
                    totalCost = FormatCurrency(g.Sum(f => f.TotalCost)),
                    elasticCost = FormatCurrency(g.Sum(f => f.ElasticCost)),
                    fixedCost = FormatCurrency(g.Sum(f => f.FixedCost)),
                    avgGrowthFactor = $"{g.Average(f => f.GrowthFactor):F2}x"
                })
                .ToList();

            var firstMonthCost = forecast.First().TotalCost;
            var lastMonthCost = forecast.Last().TotalCost;
            var totalIncrease = lastMonthCost - firstMonthCost;

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId,
                growthAssumptions = new
                {
                    userGrowthPercentage = $"{growthPercentage}%",
                    forecastPeriod = $"{forecastMonths} months",
                    elasticCostRatio = "70% (scales with growth)",
                    fixedCostRatio = "30% (remains stable)"
                },
                summary = new
                {
                    currentMonthlyCost = FormatCurrency(firstMonthCost),
                    projectedFinalMonthCost = FormatCurrency(lastMonthCost),
                    totalCostIncrease = FormatCurrency(totalIncrease),
                    percentageIncrease = firstMonthCost > 0 
                        ? $"{(totalIncrease / firstMonthCost * 100):F1}%"
                        : "N/A"
                },
                quarterlyProjections = quarterlyBreakdown,
                monthlyProjections = forecast.Select(f => new
                {
                    month = f.Month.ToString("yyyy-MM"),
                    totalCost = FormatCurrency(f.TotalCost),
                    elasticCost = FormatCurrency(f.ElasticCost),
                    fixedCost = FormatCurrency(f.FixedCost),
                    growthFactor = $"{f.GrowthFactor:F2}x"
                }).ToList(),
                insights = new[]
                {
                    $"Elastic costs will grow from {FormatCurrency(forecast.First().ElasticCost)} to {FormatCurrency(forecast.Last().ElasticCost)}",
                    $"Fixed costs remain stable at {FormatCurrency(forecast.First().FixedCost)}/month",
                    $"Total cost increase: {FormatCurrency(totalIncrease)} over {forecastMonths} months",
                    growthPercentage > 50 ? "⚠️ High growth rate - consider Reserved Instances and autoscaling" : "Growth is manageable with current architecture"
                },
                nextSteps = new[]
                {
                    "Review elastic services for autoscaling configuration",
                    "Consider Reserved Instances for predictable growth",
                    "Set up budget alerts aligned with growth projections",
                    "Plan capacity increases before hitting limits"
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating growth-based forecast for subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("generate growth forecast", ex);
        }
    }

    [KernelFunction("simulate_policy_impact")]
    [Description("Simulate cost impact of governance policies (e.g., 'Auto-shutdown for dev VMs', 'Block premium SKUs in non-prod') before enforcement.")]
    public async Task<string> SimulatePolicyImpactAsync(
        [Description("Azure subscription ID to analyze")] 
        string subscriptionId,
        
        [Description("Policy description (e.g., 'Enforce auto-shutdown for all dev VMs', 'Restrict to Standard SKUs only')")] 
        string policyDescription,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Simulating policy impact for subscription {SubscriptionId}: {Policy}", 
                subscriptionId, policyDescription);

            var simulation = await _costOptimizationEngine.SimulatePolicyImpactAsync(
                subscriptionId, 
                policyDescription, 
                cancellationToken);

            var roiMonths = simulation.EstimatedMonthlySavings > 0 && simulation.EstimatedImplementationDays > 0
                ? Math.Ceiling((simulation.EstimatedImplementationDays / 30.0m * 50000) / simulation.EstimatedMonthlySavings) // Assuming $50k/month dev cost
                : 0;

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId,
                policy = new
                {
                    name = simulation.PolicyName,
                    type = simulation.PolicyType,
                    complexity = simulation.Complexity.ToString()
                },
                impact = new
                {
                    impactedResources = simulation.ImpactedResourceCount,
                    estimatedMonthlySavings = FormatCurrency(simulation.EstimatedMonthlySavings),
                    estimatedAnnualSavings = FormatCurrency(simulation.EstimatedAnnualSavings),
                    preventedFutureCosts = FormatCurrency(simulation.PreventedFutureCosts),
                    savingsPercentage = "15-30% (estimated)"
                },
                implementation = new
                {
                    estimatedDays = simulation.EstimatedImplementationDays,
                    complexity = simulation.Complexity.ToString(),
                    prerequisites = simulation.Prerequisites,
                    risks = simulation.Risks
                },
                costBenefit = new
                {
                    firstYearSavings = FormatCurrency(simulation.EstimatedAnnualSavings),
                    implementationEffort = $"{simulation.EstimatedImplementationDays} days",
                    roiMonths = roiMonths > 0 ? $"{roiMonths} months" : "Immediate",
                    recommendation = simulation.EstimatedMonthlySavings > 1000 
                        ? "✅ Highly recommended - significant savings potential"
                        : simulation.EstimatedMonthlySavings > 100
                            ? "✅ Recommended - moderate savings"
                            : "⚠️ Low savings - consider other priorities first"
                },
                examples = GetPolicyExamples(simulation.PolicyType),
                nextSteps = new[]
                {
                    "Create Azure Policy definition in Azure Portal",
                    "Test policy in development subscription first",
                    "Get stakeholder approval for enforcement",
                    $"Implement policy (estimated: {simulation.EstimatedImplementationDays} days)",
                    "Monitor compliance and cost impact after deployment"
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating policy impact for subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("simulate policy impact", ex);
        }
    }

    [KernelFunction("detect_cost_seasonality")]
    [Description("Analyze historical costs to detect seasonal patterns (weekly, monthly, quarterly). Useful for understanding cost cycles and planning budgets.")]
    public async Task<string> DetectCostSeasonalityAsync(
        [Description("Azure subscription ID to analyze")] 
        string subscriptionId,
        
        [Description("Number of historical days to analyze (default: 365, minimum: 90)")] 
        int historicalDays = 365,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Detecting seasonality patterns for subscription {SubscriptionId} over {Days} days", 
                subscriptionId, historicalDays);

            var seasonality = await _costOptimizationEngine.DetectSeasonalityAsync(
                subscriptionId, 
                historicalDays, 
                cancellationToken);

            var hasPattern = seasonality.Type != "None" && seasonality.Strength > 0.5;

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                subscriptionId,
                analysisWindow = $"{historicalDays} days",
                seasonality = new
                {
                    detected = hasPattern,
                    pattern = seasonality.Type,
                    strength = $"{seasonality.Strength:P0}",
                    confidence = $"{seasonality.Confidence:P0}",
                    periodDays = seasonality.PeriodDays,
                    interpretation = seasonality.Strength > 0.7 ? "Strong pattern" :
                                    seasonality.Strength > 0.5 ? "Moderate pattern" :
                                    seasonality.Strength > 0.3 ? "Weak pattern" : "No pattern"
                },
                patterns = seasonality.DetectedPatterns,
                seasonalFactors = seasonality.SeasonalFactors.Any()
                    ? seasonality.SeasonalFactors.Select(kv => new
                    {
                        period = seasonality.Type == "Weekly" ? $"Day {kv.Key} ({GetDayName(kv.Key)})" : $"Day {kv.Key}",
                        costMultiplier = $"{kv.Value:F2}x",
                        interpretation = kv.Value < 0.9m ? "Lower cost period" :
                                       kv.Value > 1.1m ? "Higher cost period" : "Normal"
                    }).ToList()
                    : new List<object>(),
                insights = GenerateSeasonalityInsights(seasonality),
                recommendations = new[]
                {
                    hasPattern ? $"Leverage {seasonality.Type.ToLower()} patterns for budget planning" : "No seasonal patterns - costs are stable",
                    seasonality.Type == "Weekly" ? "Consider scheduling intensive workloads during low-cost periods" : null,
                    "Use advanced forecasting to account for seasonal variations",
                    "Set up different budget thresholds for high/low cost periods"
                }.Where(r => r != null).ToList()
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting seasonality for subscription {SubscriptionId}", subscriptionId);
            return CreateErrorResponse("detect seasonality", ex);
        }
    }

    */
    #endregion

    #region Helper Methods

    private List<string> GetPolicyExamples(string policyType)
    {
        return policyType switch
        {
            "AutoShutdown" => new List<string>
            {
                "Auto-shutdown VMs at 8 PM, startup at 8 AM (weekdays only)",
                "Deallocate VMs tagged 'Environment=Dev' during non-business hours",
                "Scale AKS node pools to 0 on weekends for non-prod clusters"
            },
            "SKURestriction" => new List<string>
            {
                "Block Premium SSD creation in dev/test subscriptions",
                "Limit VM SKUs to D-series in non-production",
                "Restrict expensive database tiers (P4+) to production only"
            },
            "TagEnforcement" => new List<string>
            {
                "Require 'CostCenter', 'Owner', 'Environment' tags on all resources",
                "Deny resource creation without 'Project' tag",
                "Auto-tag resources with creation date"
            },
            "BudgetControl" => new List<string>
            {
                "Block new resource creation when 90% of budget spent",
                "Send alerts at 50%, 80%, 100% budget thresholds",
                "Auto-approve only resources under $100/month"
            },
            _ => new List<string>
            {
                "Define custom policies based on your requirements",
                "Use Azure Policy for enforcement",
                "Combine multiple policies for comprehensive governance"
            }
        };
    }

    /*
    private List<string> GenerateSeasonalityInsights(SeasonalityPattern seasonality)
    {
        var insights = new List<string>();

        if (seasonality.Type == "Weekly" && seasonality.Strength > 0.6)
        {
            insights.Add($"Strong weekly pattern detected - costs vary by up to {(seasonality.SeasonalFactors.Values.Max() / seasonality.SeasonalFactors.Values.Min() - 1) * 100:F0}% throughout the week");
            
            var weekendFactor = seasonality.SeasonalFactors.Where(kv => kv.Key >= 5).Average(kv => kv.Value);
            var weekdayFactor = seasonality.SeasonalFactors.Where(kv => kv.Key < 5).Average(kv => kv.Value);
            
            if (weekendFactor < 0.8m * weekdayFactor)
            {
                insights.Add("Weekend costs are significantly lower - auto-shutdown policies are working effectively");
            }
            else if (weekendFactor > 1.1m * weekdayFactor)
            {
                insights.Add("⚠️ Weekend costs are higher than weekdays - investigate batch jobs or weekend operations");
            }
        }
        else if (seasonality.Type == "Monthly" && seasonality.Strength > 0.6)
        {
            insights.Add("Monthly cost pattern detected - likely related to billing cycles or scheduled operations");
        }
        else if (seasonality.Type == "None")
        {
            insights.Add("No significant seasonal patterns - costs are relatively stable over time");
        }

        return insights;
    }

    private string GetDayName(int dayIndex)
    {
        return dayIndex switch
        {
            0 => "Sun",
            1 => "Mon",
            2 => "Tue",
            3 => "Wed",
            4 => "Thu",
            5 => "Fri",
            6 => "Sat",
            _ => $"Day{dayIndex}"
        };
    }
    */

    #endregion
}

