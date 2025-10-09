using System;
using System.Collections.Generic;

namespace Platform.Engineering.Copilot.Core.Models.PredictiveScaling
{
    public class PredictiveScalingConfiguration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public ScalingStrategy Strategy { get; set; }
        public bool IsEnabled { get; set; }
        public ScalingMetrics Metrics { get; set; } = new();
        public ScalingThresholds Thresholds { get; set; } = new();
        public ScalingConstraints Constraints { get; set; } = new();
        public PredictionSettings PredictionSettings { get; set; } = new();
        public List<ScalingSchedule> Schedules { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAt { get; set; }
    }

    public class ScalingMetrics
    {
        public List<string> PrimaryMetrics { get; set; } = new();
        public List<string> SecondaryMetrics { get; set; } = new();
        public int LookbackPeriodDays { get; set; } = 30;
        public int PredictionHorizonHours { get; set; } = 24;
        public AggregationType AggregationType { get; set; } = AggregationType.Average;
        public int AggregationWindowMinutes { get; set; } = 5;
    }

    public class ScalingThresholds
    {
        public double ScaleUpThreshold { get; set; } = 70;
        public double ScaleDownThreshold { get; set; } = 30;
        public double EmergencyScaleThreshold { get; set; } = 90;
        public int CooldownMinutes { get; set; } = 10;
        public int StabilizationWindowMinutes { get; set; } = 5;
    }

    public class ScalingConstraints
    {
        public int MinimumInstances { get; set; } = 1;
        public int MaximumInstances { get; set; } = 10;
        public int ScaleUpStepSize { get; set; } = 1;
        public int ScaleDownStepSize { get; set; } = 1;
        public int MaxScaleUpPerHour { get; set; } = 5;
        public int MaxScaleDownPerHour { get; set; } = 3;
        public List<string> BlockedTimeWindows { get; set; } = new();
    }

    public class PredictionSettings
    {
        public PredictionModel Model { get; set; } = PredictionModel.ARIMA;
        public double ConfidenceLevel { get; set; } = 0.95;
        public bool UseSeasonalDecomposition { get; set; } = true;
        public int SeasonalityPeriodDays { get; set; } = 7;
        public bool UseAnomalyDetection { get; set; } = true;
        public double AnomalyThreshold { get; set; } = 3.0; // Standard deviations
    }

    public class ScalingSchedule
    {
        public string Name { get; set; } = string.Empty;
        public ScheduleType Type { get; set; }
        public string CronExpression { get; set; } = string.Empty;
        public int TargetInstances { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime? NextRun { get; set; }
    }

    public class PredictiveScalingRecommendation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ResourceId { get; set; } = string.Empty;
        public DateTime PredictionTime { get; set; }
        public ScalingAction RecommendedAction { get; set; }
        public int CurrentInstances { get; set; }
        public int RecommendedInstances { get; set; }
        public double PredictedLoad { get; set; }
        public double ConfidenceScore { get; set; }
        public List<MetricPrediction> MetricPredictions { get; set; } = new();
        public string Reasoning { get; set; } = string.Empty;
        public DateTime? ExecutionTime { get; set; }
    }

    public class MetricPrediction
    {
        public string MetricName { get; set; } = string.Empty;
        public List<PredictionPoint> Predictions { get; set; } = new();
        public double MeanAbsoluteError { get; set; }
        public double RootMeanSquaredError { get; set; }
    }

    public class PredictionPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public double LowerBound { get; set; }
        public double UpperBound { get; set; }
    }

    public class ScalingEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ResourceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public ScalingAction Action { get; set; }
        public int FromInstances { get; set; }
        public int ToInstances { get; set; }
        public string Trigger { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, double> MetricsAtTime { get; set; } = new();
    }

    public class ScalingPerformanceMetrics
    {
        public string ResourceId { get; set; } = string.Empty;
        public DateTime AnalysisStartDate { get; set; }
        public DateTime AnalysisEndDate { get; set; }
        public int TotalScalingEvents { get; set; }
        public int SuccessfulScalingEvents { get; set; }
        public double AverageResponseTime { get; set; }
        public double CostSavingsPercentage { get; set; }
        public double OverProvisioningPercentage { get; set; }
        public double UnderProvisioningPercentage { get; set; }
        public Dictionary<string, double> MetricAccuracy { get; set; } = new();
    }

    public enum ScalingStrategy
    {
        Conservative,
        Balanced,
        Aggressive,
        CostOptimized,
        PerformanceOptimized,
        Custom
    }

    public enum PredictionModel
    {
        ARIMA,
        LSTM,
        Prophet,
        ExponentialSmoothing,
        RandomForest,
        Ensemble
    }

    public enum ScalingAction
    {
        None,
        ScaleUp,
        ScaleDown,
        EmergencyScale,
        Scheduled
    }

    public enum ScheduleType
    {
        Daily,
        Weekly,
        Monthly,
        Custom
    }

    public enum AggregationType
    {
        Average,
        Maximum,
        Minimum,
        Sum,
        Percentile
    }
}