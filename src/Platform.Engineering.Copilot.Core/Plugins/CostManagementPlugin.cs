using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for Azure cost management and optimization
/// </summary>
public class CostManagementPlugin : BaseSupervisorPlugin
{
    private readonly IMcpToolHandler _costToolHandler;

    public CostManagementPlugin(
        IMcpToolHandler costToolHandler,
        ILogger<CostManagementPlugin> logger,
        Kernel kernel) : base(logger, kernel)
    {
        _costToolHandler = costToolHandler ?? throw new ArgumentNullException(nameof(costToolHandler));
    }

    [KernelFunction("analyze_azure_costs")]
    [Description("Analyze Azure spending and cost trends. Provides detailed breakdown by resource, service, location, and time period. Shows cost trends, anomalies, and forecasts. Use when user asks to: analyze costs, check spending, review bills, show cost breakdown, or understand Azure charges.")]
    public async Task<string> AnalyzeAzureCostsAsync(
        [Description("Azure subscription ID to analyze costs for")] string subscriptionId,
        [Description("Time period for analysis (e.g., 'last month', 'last 90 days', 'this year'). Optional - defaults to last 30 days.")] string? timePeriod = null,
        [Description("Grouping dimension (e.g., 'resource', 'service', 'location', 'resource_group'). Optional - defaults to service.")] string? groupBy = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Analyze costs for subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(timePeriod))
                query += $" for {timePeriod}";
            if (!string.IsNullOrEmpty(groupBy))
                query += $" grouped by {groupBy}";

            var toolCall = new McpToolCall
            {
                Name = "analyze_azure_costs",
                Arguments = new Dictionary<string, object?>
                {
                    ["subscriptionId"] = subscriptionId,
                    ["time_period"] = timePeriod,
                    ["group_by"] = groupBy
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _costToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("analyze Azure costs", ex);
        }
    }

    [KernelFunction("optimize_costs")]
    [Description("Get cost optimization recommendations and savings opportunities. Identifies underutilized resources, right-sizing opportunities, reserved instances, and potential savings. Use when user wants to: reduce costs, optimize spending, save money, find cost savings, or get cost recommendations.")]
    public async Task<string> OptimizeCostsAsync(
        [Description("Azure subscription ID to optimize")] string subscriptionId,
        [Description("Resource group to focus optimization on. Optional - analyzes entire subscription if not specified.")] string? resourceGroup = null,
        [Description("Minimum potential savings threshold (e.g., '$100', '10%'). Optional - shows all recommendations if not specified.")] string? minSavings = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Provide cost optimization recommendations for subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(resourceGroup))
                query += $" in resource group {resourceGroup}";
            if (!string.IsNullOrEmpty(minSavings))
                query += $" with minimum savings of {minSavings}";

            var toolCall = new McpToolCall
            {
                Name = "optimize_costs",
                Arguments = new Dictionary<string, object?>
                {
                    ["subscriptionId"] = subscriptionId,
                    ["resource_group"] = resourceGroup,
                    ["min_savings"] = minSavings
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _costToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("optimize costs", ex);
        }
    }

    [KernelFunction("configure_budget_alerts")]
    [Description("Set up budget alerts and spending notifications. Creates budgets with customizable thresholds and alert conditions. Sends notifications when spending exceeds limits. Use when user wants to: set budget, create spending alert, monitor costs, or prevent overspending.")]
    public async Task<string> ConfigureBudgetAlertsAsync(
        [Description("Azure subscription ID to configure budget for")] string subscriptionId,
        [Description("Budget amount (e.g., '1000', '$5000'). Required to set up budget.")] string budgetAmount,
        [Description("Alert thresholds as percentages (e.g., '80,90,100' means alert at 80%, 90%, and 100% of budget). Optional - defaults to 80,90,100.")] string? alertThresholds = null,
        [Description("Email address or webhook URL for notifications. Optional - uses default notification settings if not specified.")] string? notificationTarget = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Configure budget of {budgetAmount} for subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(alertThresholds))
                query += $" with alerts at {alertThresholds}";
            if (!string.IsNullOrEmpty(notificationTarget))
                query += $" sending notifications to {notificationTarget}";

            var toolCall = new McpToolCall
            {
                Name = "configure_budget_alerts",
                Arguments = new Dictionary<string, object?>
                {
                    ["subscriptionId"] = subscriptionId,
                    ["budget_amount"] = budgetAmount,
                    ["alert_thresholds"] = alertThresholds,
                    ["notification_target"] = notificationTarget
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _costToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("configure budget alerts", ex);
        }
    }

    [KernelFunction("forecast_costs")]
    [Description("Forecast future Azure spending based on historical trends and planned changes. Projects costs for upcoming periods and helps with budget planning. Use when user wants to: forecast costs, predict spending, estimate future costs, or plan budget.")]
    public async Task<string> ForecastCostsAsync(
        [Description("Azure subscription ID to forecast costs for")] string subscriptionId,
        [Description("Forecast period (e.g., 'next month', 'next quarter', 'next 6 months'). Optional - defaults to next month.")] string? forecastPeriod = null,
        [Description("Planned changes to consider (e.g., 'adding 3 VMs', 'increasing storage by 50%'). Optional - forecasts based on current trend if not specified.")] string? plannedChanges = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Forecast costs for subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(forecastPeriod))
                query += $" for {forecastPeriod}";
            if (!string.IsNullOrEmpty(plannedChanges))
                query += $" considering {plannedChanges}";

            var toolCall = new McpToolCall
            {
                Name = "analyze_azure_costs",
                Arguments = new Dictionary<string, object?>
                {
                    ["subscriptionId"] = subscriptionId,
                    ["forecast_period"] = forecastPeriod,
                    ["planned_changes"] = plannedChanges,
                    ["operation"] = "forecast"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _costToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("forecast costs", ex);
        }
    }

    [KernelFunction("export_cost_data")]
    [Description("Export detailed cost and usage data for analysis and reporting. Generates exports in various formats for external analysis, chargeback, or integration with financial systems. Use when user needs to: export costs, download usage data, generate cost report, or extract billing data.")]
    public async Task<string> ExportCostDataAsync(
        [Description("Azure subscription ID to export data from")] string subscriptionId,
        [Description("Export format (e.g., 'CSV', 'Excel', 'JSON', 'Parquet'). Optional - defaults to CSV.")] string? format = null,
        [Description("Time range for export (e.g., 'last month', 'last quarter', 'this year'). Optional - defaults to last 30 days.")] string? timeRange = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"Export cost data for subscription {subscriptionId}";
            if (!string.IsNullOrEmpty(format))
                query += $" in {format} format";
            if (!string.IsNullOrEmpty(timeRange))
                query += $" for {timeRange}";

            var toolCall = new McpToolCall
            {
                Name = "analyze_azure_costs",
                Arguments = new Dictionary<string, object?>
                {
                    ["subscriptionId"] = subscriptionId,
                    ["format"] = format,
                    ["time_range"] = timeRange,
                    ["operation"] = "export"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _costToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("export cost data", ex);
        }
    }
}
