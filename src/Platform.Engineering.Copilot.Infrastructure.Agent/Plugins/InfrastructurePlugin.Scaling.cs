using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Platform.Engineering.Copilot.Infrastructure.Core;

/// <summary>
/// Partial class for predictive scaling and performance analysis functionality.
/// Handles scaling predictions, configuration optimization, and performance metrics.
/// </summary>
public partial class InfrastructurePlugin
{
    [KernelFunction("predict_scaling_needs")]
    [Description("Predict future scaling needs for Azure resources based on historical metrics and trends. Use when users ask about: 'will I need to scale', 'predict scaling', 'forecast resource usage', 'when should I scale', 'anticipate load'. Supports VMSS, App Service Plans, and AKS clusters.")]
    public async Task<string> PredictScalingNeedsAsync(
        [Description("Azure resource ID (e.g., /subscriptions/xxx/resourceGroups/xxx/providers/Microsoft.Compute/virtualMachineScaleSets/xxx)")]
        string resourceId,
        [Description("How many hours into the future to predict. Examples: 24 for 1 day, 168 for 1 week, 720 for 1 month. Default: 24")]
        int predictionHoursAhead = 24)
    {
        try
        {
            if (_scalingEngine == null)
            {
                return "❌ Predictive scaling is disabled. Enable 'EnablePredictiveScaling' in configuration to use this feature.";
            }

            _logger.LogInformation("Predicting scaling needs for resource: {ResourceId}, Hours: {Hours}",
                resourceId, predictionHoursAhead);

            var targetTime = DateTime.UtcNow.AddHours(predictionHoursAhead);
            var recommendation = await _scalingEngine.GeneratePredictionAsync(resourceId, targetTime);

            var response = new StringBuilder();
            response.AppendLine($"🔮 **Predictive Scaling Analysis**");
            response.AppendLine();
            response.AppendLine($"📍 **Resource**: `{resourceId.Split('/').Last()}`");
            response.AppendLine($"⏰ **Prediction Time**: {targetTime:yyyy-MM-dd HH:mm} UTC ({predictionHoursAhead} hours ahead)");
            response.AppendLine($"📊 **Current Instances**: {recommendation.CurrentInstances}");
            response.AppendLine($"🎯 **Recommended Instances**: {recommendation.RecommendedInstances}");
            response.AppendLine($"📈 **Predicted Load**: {recommendation.PredictedLoad:F1}%");
            response.AppendLine($"✅ **Confidence Score**: {recommendation.ConfidenceScore:P0}");
            response.AppendLine();

            response.AppendLine($"### 🚀 Recommended Action: **{recommendation.RecommendedAction}**");
            response.AppendLine();

            if (!string.IsNullOrEmpty(recommendation.Reasoning))
            {
                response.AppendLine($"**Reasoning**: {recommendation.Reasoning}");
                response.AppendLine();
            }

            if (recommendation.MetricPredictions != null && recommendation.MetricPredictions.Any())
            {
                response.AppendLine("### 📊 Metric Predictions");
                response.AppendLine();
                foreach (var metric in recommendation.MetricPredictions.Take(3))
                {
                    var latest = metric.Predictions.OrderBy(p => p.Timestamp).LastOrDefault();
                    if (latest != null)
                    {
                        response.AppendLine($"- **{metric.MetricName}**: {latest.Value:F2} (range: {latest.LowerBound:F2}-{latest.UpperBound:F2})");
                    }
                }
                response.AppendLine();
            }

            response.AppendLine("💡 **Next Steps:**");
            if (recommendation.RecommendedAction != Platform.Engineering.Copilot.Core.Models.PredictiveScaling.ScalingAction.None)
            {
                response.AppendLine($"1. Review the prediction and confidence score");
                response.AppendLine($"2. If you agree, ask me to 'apply scaling recommendation' to execute the change");
                response.AppendLine($"3. Monitor the resource after scaling to validate the prediction");
            }
            else
            {
                response.AppendLine("✅ No scaling action needed - your current capacity is optimal!");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting scaling needs: {ResourceId}", resourceId);
            return $"❌ Error predicting scaling: {ex.Message}";
        }
    }

    [KernelFunction("optimize_scaling_configuration")]
    [Description("Analyze and optimize auto-scaling configuration for Azure resources. Use when users ask: 'optimize my scaling', 'improve auto-scaling', 'tune scaling rules', 'better scaling configuration', 'scaling efficiency'. IMPORTANT: Extract the ACTUAL resource name, resource group, and subscription ID from the user's message - do NOT use placeholder values like 'your-resource-group' or 'yourAppServicePlan'. Build the complete Azure resource ID in the format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/serverfarms/{appServicePlanName} or /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmssName}")]
    public async Task<string> OptimizeScalingConfigurationAsync(
        [Description("Complete Azure resource ID (extract actual resource name, resource group, and subscription from user message). Format: /subscriptions/{guid}/resourceGroups/{actual-rg-name}/providers/Microsoft.Web/serverfarms/{actual-plan-name}")]
        string resourceId)
    {
        try
        {
            if (_scalingEngine == null)
            {
                return "❌ Scaling optimization is disabled. Enable 'EnablePredictiveScaling' in configuration to use this feature.";
            }

            _logger.LogInformation("Optimizing scaling configuration for: {ResourceId}", resourceId);

            var optimizedConfig = await _scalingEngine.OptimizeScalingConfigurationAsync(resourceId);

            var response = new StringBuilder();
            response.AppendLine($"⚙️ **Scaling Configuration Optimization**");
            response.AppendLine();
            response.AppendLine($"📍 **Resource**: `{resourceId.Split('/').Last()}`");
            response.AppendLine();

            response.AppendLine("### 🎯 Optimized Configuration");
            response.AppendLine();
            response.AppendLine($"- **Min Instances**: {optimizedConfig.Constraints.MinimumInstances}");
            response.AppendLine($"- **Max Instances**: {optimizedConfig.Constraints.MaximumInstances}");
            response.AppendLine($"- **Scale-Up Threshold**: {optimizedConfig.Thresholds.ScaleUpThreshold}%");
            response.AppendLine($"- **Scale-Down Threshold**: {optimizedConfig.Thresholds.ScaleDownThreshold}%");
            response.AppendLine($"- **Cooldown Period**: {optimizedConfig.Thresholds.CooldownMinutes} minutes");
            response.AppendLine($"- **Strategy**: {optimizedConfig.Strategy}");
            response.AppendLine();

            if (optimizedConfig.Metrics.PrimaryMetrics != null && optimizedConfig.Metrics.PrimaryMetrics.Any())
            {
                response.AppendLine("### 📊 Recommended Metrics to Monitor");
                response.AppendLine();
                foreach (var metric in optimizedConfig.Metrics.PrimaryMetrics)
                {
                    response.AppendLine($"- {metric}");
                }
                response.AppendLine();
            }

            response.AppendLine("### 💡 Configuration Details");
            response.AppendLine();
            response.AppendLine($"- **Prediction Model**: {optimizedConfig.PredictionSettings.Model}");
            response.AppendLine($"- **Lookback Period**: {optimizedConfig.Metrics.LookbackPeriodDays} days");
            response.AppendLine($"- **Prediction Horizon**: {optimizedConfig.Metrics.PredictionHorizonHours} hours");
            response.AppendLine($"- **Confidence Level**: {optimizedConfig.PredictionSettings.ConfidenceLevel:P0}");
            response.AppendLine();

            response.AppendLine("### 🚀 Next Steps");
            response.AppendLine("1. Review the optimized configuration above");
            response.AppendLine("2. Apply these settings to your resource's auto-scaling rules");
            response.AppendLine("3. Monitor performance for 1-2 weeks to validate effectiveness");
            response.AppendLine("4. Ask me to 'analyze scaling performance' to review results");

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing scaling configuration: {ResourceId}", resourceId);
            return $"❌ Error optimizing configuration: {ex.Message}";
        }
    }

    [KernelFunction("analyze_scaling_performance")]
    [Description("Analyze historical scaling performance and effectiveness. Use when users ask: 'how is my scaling performing', 'scaling efficiency', 'review scaling history', 'scaling performance metrics', 'was scaling effective'. IMPORTANT: Extract the ACTUAL resource name, resource group, and subscription ID from the user's message - do NOT use placeholder values.")]
    public async Task<string> AnalyzeScalingPerformanceAsync(
        [Description("Complete Azure resource ID (extract actual resource name, resource group, and subscription from user message). Format: /subscriptions/{guid}/resourceGroups/{actual-rg-name}/providers/Microsoft.Web/serverfarms/{actual-plan-name}")]
        string resourceId,
        [Description("Number of days to analyze. Default: 7")]
        int daysToAnalyze = 7)
    {
        try
        {
            if (_scalingEngine == null)
            {
                return "❌ Scaling performance analysis is disabled. Enable 'EnablePredictiveScaling' in configuration to use this feature.";
            }

            _logger.LogInformation("Analyzing scaling performance for: {ResourceId}, Days: {Days}",
                resourceId, daysToAnalyze);

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-daysToAnalyze);

            var metrics = await _scalingEngine.AnalyzeScalingPerformanceAsync(resourceId, startDate, endDate);

            var response = new StringBuilder();
            response.AppendLine($"📈 **Scaling Performance Analysis**");
            response.AppendLine();
            response.AppendLine($"📍 **Resource**: `{resourceId.Split('/').Last()}`");
            response.AppendLine($"📅 **Period**: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} ({daysToAnalyze} days)");
            response.AppendLine();

            response.AppendLine("### 📊 Performance Metrics");
            response.AppendLine();
            response.AppendLine($"- **Total Scaling Events**: {metrics.TotalScalingEvents}");
            response.AppendLine($"- **Successful Events**: {metrics.SuccessfulScalingEvents}");
            response.AppendLine($"- **Success Rate**: {(metrics.TotalScalingEvents > 0 ? (double)metrics.SuccessfulScalingEvents / metrics.TotalScalingEvents : 0):P0}");
            response.AppendLine($"- **Average Response Time**: {metrics.AverageResponseTime:F1} minutes");
            response.AppendLine();

            response.AppendLine("### 💰 Cost Impact");
            response.AppendLine();
            response.AppendLine($"- **Cost Savings**: {metrics.CostSavingsPercentage:P1}");
            response.AppendLine($"- **Over-Provisioning Time**: {metrics.OverProvisioningPercentage:P0}");
            response.AppendLine($"- **Under-Provisioning Time**: {metrics.UnderProvisioningPercentage:P0}");
            response.AppendLine();

            response.AppendLine("### 🎯 Efficiency Score");
            response.AppendLine();
            // Calculate efficiency score based on success rate and provisioning balance
            var efficiencyScore = metrics.TotalScalingEvents > 0 
                ? ((double)metrics.SuccessfulScalingEvents / metrics.TotalScalingEvents) * 
                  (1 - Math.Abs(metrics.OverProvisioningPercentage - metrics.UnderProvisioningPercentage))
                : 0;
            
            var efficiencyEmoji = efficiencyScore switch
            {
                >= 0.9 => "🌟",
                >= 0.7 => "👍",
                >= 0.5 => "⚠️",
                _ => "❌"
            };
            response.AppendLine($"{efficiencyEmoji} **{efficiencyScore:P0}** - {GetEfficiencyRating(efficiencyScore)}");
            response.AppendLine();

            if (metrics.MetricAccuracy != null && metrics.MetricAccuracy.Any())
            {
                response.AppendLine("### 📊 Metric Prediction Accuracy");
                response.AppendLine();
                foreach (var metric in metrics.MetricAccuracy.Take(5))
                {
                    response.AppendLine($"- **{metric.Key}**: {metric.Value:P0}");
                }
                response.AppendLine();
            }

            response.AppendLine("### 🚀 Next Steps");
            response.AppendLine("1. If efficiency is low, ask me to 'optimize scaling configuration'");
            response.AppendLine("2. Review over/under-provisioning percentages for tuning opportunities");
            response.AppendLine("3. Monitor cost savings and adjust thresholds as needed");

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scaling performance: {ResourceId}", resourceId);
            return $"❌ Error analyzing performance: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the efficiency rating based on score
    /// </summary>
    private string GetEfficiencyRating(double score)
    {
        return score switch
        {
            >= 0.9 => "Excellent",
            >= 0.7 => "Good",
            >= 0.5 => "Needs Improvement",
            _ => "Poor"
        };
    }
}
