using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Comprehensive cost monitoring and optimization models for Azure resources
/// </summary>

#region Core Cost Models

/// <summary>
/// Main cost monitoring dashboard containing all cost-related metrics and insights
/// </summary>
public class CostMonitoringDashboard
{
    public CostDashboardMetadata Metadata { get; set; } = new();
    public CostSummary Summary { get; set; } = new();
    public List<CostTrend> Trends { get; set; } = new();
    public List<BudgetAlert> BudgetAlerts { get; set; } = new();
    public List<CostOptimizationRecommendation> Recommendations { get; set; } = new();
    public List<CostAnomaly> Anomalies { get; set; } = new();
    public List<ResourceCostBreakdown> ResourceBreakdown { get; set; } = new();
    public List<ServiceCostBreakdown> ServiceBreakdown { get; set; } = new();
    public CostForecast Forecast { get; set; } = new();
    public List<BudgetStatus> Budgets { get; set; } = new();
    public CostGovernance Governance { get; set; } = new();
}

/// <summary>
/// Dashboard metadata and generation information
/// </summary>
public class CostDashboardMetadata
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "1.0.0";
    public TimeSpan GenerationTime { get; set; }
    public List<string> DataSources { get; set; } = new() { "Azure Cost Management", "Azure Monitor", "Resource Manager" };
    public DateTimeOffset DataPeriodStart { get; set; }
    public DateTimeOffset DataPeriodEnd { get; set; }
    public string Currency { get; set; } = "USD";
    public List<string> SubscriptionsAnalyzed { get; set; } = new();
    public int TotalResourcesAnalyzed { get; set; }
}

/// <summary>
/// High-level cost summary and key metrics
/// </summary>
public class CostSummary
{
    public decimal CurrentMonthSpend { get; set; }
    public decimal PreviousMonthSpend { get; set; }
    public decimal MonthOverMonthChange { get; set; }
    public decimal MonthOverMonthChangePercent { get; set; }
    public decimal YearToDateSpend { get; set; }
    public decimal ProjectedMonthlySpend { get; set; }
    public decimal AverageDailyCost { get; set; }
    public decimal HighestDailyCost { get; set; }
    public DateTime HighestCostDate { get; set; }
    public decimal BudgetUtilization { get; set; }
    public CostTrendDirection TrendDirection { get; set; }
    public int ActiveBudgets { get; set; }
    public int BudgetAlertsTriggered { get; set; }
    public decimal PotentialSavings { get; set; }
    public int OptimizationOpportunities { get; set; }
}

/// <summary>
/// Cost trend data point for time-series analysis
/// </summary>
public class CostTrend
{
    public DateTime Date { get; set; }
    public decimal DailyCost { get; set; }
    public decimal CumulativeMonthlyCost { get; set; }
    public Dictionary<string, decimal> ServiceCosts { get; set; } = new();
    public Dictionary<string, decimal> ResourceGroupCosts { get; set; } = new();
    public Dictionary<string, decimal> TagBasedCosts { get; set; } = new();
    public int ResourceCount { get; set; }
    public List<string> CostDrivers { get; set; } = new(); // Top spending resources/services
}

#endregion

#region Budget Management

/// <summary>
/// Budget definition and tracking
/// </summary>
public class BudgetStatus
{
    public string BudgetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal CurrentSpend { get; set; }
    public decimal RemainingBudget { get; set; }
    public decimal UtilizationPercentage { get; set; }
    public BudgetPeriod Period { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public BudgetScope Scope { get; set; } = new();
    public List<BudgetThreshold> Thresholds { get; set; } = new();
    public BudgetHealthStatus HealthStatus { get; set; }
    public List<string> AlertRecipients { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Budget alert configuration and status
/// </summary>
public class BudgetAlert
{
    public string AlertId { get; set; } = Guid.NewGuid().ToString();
    public string BudgetId { get; set; } = string.Empty;
    public string BudgetName { get; set; } = string.Empty;
    public BudgetAlertType AlertType { get; set; }
    public BudgetAlertSeverity Severity { get; set; }
    public decimal ThresholdPercentage { get; set; }
    public decimal CurrentPercentage { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal CurrentSpend { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public List<string> RecommendedActions { get; set; } = new();
    public string NotificationStatus { get; set; } = string.Empty;
}

/// <summary>
/// Budget threshold configuration
/// </summary>
public class BudgetThreshold
{
    public decimal Percentage { get; set; }
    public BudgetAlertSeverity Severity { get; set; }
    public bool EmailNotification { get; set; }
    public bool SlackNotification { get; set; }
    public bool TeamsNotification { get; set; }
    public List<string> Recipients { get; set; } = new();
    public string CustomMessage { get; set; } = string.Empty;
}

/// <summary>
/// Budget scope definition
/// </summary>
public class BudgetScope
{
    public List<string> SubscriptionIds { get; set; } = new();
    public List<string> ResourceGroupNames { get; set; } = new();
    public List<string> ResourceTypes { get; set; } = new();
    public Dictionary<string, string> Tags { get; set; } = new();
    public List<string> Locations { get; set; } = new();
}

#endregion

#region Cost Optimization

/// <summary>
/// Cost optimization recommendation with detailed analysis
/// </summary>
public class CostOptimizationRecommendation
{
    public string RecommendationId { get; set; } = Guid.NewGuid().ToString();
    public OptimizationType Type { get; set; }
    public OptimizationPriority Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PotentialMonthlySavings { get; set; }
    public decimal PotentialAnnualSavings { get; set; }
    public decimal ImplementationCost { get; set; }
    public TimeSpan PaybackPeriod { get; set; }
    public OptimizationComplexity ImplementationComplexity { get; set; }
    public OptimizationRisk Risk { get; set; }
    public List<string> AffectedResources { get; set; } = new();
    public List<OptimizationAction> RecommendedActions { get; set; } = new();
    public OptimizationCategory Category { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string Evidence { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool IsAutomatable { get; set; }
    public string AutomationScript { get; set; } = string.Empty;
}

/// <summary>
/// Specific optimization action
/// </summary>
public class OptimizationAction
{
    public string ActionId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public decimal EstimatedSavings { get; set; }
    public TimeSpan EstimatedImplementationTime { get; set; }
    public bool RequiresDowntime { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public List<string> RisksAndConsiderations { get; set; } = new();
    public string DocumentationLink { get; set; } = string.Empty;
}

/// <summary>
/// Resource rightsizing recommendation
/// </summary>
public class RightsizingRecommendation
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string CurrentSku { get; set; } = string.Empty;
    public string RecommendedSku { get; set; } = string.Empty;
    public decimal CurrentMonthlyCost { get; set; }
    public decimal RecommendedMonthlyCost { get; set; }
    public decimal MonthlySavings { get; set; }
    public UtilizationMetrics CurrentUtilization { get; set; } = new();
    public UtilizationMetrics ProjectedUtilization { get; set; } = new();
    public RightsizingConfidence Confidence { get; set; }
    public RightsizingReason Reason { get; set; }
    public List<string> SupportingEvidence { get; set; } = new();
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
}

/// <summary>
/// Resource utilization metrics
/// </summary>
public class UtilizationMetrics
{
    public double AverageCpuUtilization { get; set; }
    public double MaxCpuUtilization { get; set; }
    public double AverageMemoryUtilization { get; set; }
    public double MaxMemoryUtilization { get; set; }
    public double AverageNetworkUtilization { get; set; }
    public double AverageStorageUtilization { get; set; }
    public double AverageIopsUtilization { get; set; }
    public int SampleCount { get; set; }
    public TimeSpan ObservationPeriod { get; set; }
}

#endregion

#region Cost Anomaly Detection

/// <summary>
/// Cost anomaly detection and analysis
/// </summary>
public class CostAnomaly
{
    public string AnomalyId { get; set; } = Guid.NewGuid().ToString();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime AnomalyDate { get; set; }
    public AnomalySeverity Severity { get; set; }
    public AnomalyType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal ExpectedCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal CostDifference { get; set; }
    public decimal PercentageDeviation { get; set; }
    public double AnomalyScore { get; set; } // ML confidence score
    public List<string> AffectedResources { get; set; } = new();
    public List<string> AffectedServices { get; set; } = new();
    public Dictionary<string, object> AnomalyContext { get; set; } = new();
    public List<string> PossibleCauses { get; set; } = new();
    public List<string> RecommendedInvestigations { get; set; } = new();
    public AnomalyStatus Status { get; set; } = AnomalyStatus.Open;
    public string Resolution { get; set; } = string.Empty;
    public DateTime? ResolvedAt { get; set; }
}

#endregion

#region Cost Breakdown and Analysis

/// <summary>
/// Resource-level cost breakdown
/// </summary>
public class ResourceCostBreakdown
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal DailyCost { get; set; }
    public decimal MonthlyCost { get; set; }
    public decimal YearToDateCost { get; set; }
    public decimal CostTrend { get; set; } // % change from previous period
    public Dictionary<string, string> Tags { get; set; } = new();
    public Dictionary<string, decimal> MeterCosts { get; set; } = new(); // Cost by meter/usage type
    public CostEfficiencyRating Efficiency { get; set; }
    public List<string> CostOptimizationFlags { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Service-level cost breakdown
/// </summary>
public class ServiceCostBreakdown
{
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceCategory { get; set; } = string.Empty;
    public decimal DailyCost { get; set; }
    public decimal MonthlyCost { get; set; }
    public decimal YearToDateCost { get; set; }
    public decimal PercentageOfTotal { get; set; }
    public decimal CostTrend { get; set; }
    public int ResourceCount { get; set; }
    public decimal AverageCostPerResource { get; set; }
    public List<ResourceCostBreakdown> TopResources { get; set; } = new();
    public List<string> CostDrivers { get; set; } = new(); // Main cost contributors
    public Dictionary<string, decimal> LocationBreakdown { get; set; } = new();
}

#endregion

#region Forecasting and Governance

/// <summary>
/// Cost forecasting and projections
/// </summary>
public class CostForecast
{
    public DateTime ForecastDate { get; set; } = DateTime.UtcNow;
    public ForecastMethod Method { get; set; }
    public double ConfidenceLevel { get; set; }
    public List<ForecastDataPoint> Projections { get; set; } = new();
    public decimal ProjectedMonthEndCost { get; set; }
    public decimal ProjectedQuarterEndCost { get; set; }
    public decimal ProjectedYearEndCost { get; set; }
    public ForecastAccuracy HistoricalAccuracy { get; set; } = new();
    public List<ForecastAssumption> Assumptions { get; set; } = new();
    public List<ForecastRisk> Risks { get; set; } = new();
}

/// <summary>
/// Individual forecast data point
/// </summary>
public class ForecastDataPoint
{
    public DateTime Date { get; set; }
    public decimal ForecastedCost { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
    public double Confidence { get; set; }
    public Dictionary<string, decimal> ServiceBreakdown { get; set; } = new();
}

/// <summary>
/// Cost governance and policies
/// </summary>
public class CostGovernance
{
    public List<CostPolicy> Policies { get; set; } = new();
    public List<CostAllocation> Allocations { get; set; } = new();
    public ChargebackStatus Chargeback { get; set; } = new();
    public List<CostCenter> CostCenters { get; set; } = new();
    public TaxonomyCompliance TagCompliance { get; set; } = new();
    public List<CostApprovalWorkflow> ApprovalWorkflows { get; set; } = new();
}

/// <summary>
/// Cost policy definition and compliance
/// </summary>
public class CostPolicy
{
    public string PolicyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> ApplicableSubscriptions { get; set; } = new();
    public List<string> ApplicableResourceTypes { get; set; } = new();
    public List<PolicyRule> Rules { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public PolicyEnforcement Enforcement { get; set; }
    public int ViolationCount { get; set; }
    public DateTime LastEvaluated { get; set; }
}

#endregion

#region Enumerations

public enum CostTrendDirection
{
    Increasing,
    Decreasing,
    Stable,
    Volatile
}

public enum BudgetPeriod
{
    Monthly,
    Quarterly,
    Annually,
    Custom
}

public enum BudgetHealthStatus
{
    Healthy,
    Warning,
    Critical,
    Exceeded
}

public enum BudgetAlertType
{
    Threshold,
    Forecast,
    Anomaly
}

public enum BudgetAlertSeverity
{
    Info,
    Warning,
    Critical
}

public enum OptimizationType
{
    Rightsizing,
    ReservedInstances,
    StorageOptimization,
    NetworkOptimization,
    UnusedResources,
    SchedulingAutomation,
    LocationOptimization,
    ServiceTierOptimization
}

public enum OptimizationPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum OptimizationComplexity
{
    Simple,
    Moderate,
    Complex,
    Expert
}

public enum OptimizationCategory
{
    Compute,
    Storage,
    Network,
    Database,
    Analytics,
    Security,
    Monitoring,
    Integration
}

public enum OptimizationRisk
{
    Low,
    Medium,
    High
}

public enum RightsizingConfidence
{
    Low,
    Medium,
    High,
    VeryHigh
}

public enum RightsizingReason
{
    Underutilized,
    Overutilized,
    WrongSkuFamily,
    LocationMismatch,
    UsagePatternChange
}

public enum AnomalySeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum AnomalyType
{
    SpikeCost,
    UnexpectedUsage,
    ServiceCostIncrease,
    ResourceCostIncrease,
    BillingAnomaly,
    UsagePatternChange
}

public enum AnomalyStatus
{
    Open,
    Investigating,
    Resolved,
    False_Positive
}

public enum CostEfficiencyRating
{
    Excellent,
    Good,
    Fair,
    Poor,
    Critical
}

public enum ForecastMethod
{
    LinearRegression,
    ExponentialSmoothing,
    MachineLearning,
    Historical_Average,
    Seasonal_Decomposition
}

public enum PolicyEnforcement
{
    Advisory,
    Preventive,
    Detective
}

#endregion

#region Supporting Classes

public class ForecastAccuracy
{
    public double MeanAbsolutePercentageError { get; set; }
    public double RootMeanSquareError { get; set; }
    public int SampleSize { get; set; }
    public DateTime LastEvaluated { get; set; }
}

public class ForecastAssumption
{
    public string Description { get; set; } = string.Empty;
    public double Impact { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class ForecastRisk
{
    public string Risk { get; set; } = string.Empty;
    public double Probability { get; set; }
    public decimal PotentialImpact { get; set; }
    public string Mitigation { get; set; } = string.Empty;
}

public class CostAllocation
{
    public string AllocationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, decimal> AllocationPercentages { get; set; } = new();
    public List<string> ApplicableResources { get; set; } = new();
}

public class ChargebackStatus
{
    public bool IsEnabled { get; set; }
    public string Method { get; set; } = string.Empty;
    public DateTime LastProcessed { get; set; }
    public int PendingChargebacks { get; set; }
}

public class CostCenter
{
    public string CostCenterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public decimal AllocatedBudget { get; set; }
    public decimal CurrentSpend { get; set; }
    public List<string> AssignedResources { get; set; } = new();
}

public class TaxonomyCompliance
{
    public decimal OverallCompliance { get; set; }
    public Dictionary<string, decimal> TagCompliance { get; set; } = new();
    public List<string> NonCompliantResources { get; set; } = new();
}

public class CostApprovalWorkflow
{
    public string WorkflowId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal ThresholdAmount { get; set; }
    public List<string> Approvers { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class PolicyRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

#endregion