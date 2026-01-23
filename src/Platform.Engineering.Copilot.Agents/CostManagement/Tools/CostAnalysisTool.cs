using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.CostManagement.State;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// Tool for analyzing Azure costs with dashboard view, historical trends, and service breakdown.
/// Consolidates: process_cost_management_query for analysis intent.
/// </summary>
public class CostAnalysisTool : BaseTool
{
    private readonly CostManagementStateAccessors _stateAccessors;
    private readonly ConfigService _configService;

    public override string Name => "analyze_azure_costs";

    public override string Description =>
        "Analyze Azure costs with comprehensive dashboard including current spend, trends, " +
        "service breakdown, and comparison to previous periods. " +
        "If subscriptionId is not provided, uses the configured default subscription. " +
        "Use for cost overview, spend analysis, and identifying cost drivers.";

    public CostAnalysisTool(
        ILogger<CostAnalysisTool> logger,
        CostManagementStateAccessors stateAccessors,
        ConfigService configService) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Azure subscription ID to analyze costs for. If not provided, uses the configured default subscription.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "lookbackDays",
            description: "Number of days to analyze (default: 30, options: 7, 14, 30, 60, 90)",
            required: false,
            type: "integer"));

        Parameters.Add(new ToolParameter(
            name: "groupBy",
            description: "Group costs by: 'service', 'resource-group', 'location', 'tag' (default: service)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "tagKey",
            description: "Tag key for filtering or grouping when groupBy='tag'",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var lookbackDays = GetOptionalInt(arguments, "lookbackDays") ?? 30;
        var groupBy = GetOptionalString(arguments, "groupBy") ?? "service";
        var tagKey = GetOptionalString(arguments, "tagKey");

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

        Logger.LogInformation("Analyzing Azure costs for subscription {SubscriptionId}, {Days} days lookback",
            subscriptionId, lookbackDays);

        try
        {
            // Check cache first
            var cached = _stateAccessors.GetCachedDashboard(subscriptionId, lookbackDays);
            if (cached != null)
            {
                Logger.LogDebug("Returning cached cost dashboard");
                return ToJson(new
                {
                    success = true,
                    fromCache = true,
                    subscriptionId,
                    lookbackDays,
                    dashboard = cached
                });
            }

            // TODO: Integrate with actual Azure Cost Management service
            // For now, return a placeholder indicating real service integration needed
            Logger.LogWarning("Cost analysis requires Azure Cost Management service integration. Returning sample data.");

            var sampleDashboard = new CostDashboardCache
            {
                SubscriptionId = subscriptionId,
                LookbackDays = lookbackDays,
                CurrentMonthSpend = 2450.00m,
                PreviousMonthSpend = 2100.00m,
                ProjectedMonthlySpend = 2530.00m,
                AverageDailyCost = 81.67m,
                YearToDateSpend = 18500.00m,
                PotentialSavings = 420.00m,
                OptimizationOpportunities = 5,
                ServiceBreakdown = new List<ServiceCostBreakdown>
                {
                    new() { ServiceName = "Virtual Machines", MonthlyCost = 1200.00m, PercentageOfTotal = 49.0m, ResourceCount = 5 },
                    new() { ServiceName = "Storage", MonthlyCost = 450.00m, PercentageOfTotal = 18.4m, ResourceCount = 8 },
                    new() { ServiceName = "Cognitive Services", MonthlyCost = 380.00m, PercentageOfTotal = 15.5m, ResourceCount = 2 },
                    new() { ServiceName = "Networking", MonthlyCost = 280.00m, PercentageOfTotal = 11.4m, ResourceCount = 4 },
                    new() { ServiceName = "Other", MonthlyCost = 140.00m, PercentageOfTotal = 5.7m, ResourceCount = 12 }
                },
                BudgetAlerts = new List<BudgetAlertSummary>
                {
                    new() { BudgetName = "Monthly-Production", BudgetAmount = 3000.00m, CurrentSpend = 2450.00m, CurrentPercentage = 81.7m, Severity = "Warning" }
                },
                CachedAt = DateTime.UtcNow
            };

            // Cache the result
            _stateAccessors.CacheDashboard(subscriptionId, lookbackDays, sampleDashboard, TimeSpan.FromMinutes(60));

            await Task.CompletedTask;

            return ToJson(new
            {
                success = true,
                fromCache = false,
                subscriptionId,
                lookbackDays,
                groupBy,
                dashboard = new
                {
                    sampleDashboard.CurrentMonthSpend,
                    sampleDashboard.PreviousMonthSpend,
                    monthOverMonthChange = ((sampleDashboard.CurrentMonthSpend - sampleDashboard.PreviousMonthSpend) / sampleDashboard.PreviousMonthSpend * 100),
                    sampleDashboard.ProjectedMonthlySpend,
                    sampleDashboard.AverageDailyCost,
                    sampleDashboard.YearToDateSpend,
                    sampleDashboard.PotentialSavings,
                    sampleDashboard.OptimizationOpportunities,
                    serviceBreakdown = sampleDashboard.ServiceBreakdown,
                    budgetAlerts = sampleDashboard.BudgetAlerts,
                    analysisDate = DateTime.UtcNow
                },
                note = "This is sample data. Integrate with Azure Cost Management API for real data."
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error analyzing Azure costs");
            return ToJson(new { success = false, error = $"Failed to analyze costs: {ex.Message}" });
        }
    }
}
