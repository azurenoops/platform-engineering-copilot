using FluentAssertions;
using Platform.Engineering.Copilot.CostManagement.Core.Configuration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.CostOptimization.Analysis;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Plugins;

/// <summary>
/// Unit tests for CostManagementPlugin models and configuration
/// Tests cost analysis models, optimization recommendations, and budget management
/// </summary>
public class CostManagementPluginTests
{
    #region CostManagementAgentOptions Tests

    [Fact]
    public void CostManagementAgentOptions_SectionName_IsCorrect()
    {
        // Assert
        CostManagementAgentOptions.SectionName.Should().Be("CostManagementAgent");
    }

    [Fact]
    public void CostManagementAgentOptions_CostManagement_DefaultValues()
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions();

        // Assert
        options.CostManagement.Should().NotBeNull();
    }

    [Fact]
    public void CostManagementAgentOptions_Budgets_DefaultValues()
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions();

        // Assert
        options.Budgets.Should().NotBeNull();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(128000)]
    public void CostManagementAgentOptions_MaxTokens_AcceptsValidRange(int maxTokens)
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions { MaxTokens = maxTokens };

        // Assert
        options.MaxTokens.Should().Be(maxTokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CostManagementAgentOptions_EnableAnomalyDetection_CanBeSet(bool enabled)
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions { EnableAnomalyDetection = enabled };

        // Assert
        options.EnableAnomalyDetection.Should().Be(enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CostManagementAgentOptions_EnableOptimizationRecommendations_CanBeSet(bool enabled)
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions { EnableOptimizationRecommendations = enabled };

        // Assert
        options.EnableOptimizationRecommendations.Should().Be(enabled);
    }

    #endregion

    #region OptimizationType Enum Tests

    [Fact]
    public void OptimizationType_AllValuesAreDefined()
    {
        // Assert
        var types = Enum.GetValues<OptimizationType>();
        types.Should().Contain(OptimizationType.RightSizing);
        types.Should().Contain(OptimizationType.UnusedResources);
        types.Should().Contain(OptimizationType.ReservedInstances);
        types.Should().Contain(OptimizationType.SpotInstances);
        types.Should().Contain(OptimizationType.AutoScaling);
        types.Should().Contain(OptimizationType.ScheduledShutdown);
        types.Should().Contain(OptimizationType.StorageOptimization);
        types.Should().Contain(OptimizationType.NetworkOptimization);
        types.Should().Contain(OptimizationType.LicenseOptimization);
        types.Should().Contain(OptimizationType.TagCompliance);
    }

    #endregion

    #region OptimizationPriority Enum Tests

    [Fact]
    public void OptimizationPriority_AllValuesAreDefined()
    {
        // Assert
        var priorities = Enum.GetValues<OptimizationPriority>();
        priorities.Should().Contain(OptimizationPriority.Critical);
        priorities.Should().Contain(OptimizationPriority.High);
        priorities.Should().Contain(OptimizationPriority.Medium);
        priorities.Should().Contain(OptimizationPriority.Low);
    }

    [Fact]
    public void OptimizationPriority_ValuesAreOrdered()
    {
        // Assert - verify priority ordering for correct prioritization
        ((int)OptimizationPriority.Critical).Should().BeLessThan((int)OptimizationPriority.High);
        ((int)OptimizationPriority.High).Should().BeLessThan((int)OptimizationPriority.Medium);
        ((int)OptimizationPriority.Medium).Should().BeLessThan((int)OptimizationPriority.Low);
    }

    #endregion

    #region ActionType Enum Tests

    [Fact]
    public void ActionType_AllValuesAreDefined()
    {
        // Assert
        var types = Enum.GetValues<ActionType>();
        types.Should().Contain(ActionType.Resize);
        types.Should().Contain(ActionType.Delete);
        types.Should().Contain(ActionType.Stop);
        types.Should().Contain(ActionType.Start);
        types.Should().Contain(ActionType.Schedule);
        types.Should().Contain(ActionType.Purchase);
        types.Should().Contain(ActionType.Migrate);
        types.Should().Contain(ActionType.Configure);
        types.Should().Contain(ActionType.Tag);
    }

    #endregion

    #region UsagePattern Enum Tests

    [Fact]
    public void UsagePattern_AllValuesAreDefined()
    {
        // Assert
        var patterns = Enum.GetValues<UsagePattern>();
        patterns.Should().Contain(UsagePattern.Steady);
        patterns.Should().Contain(UsagePattern.Periodic);
        patterns.Should().Contain(UsagePattern.Sporadic);
        patterns.Should().Contain(UsagePattern.Growing);
        patterns.Should().Contain(UsagePattern.Declining);
        patterns.Should().Contain(UsagePattern.Seasonal);
    }

    #endregion

    #region CostOptimizationRecommendation Tests

    [Fact]
    public void CostOptimizationRecommendation_Properties_CanBeSet()
    {
        // Arrange & Act
        var recommendation = new CostOptimizationRecommendation
        {
            ResourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1",
            ResourceName = "vm1",
            ResourceType = "Microsoft.Compute/virtualMachines",
            ResourceGroup = "rg",
            Type = OptimizationType.RightSizing,
            Priority = OptimizationPriority.High,
            CurrentMonthlyCost = 500.00m,
            EstimatedMonthlySavings = 150.00m,
            Description = "Resize VM from D4s_v3 to D2s_v3 based on average utilization"
        };

        // Assert
        recommendation.ResourceId.Should().Contain("virtualMachines");
        recommendation.ResourceName.Should().Be("vm1");
        recommendation.Type.Should().Be(OptimizationType.RightSizing);
        recommendation.Priority.Should().Be(OptimizationPriority.High);
        recommendation.CurrentMonthlyCost.Should().Be(500.00m);
        recommendation.EstimatedMonthlySavings.Should().Be(150.00m);
    }

    [Fact]
    public void CostOptimizationRecommendation_EstimatedSavingsPercentage_IsComputed()
    {
        // Arrange
        var recommendation = new CostOptimizationRecommendation
        {
            CurrentMonthlyCost = 500.00m,
            EstimatedMonthlySavings = 150.00m
        };

        // Act & Assert
        recommendation.EstimatedSavingsPercentage.Should().Be(30.0m);
    }

    [Fact]
    public void CostOptimizationRecommendation_EstimatedSavingsPercentage_ZeroCost_ReturnsZero()
    {
        // Arrange
        var recommendation = new CostOptimizationRecommendation
        {
            CurrentMonthlyCost = 0m,
            EstimatedMonthlySavings = 50.00m
        };

        // Act & Assert
        recommendation.EstimatedSavingsPercentage.Should().Be(0m);
    }

    [Fact]
    public void CostOptimizationRecommendation_Actions_DefaultsToEmpty()
    {
        // Arrange & Act
        var recommendation = new CostOptimizationRecommendation();

        // Assert
        recommendation.Actions.Should().NotBeNull();
        recommendation.Actions.Should().BeEmpty();
    }

    [Fact]
    public void CostOptimizationRecommendation_Metadata_DefaultsToEmpty()
    {
        // Arrange & Act
        var recommendation = new CostOptimizationRecommendation();

        // Assert
        recommendation.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void CostOptimizationRecommendation_Id_IsGeneratedByDefault()
    {
        // Arrange & Act
        var recommendation = new CostOptimizationRecommendation();

        // Assert
        recommendation.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(recommendation.Id, out _).Should().BeTrue();
    }

    #endregion

    #region OptimizationAction Tests

    [Fact]
    public void OptimizationAction_Properties_CanBeSet()
    {
        // Arrange & Act
        var action = new OptimizationAction
        {
            Description = "Resize virtual machine to smaller SKU",
            Type = ActionType.Resize,
            IsAutomatable = true,
            AutomationScript = "az vm resize --resource-group rg --name vm1 --size Standard_D2s_v3"
        };

        // Assert
        action.Description.Should().Contain("Resize");
        action.Type.Should().Be(ActionType.Resize);
        action.IsAutomatable.Should().BeTrue();
        action.AutomationScript.Should().Contain("az vm resize");
    }

    [Fact]
    public void OptimizationAction_Parameters_DefaultsToEmpty()
    {
        // Arrange & Act
        var action = new OptimizationAction();

        // Assert
        action.Parameters.Should().NotBeNull();
    }

    [Fact]
    public void OptimizationAction_Prerequisites_DefaultsToEmpty()
    {
        // Arrange & Act
        var action = new OptimizationAction();

        // Assert
        action.Prerequisites.Should().NotBeNull();
        action.Prerequisites.Should().BeEmpty();
    }

    #endregion

    #region ResourceUsagePattern Tests

    [Fact]
    public void ResourceUsagePattern_Properties_CanBeSet()
    {
        // Arrange & Act
        var pattern = new ResourceUsagePattern
        {
            ResourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1",
            MetricName = "Percentage CPU",
            AverageUsage = 25.5,
            PeakUsage = 75.0,
            MinUsage = 5.0,
            StandardDeviation = 15.2,
            Pattern = UsagePattern.Periodic
        };

        // Assert
        pattern.ResourceId.Should().Contain("virtualMachines");
        pattern.MetricName.Should().Be("Percentage CPU");
        pattern.AverageUsage.Should().Be(25.5);
        pattern.PeakUsage.Should().Be(75.0);
        pattern.MinUsage.Should().Be(5.0);
        pattern.Pattern.Should().Be(UsagePattern.Periodic);
    }

    [Fact]
    public void ResourceUsagePattern_DataPoints_DefaultsToEmpty()
    {
        // Arrange & Act
        var pattern = new ResourceUsagePattern();

        // Assert
        pattern.DataPoints.Should().NotBeNull();
        pattern.DataPoints.Should().BeEmpty();
    }

    [Fact]
    public void ResourceUsagePattern_TimeBasedPatterns_DefaultsToEmpty()
    {
        // Arrange & Act
        var pattern = new ResourceUsagePattern();

        // Assert
        pattern.TimeBasedPatterns.Should().NotBeNull();
    }

    #endregion

    #region UsageDataPoint Tests

    [Fact]
    public void UsageDataPoint_Properties_CanBeSet()
    {
        // Arrange & Act
        var dataPoint = new UsageDataPoint
        {
            Timestamp = DateTime.UtcNow,
            Value = 45.5,
            Unit = "Percent"
        };

        // Assert
        dataPoint.Value.Should().Be(45.5);
        dataPoint.Unit.Should().Be("Percent");
    }

    #endregion

    #region CostAnalysisResult Tests

    [Fact]
    public void CostAnalysisResult_Properties_CanBeSet()
    {
        // Arrange & Act
        var result = new CostAnalysisResult
        {
            SubscriptionId = "test-subscription-id",
            TotalMonthlyCost = 5000.00m,
            PotentialMonthlySavings = 750.00m,
            TotalRecommendations = 5
        };

        // Assert
        result.SubscriptionId.Should().Be("test-subscription-id");
        result.TotalMonthlyCost.Should().Be(5000.00m);
        result.PotentialMonthlySavings.Should().Be(750.00m);
        result.TotalRecommendations.Should().Be(5);
    }

    [Fact]
    public void CostAnalysisResult_AnalysisDate_HasDefaultValue()
    {
        // Arrange & Act
        var result = new CostAnalysisResult();

        // Assert
        result.AnalysisDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void CostAnalysisResult_CostByService_DefaultsToEmpty()
    {
        // Arrange & Act
        var result = new CostAnalysisResult();

        // Assert
        result.CostByService.Should().NotBeNull();
    }

    [Fact]
    public void CostAnalysisResult_CostByResourceGroup_DefaultsToEmpty()
    {
        // Arrange & Act
        var result = new CostAnalysisResult();

        // Assert
        result.CostByResourceGroup.Should().NotBeNull();
    }

    [Fact]
    public void CostAnalysisResult_Recommendations_DefaultsToEmpty()
    {
        // Arrange & Act
        var result = new CostAnalysisResult();

        // Assert
        result.Recommendations.Should().NotBeNull();
        result.Recommendations.Should().BeEmpty();
    }

    #endregion

    #region CostTrend Tests

    [Fact]
    public void CostTrend_Properties_CanBeSet()
    {
        // Arrange & Act
        var trend = new CostTrend
        {
            Date = DateTime.UtcNow,
            Cost = 100.00m,
            DailyCost = 33.33m,
            CumulativeMonthlyCost = 2500.00m,
            ResourceCount = 15
        };

        // Assert
        trend.Cost.Should().Be(100.00m);
        trend.DailyCost.Should().Be(33.33m);
        trend.CumulativeMonthlyCost.Should().Be(2500.00m);
        trend.ResourceCount.Should().Be(15);
    }

    [Fact]
    public void CostTrend_ServiceCosts_DefaultsToEmpty()
    {
        // Arrange & Act
        var trend = new CostTrend();

        // Assert
        trend.ServiceCosts.Should().NotBeNull();
    }

    [Fact]
    public void CostTrend_CostDrivers_DefaultsToEmpty()
    {
        // Arrange & Act
        var trend = new CostTrend();

        // Assert
        trend.CostDrivers.Should().NotBeNull();
        trend.CostDrivers.Should().BeEmpty();
    }

    #endregion

    #region CostMonitoringDashboard Tests

    [Fact]
    public void CostMonitoringDashboard_AllPropertiesInitialized()
    {
        // Arrange & Act
        var dashboard = new CostMonitoringDashboard();

        // Assert
        dashboard.Metadata.Should().NotBeNull();
        dashboard.Summary.Should().NotBeNull();
        dashboard.Trends.Should().NotBeNull();
        dashboard.BudgetAlerts.Should().NotBeNull();
        dashboard.Recommendations.Should().NotBeNull();
        dashboard.Anomalies.Should().NotBeNull();
        dashboard.ResourceBreakdown.Should().NotBeNull();
        dashboard.ServiceBreakdown.Should().NotBeNull();
        dashboard.Forecast.Should().NotBeNull();
        dashboard.Budgets.Should().NotBeNull();
        dashboard.Governance.Should().NotBeNull();
    }

    #endregion

    #region CostSummary Tests

    [Fact]
    public void CostSummary_Properties_CanBeSet()
    {
        // Arrange & Act
        var summary = new CostSummary
        {
            CurrentMonthSpend = 5000.00m,
            PreviousMonthSpend = 4500.00m,
            MonthOverMonthChange = 500.00m,
            MonthOverMonthChangePercent = 11.11m,
            YearToDateSpend = 45000.00m,
            ProjectedMonthlySpend = 5200.00m,
            AverageDailyCost = 166.67m,
            PotentialSavings = 750.00m,
            OptimizationOpportunities = 5
        };

        // Assert
        summary.CurrentMonthSpend.Should().Be(5000.00m);
        summary.PreviousMonthSpend.Should().Be(4500.00m);
        summary.MonthOverMonthChangePercent.Should().Be(11.11m);
        summary.PotentialSavings.Should().Be(750.00m);
        summary.OptimizationOpportunities.Should().Be(5);
    }

    [Theory]
    [InlineData(CostTrendDirection.Increasing)]
    [InlineData(CostTrendDirection.Decreasing)]
    [InlineData(CostTrendDirection.Stable)]
    public void CostSummary_TrendDirection_CanBeSet(CostTrendDirection direction)
    {
        // Arrange & Act
        var summary = new CostSummary { TrendDirection = direction };

        // Assert
        summary.TrendDirection.Should().Be(direction);
    }

    #endregion

    #region BudgetStatus Tests

    [Fact]
    public void BudgetStatus_Properties_CanBeSet()
    {
        // Arrange & Act
        var budget = new BudgetStatus
        {
            BudgetId = "budget-001",
            Name = "Development Budget",
            Amount = 10000.00m,
            CurrentSpend = 7500.00m,
            RemainingBudget = 2500.00m,
            UtilizationPercentage = 75.00m
        };

        // Assert
        budget.BudgetId.Should().Be("budget-001");
        budget.Name.Should().Be("Development Budget");
        budget.Amount.Should().Be(10000.00m);
        budget.CurrentSpend.Should().Be(7500.00m);
        budget.UtilizationPercentage.Should().Be(75.00m);
    }

    [Theory]
    [InlineData(BudgetHealthStatus.Healthy)]
    [InlineData(BudgetHealthStatus.Warning)]
    [InlineData(BudgetHealthStatus.Critical)]
    [InlineData(BudgetHealthStatus.Exceeded)]
    public void BudgetStatus_HealthStatus_CanBeSet(BudgetHealthStatus status)
    {
        // Arrange & Act
        var budget = new BudgetStatus { HealthStatus = status };

        // Assert
        budget.HealthStatus.Should().Be(status);
    }

    [Fact]
    public void BudgetStatus_Scope_DefaultsToNew()
    {
        // Arrange & Act
        var budget = new BudgetStatus();

        // Assert
        budget.Scope.Should().NotBeNull();
    }

    [Fact]
    public void BudgetStatus_Thresholds_DefaultsToEmpty()
    {
        // Arrange & Act
        var budget = new BudgetStatus();

        // Assert
        budget.Thresholds.Should().NotBeNull();
        budget.Thresholds.Should().BeEmpty();
    }

    #endregion

    #region BudgetAlert Tests

    [Fact]
    public void BudgetAlert_Properties_CanBeSet()
    {
        // Arrange & Act
        var alert = new BudgetAlert
        {
            BudgetId = "budget-001",
            BudgetName = "Development Budget",
            AlertType = BudgetAlertType.Threshold,
            Severity = BudgetAlertSeverity.Warning,
            ThresholdPercentage = 80.00m,
            CurrentPercentage = 85.00m,
            BudgetAmount = 10000.00m,
            CurrentSpend = 8500.00m,
            Message = "Budget usage at 85% of threshold"
        };

        // Assert
        alert.BudgetName.Should().Be("Development Budget");
        alert.AlertType.Should().Be(BudgetAlertType.Threshold);
        alert.Severity.Should().Be(BudgetAlertSeverity.Warning);
        alert.ThresholdPercentage.Should().Be(80.00m);
        alert.CurrentPercentage.Should().Be(85.00m);
    }

    [Fact]
    public void BudgetAlert_AlertId_IsGeneratedByDefault()
    {
        // Arrange & Act
        var alert = new BudgetAlert();

        // Assert
        alert.AlertId.Should().NotBeNullOrEmpty();
        Guid.TryParse(alert.AlertId, out _).Should().BeTrue();
    }

    [Fact]
    public void BudgetAlert_IsActive_DefaultsToTrue()
    {
        // Arrange & Act
        var alert = new BudgetAlert();

        // Assert
        alert.IsActive.Should().BeTrue();
    }

    #endregion

    #region CostAnomaly Tests

    [Fact]
    public void CostAnomaly_Properties_CanBeSet()
    {
        // Arrange & Act
        var anomaly = new CostAnomaly
        {
            Title = "Unexpected cost spike",
            Description = "Compute costs increased by 150%",
            Severity = AnomalySeverity.High,
            Type = AnomalyType.SpikeCost,
            ExpectedCost = 1000.00m,
            ActualCost = 2500.00m,
            CostDifference = 1500.00m,
            PercentageDeviation = 150.00m,
            Status = AnomalyStatus.Open
        };

        // Assert
        anomaly.Title.Should().Be("Unexpected cost spike");
        anomaly.Severity.Should().Be(AnomalySeverity.High);
        anomaly.Type.Should().Be(AnomalyType.SpikeCost);
        anomaly.CostDifference.Should().Be(1500.00m);
        anomaly.PercentageDeviation.Should().Be(150.00m);
    }

    [Fact]
    public void CostAnomaly_AnomalyId_IsGeneratedByDefault()
    {
        // Arrange & Act
        var anomaly = new CostAnomaly();

        // Assert
        anomaly.AnomalyId.Should().NotBeNullOrEmpty();
        Guid.TryParse(anomaly.AnomalyId, out _).Should().BeTrue();
    }

    [Fact]
    public void CostAnomaly_AffectedResources_DefaultsToEmpty()
    {
        // Arrange & Act
        var anomaly = new CostAnomaly();

        // Assert
        anomaly.AffectedResources.Should().NotBeNull();
        anomaly.AffectedResources.Should().BeEmpty();
    }

    [Fact]
    public void CostAnomaly_PossibleCauses_DefaultsToEmpty()
    {
        // Arrange & Act
        var anomaly = new CostAnomaly();

        // Assert
        anomaly.PossibleCauses.Should().NotBeNull();
        anomaly.PossibleCauses.Should().BeEmpty();
    }

    #endregion

    #region CostForecast Tests

    [Fact]
    public void CostForecast_Properties_CanBeSet()
    {
        // Arrange & Act
        var forecast = new CostForecast
        {
            ProjectedMonthEndCost = 5500.00m,
            ProjectedQuarterEndCost = 16500.00m,
            ProjectedYearEndCost = 66000.00m,
            ConfidenceLevel = 0.85
        };

        // Assert
        forecast.ProjectedMonthEndCost.Should().Be(5500.00m);
        forecast.ProjectedQuarterEndCost.Should().Be(16500.00m);
        forecast.ProjectedYearEndCost.Should().Be(66000.00m);
        forecast.ConfidenceLevel.Should().Be(0.85);
    }

    [Fact]
    public void CostForecast_Projections_DefaultsToEmpty()
    {
        // Arrange & Act
        var forecast = new CostForecast();

        // Assert
        forecast.Projections.Should().NotBeNull();
        forecast.Projections.Should().BeEmpty();
    }

    [Fact]
    public void CostForecast_HistoricalAccuracy_IsInitialized()
    {
        // Arrange & Act
        var forecast = new CostForecast();

        // Assert
        forecast.HistoricalAccuracy.Should().NotBeNull();
    }

    #endregion

    #region ResourceCostBreakdown Tests

    [Fact]
    public void ResourceCostBreakdown_Properties_CanBeSet()
    {
        // Arrange & Act
        var breakdown = new ResourceCostBreakdown
        {
            SubscriptionId = "test-subscription",
            ResourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1",
            ResourceName = "vm1",
            ResourceType = "Microsoft.Compute/virtualMachines",
            ResourceGroup = "rg",
            Location = "eastus",
            DailyCost = 50.00m,
            MonthlyCost = 1500.00m,
            YearToDateCost = 12000.00m
        };

        // Assert
        breakdown.ResourceName.Should().Be("vm1");
        breakdown.ResourceType.Should().Be("Microsoft.Compute/virtualMachines");
        breakdown.Location.Should().Be("eastus");
        breakdown.MonthlyCost.Should().Be(1500.00m);
    }

    [Fact]
    public void ResourceCostBreakdown_Tags_DefaultsToEmpty()
    {
        // Arrange & Act
        var breakdown = new ResourceCostBreakdown();

        // Assert
        breakdown.Tags.Should().NotBeNull();
    }

    #endregion

    #region UtilizationMetrics Tests

    [Fact]
    public void UtilizationMetrics_Properties_CanBeSet()
    {
        // Arrange & Act
        var metrics = new UtilizationMetrics
        {
            AverageCpuUtilization = 25.5,
            MaxCpuUtilization = 75.0,
            AverageMemoryUtilization = 40.0,
            MaxMemoryUtilization = 85.0,
            AverageNetworkUtilization = 15.0,
            AverageStorageUtilization = 50.0,
            SampleCount = 720,
            ObservationPeriod = TimeSpan.FromDays(30)
        };

        // Assert
        metrics.AverageCpuUtilization.Should().Be(25.5);
        metrics.MaxCpuUtilization.Should().Be(75.0);
        metrics.AverageMemoryUtilization.Should().Be(40.0);
        metrics.SampleCount.Should().Be(720);
        metrics.ObservationPeriod.Should().Be(TimeSpan.FromDays(30));
    }

    #endregion

    #region RightsizingRecommendation Tests

    [Fact]
    public void RightsizingRecommendation_Properties_CanBeSet()
    {
        // Arrange & Act
        var recommendation = new RightsizingRecommendation
        {
            ResourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1",
            ResourceName = "vm1",
            ResourceType = "Microsoft.Compute/virtualMachines",
            CurrentSku = "Standard_D4s_v3",
            RecommendedSku = "Standard_D2s_v3",
            CurrentMonthlyCost = 200.00m,
            RecommendedMonthlyCost = 100.00m,
            MonthlySavings = 100.00m,
            Confidence = RightsizingConfidence.High,
            Reason = RightsizingReason.Underutilized
        };

        // Assert
        recommendation.CurrentSku.Should().Be("Standard_D4s_v3");
        recommendation.RecommendedSku.Should().Be("Standard_D2s_v3");
        recommendation.MonthlySavings.Should().Be(100.00m);
        recommendation.Confidence.Should().Be(RightsizingConfidence.High);
        recommendation.Reason.Should().Be(RightsizingReason.Underutilized);
    }

    [Fact]
    public void RightsizingRecommendation_CurrentUtilization_IsInitialized()
    {
        // Arrange & Act
        var recommendation = new RightsizingRecommendation();

        // Assert
        recommendation.CurrentUtilization.Should().NotBeNull();
    }

    [Fact]
    public void RightsizingRecommendation_ProjectedUtilization_IsInitialized()
    {
        // Arrange & Act
        var recommendation = new RightsizingRecommendation();

        // Assert
        recommendation.ProjectedUtilization.Should().NotBeNull();
    }

    #endregion
}
