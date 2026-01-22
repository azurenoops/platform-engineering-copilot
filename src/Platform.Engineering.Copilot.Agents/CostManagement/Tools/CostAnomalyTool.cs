using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.CostManagement.State;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Tools;

/// <summary>
/// Tool for detecting cost anomalies and seasonality patterns.
/// Consolidates: detect_cost_seasonality.
/// </summary>
public class CostAnomalyTool : BaseTool
{
    private readonly CostManagementStateAccessors _stateAccessors;
    private readonly ConfigService _configService;

    public override string Name => "detect_cost_anomalies";

    public override string Description =>
        "Detect cost anomalies, unusual spending patterns, and seasonality in Azure costs. " +
        "If subscriptionId is not provided, uses the configured default subscription. " +
        "Identifies spikes, drops, and recurring patterns that may require attention.";

    public CostAnomalyTool(
        ILogger<CostAnomalyTool> logger,
        CostManagementStateAccessors stateAccessors,
        ConfigService configService) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Azure subscription ID to analyze for anomalies. If not provided, uses the configured default subscription.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "lookbackDays",
            description: "Number of days of historical data to analyze (default: 30, max: 90)",
            required: false,
            type: "integer"));

        Parameters.Add(new ToolParameter(
            name: "sensitivityThreshold",
            description: "Anomaly detection sensitivity as percentage deviation (default: 50 for 50%)",
            required: false,
            type: "integer"));

        Parameters.Add(new ToolParameter(
            name: "includeSeasonality",
            description: "Detect seasonality patterns (weekly, monthly, quarterly). Default: true",
            required: false,
            type: "boolean"));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var lookbackDays = GetOptionalInt(arguments, "lookbackDays") ?? 30;
        var sensitivityThreshold = GetOptionalInt(arguments, "sensitivityThreshold") ?? 50;
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

        lookbackDays = Math.Clamp(lookbackDays, 7, 90);

        Logger.LogInformation("Detecting cost anomalies for {SubscriptionId}, {Days} days lookback, threshold: {Threshold}%",
            subscriptionId, lookbackDays, sensitivityThreshold);

        try
        {
            // TODO: Integrate with Azure Cost Management API and Anomaly Detection
            Logger.LogWarning("Cost anomaly detection requires Azure Cost Management API integration. Returning sample data.");

            // Sample historical data analysis
            var averageDailyCost = 81.67m;
            
            var anomalies = new List<CostAnomaly>
            {
                new()
                {
                    AnomalyDate = DateTime.UtcNow.AddDays(-5),
                    Description = "Unusual spike in Virtual Machines costs - 3 new VMs deployed",
                    ExpectedCost = averageDailyCost,
                    ActualCost = 185.00m,
                    CostDifference = 103.33m,
                    PercentageDeviation = 126.5m,
                    Severity = "High",
                    AffectedService = "Virtual Machines",
                    PotentialCause = "New resource deployment"
                },
                new()
                {
                    AnomalyDate = DateTime.UtcNow.AddDays(-12),
                    Description = "Storage costs spike - large data transfer or backup operation",
                    ExpectedCost = 15.00m,
                    ActualCost = 45.00m,
                    CostDifference = 30.00m,
                    PercentageDeviation = 200.0m,
                    Severity = "Medium",
                    AffectedService = "Storage",
                    PotentialCause = "Backup or data migration"
                },
                new()
                {
                    AnomalyDate = DateTime.UtcNow.AddDays(-20),
                    Description = "Cognitive Services usage increase - API calls above baseline",
                    ExpectedCost = 12.00m,
                    ActualCost = 28.00m,
                    CostDifference = 16.00m,
                    PercentageDeviation = 133.3m,
                    Severity = "Low",
                    AffectedService = "Cognitive Services",
                    PotentialCause = "Increased API usage"
                }
            };

            // Filter by threshold
            var filteredAnomalies = anomalies
                .Where(a => a.PercentageDeviation >= sensitivityThreshold)
                .OrderByDescending(a => a.AnomalyDate)
                .ToList();

            // Seasonality patterns
            var seasonalityPatterns = new List<SeasonalityPattern>();
            if (includeSeasonality)
            {
                seasonalityPatterns = new List<SeasonalityPattern>
                {
                    new()
                    {
                        PatternType = "Weekly",
                        Description = "Lower costs on weekends (avg -15%)",
                        Confidence = 0.92m,
                        PeakPeriod = "Tuesday-Thursday",
                        TroughPeriod = "Saturday-Sunday"
                    },
                    new()
                    {
                        PatternType = "Monthly",
                        Description = "Higher costs at month-end (avg +8%)",
                        Confidence = 0.78m,
                        PeakPeriod = "Last week of month",
                        TroughPeriod = "First week of month"
                    }
                };
            }

            await Task.CompletedTask;

            return ToJson(new
            {
                success = true,
                subscriptionId,
                analysisParameters = new
                {
                    lookbackDays,
                    sensitivityThreshold,
                    includeSeasonality
                },
                summary = new
                {
                    averageDailyCost,
                    anomalyCount = filteredAnomalies.Count,
                    highSeverityCount = filteredAnomalies.Count(a => a.Severity == "High"),
                    totalAnomalousCost = filteredAnomalies.Sum(a => a.CostDifference),
                    seasonalityPatternsDetected = seasonalityPatterns.Count
                },
                anomalies = filteredAnomalies.Select(a => new
                {
                    date = a.AnomalyDate.ToString("yyyy-MM-dd"),
                    a.Description,
                    a.ExpectedCost,
                    a.ActualCost,
                    a.CostDifference,
                    percentageDeviation = $"+{a.PercentageDeviation:N1}%",
                    a.Severity,
                    a.AffectedService,
                    a.PotentialCause
                }),
                seasonality = includeSeasonality ? seasonalityPatterns.Select(p => new
                {
                    p.PatternType,
                    p.Description,
                    confidence = $"{p.Confidence * 100:N0}%",
                    p.PeakPeriod,
                    p.TroughPeriod
                }) : null,
                recommendations = new List<string>
                {
                    "Set up budget alerts for early anomaly detection",
                    "Review high-severity anomalies within 24 hours",
                    "Consider resource tagging for better cost attribution"
                },
                note = "This is sample data. Integrate with Azure Cost Management API for real anomaly detection."
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error detecting cost anomalies");
            return ToJson(new { success = false, error = $"Failed to detect anomalies: {ex.Message}" });
        }
    }

    private class CostAnomaly
    {
        public DateTime AnomalyDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal ExpectedCost { get; set; }
        public decimal ActualCost { get; set; }
        public decimal CostDifference { get; set; }
        public decimal PercentageDeviation { get; set; }
        public string Severity { get; set; } = "Medium";
        public string AffectedService { get; set; } = string.Empty;
        public string PotentialCause { get; set; } = string.Empty;
    }

    private class SeasonalityPattern
    {
        public string PatternType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string PeakPeriod { get; set; } = string.Empty;
        public string TroughPeriod { get; set; } = string.Empty;
    }
}
