using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.PredictiveScaling;
using Platform.Engineering.Copilot.Core.Models.CostOptimization;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services.Azure.Cost;
using AzureResource = Platform.Engineering.Copilot.Core.Models.AzureResource;

namespace Platform.Engineering.Copilot.Core.Services.Infrastructure
{
    public interface IPredictiveScalingEngine
    {
        Task<PredictiveScalingRecommendation> GeneratePredictionAsync(string resourceId, DateTime targetTime);
        Task<List<MetricPrediction>> PredictMetricsAsync(string resourceId, List<string> metrics, int horizonHours);
        Task<bool> ApplyScalingRecommendationAsync(PredictiveScalingRecommendation recommendation);
        Task<ScalingPerformanceMetrics> AnalyzeScalingPerformanceAsync(string resourceId, DateTime startDate, DateTime endDate);
        Task<PredictiveScalingConfiguration> OptimizeScalingConfigurationAsync(string resourceId);
    }

    public class PredictiveScalingEngine : IPredictiveScalingEngine
    {
        private readonly ILogger<PredictiveScalingEngine> _logger;
        private readonly IAzureMetricsService _metricsService;
        private readonly IAzureResourceService _resourceService;
        private readonly ICostOptimizationEngine _costOptimizationEngine;

        public PredictiveScalingEngine(
            ILogger<PredictiveScalingEngine> logger,
            IAzureMetricsService metricsService,
            IAzureResourceService resourceService,
            ICostOptimizationEngine costOptimizationEngine)
        {
            _logger = logger;
            _metricsService = metricsService;
            _resourceService = resourceService;
            _costOptimizationEngine = costOptimizationEngine;
        }

        public async Task<PredictiveScalingRecommendation> GeneratePredictionAsync(string resourceId, DateTime targetTime)
        {
            _logger.LogInformation("Generating scaling prediction for resource {ResourceId} at {TargetTime}", 
                resourceId, targetTime);

            var recommendation = new PredictiveScalingRecommendation
            {
                ResourceId = resourceId,
                PredictionTime = targetTime
            };

            try
            {
                // Get resource details
                var resource = await _resourceService.GetResourceAsync(resourceId);
                if (resource == null)
                {
                    throw new InvalidOperationException($"Resource {resourceId} not found");
                }

                // Get current instance count
                recommendation.CurrentInstances = await GetCurrentInstanceCountAsync(resource);

                // Get historical metrics
                var metrics = await GetRelevantMetricsAsync(resource);
                var historicalData = new Dictionary<string, List<MetricDataPoint>>();

                foreach (var metric in metrics)
                {
                    var data = await _metricsService.GetMetricsAsync(
                        resourceId, 
                        metric, 
                        DateTime.UtcNow.AddDays(-30), 
                        DateTime.UtcNow);
                    historicalData[metric] = data;
                }

                // Perform predictions
                var predictions = await PredictMetricsAsync(resourceId, metrics, 
                    (int)(targetTime - DateTime.UtcNow).TotalHours);
                recommendation.MetricPredictions = predictions;

                // Analyze predictions and determine scaling action
                var analysis = AnalyzePredictions(predictions, recommendation.CurrentInstances);
                recommendation.RecommendedAction = analysis.Action;
                recommendation.RecommendedInstances = analysis.RecommendedInstances;
                recommendation.PredictedLoad = analysis.PredictedLoad;
                recommendation.ConfidenceScore = analysis.Confidence;
                recommendation.Reasoning = analysis.Reasoning;

                _logger.LogInformation(
                    "Scaling prediction completed. Action: {Action}, From: {Current}, To: {Recommended}", 
                    recommendation.RecommendedAction, 
                    recommendation.CurrentInstances, 
                    recommendation.RecommendedInstances);

                return recommendation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating scaling prediction for resource {ResourceId}", resourceId);
                throw;
            }
        }

        public async Task<List<MetricPrediction>> PredictMetricsAsync(string resourceId, List<string> metrics, int horizonHours)
        {
            var predictions = new List<MetricPrediction>();

            foreach (var metric in metrics)
            {
                try
                {
                    // Get historical data
                    var historicalData = await _metricsService.GetMetricsAsync(
                        resourceId, 
                        metric, 
                        DateTime.UtcNow.AddDays(-30), 
                        DateTime.UtcNow);

                    if (!historicalData.Any())
                    {
                        _logger.LogWarning("No historical data found for metric {Metric}", metric);
                        continue;
                    }

                    // Perform time series prediction
                    var prediction = await PredictTimeSeriesAsync(metric, historicalData, horizonHours);
                    predictions.Add(prediction);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error predicting metric {Metric} for resource {ResourceId}", 
                        metric, resourceId);
                }
            }

            return predictions;
        }

        public async Task<bool> ApplyScalingRecommendationAsync(PredictiveScalingRecommendation recommendation)
        {
            try
            {
                _logger.LogInformation(
                    "Applying scaling recommendation {RecommendationId} for resource {ResourceId}", 
                    recommendation.Id, 
                    recommendation.ResourceId);

                if (recommendation.RecommendedAction == ScalingAction.None)
                {
                    _logger.LogInformation("No scaling action required");
                    return true;
                }

                // Get resource
                var resource = await _resourceService.GetResourceAsync(recommendation.ResourceId);
                if (resource == null)
                {
                    _logger.LogError("Resource {ResourceId} not found", recommendation.ResourceId);
                    return false;
                }

                // Apply scaling based on resource type
                bool success = resource.Type.ToLower() switch
                {
                    "microsoft.compute/virtualmachinescalesets" => 
                        await ScaleVmssAsync(resource, recommendation.RecommendedInstances),
                    "microsoft.web/serverfarms" => 
                        await ScaleAppServicePlanAsync(resource, recommendation.RecommendedInstances),
                    "microsoft.containerservice/managedclusters" => 
                        await ScaleAksClusterAsync(resource, recommendation.RecommendedInstances),
                    _ => false
                };

                if (success)
                {
                    recommendation.ExecutionTime = DateTime.UtcNow;
                    
                    // Log scaling event
                    await LogScalingEventAsync(new ScalingEvent
                    {
                        ResourceId = recommendation.ResourceId,
                        Action = recommendation.RecommendedAction,
                        FromInstances = recommendation.CurrentInstances,
                        ToInstances = recommendation.RecommendedInstances,
                        Trigger = "Predictive Scaling",
                        Success = true
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying scaling recommendation {RecommendationId}", 
                    recommendation.Id);
                
                await LogScalingEventAsync(new ScalingEvent
                {
                    ResourceId = recommendation.ResourceId,
                    Action = recommendation.RecommendedAction,
                    FromInstances = recommendation.CurrentInstances,
                    ToInstances = recommendation.RecommendedInstances,
                    Trigger = "Predictive Scaling",
                    Success = false,
                    ErrorMessage = ex.Message
                });
                
                return false;
            }
        }

        public async Task<ScalingPerformanceMetrics> AnalyzeScalingPerformanceAsync(
            string resourceId, 
            DateTime startDate, 
            DateTime endDate)
        {
            var metrics = new ScalingPerformanceMetrics
            {
                ResourceId = resourceId,
                AnalysisStartDate = startDate,
                AnalysisEndDate = endDate
            };

            try
            {
                // Get scaling events for the period
                var scalingEvents = await GetScalingEventsAsync(resourceId, startDate, endDate);
                metrics.TotalScalingEvents = scalingEvents.Count;
                metrics.SuccessfulScalingEvents = scalingEvents.Count(e => e.Success);

                // Calculate average response time
                var responseTimes = scalingEvents
                    .Where(e => e.Success)
                    .Select(e => (e.Timestamp - e.Timestamp).TotalMinutes)
                    .ToList();
                
                if (responseTimes.Any())
                {
                    metrics.AverageResponseTime = responseTimes.Average();
                }

                // Analyze resource utilization
                var utilizationData = await AnalyzeResourceUtilizationAsync(resourceId, startDate, endDate);
                metrics.OverProvisioningPercentage = utilizationData.OverProvisionedTime;
                metrics.UnderProvisioningPercentage = utilizationData.UnderProvisionedTime;

                // Calculate cost savings
                var costAnalysis = await _costOptimizationEngine.AnalyzeSubscriptionAsync(resourceId);
                if (costAnalysis.TotalMonthlyCost > 0)
                {
                    metrics.CostSavingsPercentage =
                        (double)(costAnalysis.PotentialMonthlySavings / costAnalysis.TotalMonthlyCost * 100);
                }

                // Calculate prediction accuracy
                metrics.MetricAccuracy = await CalculatePredictionAccuracyAsync(resourceId, startDate, endDate);

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing scaling performance for resource {ResourceId}", resourceId);
                throw;
            }
        }

        public async Task<PredictiveScalingConfiguration> OptimizeScalingConfigurationAsync(string resourceId)
        {
            _logger.LogInformation("Optimizing scaling configuration for resource {ResourceId}", resourceId);

            try
            {
                // Get resource details
                var resource = await _resourceService.GetResourceAsync(resourceId);
                if (resource == null)
                {
                    throw new InvalidOperationException($"Resource {resourceId} not found");
                }

                // Analyze historical patterns
                var usagePatterns = await AnalyzeHistoricalPatternsAsync(resourceId);
                
                // Generate optimized configuration
                var config = new PredictiveScalingConfiguration
                {
                    ResourceId = resourceId,
                    ResourceType = resource.Type,
                    Strategy = DetermineOptimalStrategy(usagePatterns),
                    IsEnabled = true
                };

                // Set metrics based on resource type
                config.Metrics = GetOptimalMetrics(resource.Type);

                // Set thresholds based on usage patterns
                config.Thresholds = CalculateOptimalThresholds(usagePatterns);

                // Set constraints based on resource limits and cost
                config.Constraints = await DetermineOptimalConstraintsAsync(resource, usagePatterns);

                // Configure prediction settings
                config.PredictionSettings = new PredictionSettings
                {
                    Model = SelectBestPredictionModel(usagePatterns),
                    ConfidenceLevel = 0.95,
                    UseSeasonalDecomposition = usagePatterns.HasSeasonality,
                    SeasonalityPeriodDays = usagePatterns.SeasonalityPeriod,
                    UseAnomalyDetection = true
                };

                // Generate schedules based on patterns
                config.Schedules = GenerateOptimalSchedules(usagePatterns);

                _logger.LogInformation(
                    "Optimized configuration generated. Strategy: {Strategy}, Min: {Min}, Max: {Max}", 
                    config.Strategy, 
                    config.Constraints.MinimumInstances, 
                    config.Constraints.MaximumInstances);

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing scaling configuration for resource {ResourceId}", resourceId);
                throw;
            }
        }

        private Task<MetricPrediction> PredictTimeSeriesAsync(
            string metricName, 
            List<MetricDataPoint> historicalData, 
            int horizonHours)
        {
            var prediction = new MetricPrediction
            {
                MetricName = metricName,
                Predictions = new List<PredictionPoint>()
            };

            // Simple moving average prediction (should be replaced with proper ML model)
            var values = historicalData.Select(d => d.Value).ToList();
            var windowSize = Math.Min(24, values.Count / 4); // Use 24-hour window or 25% of data
            
            if (values.Count < windowSize)
            {
                _logger.LogWarning("Insufficient data for prediction. Using simple average.");
                var avg = values.Average();
                
                for (int i = 0; i < horizonHours; i++)
                {
                    prediction.Predictions.Add(new PredictionPoint
                    {
                        Timestamp = DateTime.UtcNow.AddHours(i),
                        Value = avg,
                        LowerBound = avg * 0.8,
                        UpperBound = avg * 1.2
                    });
                }
                
                return Task.FromResult(prediction);
            }

            // Calculate moving average and standard deviation
            var movingAverages = new List<double>();
            for (int i = windowSize; i <= values.Count; i++)
            {
                var window = values.Skip(i - windowSize).Take(windowSize);
                movingAverages.Add(window.Average());
            }

            var trend = movingAverages.Count > 1 ? 
                (movingAverages.Last() - movingAverages.First()) / movingAverages.Count : 0;
            
            var lastAvg = movingAverages.LastOrDefault();
            var stdDev = CalculateStandardDeviation(movingAverages);

            // Generate predictions
            for (int hour = 1; hour <= horizonHours; hour++)
            {
                var predictedValue = lastAvg + (trend * hour);
                var confidence = 1.96 * stdDev; // 95% confidence interval
                
                prediction.Predictions.Add(new PredictionPoint
                {
                    Timestamp = DateTime.UtcNow.AddHours(hour),
                    Value = Math.Max(0, predictedValue),
                    LowerBound = Math.Max(0, predictedValue - confidence),
                    UpperBound = predictedValue + confidence
                });
            }

            // Calculate error metrics (simplified)
            prediction.MeanAbsoluteError = stdDev;
            prediction.RootMeanSquaredError = stdDev * 1.1;

            return Task.FromResult(prediction);
        }

        private async Task<int> GetCurrentInstanceCountAsync(AzureResource resource)
        {
            // Get instance count based on resource type
            return resource.Type.ToLower() switch
            {
                "microsoft.compute/virtualmachinescalesets" => 
                    await GetVmssInstanceCountAsync(resource),
                "microsoft.web/serverfarms" => 
                    await GetAppServicePlanInstanceCountAsync(resource),
                "microsoft.containerservice/managedclusters" => 
                    await GetAksNodeCountAsync(resource),
                _ => 1
            };
        }

        private Task<List<string>> GetRelevantMetricsAsync(AzureResource resource)
        {
            // Return relevant metrics based on resource type
            var metrics = resource.Type.ToLower() switch
            {
                "microsoft.compute/virtualmachinescalesets" => 
                    new List<string> { "Percentage CPU", "Network In", "Network Out", "Disk Read Bytes", "Disk Write Bytes" },
                "microsoft.web/serverfarms" => 
                    new List<string> { "CpuPercentage", "MemoryPercentage", "HttpQueueLength", "DiskQueueLength" },
                "microsoft.containerservice/managedclusters" => 
                    new List<string> { "node_cpu_usage_percentage", "node_memory_usage_percentage", "node_network_in_bytes", "node_network_out_bytes" },
                _ => new List<string> { "Percentage CPU" }
            };

            return Task.FromResult(metrics);
        }

        private ScalingAnalysis AnalyzePredictions(List<MetricPrediction> predictions, int currentInstances)
        {
            var analysis = new ScalingAnalysis
            {
                Action = ScalingAction.None,
                RecommendedInstances = currentInstances,
                Confidence = 0.0
            };

            if (!predictions.Any()) return analysis;

            // Find the primary metric (usually CPU)
            var cpuPrediction = predictions.FirstOrDefault(p => 
                p.MetricName.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                p.MetricName.Contains("Cpu", StringComparison.OrdinalIgnoreCase));

            if (cpuPrediction == null) return analysis;

            // Get average predicted CPU for next hour
            var nextHourPredictions = cpuPrediction.Predictions.Take(1);
            if (!nextHourPredictions.Any()) return analysis;

            var avgPredictedCpu = nextHourPredictions.Average(p => p.Value);
            var maxPredictedCpu = nextHourPredictions.Max(p => p.UpperBound);

            analysis.PredictedLoad = avgPredictedCpu;
            analysis.Confidence = CalculateConfidenceScore(cpuPrediction);

            // Determine scaling action
            if (maxPredictedCpu > 80)
            {
                analysis.Action = maxPredictedCpu > 90 ? ScalingAction.EmergencyScale : ScalingAction.ScaleUp;
                var scaleFactor = maxPredictedCpu / 70; // Target 70% utilization
                analysis.RecommendedInstances = (int)Math.Ceiling(currentInstances * scaleFactor);
                analysis.Reasoning = $"Predicted CPU usage of {avgPredictedCpu:F1}% (max {maxPredictedCpu:F1}%) exceeds threshold. Scaling up to maintain performance.";
            }
            else if (avgPredictedCpu < 30 && currentInstances > 1)
            {
                analysis.Action = ScalingAction.ScaleDown;
                var scaleFactor = avgPredictedCpu / 50; // Target 50% utilization
                analysis.RecommendedInstances = Math.Max(1, (int)Math.Floor(currentInstances * scaleFactor));
                analysis.Reasoning = $"Predicted CPU usage of {avgPredictedCpu:F1}% is below threshold. Scaling down to optimize costs.";
            }
            else
            {
                analysis.Reasoning = $"Predicted CPU usage of {avgPredictedCpu:F1}% is within acceptable range. No scaling needed.";
            }

            return analysis;
        }

        private double CalculateConfidenceScore(MetricPrediction prediction)
        {
            if (prediction.MeanAbsoluteError == 0) return 1.0;
            
            var avgValue = prediction.Predictions.Average(p => p.Value);
            if (avgValue == 0) return 0.5;
            
            var errorRatio = prediction.MeanAbsoluteError / avgValue;
            return Math.Max(0, Math.Min(1, 1 - errorRatio));
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (!values.Any()) return 0;
            var avg = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }

        private async Task<bool> ScaleVmssAsync(AzureResource resource, int targetInstances)
        {
            // TODO: Implement actual VMSS scaling using Azure SDK
            _logger.LogInformation("Scaling VMSS {Name} to {Instances} instances", 
                resource.Name, targetInstances);
            await Task.Delay(100);
            return true;
        }

        private async Task<bool> ScaleAppServicePlanAsync(AzureResource resource, int targetInstances)
        {
            // TODO: Implement actual App Service Plan scaling using Azure SDK
            _logger.LogInformation("Scaling App Service Plan {Name} to {Instances} instances", 
                resource.Name, targetInstances);
            await Task.Delay(100);
            return true;
        }

        private async Task<bool> ScaleAksClusterAsync(AzureResource resource, int targetNodes)
        {
            // TODO: Implement actual AKS cluster scaling using Azure SDK
            _logger.LogInformation("Scaling AKS cluster {Name} to {Nodes} nodes", 
                resource.Name, targetNodes);
            await Task.Delay(100);
            return true;
        }

        private Task<int> GetVmssInstanceCountAsync(AzureResource resource)
        {
            // TODO: Get actual instance count from Azure
            return Task.FromResult(2);
        }

        private Task<int> GetAppServicePlanInstanceCountAsync(AzureResource resource)
        {
            // TODO: Get actual instance count from Azure
            return Task.FromResult(1);
        }

        private Task<int> GetAksNodeCountAsync(AzureResource resource)
        {
            // TODO: Get actual node count from Azure
            return Task.FromResult(3);
        }

        private async Task LogScalingEventAsync(ScalingEvent scalingEvent)
        {
            // TODO: Implement actual event logging to database or event store
            _logger.LogInformation("Scaling event logged: {Event}", scalingEvent);
            await Task.CompletedTask;
        }

        private Task<List<ScalingEvent>> GetScalingEventsAsync(
            string resourceId, 
            DateTime startDate, 
            DateTime endDate)
        {
            // TODO: Retrieve actual scaling events from storage
            return Task.FromResult(new List<ScalingEvent>());
        }

        private Task<ResourceUtilization> AnalyzeResourceUtilizationAsync(
            string resourceId, 
            DateTime startDate, 
            DateTime endDate)
        {
            // TODO: Implement actual utilization analysis
            return Task.FromResult(new ResourceUtilization
            {
                OverProvisionedTime = 15.5,
                UnderProvisionedTime = 5.2
            });
        }

        private Task<Dictionary<string, double>> CalculatePredictionAccuracyAsync(
            string resourceId, 
            DateTime startDate, 
            DateTime endDate)
        {
            // TODO: Implement actual accuracy calculation
            return Task.FromResult(new Dictionary<string, double>
            {
                ["CPU"] = 0.92,
                ["Memory"] = 0.88,
                ["Network"] = 0.85
            });
        }

        private Task<UsagePatternAnalysis> AnalyzeHistoricalPatternsAsync(string resourceId)
        {
            // TODO: Implement actual pattern analysis
            return Task.FromResult(new UsagePatternAnalysis
            {
                HasSeasonality = true,
                SeasonalityPeriod = 7,
                PeakHours = new List<int> { 9, 10, 11, 14, 15, 16 },
                LowUsageHours = new List<int> { 0, 1, 2, 3, 4, 5 },
                WeekendPattern = UsagePattern.Low,
                GrowthTrend = 0.05
            });
        }

        private ScalingStrategy DetermineOptimalStrategy(UsagePatternAnalysis patterns)
        {
            if (patterns.GrowthTrend > 0.1)
                return ScalingStrategy.Aggressive;
            else if (patterns.HasSeasonality && patterns.PeakHours.Count > 6)
                return ScalingStrategy.PerformanceOptimized;
            else if (patterns.WeekendPattern == UsagePattern.Low)
                return ScalingStrategy.CostOptimized;
            else
                return ScalingStrategy.Balanced;
        }

        private ScalingMetrics GetOptimalMetrics(string resourceType)
        {
            var metrics = new ScalingMetrics
            {
                LookbackPeriodDays = 30,
                PredictionHorizonHours = 24,
                AggregationType = AggregationType.Average,
                AggregationWindowMinutes = 5
            };

            switch (resourceType.ToLower())
            {
                case "microsoft.compute/virtualmachinescalesets":
                    metrics.PrimaryMetrics = new List<string> { "Percentage CPU", "Available Memory Bytes" };
                    metrics.SecondaryMetrics = new List<string> { "Network In Total", "Disk Read Operations/Sec" };
                    break;
                    
                case "microsoft.web/serverfarms":
                    metrics.PrimaryMetrics = new List<string> { "CpuPercentage", "MemoryPercentage" };
                    metrics.SecondaryMetrics = new List<string> { "HttpQueueLength", "Requests" };
                    break;
                    
                default:
                    metrics.PrimaryMetrics = new List<string> { "Percentage CPU" };
                    break;
            }

            return metrics;
        }

        private ScalingThresholds CalculateOptimalThresholds(UsagePatternAnalysis patterns)
        {
            return new ScalingThresholds
            {
                ScaleUpThreshold = patterns.HasSeasonality ? 65 : 70,
                ScaleDownThreshold = 30,
                EmergencyScaleThreshold = 85,
                CooldownMinutes = patterns.GrowthTrend > 0.05 ? 5 : 10,
                StabilizationWindowMinutes = 5
            };
        }

        private Task<ScalingConstraints> DetermineOptimalConstraintsAsync(
            AzureResource resource, 
            UsagePatternAnalysis patterns)
        {
            var constraints = new ScalingConstraints
            {
                MinimumInstances = 1,
                MaximumInstances = 10,
                ScaleUpStepSize = patterns.GrowthTrend > 0.1 ? 2 : 1,
                ScaleDownStepSize = 1,
                MaxScaleUpPerHour = 5,
                MaxScaleDownPerHour = 3
            };

            // Add blocked time windows for maintenance
            constraints.BlockedTimeWindows = new List<string>
            {
                "Sunday 02:00-04:00",
                "Wednesday 02:00-04:00"
            };

            return Task.FromResult(constraints);
        }

        private PredictionModel SelectBestPredictionModel(UsagePatternAnalysis patterns)
        {
            if (patterns.HasSeasonality && patterns.SeasonalityPeriod > 1)
                return PredictionModel.Prophet;
            else if (patterns.GrowthTrend != 0)
                return PredictionModel.ARIMA;
            else
                return PredictionModel.ExponentialSmoothing;
        }

        private List<ScalingSchedule> GenerateOptimalSchedules(UsagePatternAnalysis patterns)
        {
            var schedules = new List<ScalingSchedule>();

            // Add weekday peak schedule
            if (patterns.PeakHours.Any())
            {
                schedules.Add(new ScalingSchedule
                {
                    Name = "Weekday Peak Hours",
                    Type = ScheduleType.Daily,
                    CronExpression = "0 9 * * 1-5", // 9 AM Mon-Fri
                    TargetInstances = 5,
                    IsEnabled = true
                });
                
                schedules.Add(new ScalingSchedule
                {
                    Name = "Weekday Off-Peak",
                    Type = ScheduleType.Daily,
                    CronExpression = "0 18 * * 1-5", // 6 PM Mon-Fri
                    TargetInstances = 2,
                    IsEnabled = true
                });
            }

            // Add weekend schedule
            if (patterns.WeekendPattern == UsagePattern.Low)
            {
                schedules.Add(new ScalingSchedule
                {
                    Name = "Weekend Low Usage",
                    Type = ScheduleType.Weekly,
                    CronExpression = "0 0 * * 6", // Saturday midnight
                    TargetInstances = 1,
                    IsEnabled = true
                });
            }

            return schedules;
        }

        // Helper classes
        private class ScalingAnalysis
        {
            public ScalingAction Action { get; set; }
            public int RecommendedInstances { get; set; }
            public double PredictedLoad { get; set; }
            public double Confidence { get; set; }
            public string Reasoning { get; set; } = string.Empty;
        }

        private class ResourceUtilization
        {
            public double OverProvisionedTime { get; set; }
            public double UnderProvisionedTime { get; set; }
        }

        private class UsagePatternAnalysis
        {
            public bool HasSeasonality { get; set; }
            public int SeasonalityPeriod { get; set; }
            public List<int> PeakHours { get; set; } = new();
            public List<int> LowUsageHours { get; set; } = new();
            public UsagePattern WeekendPattern { get; set; }
            public double GrowthTrend { get; set; }
        }

        private enum UsagePattern
        {
            Low,
            Medium,
            High
        }
    }
}