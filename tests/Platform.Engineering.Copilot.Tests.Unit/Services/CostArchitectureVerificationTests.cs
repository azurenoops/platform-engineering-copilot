using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services.Azure.Cost;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.CostOptimization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

// Type aliases to resolve ambiguities
using CoreCostTrend = Platform.Engineering.Copilot.Core.Models.CostTrend;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Verification tests for cost management architecture refactoring
/// Ensures clean separation between AzureResourceService, CostOptimizationEngine, and AzureCostManagementService
/// </summary>
public class CostArchitectureVerificationTests
{
    private readonly Mock<ILogger<CostOptimizationEngine>> _mockLogger;
    private readonly Mock<IAzureMetricsService> _mockMetricsService;
    private readonly Mock<IAzureCostManagementService> _mockCostService;
    private readonly Mock<IAzureResourceService> _mockResourceService;
    private readonly CostOptimizationEngine _costEngine;

    public CostArchitectureVerificationTests()
    {
        _mockLogger = new Mock<ILogger<CostOptimizationEngine>>();
        _mockMetricsService = new Mock<IAzureMetricsService>();
        _mockCostService = new Mock<IAzureCostManagementService>();
        _mockResourceService = new Mock<IAzureResourceService>();
        
        _costEngine = new CostOptimizationEngine(
            _mockLogger.Object,
            _mockMetricsService.Object,
            _mockCostService.Object,
            _mockResourceService.Object
        );
    }

    #region Architecture Verification Tests

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Refactoring", "CostSeparation")]
    public void AzureResourceService_ShouldNotHave_CostMethods()
    {
        // Arrange
        var azureResourceServiceType = typeof(AzureResourceService);
        
        // Act
        var costMethods = new[]
        {
            "GetSubscriptionCostsAsync",
            "GetResourceGroupCostsAsync",
            "GetBudgetsAsync",
            "GetCostRecommendationsAsync",
            "EstimateVmSavingsAsync",
            "EstimateStorageSavingsAsync",
            "EstimateDatabaseSavingsAsync"
        };

        var foundMethods = costMethods
            .Select(methodName => azureResourceServiceType.GetMethod(methodName))
            .Where(method => method != null)
            .ToList();

        // Assert
        Assert.Empty(foundMethods);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Refactoring", "CostSeparation")]
    public void CostOptimizationEngine_ShouldHave_AnomalyDetectionMethod()
    {
        // Arrange
        var costEngineType = typeof(CostOptimizationEngine);
        
        // Act
        var method = costEngineType.GetMethod("DetectCostAnomaliesAsync");
        
        // Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.Equal(typeof(Task<List<CostAnomaly>>), method.ReturnType);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Refactoring", "CostSeparation")]
    public void CostOptimizationEngine_ShouldHave_ForecastingMethod()
    {
        // Arrange
        var costEngineType = typeof(CostOptimizationEngine);
        
        // Act
        var method = costEngineType.GetMethod("GetCostForecastAsync");
        
        // Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.Equal(typeof(Task<CostForecast>), method.ReturnType);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Refactoring", "CostSeparation")]
    public void CostOptimizationEngine_ShouldHave_DashboardMethod()
    {
        // Arrange
        var costEngineType = typeof(CostOptimizationEngine);
        
        // Act
        var method = costEngineType.GetMethod("GetCostDashboardAsync");
        
        // Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.Equal(typeof(Task<CostMonitoringDashboard>), method.ReturnType);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Refactoring", "CostSeparation")]
    public void ICostOptimizationEngine_Interface_ShouldHave_NewMethods()
    {
        // Arrange
        var interfaceType = typeof(ICostOptimizationEngine);
        
        // Act
        var methods = interfaceType.GetMethods();
        var methodNames = methods.Select(m => m.Name).ToList();
        
        // Assert
        Assert.Contains("DetectCostAnomaliesAsync", methodNames);
        Assert.Contains("GetCostForecastAsync", methodNames);
        Assert.Contains("GetCostDashboardAsync", methodNames);
        Assert.Contains("AnalyzeSubscriptionAsync", methodNames); // Existing
        Assert.Contains("GenerateRecommendationsAsync", methodNames); // Existing
    }

    #endregion

    #region Anomaly Detection Tests

    [Fact]
    [Trait("Category", "AnomalyDetection")]
    public async Task DetectCostAnomaliesAsync_WithNormalCosts_ReturnsNoAnomalies()
    {
        // Arrange
        var subscriptionId = "test-subscription";
        var startDate = DateTimeOffset.UtcNow.AddDays(-30);
        var endDate = DateTimeOffset.UtcNow;
        
        var normalTrends = Enumerable.Range(1, 30)
            .Select(i => new CoreCostTrend
            {
                Date = DateTime.UtcNow.AddDays(-i),
                DailyCost = 100.0m, // Consistent cost
                ServiceCosts = new Dictionary<string, decimal>()
            })
            .ToList();

        _mockCostService
            .Setup(s => s.GetCostTrendsAsync(subscriptionId, It.IsAny<DateTimeOffset>(), endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalTrends);

        // Act
        var anomalies = await _costEngine.DetectCostAnomaliesAsync(subscriptionId, startDate, endDate);

        // Assert
        Assert.NotNull(anomalies);
        Assert.Empty(anomalies);
    }

    [Fact]
    [Trait("Category", "AnomalyDetection")]
    public async Task DetectCostAnomaliesAsync_WithCostSpike_ReturnsAnomaly()
    {
        // Arrange
        var subscriptionId = "test-subscription";
        var startDate = DateTimeOffset.UtcNow.AddDays(-30);
        var endDate = DateTimeOffset.UtcNow;
        
        var trendsWithSpike = new List<CoreCostTrend>();
        for (int i = 1; i <= 30; i++)
        {
            trendsWithSpike.Add(new CoreCostTrend
            {
                Date = DateTime.UtcNow.AddDays(-i),
                DailyCost = i == 1 ? 500.0m : 100.0m, // Spike on day 1
                ServiceCosts = new Dictionary<string, decimal>
                {
                    ["Compute"] = i == 1 ? 400.0m : 80.0m
                }
            });
        }

        _mockCostService
            .Setup(s => s.GetCostTrendsAsync(subscriptionId, It.IsAny<DateTimeOffset>(), endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trendsWithSpike);

        // Act
        var anomalies = await _costEngine.DetectCostAnomaliesAsync(subscriptionId, startDate, endDate);

        // Assert
        Assert.NotNull(anomalies);
        Assert.NotEmpty(anomalies);
        var firstAnomaly = anomalies.First();
        Assert.Equal(AnomalyType.SpikeCost, firstAnomaly.Type);
        Assert.True(firstAnomaly.ActualCost > firstAnomaly.ExpectedCost);
        Assert.Contains("Compute", firstAnomaly.AffectedServices);
    }

    [Fact]
    [Trait("Category", "AnomalyDetection")]
    public async Task DetectCostAnomaliesAsync_WithInsufficientData_ReturnsEmpty()
    {
        // Arrange
        var subscriptionId = "test-subscription";
        var startDate = DateTimeOffset.UtcNow.AddDays(-3);
        var endDate = DateTimeOffset.UtcNow;
        
        var insufficientTrends = new List<CoreCostTrend>
        {
            new CoreCostTrend { Date = DateTime.UtcNow.AddDays(-1), DailyCost = 100.0m, ServiceCosts = new Dictionary<string, decimal>() },
            new CoreCostTrend { Date = DateTime.UtcNow.AddDays(-2), DailyCost = 100.0m, ServiceCosts = new Dictionary<string, decimal>() }
        };

        _mockCostService
            .Setup(s => s.GetCostTrendsAsync(subscriptionId, It.IsAny<DateTimeOffset>(), endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insufficientTrends);

        // Act
        var anomalies = await _costEngine.DetectCostAnomaliesAsync(subscriptionId, startDate, endDate);

        // Assert
        Assert.NotNull(anomalies);
        Assert.Empty(anomalies);
    }

    #endregion

    #region Forecasting Tests

    [Fact]
    [Trait("Category", "Forecasting")]
    public async Task GetCostForecastAsync_WithHistoricalData_ReturnsProjections()
    {
        // Arrange
        var subscriptionId = "test-subscription";
        var forecastDays = 30;
        
        var historicalTrends = Enumerable.Range(1, 30)
            .Select(i => new CoreCostTrend
            {
                Date = DateTime.UtcNow.AddDays(-i),
                DailyCost = 100.0m + (i * 2.0m), // Increasing trend
                ServiceCosts = new Dictionary<string, decimal>()
            })
            .ToList();

        _mockCostService
            .Setup(s => s.GetCostTrendsAsync(subscriptionId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(historicalTrends);

        // Act
        var forecast = await _costEngine.GetCostForecastAsync(subscriptionId, forecastDays);

        // Assert
        Assert.NotNull(forecast);
        Assert.Equal(ForecastMethod.LinearRegression, forecast.Method);
        Assert.Equal(forecastDays, forecast.Projections.Count);
        Assert.True(forecast.ConfidenceLevel > 0);
        Assert.True(forecast.ProjectedMonthEndCost > 0);
        
        // Verify projections have confidence intervals
        foreach (var projection in forecast.Projections)
        {
            Assert.True(projection.LowerBound < projection.ForecastedCost);
            Assert.True(projection.UpperBound > projection.ForecastedCost);
            Assert.True(projection.Confidence > 0 && projection.Confidence <= 1);
        }
        
        // Verify confidence decreases over time
        Assert.True(forecast.Projections[0].Confidence > forecast.Projections[^1].Confidence);
    }

    [Fact]
    [Trait("Category", "Forecasting")]
    public async Task GetCostForecastAsync_WithInsufficientData_ReturnsFallbackForecast()
    {
        // Arrange
        var subscriptionId = "test-subscription";
        var forecastDays = 30;
        
        var insufficientTrends = new List<CoreCostTrend>
        {
            new CoreCostTrend { Date = DateTime.UtcNow.AddDays(-1), DailyCost = 100.0m, ServiceCosts = new Dictionary<string, decimal>() }
        };

        _mockCostService
            .Setup(s => s.GetCostTrendsAsync(subscriptionId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(insufficientTrends);

        // Act
        var forecast = await _costEngine.GetCostForecastAsync(subscriptionId, forecastDays);

        // Assert
        Assert.NotNull(forecast);
        // Should return a forecast but with no projections due to insufficient data
        Assert.Equal(0, forecast.Projections.Count);
    }

    [Fact]
    [Trait("Category", "Forecasting")]
    public async Task GetCostForecastAsync_IncludesAssumptions()
    {
        // Arrange
        var subscriptionId = "test-subscription";
        var forecastDays = 30;
        
        var historicalTrends = Enumerable.Range(1, 30)
            .Select(i => new CoreCostTrend
            {
                Date = DateTime.UtcNow.AddDays(-i),
                DailyCost = 100.0m,
                ServiceCosts = new Dictionary<string, decimal>()
            })
            .ToList();

        _mockCostService
            .Setup(s => s.GetCostTrendsAsync(subscriptionId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(historicalTrends);

        // Act
        var forecast = await _costEngine.GetCostForecastAsync(subscriptionId, forecastDays);

        // Assert
        Assert.NotNull(forecast.Assumptions);
        Assert.NotEmpty(forecast.Assumptions);
        Assert.Contains(forecast.Assumptions, a => a.Category == "Usage");
        Assert.Contains(forecast.Assumptions, a => a.Category == "Infrastructure");
        Assert.Contains(forecast.Assumptions, a => a.Category == "Pricing");
    }

    #endregion

    #region Dashboard Tests

    [Fact]
    [Trait("Category", "Dashboard")]
    public async Task GetCostDashboardAsync_DelegatesToCostService()
    {
        // Arrange
        var subscriptionId = "test-subscription";
        var startDate = DateTimeOffset.UtcNow.AddDays(-30);
        var endDate = DateTimeOffset.UtcNow;
        
        var expectedDashboard = new CostMonitoringDashboard
        {
            Metadata = new CostDashboardMetadata
            {
                GeneratedAt = DateTime.UtcNow,
                SubscriptionsAnalyzed = new List<string> { subscriptionId }
            },
            Summary = new CostSummary
            {
                CurrentMonthSpend = 1000.0m,
                PotentialSavings = 150.0m
            }
        };

        _mockCostService
            .Setup(s => s.GetCostDashboardAsync(subscriptionId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDashboard);

        // Act
        var dashboard = await _costEngine.GetCostDashboardAsync(subscriptionId, startDate, endDate);

        // Assert
        Assert.NotNull(dashboard);
        Assert.Equal(expectedDashboard.Summary.CurrentMonthSpend, dashboard.Summary.CurrentMonthSpend);
        _mockCostService.Verify(
            s => s.GetCostDashboardAsync(subscriptionId, startDate, endDate, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion

    #region Dependency Verification Tests

    [Fact]
    [Trait("Category", "Dependencies")]
    public void CostOptimizationEngine_ShouldDependOn_IAzureCostManagementService()
    {
        // Arrange
        var costEngineType = typeof(CostOptimizationEngine);
        
        // Act
        var constructor = costEngineType.GetConstructors().First();
        var parameters = constructor.GetParameters();
        
        // Assert
        Assert.Contains(parameters, p => p.ParameterType == typeof(IAzureCostManagementService));
    }

    [Fact]
    [Trait("Category", "Dependencies")]
    public void CostOptimizationEngine_ShouldDependOn_IAzureMetricsService()
    {
        // Arrange
        var costEngineType = typeof(CostOptimizationEngine);
        
        // Act
        var constructor = costEngineType.GetConstructors().First();
        var parameters = constructor.GetParameters();
        
        // Assert
        Assert.Contains(parameters, p => p.ParameterType == typeof(IAzureMetricsService));
    }

    [Fact]
    [Trait("Category", "Dependencies")]
    public void CostOptimizationEngine_ShouldDependOn_IAzureResourceService()
    {
        // Arrange
        var costEngineType = typeof(CostOptimizationEngine);
        
        // Act
        var constructor = costEngineType.GetConstructors().First();
        var parameters = constructor.GetParameters();
        
        // Assert
        Assert.Contains(parameters, p => p.ParameterType == typeof(IAzureResourceService));
    }

    #endregion

    #region Integration Tests

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeSubscriptionAsync_WithCostData_GeneratesRecommendations()
    {
        // Arrange
        var subscriptionId = "test-subscription";
        
        var mockCostData = new CostData
        {
            TotalCost = 1000.0m,
            ServiceCosts = new Dictionary<string, decimal>
            {
                ["Compute"] = 500.0m,
                ["Storage"] = 300.0m,
                ["Database"] = 200.0m
            },
            ResourceGroupCosts = new Dictionary<string, decimal>
            {
                ["rg-prod"] = 700.0m,
                ["rg-dev"] = 300.0m
            }
        };

        var mockResources = new List<AzureResource>
        {
            new AzureResource
            {
                Id = "/subscriptions/test/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/vm1",
                Name = "vm1",
                Type = "Microsoft.Compute/virtualMachines",
                ResourceGroup = "rg-prod",
                Location = "eastus"
            }
        };

        _mockCostService
            .Setup(s => s.GetCurrentMonthCostsAsync(subscriptionId))
            .ReturnsAsync(mockCostData);

        _mockResourceService
            .Setup(s => s.ListAllResourcesAsync(subscriptionId))
            .ReturnsAsync(mockResources);

        // Act
        var result = await _costEngine.AnalyzeSubscriptionAsync(subscriptionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(subscriptionId, result.SubscriptionId);
        Assert.Equal(1000.0m, result.TotalMonthlyCost);
        Assert.NotNull(result.CostByService);
        Assert.Equal(3, result.CostByService.Count);
    }

    #endregion
}
