using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Cost;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Azure;
using Platform.Engineering.Copilot.Core.Models.Cost;
using Platform.Engineering.Copilot.Core.Models.CostOptimization.Analysis;
using Platform.Engineering.Copilot.Core.Services.Azure.Cost;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for CostOptimizationEngine
/// Tests cost analysis, recommendations generation, usage pattern analysis, and savings calculations
/// </summary>
public class CostOptimizationEngineTests
{
    private readonly Mock<ILogger<CostOptimizationEngine>> _loggerMock;
    private readonly Mock<IAzureMetricsService> _metricsServiceMock;
    private readonly Mock<IAzureCostManagementService> _costServiceMock;
    private readonly Mock<IAzureResourceService> _resourceServiceMock;
    private readonly CostOptimizationEngine _engine;

    public CostOptimizationEngineTests()
    {
        _loggerMock = new Mock<ILogger<CostOptimizationEngine>>();
        _metricsServiceMock = new Mock<IAzureMetricsService>();
        _costServiceMock = new Mock<IAzureCostManagementService>();
        _resourceServiceMock = new Mock<IAzureResourceService>();

        _engine = new CostOptimizationEngine(
            _loggerMock.Object,
            _metricsServiceMock.Object,
            _costServiceMock.Object,
            _resourceServiceMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange & Act
        var engine = new CostOptimizationEngine(
            _loggerMock.Object,
            _metricsServiceMock.Object,
            _costServiceMock.Object,
            _resourceServiceMock.Object);

        // Assert
        engine.Should().NotBeNull();
    }

    #endregion

    #region AnalyzeSubscriptionAsync Tests

    [Fact]
    public async Task AnalyzeSubscriptionAsync_WithValidSubscription_ReturnsResult()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var costData = new CostData
        {
            TotalCost = 5000.00m,
            ServiceCosts = new Dictionary<string, decimal> { ["Compute"] = 3000.00m, ["Storage"] = 2000.00m },
            ResourceGroupCosts = new Dictionary<string, decimal> { ["rg-prod"] = 4000.00m, ["rg-dev"] = 1000.00m }
        };

        _costServiceMock
            .Setup(x => x.GetCurrentMonthCostsAsync(subscriptionId))
            .ReturnsAsync(costData);

        _resourceServiceMock
            .Setup(x => x.ListAllResourcesAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AzureResource>());

        // Act
        var result = await _engine.AnalyzeSubscriptionAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
        result.TotalMonthlyCost.Should().Be(5000.00m);
        result.CostByService.Should().ContainKey("Compute");
        result.CostByResourceGroup.Should().ContainKey("rg-prod");
    }

    [Fact]
    public async Task AnalyzeSubscriptionAsync_WithResources_GeneratesRecommendations()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var costData = new CostData
        {
            TotalCost = 5000.00m,
            ServiceCosts = new Dictionary<string, decimal>(),
            ResourceGroupCosts = new Dictionary<string, decimal>()
        };

        var resources = new List<AzureResource>
        {
            new AzureResource
            {
                Id = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1",
                Name = "vm1",
                Type = "Microsoft.Compute/virtualMachines",
                ResourceGroup = "rg",
                Location = "eastus"
            }
        };

        _costServiceMock
            .Setup(x => x.GetCurrentMonthCostsAsync(subscriptionId))
            .ReturnsAsync(costData);

        _resourceServiceMock
            .Setup(x => x.ListAllResourcesAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resources);

        _resourceServiceMock
            .Setup(x => x.GetResourceAsync(It.IsAny<string>()))
            .ReturnsAsync(resources[0]);

        _metricsServiceMock
            .Setup(x => x.GetMetricsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MetricDataPoint>());

        _costServiceMock
            .Setup(x => x.GetResourceMonthlyCostAsync(It.IsAny<string>()))
            .ReturnsAsync(200.00m);

        // Act
        var result = await _engine.AnalyzeSubscriptionAsync(subscriptionId);

        // Assert
        result.Should().NotBeNull();
        result.SubscriptionId.Should().Be(subscriptionId);
    }

    [Fact]
    public async Task AnalyzeSubscriptionAsync_SetsAnalysisDate()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";
        var beforeAnalysis = DateTime.UtcNow;

        _costServiceMock
            .Setup(x => x.GetCurrentMonthCostsAsync(subscriptionId))
            .ReturnsAsync(new CostData());

        _resourceServiceMock
            .Setup(x => x.ListAllResourcesAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AzureResource>());

        // Act
        var result = await _engine.AnalyzeSubscriptionAsync(subscriptionId);

        // Assert
        result.AnalysisDate.Should().BeOnOrAfter(beforeAnalysis);
        result.AnalysisDate.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    #endregion

    #region GenerateRecommendationsAsync Tests

    [Fact]
    public async Task GenerateRecommendationsAsync_WithNonExistentResource_ReturnsEmptyList()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/nonexistent";

        _resourceServiceMock
            .Setup(x => x.GetResourceAsync(resourceId))
            .ReturnsAsync((AzureResource?)null);

        // Act
        var recommendations = await _engine.GenerateRecommendationsAsync(resourceId);

        // Assert
        recommendations.Should().NotBeNull();
        recommendations.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_WithValidVm_AnalyzesResource()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1";
        var resource = new AzureResource
        {
            Id = resourceId,
            Name = "vm1",
            Type = "microsoft.compute/virtualmachines",
            ResourceGroup = "rg",
            Location = "eastus"
        };

        _resourceServiceMock
            .Setup(x => x.GetResourceAsync(resourceId))
            .ReturnsAsync(resource);

        _metricsServiceMock
            .Setup(x => x.GetMetricsAsync(resourceId, "Percentage CPU", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MetricDataPoint>
            {
                new MetricDataPoint { Timestamp = DateTime.UtcNow, Value = 5.0, Unit = "Percent" }
            });

        _costServiceMock
            .Setup(x => x.GetResourceMonthlyCostAsync(resourceId))
            .ReturnsAsync(200.00m);

        // Act
        var recommendations = await _engine.GenerateRecommendationsAsync(resourceId);

        // Assert
        recommendations.Should().NotBeNull();
        _metricsServiceMock.Verify(x => x.GetMetricsAsync(resourceId, "Percentage CPU", It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_WithStorageAccount_AnalyzesResource()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/storage1";
        var resource = new AzureResource
        {
            Id = resourceId,
            Name = "storage1",
            Type = "microsoft.storage/storageaccounts",
            ResourceGroup = "rg",
            Location = "eastus"
        };

        _resourceServiceMock
            .Setup(x => x.GetResourceAsync(resourceId))
            .ReturnsAsync(resource);

        _costServiceMock
            .Setup(x => x.GetResourceMonthlyCostAsync(resourceId))
            .ReturnsAsync(50.00m);

        // Act
        var recommendations = await _engine.GenerateRecommendationsAsync(resourceId);

        // Assert
        recommendations.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_WithSqlDatabase_AnalyzesResource()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Sql/servers/sqlserver/databases/db1";
        var resource = new AzureResource
        {
            Id = resourceId,
            Name = "db1",
            Type = "microsoft.sql/servers/databases",
            ResourceGroup = "rg",
            Location = "eastus"
        };

        _resourceServiceMock
            .Setup(x => x.GetResourceAsync(resourceId))
            .ReturnsAsync(resource);

        _metricsServiceMock
            .Setup(x => x.GetMetricsAsync(resourceId, "dtu_consumption_percent", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MetricDataPoint>());

        _costServiceMock
            .Setup(x => x.GetResourceMonthlyCostAsync(resourceId))
            .ReturnsAsync(100.00m);

        // Act
        var recommendations = await _engine.GenerateRecommendationsAsync(resourceId);

        // Assert
        recommendations.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_WithAppService_AnalyzesResource()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Web/sites/webapp1";
        var resource = new AzureResource
        {
            Id = resourceId,
            Name = "webapp1",
            Type = "microsoft.web/sites",
            ResourceGroup = "rg",
            Location = "eastus"
        };

        _resourceServiceMock
            .Setup(x => x.GetResourceAsync(resourceId))
            .ReturnsAsync(resource);

        _metricsServiceMock
            .Setup(x => x.GetMetricsAsync(resourceId, "Requests", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MetricDataPoint>());

        _costServiceMock
            .Setup(x => x.GetResourceMonthlyCostAsync(resourceId))
            .ReturnsAsync(75.00m);

        // Act
        var recommendations = await _engine.GenerateRecommendationsAsync(resourceId);

        // Assert
        recommendations.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_WithAksCluster_AnalyzesResource()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/aks1";
        var resource = new AzureResource
        {
            Id = resourceId,
            Name = "aks1",
            Type = "microsoft.containerservice/managedclusters",
            ResourceGroup = "rg",
            Location = "eastus"
        };

        _resourceServiceMock
            .Setup(x => x.GetResourceAsync(resourceId))
            .ReturnsAsync(resource);

        _metricsServiceMock
            .Setup(x => x.GetMetricsAsync(resourceId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MetricDataPoint>());

        _costServiceMock
            .Setup(x => x.GetResourceMonthlyCostAsync(resourceId))
            .ReturnsAsync(500.00m);

        // Act
        var recommendations = await _engine.GenerateRecommendationsAsync(resourceId);

        // Assert
        recommendations.Should().NotBeNull();
    }

    #endregion

    #region AnalyzeUsagePatternsAsync Tests

    [Fact]
    public async Task AnalyzeUsagePatternsAsync_WithValidData_ReturnsPattern()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1";
        var metricName = "Percentage CPU";
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        var metrics = new List<MetricDataPoint>
        {
            new MetricDataPoint { Timestamp = startDate.AddDays(1), Value = 25.0, Unit = "Percent" },
            new MetricDataPoint { Timestamp = startDate.AddDays(2), Value = 30.0, Unit = "Percent" },
            new MetricDataPoint { Timestamp = startDate.AddDays(3), Value = 20.0, Unit = "Percent" },
            new MetricDataPoint { Timestamp = startDate.AddDays(4), Value = 35.0, Unit = "Percent" },
            new MetricDataPoint { Timestamp = startDate.AddDays(5), Value = 28.0, Unit = "Percent" }
        };

        _metricsServiceMock
            .Setup(x => x.GetMetricsAsync(resourceId, metricName, startDate, endDate))
            .ReturnsAsync(metrics);

        // Act
        var pattern = await _engine.AnalyzeUsagePatternsAsync(resourceId, metricName, startDate, endDate);

        // Assert
        pattern.Should().NotBeNull();
        pattern.ResourceId.Should().Be(resourceId);
        pattern.MetricName.Should().Be(metricName);
        pattern.DataPoints.Should().HaveCount(5);
        pattern.AverageUsage.Should().BeApproximately(27.6, 0.1);
        pattern.PeakUsage.Should().Be(35.0);
        pattern.MinUsage.Should().Be(20.0);
    }

    [Fact]
    public async Task AnalyzeUsagePatternsAsync_WithEmptyData_ReturnsEmptyPattern()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1";
        var metricName = "Percentage CPU";
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        _metricsServiceMock
            .Setup(x => x.GetMetricsAsync(resourceId, metricName, startDate, endDate))
            .ReturnsAsync(new List<MetricDataPoint>());

        // Act
        var pattern = await _engine.AnalyzeUsagePatternsAsync(resourceId, metricName, startDate, endDate);

        // Assert
        pattern.Should().NotBeNull();
        pattern.DataPoints.Should().BeEmpty();
        pattern.AverageUsage.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeUsagePatternsAsync_CalculatesStandardDeviation()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1";
        var metricName = "Percentage CPU";
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        var metrics = new List<MetricDataPoint>
        {
            new MetricDataPoint { Timestamp = startDate.AddDays(1), Value = 10.0, Unit = "Percent" },
            new MetricDataPoint { Timestamp = startDate.AddDays(2), Value = 20.0, Unit = "Percent" },
            new MetricDataPoint { Timestamp = startDate.AddDays(3), Value = 30.0, Unit = "Percent" },
            new MetricDataPoint { Timestamp = startDate.AddDays(4), Value = 40.0, Unit = "Percent" },
            new MetricDataPoint { Timestamp = startDate.AddDays(5), Value = 50.0, Unit = "Percent" }
        };

        _metricsServiceMock
            .Setup(x => x.GetMetricsAsync(resourceId, metricName, startDate, endDate))
            .ReturnsAsync(metrics);

        // Act
        var pattern = await _engine.AnalyzeUsagePatternsAsync(resourceId, metricName, startDate, endDate);

        // Assert
        pattern.StandardDeviation.Should().BeGreaterThan(0);
    }

    #endregion

    #region ApplyRecommendationAsync Tests

    [Fact]
    public async Task ApplyRecommendationAsync_WithValidRecommendation_ReturnsTrue()
    {
        // Arrange
        var recommendationId = "recommendation-001";
        var parameters = new Dictionary<string, object>
        {
            ["targetSize"] = "Standard_B2s"
        };

        // Act
        var result = await _engine.ApplyRecommendationAsync(recommendationId, parameters);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyRecommendationAsync_WithNullParameters_ReturnsTrue()
    {
        // Arrange
        var recommendationId = "recommendation-001";

        // Act
        var result = await _engine.ApplyRecommendationAsync(recommendationId, null);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region CalculateSavingsPotentialAsync Tests

    [Fact]
    public async Task CalculateSavingsPotentialAsync_WithRecommendations_CalculatesByType()
    {
        // Arrange
        var recommendations = new List<CostOptimizationRecommendation>
        {
            new CostOptimizationRecommendation
            {
                Type = OptimizationType.RightSizing,
                EstimatedMonthlySavings = 100.00m
            },
            new CostOptimizationRecommendation
            {
                Type = OptimizationType.RightSizing,
                EstimatedMonthlySavings = 150.00m
            },
            new CostOptimizationRecommendation
            {
                Type = OptimizationType.UnusedResources,
                EstimatedMonthlySavings = 200.00m
            }
        };

        // Act
        var result = await _engine.CalculateSavingsPotentialAsync(recommendations);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("RightSizing");
        result["RightSizing"].Should().Be(250.00m);
        result.Should().ContainKey("UnusedResources");
        result["UnusedResources"].Should().Be(200.00m);
    }

    [Fact]
    public async Task CalculateSavingsPotentialAsync_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var recommendations = new List<CostOptimizationRecommendation>();

        // Act
        var result = await _engine.CalculateSavingsPotentialAsync(recommendations);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateSavingsPotentialAsync_GroupsAllOptimizationTypes()
    {
        // Arrange
        var recommendations = new List<CostOptimizationRecommendation>
        {
            new CostOptimizationRecommendation { Type = OptimizationType.RightSizing, EstimatedMonthlySavings = 100.00m },
            new CostOptimizationRecommendation { Type = OptimizationType.UnusedResources, EstimatedMonthlySavings = 50.00m },
            new CostOptimizationRecommendation { Type = OptimizationType.ReservedInstances, EstimatedMonthlySavings = 300.00m },
            new CostOptimizationRecommendation { Type = OptimizationType.StorageOptimization, EstimatedMonthlySavings = 25.00m },
            new CostOptimizationRecommendation { Type = OptimizationType.ScheduledShutdown, EstimatedMonthlySavings = 75.00m }
        };

        // Act
        var result = await _engine.CalculateSavingsPotentialAsync(recommendations);

        // Assert
        result.Should().HaveCount(5);
        result.Values.Sum().Should().Be(550.00m);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task AnalyzeSubscriptionAsync_WhenCostServiceThrows_PropagatesException()
    {
        // Arrange
        var subscriptionId = "test-subscription-id";

        _costServiceMock
            .Setup(x => x.GetCurrentMonthCostsAsync(subscriptionId))
            .ThrowsAsync(new Exception("Cost service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _engine.AnalyzeSubscriptionAsync(subscriptionId));
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_WhenResourceServiceThrows_ReturnsEmptyList()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1";

        _resourceServiceMock
            .Setup(x => x.GetResourceAsync(resourceId))
            .ThrowsAsync(new Exception("Resource service unavailable"));

        // Act
        var recommendations = await _engine.GenerateRecommendationsAsync(resourceId);

        // Assert
        recommendations.Should().NotBeNull();
        recommendations.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeUsagePatternsAsync_WhenMetricsServiceThrows_PropagatesException()
    {
        // Arrange
        var resourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1";
        var metricName = "Percentage CPU";
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        _metricsServiceMock
            .Setup(x => x.GetMetricsAsync(resourceId, metricName, startDate, endDate))
            .ThrowsAsync(new Exception("Metrics service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _engine.AnalyzeUsagePatternsAsync(resourceId, metricName, startDate, endDate));
    }

    #endregion

    #region CostData Model Tests

    [Fact]
    public void CostData_Properties_CanBeSet()
    {
        // Arrange & Act
        var costData = new CostData
        {
            TotalCost = 5000.00m,
            ServiceCosts = new Dictionary<string, decimal> { ["Compute"] = 3000.00m },
            ResourceGroupCosts = new Dictionary<string, decimal> { ["rg-prod"] = 4000.00m }
        };

        // Assert
        costData.TotalCost.Should().Be(5000.00m);
        costData.ServiceCosts.Should().ContainKey("Compute");
        costData.ResourceGroupCosts.Should().ContainKey("rg-prod");
    }

    #endregion

    #region MetricDataPoint Model Tests

    [Fact]
    public void MetricDataPoint_Properties_CanBeSet()
    {
        // Arrange & Act
        var dataPoint = new MetricDataPoint
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
}
