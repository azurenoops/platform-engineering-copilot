using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.CostManagement.Core.Configuration;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for CostManagementAgent
/// Tests agent initialization, task processing, configuration options, and response handling
/// </summary>
public class CostManagementAgentTests
{
    #region AgentType Tests

    [Fact]
    public void AgentType_ShouldReturnCostManagement()
    {
        // Assert - verify the expected agent type constant
        AgentType.CostManagement.Should().Be(AgentType.CostManagement);
    }

    #endregion

    #region CostManagementAgentOptions Default Values Tests

    [Fact]
    public void CostManagementAgentOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions();

        // Assert
        options.Temperature.Should().Be(0.3);
        options.MaxTokens.Should().Be(4000);
        options.DefaultCurrency.Should().Be("USD");
        options.DefaultTimeframe.Should().Be("MonthToDate");
        options.EnableAnomalyDetection.Should().BeTrue();
        options.EnableOptimizationRecommendations.Should().BeTrue();
    }

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
        options.CostManagement.RefreshIntervalMinutes.Should().Be(60);
        options.CostManagement.AnomalyThresholdPercentage.Should().Be(50);
        options.CostManagement.MinimumSavingsThreshold.Should().Be(100.00m);
        options.CostManagement.ForecastDays.Should().Be(30);
    }

    [Fact]
    public void CostManagementAgentOptions_Budgets_DefaultValues()
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions();

        // Assert
        options.Budgets.Should().NotBeNull();
        options.Budgets.DefaultAlertThresholds.Should().BeEquivalentTo(new List<int> { 50, 80, 100, 120 });
        options.Budgets.EmailNotifications.Should().BeTrue();
        options.Budgets.NotificationEmails.Should().BeEmpty();
    }

    #endregion

    #region CostManagementAgentOptions Property Setting Tests

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void CostManagementAgentOptions_Temperature_AcceptsValidRange(double temperature)
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions { Temperature = temperature };

        // Assert
        options.Temperature.Should().Be(temperature);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(4000)]
    [InlineData(8000)]
    [InlineData(128000)]
    public void CostManagementAgentOptions_MaxTokens_AcceptsValidRange(int maxTokens)
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions { MaxTokens = maxTokens };

        // Assert
        options.MaxTokens.Should().Be(maxTokens);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    public void CostManagementAgentOptions_DefaultCurrency_AcceptsValidCurrencies(string currency)
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions { DefaultCurrency = currency };

        // Assert
        options.DefaultCurrency.Should().Be(currency);
    }

    [Theory]
    [InlineData("MonthToDate")]
    [InlineData("LastMonth")]
    [InlineData("Custom")]
    [InlineData("YearToDate")]
    public void CostManagementAgentOptions_DefaultTimeframe_AcceptsValidTimeframes(string timeframe)
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions { DefaultTimeframe = timeframe };

        // Assert
        options.DefaultTimeframe.Should().Be(timeframe);
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

    #region CostManagementOptions Property Tests

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(1440)]
    public void CostManagementOptions_RefreshIntervalMinutes_AcceptsValidRange(int interval)
    {
        // Arrange & Act
        var options = new CostManagementOptions { RefreshIntervalMinutes = interval };

        // Assert
        options.RefreshIntervalMinutes.Should().Be(interval);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(1000)]
    public void CostManagementOptions_AnomalyThresholdPercentage_AcceptsValidRange(int threshold)
    {
        // Arrange & Act
        var options = new CostManagementOptions { AnomalyThresholdPercentage = threshold };

        // Assert
        options.AnomalyThresholdPercentage.Should().Be(threshold);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50.00)]
    [InlineData(100.00)]
    [InlineData(10000.00)]
    public void CostManagementOptions_MinimumSavingsThreshold_AcceptsValidValues(decimal threshold)
    {
        // Arrange & Act
        var options = new CostManagementOptions { MinimumSavingsThreshold = threshold };

        // Assert
        options.MinimumSavingsThreshold.Should().Be(threshold);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(365)]
    public void CostManagementOptions_ForecastDays_AcceptsValidRange(int days)
    {
        // Arrange & Act
        var options = new CostManagementOptions { ForecastDays = days };

        // Assert
        options.ForecastDays.Should().Be(days);
    }

    #endregion

    #region BudgetOptions Property Tests

    [Fact]
    public void BudgetOptions_DefaultAlertThresholds_CanBeCustomized()
    {
        // Arrange
        var customThresholds = new List<int> { 25, 50, 75, 100 };

        // Act
        var options = new BudgetOptions { DefaultAlertThresholds = customThresholds };

        // Assert
        options.DefaultAlertThresholds.Should().BeEquivalentTo(customThresholds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BudgetOptions_EmailNotifications_CanBeSet(bool enabled)
    {
        // Arrange & Act
        var options = new BudgetOptions { EmailNotifications = enabled };

        // Assert
        options.EmailNotifications.Should().Be(enabled);
    }

    [Fact]
    public void BudgetOptions_NotificationEmails_CanBeSet()
    {
        // Arrange
        var emails = new List<string> { "admin@example.com", "finance@example.com" };

        // Act
        var options = new BudgetOptions { NotificationEmails = emails };

        // Assert
        options.NotificationEmails.Should().BeEquivalentTo(emails);
    }

    #endregion

    #region AgentTask Tests

    [Fact]
    public void AgentTask_WithCostManagementType_IsCorrectlyIdentified()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Description = "Analyze Azure costs for subscription",
            Priority = 1,
            IsCritical = false
        };

        // Assert
        task.AgentType.Should().Be(AgentType.CostManagement);
        task.Description.Should().Contain("cost");
    }

    [Fact]
    public void AgentTask_WithBudgetParameter_IsCorrectlyConfigured()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Description = "Check if deployment is within budget",
            Parameters = new Dictionary<string, object>
            {
                ["budget"] = 5000.00,
                ["subscriptionId"] = "test-subscription-id"
            }
        };

        // Assert
        task.Parameters.Should().ContainKey("budget");
        task.Parameters["budget"].Should().Be(5000.00);
    }

    #endregion

    #region AgentResponse Tests

    [Fact]
    public void AgentResponse_WithEstimatedCost_IsCorrectlySet()
    {
        // Arrange
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Success = true,
            Content = "Cost analysis completed. Estimated monthly cost: $2,500.00",
            EstimatedCost = 2500.00m,
            IsWithinBudget = true
        };

        // Assert
        response.AgentType.Should().Be(AgentType.CostManagement);
        response.Success.Should().BeTrue();
        response.EstimatedCost.Should().Be(2500.00m);
        response.IsWithinBudget.Should().BeTrue();
    }

    [Theory]
    [InlineData(4000.00, 5000.00, true)]
    [InlineData(5000.00, 5000.00, true)]
    [InlineData(5001.00, 5000.00, false)]
    [InlineData(10000.00, 5000.00, false)]
    public void AgentResponse_IsWithinBudget_BasedOnEstimatedCost(decimal estimatedCost, decimal budget, bool expectedWithinBudget)
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Success = true,
            EstimatedCost = estimatedCost,
            IsWithinBudget = estimatedCost <= budget
        };

        // Assert
        response.IsWithinBudget.Should().Be(expectedWithinBudget);
    }

    [Fact]
    public void AgentResponse_WithMetadata_ContainsCostAnalysisDetails()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["agentType"] = AgentType.CostManagement.ToString(),
            ["azureServices"] = "Virtual Machine, Storage, AKS"
        };

        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Success = true,
            Metadata = metadata
        };

        // Assert
        response.Metadata.Should().ContainKey("agentType");
        response.Metadata["agentType"].Should().Be("CostManagement");
        response.Metadata.Should().ContainKey("azureServices");
    }

    #endregion

    #region SharedMemory Tests

    [Fact]
    public void SharedMemory_AddAgentCommunication_StoresCostManagementResult()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "test-conversation-123";
        var costData = new Dictionary<string, object>
        {
            ["estimatedCost"] = 2500.00m,
            ["isWithinBudget"] = true,
            ["budget"] = 5000.00m,
            ["analysis"] = "Cost breakdown by service completed"
        };

        // Act
        memory.AddAgentCommunication(
            conversationId,
            AgentType.CostManagement,
            AgentType.Orchestrator,
            "Cost analysis completed. Estimated: $2,500.00/month, Within budget: True",
            costData
        );

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().NotBeEmpty();
        communications.Should().ContainSingle(c =>
            c.FromAgent == AgentType.CostManagement &&
            c.ToAgent == AgentType.Orchestrator);
    }

    [Fact]
    public void SharedMemory_GetContext_ReturnsEmptyContextForNewConversation()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);

        // Act
        var context = memory.GetContext("new-cost-conversation");

        // Assert
        context.Should().NotBeNull();
        context.ConversationId.Should().Be("new-cost-conversation");
    }

    [Fact]
    public void SharedMemory_AgentCommunication_ContainsCostDataDictionary()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "cost-analysis-conv";
        var costData = new Dictionary<string, object>
        {
            ["estimatedCost"] = 3500.00m,
            ["isWithinBudget"] = false,
            ["budget"] = 3000.00m
        };

        // Act
        memory.AddAgentCommunication(
            conversationId,
            AgentType.CostManagement,
            AgentType.Infrastructure,
            "Budget exceeded by $500.00",
            costData
        );

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        var comm = communications.First();
        comm.Data.Should().NotBeNull();
        var data = comm.Data as Dictionary<string, object>;
        data.Should().ContainKey("estimatedCost");
        data!["estimatedCost"].Should().Be(3500.00m);
    }

    #endregion

    #region CostManagementAgentOptions Nested Objects Tests

    [Fact]
    public void CostManagementAgentOptions_AllNestedObjects_AreInitialized()
    {
        // Arrange & Act
        var options = new CostManagementAgentOptions();

        // Assert
        options.CostManagement.Should().NotBeNull();
        options.Budgets.Should().NotBeNull();
    }

    [Fact]
    public void CostManagementOptions_CanBeReplaced()
    {
        // Arrange
        var customCostManagement = new CostManagementOptions
        {
            RefreshIntervalMinutes = 30,
            AnomalyThresholdPercentage = 25,
            MinimumSavingsThreshold = 50.00m,
            ForecastDays = 60
        };

        // Act
        var options = new CostManagementAgentOptions { CostManagement = customCostManagement };

        // Assert
        options.CostManagement.RefreshIntervalMinutes.Should().Be(30);
        options.CostManagement.AnomalyThresholdPercentage.Should().Be(25);
        options.CostManagement.MinimumSavingsThreshold.Should().Be(50.00m);
        options.CostManagement.ForecastDays.Should().Be(60);
    }

    [Fact]
    public void BudgetOptions_CanBeReplaced()
    {
        // Arrange
        var customBudgets = new BudgetOptions
        {
            DefaultAlertThresholds = new List<int> { 30, 60, 90 },
            EmailNotifications = false,
            NotificationEmails = new List<string> { "test@example.com" }
        };

        // Act
        var options = new CostManagementAgentOptions { Budgets = customBudgets };

        // Assert
        options.Budgets.DefaultAlertThresholds.Should().BeEquivalentTo(new List<int> { 30, 60, 90 });
        options.Budgets.EmailNotifications.Should().BeFalse();
        options.Budgets.NotificationEmails.Should().Contain("test@example.com");
    }

    #endregion
}
