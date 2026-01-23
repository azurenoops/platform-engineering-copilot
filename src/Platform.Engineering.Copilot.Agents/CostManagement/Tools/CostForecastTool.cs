using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.CostManagement.State;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// Tool for forecasting Azure costs with seasonality and growth projections.
/// Consolidates: forecast_costs_with_seasonality, forecast_with_growth_projection.
/// </summary>
public class CostForecastTool : BaseTool
{
    private readonly CostManagementStateAccessors _stateAccessors;
    private readonly ConfigService _configService;

    public override string Name => "forecast_costs";

    public override string Description =>
        "Forecast future Azure costs based on historical trends, seasonality patterns, and growth projections. " +
        "If subscriptionId is not provided, uses the configured default subscription. " +
        "Includes confidence intervals and trend analysis.";

    public CostForecastTool(
        ILogger<CostForecastTool> logger,
        CostManagementStateAccessors stateAccessors,
        ConfigService configService) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Azure subscription ID to forecast costs for. If not provided, uses the configured default subscription.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "forecastDays",
            description: "Number of days to forecast (default: 30, max: 365)",
            required: false,
            type: "integer"));

        Parameters.Add(new ToolParameter(
            name: "growthRate",
            description: "Expected growth rate percentage (e.g., 10 for 10% growth). Default: 0 (based on historical trend)",
            required: false,
            type: "number"));

        Parameters.Add(new ToolParameter(
            name: "includeSeasonality",
            description: "Account for seasonal patterns in forecast (default: true)",
            required: false,
            type: "boolean"));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var forecastDays = GetOptionalInt(arguments, "forecastDays") ?? 30;
        var growthRate = GetOptionalDecimal(arguments, "growthRate") ?? 0;
        var includeSeasonality = GetOptionalBool(arguments, "includeSeasonality", defaultValue: true);

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

        forecastDays = Math.Clamp(forecastDays, 1, 365);

        Logger.LogInformation("Forecasting costs for {SubscriptionId}, {Days} days, growth: {GrowthRate}%",
            subscriptionId, forecastDays, growthRate);

        try
        {
            // TODO: Integrate with Azure Cost Management Forecast API
            Logger.LogWarning("Cost forecasting requires Azure Cost Management API integration. Returning sample data.");

            // Sample historical data (would come from API)
            var currentMonthlyCost = 2450.00m;
            var dailyBaseCost = currentMonthlyCost / 30;
            
            // Calculate projected costs
            var projectedDailyCost = dailyBaseCost * (1 + growthRate / 100);
            var projectedTotalCost = projectedDailyCost * forecastDays;
            
            // Seasonality factors (sample)
            var seasonalityDetected = includeSeasonality;
            var seasonalityFactor = 1.0m;
            if (includeSeasonality)
            {
                // Simulate end-of-month/quarter effects
                var forecastEndDate = DateTime.UtcNow.AddDays(forecastDays);
                if (forecastEndDate.Month == 12) seasonalityFactor = 1.15m; // Year-end
                else if (forecastEndDate.Month == 3 || forecastEndDate.Month == 6 || forecastEndDate.Month == 9)
                    seasonalityFactor = 1.08m; // Quarter-end
                
                projectedTotalCost *= seasonalityFactor;
                projectedDailyCost *= seasonalityFactor;
            }

            // Historical trend analysis
            var trendDirection = growthRate > 5 ? "Increasing" : growthRate < -5 ? "Decreasing" : "Stable";
            
            // Confidence intervals
            var confidenceLevel = 0.85m; // 85% confidence
            var variance = projectedTotalCost * 0.15m; // 15% variance
            var lowerBound = projectedTotalCost - variance;
            var upperBound = projectedTotalCost + variance;

            // Generate daily forecast data
            var dailyForecast = new List<DailyForecastPoint>();
            var runningTotal = 0m;
            for (var day = 1; day <= forecastDays; day++)
            {
                var date = DateTime.UtcNow.AddDays(day);
                var dayCost = projectedDailyCost;
                
                // Weekend adjustment
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    dayCost *= 0.85m;
                
                runningTotal += dayCost;
                dailyForecast.Add(new DailyForecastPoint
                {
                    Date = date,
                    DailyCost = Math.Round(dayCost, 2),
                    CumulativeCost = Math.Round(runningTotal, 2)
                });
            }

            var summary = new ForecastResultSummary
            {
                SubscriptionId = subscriptionId,
                ForecastDays = forecastDays,
                ProjectedTotalCost = Math.Round(projectedTotalCost, 2),
                ProjectedDailyCost = Math.Round(projectedDailyCost, 2),
                ConfidenceLevel = confidenceLevel,
                TrendDirection = trendDirection,
                TrendPercentage = growthRate,
                SeasonalityDetected = seasonalityDetected,
                ForecastGeneratedAt = DateTime.UtcNow
            };

            await Task.CompletedTask;

            return ToJson(new
            {
                success = true,
                subscriptionId,
                forecast = new
                {
                    forecastDays,
                    summary.ProjectedTotalCost,
                    summary.ProjectedDailyCost,
                    lowerBound = Math.Round(lowerBound, 2),
                    upperBound = Math.Round(upperBound, 2),
                    confidenceLevel = $"{confidenceLevel * 100}%",
                    summary.TrendDirection,
                    trendPercentage = growthRate,
                    seasonalityDetected,
                    seasonalityFactor = includeSeasonality ? seasonalityFactor : 1.0m
                },
                comparison = new
                {
                    currentMonthlySpend = currentMonthlyCost,
                    projectedMonthlySpend = Math.Round(projectedDailyCost * 30, 2),
                    changeAmount = Math.Round(projectedDailyCost * 30 - currentMonthlyCost, 2),
                    changePercentage = Math.Round((projectedDailyCost * 30 - currentMonthlyCost) / currentMonthlyCost * 100, 1)
                },
                dailyForecast = dailyForecast.Take(14).Select(f => new // First 2 weeks
                {
                    date = f.Date.ToString("yyyy-MM-dd"),
                    f.DailyCost,
                    f.CumulativeCost
                }),
                note = "This is sample data. Integrate with Azure Cost Management Forecast API for real projections."
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error forecasting costs");
            return ToJson(new { success = false, error = $"Failed to forecast costs: {ex.Message}" });
        }
    }

    private class DailyForecastPoint
    {
        public DateTime Date { get; set; }
        public decimal DailyCost { get; set; }
        public decimal CumulativeCost { get; set; }
    }
}
