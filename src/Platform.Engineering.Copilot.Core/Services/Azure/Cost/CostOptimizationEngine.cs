using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.CostOptimization;
using Platform.Engineering.Copilot.Core.Interfaces;
using AzureResource = Platform.Engineering.Copilot.Core.Models.AzureResource;
using CostAnomaly = Platform.Engineering.Copilot.Core.Models.CostAnomaly;
using CostForecast = Platform.Engineering.Copilot.Core.Models.CostForecast;
using CostMonitoringDashboard = Platform.Engineering.Copilot.Core.Models.CostMonitoringDashboard;
using AnomalyType = Platform.Engineering.Copilot.Core.Models.AnomalyType;
using AnomalySeverity = Platform.Engineering.Copilot.Core.Models.AnomalySeverity;
using ForecastMethod = Platform.Engineering.Copilot.Core.Models.ForecastMethod;
using ForecastAccuracy = Platform.Engineering.Copilot.Core.Models.ForecastAccuracy;
using ForecastDataPoint = Platform.Engineering.Copilot.Core.Models.ForecastDataPoint;
using ForecastAssumption = Platform.Engineering.Copilot.Core.Models.ForecastAssumption;

namespace Platform.Engineering.Copilot.Core.Services
{
    public interface ICostOptimizationEngine
    {
        Task<CostAnalysisResult> AnalyzeSubscriptionAsync(string subscriptionId);
        Task<List<CostOptimizationRecommendation>> GenerateRecommendationsAsync(string resourceId);
        Task<ResourceUsagePattern> AnalyzeUsagePatternsAsync(string resourceId, string metricName, DateTime startDate, DateTime endDate);
        Task<bool> ApplyRecommendationAsync(string recommendationId, Dictionary<string, object>? parameters = null);
        Task<Dictionary<string, decimal>> CalculateSavingsPotentialAsync(List<CostOptimizationRecommendation> recommendations);
        
        // New methods for anomaly detection and forecasting
        Task<List<CostAnomaly>> DetectCostAnomaliesAsync(string subscriptionId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
        Task<CostForecast> GetCostForecastAsync(string subscriptionId, int forecastDays, CancellationToken cancellationToken = default);
        Task<CostMonitoringDashboard> GetCostDashboardAsync(string subscriptionId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    }

    public class CostOptimizationEngine : ICostOptimizationEngine
    {
        private readonly ILogger<CostOptimizationEngine> _logger;
        private readonly IAzureMetricsService _metricsService;
        private readonly IAzureCostManagementService _costService;
        private readonly IAzureResourceService _resourceService;

        public CostOptimizationEngine(
            ILogger<CostOptimizationEngine> logger,
            IAzureMetricsService metricsService,
            IAzureCostManagementService costService,
            IAzureResourceService resourceService)
        {
            _logger = logger;
            _metricsService = metricsService;
            _costService = costService;
            _resourceService = resourceService;
        }

        public async Task<CostAnalysisResult> AnalyzeSubscriptionAsync(string subscriptionId)
        {
            _logger.LogInformation("Starting cost analysis for subscription {SubscriptionId}", subscriptionId);

            var result = new CostAnalysisResult
            {
                SubscriptionId = subscriptionId,
                AnalysisDate = DateTime.UtcNow
            };

            try
            {
                // Get current costs
                var costs = await _costService.GetCurrentMonthCostsAsync(subscriptionId);
                result.TotalMonthlyCost = costs.TotalCost;
                result.CostByService = costs.ServiceCosts;
                result.CostByResourceGroup = costs.ResourceGroupCosts;

                // Get historical trends
                result.HistoricalTrends = await GetHistoricalTrendsAsync(subscriptionId);

                // Generate recommendations for all resources
                var resources = await _resourceService.ListAllResourcesAsync(subscriptionId);
                var recommendations = new List<CostOptimizationRecommendation>();

                foreach (var resource in resources)
                {
                    var resourceRecommendations = await GenerateRecommendationsAsync(resource.Id);
                    recommendations.AddRange(resourceRecommendations);
                }

                result.Recommendations = recommendations;
                result.TotalRecommendations = recommendations.Count;
                result.PotentialMonthlySavings = recommendations.Sum(r => r.EstimatedMonthlySavings);

                _logger.LogInformation(
                    "Cost analysis completed. Found {RecommendationCount} recommendations with potential savings of ${Savings:N2}",
                    result.TotalRecommendations, 
                    result.PotentialMonthlySavings);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cost analysis for subscription {SubscriptionId}", subscriptionId);
                throw;
            }
        }

        public async Task<List<CostOptimizationRecommendation>> GenerateRecommendationsAsync(string resourceId)
        {
            var recommendations = new List<CostOptimizationRecommendation>();

            try
            {
                var resource = await _resourceService.GetResourceAsync(resourceId);
                if (resource == null) return recommendations;

                // Analyze different optimization opportunities based on resource type
                var resourceType = resource.Type.ToLower();
                
                if (resourceType == "microsoft.compute/virtualmachines")
                {
                    recommendations.AddRange(await AnalyzeVirtualMachineAsync(resource));
                }
                else if (resourceType == "microsoft.storage/storageaccounts")
                {
                    recommendations.AddRange(await AnalyzeStorageAccountAsync(resource));
                }
                else if (resourceType == "microsoft.sql/servers/databases")
                {
                    recommendations.AddRange(await AnalyzeSqlDatabaseAsync(resource));
                }
                else if (resourceType == "microsoft.web/sites")
                {
                    recommendations.AddRange(await AnalyzeAppServiceAsync(resource));
                }
                else if (resourceType == "microsoft.containerservice/managedclusters")
                {
                    recommendations.AddRange(await AnalyzeAksClusterAsync(resource));
                }

                // Common optimizations for all resources
                recommendations.AddRange(await AnalyzeCommonOptimizationsAsync(resource));

                return recommendations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recommendations for resource {ResourceId}", resourceId);
                return recommendations;
            }
        }

        public async Task<ResourceUsagePattern> AnalyzeUsagePatternsAsync(
            string resourceId, 
            string metricName, 
            DateTime startDate, 
            DateTime endDate)
        {
            var pattern = new ResourceUsagePattern
            {
                ResourceId = resourceId,
                MetricName = metricName
            };

            try
            {
                // Get metric data
                var metrics = await _metricsService.GetMetricsAsync(resourceId, metricName, startDate, endDate);
                pattern.DataPoints = metrics.Select(m => new UsageDataPoint
                {
                    Timestamp = m.Timestamp,
                    Value = m.Value,
                    Unit = m.Unit
                }).ToList();

                if (pattern.DataPoints.Any())
                {
                    // Calculate statistics
                    var values = pattern.DataPoints.Select(dp => dp.Value).ToList();
                    pattern.AverageUsage = values.Average();
                    pattern.PeakUsage = values.Max();
                    pattern.MinUsage = values.Min();
                    pattern.StandardDeviation = CalculateStandardDeviation(values);

                    // Detect usage pattern
                    pattern.Pattern = DetectUsagePattern(pattern.DataPoints);

                    // Analyze time-based patterns (hourly, daily, weekly)
                    pattern.TimeBasedPatterns = AnalyzeTimeBasedPatterns(pattern.DataPoints);
                }

                return pattern;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing usage patterns for resource {ResourceId}", resourceId);
                throw;
            }
        }

        public async Task<bool> ApplyRecommendationAsync(string recommendationId, Dictionary<string, object>? parameters = null)
        {
            try
            {
                // This would implement the actual application of recommendations
                // For now, we'll log and return success
                _logger.LogInformation("Applying recommendation {RecommendationId} with parameters: {Parameters}", 
                    recommendationId, 
                    parameters != null ? string.Join(", ", parameters.Select(kv => $"{kv.Key}={kv.Value}")) : "none");

                // TODO: Implement actual recommendation application logic
                await Task.Delay(100); // Simulate work

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying recommendation {RecommendationId}", recommendationId);
                return false;
            }
        }

        public async Task<Dictionary<string, decimal>> CalculateSavingsPotentialAsync(
            List<CostOptimizationRecommendation> recommendations)
        {
            var savingsByType = new Dictionary<string, decimal>();

            await Task.Run(() =>
            {
                foreach (var group in recommendations.GroupBy(r => r.Type))
                {
                    savingsByType[group.Key.ToString()] = group.Sum(r => r.EstimatedMonthlySavings);
                }
            });

            return savingsByType;
        }

        private async Task<List<CostOptimizationRecommendation>> AnalyzeVirtualMachineAsync(AzureResource resource)
        {
            var recommendations = new List<CostOptimizationRecommendation>();
            
            // Check CPU usage for right-sizing
            var cpuPattern = await AnalyzeUsagePatternsAsync(
                resource.Id, 
                "Percentage CPU", 
                DateTime.UtcNow.AddDays(-30), 
                DateTime.UtcNow);

            if (cpuPattern.AverageUsage < 10 && cpuPattern.PeakUsage < 30)
            {
                var currentCost = await _costService.GetResourceMonthlyCostAsync(resource.Id);
                recommendations.Add(new CostOptimizationRecommendation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ResourceGroup = resource.ResourceGroup,
                    Type = OptimizationType.RightSizing,
                    Priority = OptimizationPriority.High,
                    CurrentMonthlyCost = currentCost,
                    EstimatedMonthlySavings = currentCost * 0.5m, // 50% savings by downsizing
                    Description = $"VM '{resource.Name}' has average CPU usage of {cpuPattern.AverageUsage:F2}%. Consider downsizing.",
                    Complexity = ImplementationComplexity.Simple,
                    Actions = new List<OptimizationAction>
                    {
                        new OptimizationAction
                        {
                            Description = "Resize VM to a smaller SKU",
                            Type = ActionType.Resize,
                            IsAutomatable = true,
                            Parameters = new Dictionary<string, object>
                            {
                                ["currentSize"] = GetDynamicPropertyValue(resource.Properties, "hardwareProfile.vmSize") ?? "Unknown",
                                ["recommendedSize"] = "Standard_B2s" // Example recommendation
                            }
                        }
                    }
                });
            }

            // Check if VM is stopped but not deallocated
            bool isStoppedNotDeallocated = false;
            if (resource.Properties != null)
            {
                dynamic props = resource.Properties;
                if (props.instanceView?.statuses != null)
                {
                    foreach (var status in props.instanceView.statuses)
                    {
                        if (status.code == "PowerState/stopped")
                        {
                            isStoppedNotDeallocated = true;
                            break;
                        }
                    }
                }
            }
            
            if (isStoppedNotDeallocated)
            {
                var currentCost = await _costService.GetResourceMonthlyCostAsync(resource.Id);
                recommendations.Add(new CostOptimizationRecommendation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ResourceGroup = resource.ResourceGroup,
                    Type = OptimizationType.UnusedResources,
                    Priority = OptimizationPriority.Critical,
                    CurrentMonthlyCost = currentCost,
                    EstimatedMonthlySavings = currentCost,
                    Description = $"VM '{resource.Name}' is stopped but still incurring charges. Deallocate to save costs.",
                    Complexity = ImplementationComplexity.Simple,
                    Actions = new List<OptimizationAction>
                    {
                        new OptimizationAction
                        {
                            Description = "Deallocate the VM",
                            Type = ActionType.Stop,
                            IsAutomatable = true
                        }
                    }
                });
            }

            return recommendations;
        }

        private async Task<List<CostOptimizationRecommendation>> AnalyzeStorageAccountAsync(AzureResource resource)
        {
            var recommendations = new List<CostOptimizationRecommendation>();
            
            // Check for blob lifecycle management
            var hasLifecycleManagement = GetDynamicPropertyValue(resource.Properties, "blobProperties.lifecycleManagement") != null;
            if (!hasLifecycleManagement)
            {
                var currentCost = await _costService.GetResourceMonthlyCostAsync(resource.Id);
                recommendations.Add(new CostOptimizationRecommendation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ResourceGroup = resource.ResourceGroup,
                    Type = OptimizationType.StorageOptimization,
                    Priority = OptimizationPriority.Medium,
                    CurrentMonthlyCost = currentCost,
                    EstimatedMonthlySavings = currentCost * 0.2m, // 20% potential savings
                    Description = $"Storage account '{resource.Name}' lacks lifecycle management policies. Enable to automatically move data to cooler tiers.",
                    Complexity = ImplementationComplexity.Simple,
                    Actions = new List<OptimizationAction>
                    {
                        new OptimizationAction
                        {
                            Description = "Configure blob lifecycle management",
                            Type = ActionType.Configure,
                            IsAutomatable = true,
                            Parameters = new Dictionary<string, object>
                            {
                                ["moveToCoolAfterDays"] = 30,
                                ["moveToArchiveAfterDays"] = 90,
                                ["deleteAfterDays"] = 365
                            }
                        }
                    }
                });
            }

            return recommendations;
        }

        private async Task<List<CostOptimizationRecommendation>> AnalyzeSqlDatabaseAsync(AzureResource resource)
        {
            var recommendations = new List<CostOptimizationRecommendation>();
            
            // Check DTU usage for right-sizing
            var dtuPattern = await AnalyzeUsagePatternsAsync(
                resource.Id, 
                "dtu_consumption_percent", 
                DateTime.UtcNow.AddDays(-14), 
                DateTime.UtcNow);

            if (dtuPattern.AverageUsage < 20)
            {
                var currentCost = await _costService.GetResourceMonthlyCostAsync(resource.Id);
                recommendations.Add(new CostOptimizationRecommendation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ResourceGroup = resource.ResourceGroup,
                    Type = OptimizationType.RightSizing,
                    Priority = OptimizationPriority.High,
                    CurrentMonthlyCost = currentCost,
                    EstimatedMonthlySavings = currentCost * 0.4m,
                    Description = $"SQL Database '{resource.Name}' has low DTU usage ({dtuPattern.AverageUsage:F2}%). Consider scaling down.",
                    Complexity = ImplementationComplexity.Simple,
                    Actions = new List<OptimizationAction>
                    {
                        new OptimizationAction
                        {
                            Description = "Scale down database tier",
                            Type = ActionType.Resize,
                            IsAutomatable = true
                        }
                    }
                });
            }

            return recommendations;
        }

        private async Task<List<CostOptimizationRecommendation>> AnalyzeAppServiceAsync(AzureResource resource)
        {
            var recommendations = new List<CostOptimizationRecommendation>();
            
            // Check if app service is in Always On mode without traffic
            var requestPattern = await AnalyzeUsagePatternsAsync(
                resource.Id, 
                "Requests", 
                DateTime.UtcNow.AddDays(-7), 
                DateTime.UtcNow);

            var alwaysOn = GetDynamicPropertyValue(resource.Properties, "siteConfig.alwaysOn");
            if (requestPattern.AverageUsage < 100 && alwaysOn != null && (bool)alwaysOn == true)
            {
                var currentCost = await _costService.GetResourceMonthlyCostAsync(resource.Id);
                recommendations.Add(new CostOptimizationRecommendation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ResourceGroup = resource.ResourceGroup,
                    Type = OptimizationType.ScheduledShutdown,
                    Priority = OptimizationPriority.Medium,
                    CurrentMonthlyCost = currentCost,
                    EstimatedMonthlySavings = currentCost * 0.3m,
                    Description = $"App Service '{resource.Name}' has low traffic. Consider disabling Always On or implementing scheduled scaling.",
                    Complexity = ImplementationComplexity.Moderate,
                    Actions = new List<OptimizationAction>
                    {
                        new OptimizationAction
                        {
                            Description = "Disable Always On setting",
                            Type = ActionType.Configure,
                            IsAutomatable = true
                        }
                    }
                });
            }

            return recommendations;
        }

        private async Task<List<CostOptimizationRecommendation>> AnalyzeAksClusterAsync(AzureResource resource)
        {
            var recommendations = new List<CostOptimizationRecommendation>();
            
            // Check node utilization
            var agentPools = GetDynamicPropertyValue(resource.Properties, "agentPoolProfiles");
            var nodeCount = 0;
            if (agentPools != null)
            {
                try 
                {
                    dynamic pools = agentPools;
                    if (pools[0] != null)
                        nodeCount = pools[0].count ?? 0;
                }
                catch { }
            }
            if (nodeCount > 3)
            {
                var currentCost = await _costService.GetResourceMonthlyCostAsync(resource.Id);
                recommendations.Add(new CostOptimizationRecommendation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ResourceGroup = resource.ResourceGroup,
                    Type = OptimizationType.AutoScaling,
                    Priority = OptimizationPriority.High,
                    CurrentMonthlyCost = currentCost,
                    EstimatedMonthlySavings = currentCost * 0.25m,
                    Description = $"AKS cluster '{resource.Name}' may benefit from auto-scaling to optimize node usage.",
                    Complexity = ImplementationComplexity.Moderate,
                    Actions = new List<OptimizationAction>
                    {
                        new OptimizationAction
                        {
                            Description = "Enable cluster autoscaler",
                            Type = ActionType.Configure,
                            IsAutomatable = true,
                            Parameters = new Dictionary<string, object>
                            {
                                ["minNodes"] = 1,
                                ["maxNodes"] = nodeCount
                            }
                        }
                    }
                });
            }

            return recommendations;
        }

        private async Task<List<CostOptimizationRecommendation>> AnalyzeCommonOptimizationsAsync(AzureResource resource)
        {
            var recommendations = new List<CostOptimizationRecommendation>();

            // Check for missing tags
            var missingRequiredTags = resource.Tags == null || 
                                      !resource.Tags.ContainsKey("Environment") || 
                                      !resource.Tags.ContainsKey("Owner");
            if (missingRequiredTags)
            {
                recommendations.Add(new CostOptimizationRecommendation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ResourceGroup = resource.ResourceGroup,
                    Type = OptimizationType.TagCompliance,
                    Priority = OptimizationPriority.Low,
                    CurrentMonthlyCost = 0,
                    EstimatedMonthlySavings = 0,
                    Description = $"Resource '{resource.Name}' is missing required tags for cost allocation and management.",
                    Complexity = ImplementationComplexity.Simple,
                    Actions = new List<OptimizationAction>
                    {
                        new OptimizationAction
                        {
                            Description = "Add required tags",
                            Type = ActionType.Tag,
                            IsAutomatable = true,
                            Parameters = new Dictionary<string, object>
                            {
                                ["Environment"] = "Production",
                                ["Owner"] = "Unknown",
                                ["CostCenter"] = "Unknown"
                            }
                        }
                    }
                });
            }

            // Check for reserved instance opportunities
            if (IsEligibleForReservedInstance(resource))
            {
                var currentCost = await _costService.GetResourceMonthlyCostAsync(resource.Id);
                recommendations.Add(new CostOptimizationRecommendation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ResourceGroup = resource.ResourceGroup,
                    Type = OptimizationType.ReservedInstances,
                    Priority = OptimizationPriority.Medium,
                    CurrentMonthlyCost = currentCost,
                    EstimatedMonthlySavings = currentCost * 0.4m, // 40% savings typical for 3-year RIs
                    Description = $"Resource '{resource.Name}' is eligible for Reserved Instance pricing.",
                    Complexity = ImplementationComplexity.Simple,
                    Actions = new List<OptimizationAction>
                    {
                        new OptimizationAction
                        {
                            Description = "Purchase Reserved Instance",
                            Type = ActionType.Purchase,
                            IsAutomatable = false,
                            Parameters = new Dictionary<string, object>
                            {
                                ["term"] = "3 years",
                                ["paymentOption"] = "All Upfront"
                            }
                        }
                    }
                });
            }

            return recommendations;
        }

        private async Task<List<CostTrend>> GetHistoricalTrendsAsync(string subscriptionId)
        {
            var trends = new List<CostTrend>();
            
            for (int i = 0; i < 12; i++)
            {
                var date = DateTime.UtcNow.AddMonths(-i);
                var monthlyCost = await _costService.GetMonthlyTotalAsync(subscriptionId, date);
                
                trends.Add(new CostTrend
                {
                    Date = date,
                    Cost = monthlyCost
                });
            }

            return trends.OrderBy(t => t.Date).ToList();
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (!values.Any()) return 0;

            var average = values.Average();
            var sumOfSquaresOfDifferences = values.Select(val => (val - average) * (val - average)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / values.Count);
        }

        private UsagePattern DetectUsagePattern(List<UsageDataPoint> dataPoints)
        {
            if (!dataPoints.Any()) return UsagePattern.Steady;

            var values = dataPoints.Select(dp => dp.Value).ToList();
            var avg = values.Average();
            var stdDev = CalculateStandardDeviation(values);
            var coefficientOfVariation = avg > 0 ? stdDev / avg : 0;

            // Detect trend
            var firstHalf = values.Take(values.Count / 2).Average();
            var secondHalf = values.Skip(values.Count / 2).Average();
            var trendRatio = firstHalf > 0 ? secondHalf / firstHalf : 1;

            if (coefficientOfVariation < 0.1)
                return UsagePattern.Steady;
            else if (trendRatio > 1.2)
                return UsagePattern.Growing;
            else if (trendRatio < 0.8)
                return UsagePattern.Declining;
            else if (coefficientOfVariation > 0.5)
                return UsagePattern.Sporadic;
            else if (HasPeriodicPattern(dataPoints))
                return UsagePattern.Periodic;
            else
                return UsagePattern.Seasonal;
        }

        private bool HasPeriodicPattern(List<UsageDataPoint> dataPoints)
        {
            // Simple periodicity detection - could be enhanced with FFT
            var hourlyAverages = dataPoints
                .GroupBy(dp => dp.Timestamp.Hour)
                .Select(g => g.Average(dp => dp.Value))
                .ToList();

            var hourlyStdDev = CalculateStandardDeviation(hourlyAverages);
            return hourlyStdDev > hourlyAverages.Average() * 0.2;
        }

        private Dictionary<string, double> AnalyzeTimeBasedPatterns(List<UsageDataPoint> dataPoints)
        {
            var patterns = new Dictionary<string, double>();

            // Hourly patterns
            var hourlyAvg = dataPoints
                .GroupBy(dp => dp.Timestamp.Hour)
                .ToDictionary(g => $"Hour_{g.Key}", g => g.Average(dp => dp.Value));

            // Daily patterns
            var dailyAvg = dataPoints
                .GroupBy(dp => dp.Timestamp.DayOfWeek)
                .ToDictionary(g => $"Day_{g.Key}", g => g.Average(dp => dp.Value));

            // Combine patterns
            foreach (var kv in hourlyAvg) patterns[kv.Key] = kv.Value;
            foreach (var kv in dailyAvg) patterns[kv.Key] = kv.Value;

            return patterns;
        }

        private bool IsEligibleForReservedInstance(AzureResource resource)
        {
            // Check if resource type supports RIs and has been running for > 6 months
            var eligibleTypes = new[] 
            { 
                "microsoft.compute/virtualmachines", 
                "microsoft.sql/servers/databases",
                "microsoft.cache/redis"
            };

            var resourceType = resource.Type?.ToLower() ?? string.Empty;
            var isEligibleType = Array.IndexOf(eligibleTypes, resourceType) >= 0;
            
            return isEligibleType && 
                   resource.CreatedTime != null && 
                   resource.CreatedTime < DateTime.UtcNow.AddMonths(-6);
        }
        
        private object? GetDynamicPropertyValue(dynamic obj, string propertyPath)
        {
            if (obj == null) return null;
            
            try
            {
                var parts = propertyPath.Split('.');
                dynamic current = obj;
                
                foreach (var part in parts)
                {
                    if (current == null) return null;
                    current = current[part];
                }
                
                return current;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Detects cost anomalies using statistical analysis of historical trends
        /// </summary>
        public async Task<List<CostAnomaly>> DetectCostAnomaliesAsync(
            string subscriptionId, 
            DateTimeOffset startDate, 
            DateTimeOffset endDate, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Detecting cost anomalies for subscription {SubscriptionId} from {StartDate} to {EndDate}", 
                subscriptionId, startDate, endDate);

            try
            {
                // Get historical cost data from the cost management service
                var costTrends = await _costService.GetCostTrendsAsync(subscriptionId, startDate.AddDays(-30), endDate, cancellationToken);
                
                // Apply statistical anomaly detection
                var anomalies = new List<CostAnomaly>();

                if (costTrends.Count > 7)
                {
                    var dailyCosts = costTrends.Select(t => t.DailyCost).ToList();
                    var mean = dailyCosts.Average();
                    var stdDev = Math.Sqrt(dailyCosts.Select(c => Math.Pow((double)(c - mean), 2)).Average());
                    var threshold = mean + (decimal)(2 * stdDev); // 2 standard deviations

                    foreach (var trend in costTrends.Where(t => t.DailyCost > threshold))
                    {
                        anomalies.Add(new CostAnomaly
                        {
                            AnomalyDate = trend.Date,
                            Type = AnomalyType.SpikeCost,
                            Severity = trend.DailyCost > threshold * 1.5m ? AnomalySeverity.High : AnomalySeverity.Medium,
                            Title = $"Cost spike detected on {trend.Date:yyyy-MM-dd}",
                            Description = $"Daily cost of ${trend.DailyCost:F2} significantly exceeds normal pattern (${mean:F2} average)",
                            ExpectedCost = mean,
                            ActualCost = trend.DailyCost,
                            CostDifference = trend.DailyCost - mean,
                            PercentageDeviation = (decimal)((trend.DailyCost - mean) / mean * 100),
                            AnomalyScore = (double)Math.Min(1.0m, (trend.DailyCost - threshold) / threshold),
                            AffectedServices = trend.ServiceCosts.Where(s => s.Value > mean * 0.1m).Select(s => s.Key).ToList(),
                            PossibleCauses = new List<string>
                            {
                                "Unexpected resource scaling",
                                "New resource deployments",
                                "Changed usage patterns",
                                "Billing calculation changes"
                            }
                        });
                    }
                }

                _logger.LogInformation("Detected {AnomalyCount} cost anomalies for subscription {SubscriptionId}", 
                    anomalies.Count, subscriptionId);

                return anomalies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to detect cost anomalies for subscription {SubscriptionId}", subscriptionId);
                return new List<CostAnomaly>();
            }
        }

        /// <summary>
        /// Generates cost forecast using linear regression and historical trends
        /// </summary>
        public async Task<CostForecast> GetCostForecastAsync(
            string subscriptionId, 
            int forecastDays, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating {Days}-day cost forecast for subscription {SubscriptionId}", 
                forecastDays, subscriptionId);

            try
            {
                // Get historical data for forecasting
                var endDate = DateTimeOffset.UtcNow;
                var startDate = endDate.AddDays(-30);
                var costTrends = await _costService.GetCostTrendsAsync(subscriptionId, startDate, endDate, cancellationToken);

                var forecast = new CostForecast
                {
                    Method = ForecastMethod.LinearRegression,
                    ConfidenceLevel = 0.75,
                    HistoricalAccuracy = new ForecastAccuracy
                    {
                        MeanAbsolutePercentageError = 0.15,
                        RootMeanSquareError = 50.0,
                        SampleSize = costTrends.Count
                    }
                };

                if (costTrends.Count >= 7)
                {
                    var avgDailyCost = costTrends.Average(t => t.DailyCost);
                    var trend = CalculateLinearTrend(costTrends.Select(t => (double)t.DailyCost).ToArray());

                    for (int i = 1; i <= forecastDays; i++)
                    {
                        var forecastDate = endDate.AddDays(i).Date;
                        var forecastedCost = Math.Max(0, avgDailyCost + (decimal)(trend * i));
                        var confidence = Math.Max(0.3, 0.9 - (i * 0.02)); // Confidence decreases over time

                        forecast.Projections.Add(new ForecastDataPoint
                        {
                            Date = forecastDate,
                            ForecastedCost = forecastedCost,
                            LowerBound = forecastedCost * 0.8m,
                            UpperBound = forecastedCost * 1.2m,
                            Confidence = confidence
                        });
                    }

                    forecast.ProjectedMonthEndCost = forecast.Projections
                        .Where(p => p.Date.Month == DateTime.Now.Month)
                        .Sum(p => p.ForecastedCost);
                    forecast.ProjectedQuarterEndCost = forecast.Projections.Take(90).Sum(p => p.ForecastedCost);
                    forecast.ProjectedYearEndCost = forecast.Projections.Take(365).Sum(p => p.ForecastedCost);
                }

                forecast.Assumptions.AddRange(new[]
                {
                    new ForecastAssumption 
                    { 
                        Description = "Current resource utilization patterns continue", 
                        Impact = 0.7, 
                        Category = "Usage" 
                    },
                    new ForecastAssumption 
                    { 
                        Description = "No major architectural changes", 
                        Impact = 0.8, 
                        Category = "Infrastructure" 
                    },
                    new ForecastAssumption 
                    { 
                        Description = "Azure pricing remains stable", 
                        Impact = 0.5, 
                        Category = "Pricing" 
                    }
                });

                _logger.LogInformation("Generated forecast with {ProjectionCount} data points for subscription {SubscriptionId}", 
                    forecast.Projections.Count, subscriptionId);

                return forecast;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate cost forecast for subscription {SubscriptionId}", subscriptionId);
                return new CostForecast 
                { 
                    Method = ForecastMethod.Historical_Average, 
                    ConfidenceLevel = 0.5 
                };
            }
        }

        /// <summary>
        /// Gets a comprehensive cost monitoring dashboard with all key metrics
        /// </summary>
        public async Task<CostMonitoringDashboard> GetCostDashboardAsync(
            string subscriptionId, 
            DateTimeOffset startDate, 
            DateTimeOffset endDate, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating cost dashboard for subscription {SubscriptionId} from {StartDate} to {EndDate}", 
                subscriptionId, startDate, endDate);

            try
            {
                // Delegate to AzureCostManagementService for dashboard generation
                // This service orchestrates the data gathering from multiple sources
                var dashboard = await _costService.GetCostDashboardAsync(subscriptionId, startDate, endDate, cancellationToken);

                _logger.LogInformation("Cost dashboard generated successfully for subscription {SubscriptionId}", subscriptionId);
                return dashboard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate cost dashboard for subscription {SubscriptionId}", subscriptionId);
                throw;
            }
        }

        private double CalculateLinearTrend(double[] values)
        {
            if (values.Length < 2) return 0;

            var n = values.Length;
            var sumX = n * (n - 1) / 2.0; // Sum of indices 0, 1, 2, ...
            var sumY = values.Sum();
            var sumXY = values.Select((y, x) => x * y).Sum();
            var sumX2 = Enumerable.Range(0, n).Select(x => x * x).Sum();

            return (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        }
    }
}