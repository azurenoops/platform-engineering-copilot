using System;
using System.Collections.Generic;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Models.CostOptimization.Analysis
{
    public class CostOptimizationRecommendation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public OptimizationType Type { get; set; }
        public OptimizationPriority Priority { get; set; }
        public decimal CurrentMonthlyCost { get; set; }
        public decimal EstimatedMonthlySavings { get; set; }
        public decimal EstimatedSavingsPercentage => CurrentMonthlyCost > 0 
            ? (EstimatedMonthlySavings / CurrentMonthlyCost) * 100 
            : 0;
        public string Description { get; set; } = string.Empty;
        public List<OptimizationAction> Actions { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public OptimizationComplexity Complexity { get; set; }
        public List<string> Tags { get; set; } = new();
        
        // Additional properties from detailed version
        public string Title { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public OptimizationCategory Category { get; set; }
        public string RecommendationId { get; set; } = Guid.NewGuid().ToString();
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public decimal PotentialMonthlySavings { get; set; }
        public decimal PotentialAnnualSavings { get; set; }
        public object? ScheduleDetails { get; set; }
        public OptimizationRisk Risk { get; set; }
        public List<string> AffectedResources { get; set; } = new();
    }

    public class OptimizationAction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; } = string.Empty;
        public ActionType Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool IsAutomatable { get; set; }
        public string? AutomationScript { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        
        // Additional properties from detailed version
        public string ActionType { get; set; } = string.Empty;
        public bool Automated { get; set; }
        public string EstimatedDuration { get; set; } = string.Empty;
        public List<string> Resources { get; set; } = new();
    }

    public class ResourceUsagePattern
    {
        public string ResourceId { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public List<UsageDataPoint> DataPoints { get; set; } = new();
        public double AverageUsage { get; set; }
        public double PeakUsage { get; set; }
        public double MinUsage { get; set; }
        public double StandardDeviation { get; set; }
        public UsagePattern Pattern { get; set; }
        public Dictionary<string, double> TimeBasedPatterns { get; set; } = new();
    }

    public class UsageDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Unit { get; set; }
    }

    public class CostAnalysisResult
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
        public decimal TotalMonthlyCost { get; set; }
        public decimal PotentialMonthlySavings { get; set; }
        public int TotalRecommendations { get; set; }
        public Dictionary<string, decimal> CostByService { get; set; } = new();
        public Dictionary<string, decimal> CostByResourceGroup { get; set; } = new();
        public List<CostTrend> HistoricalTrends { get; set; } = new();
        public List<CostOptimizationRecommendation> Recommendations { get; set; } = new();
    }

    public class CostTrend
    {
        public DateTime Date { get; set; }
        public decimal Cost { get; set; }
        public decimal DailyCost { get; set; }
        public decimal CumulativeMonthlyCost { get; set; }
        public Dictionary<string, decimal> ServiceCosts { get; set; } = new();
        public Dictionary<string, decimal> ResourceGroupCosts { get; set; } = new();
        public Dictionary<string, decimal> TagBasedCosts { get; set; } = new();
        public int ResourceCount { get; set; }
        public List<string> CostDrivers { get; set; } = new();
        public string? Category { get; set; }
    }

    public enum OptimizationType
    {
        RightSizing,
        UnusedResources,
        ReservedInstances,
        SpotInstances,
        AutoScaling,
        ScheduledShutdown,
        StorageOptimization,
        NetworkOptimization,
        LicenseOptimization,
        TagCompliance
    }

    public enum OptimizationPriority
    {
        Critical,
        High,
        Medium,
        Low
    }

    public enum ActionType
    {
        Resize,
        Delete,
        Stop,
        Start,
        Schedule,
        Purchase,
        Migrate,
        Configure,
        Tag
    }

    public enum UsagePattern
    {
        Steady,
        Periodic,
        Sporadic,
        Growing,
        Declining,
        Seasonal
    }
}