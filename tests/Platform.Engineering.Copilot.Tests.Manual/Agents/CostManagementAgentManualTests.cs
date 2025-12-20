using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual.Agents;

/// <summary>
/// Manual tests for Cost Management Agent via MCP HTTP API.
/// Tests cost analysis, optimization recommendations, budgeting, and forecasting.
/// 
/// Prerequisites:
/// - MCP server running in HTTP mode: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5100
/// - Azure CLI authenticated with Cost Management reader permissions
/// </summary>
public class CostManagementAgentManualTests : McpHttpTestBase
{
    private static readonly string[] AcceptableIntents = { "cost", "costmanagement", "multi_agent", "agent_execution", "orchestrat" };

    public CostManagementAgentManualTests(ITestOutputHelper output) : base(output) { }

    #region Cost Analysis

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task GetCostBreakdown_ByResourceType_ShouldReturnAnalysis()
    {
        // Arrange
        var message = "Show me a cost breakdown by resource type for the last month and identify the top 5 most expensive resources";

        // Act
        var response = await SendChatRequestAsync(message, "cost-breakdown-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Cost analysis structure
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertCostAnalysisStructure(response);
        AssertResponseContains(response, "cost", "resource", "$", "expensive");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task GetCostBreakdown_ByService_ShouldReturnAnalysis()
    {
        // Arrange
        var message = "Show my Azure cost breakdown by service category for the last 90 days";

        // Act
        var response = await SendChatRequestAsync(message, "cost-breakdown-service-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Cost analysis structure
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertCostAnalysisStructure(response);
        AssertResponseContains(response, "service", "cost", "$");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task GetCostBreakdown_ByResourceGroup_ShouldReturnAnalysis()
    {
        // Arrange
        var message = "Show cost analysis grouped by resource group with month-over-month comparison";

        // Act
        var response = await SendChatRequestAsync(message, "cost-breakdown-rg-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Cost analysis structure
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertCostAnalysisStructure(response);
        AssertResponseContains(response, "resource group", "cost", "month");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task GetCostBreakdown_ByTag_ShouldReturnAnalysis()
    {
        // Arrange
        var message = "Show cost breakdown by Environment tag (dev, staging, production)";

        // Act
        var response = await SendChatRequestAsync(message, "cost-breakdown-tag-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Cost analysis structure
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertCostAnalysisStructure(response);
        AssertResponseContains(response, "tag", "environment", "cost");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion

    #region Optimization Recommendations

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task GetOptimizationRecommendations_RightSizing_ShouldReturnSuggestions()
    {
        // Arrange
        var message = "Analyze my Azure resources and provide cost optimization recommendations including right-sizing opportunities and reserved instance suggestions";

        // Act
        var response = await SendChatRequestAsync(message, "cost-optimize-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertCostAnalysisStructure(response);
        AssertResponseContains(response, "optimization", "recommend", "savings", "right-siz", "reserved");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task GetOptimizationRecommendations_UnusedResources_ShouldReturnSuggestions()
    {
        // Arrange
        var message = "Find unused or underutilized resources that could be shut down or deleted to save costs";

        // Act
        var response = await SendChatRequestAsync(message, "cost-optimize-unused-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task GetOptimizationRecommendations_StorageTiers_ShouldReturnSuggestions()
    {
        // Arrange
        var message = "Analyze storage accounts and recommend optimal access tiers based on usage patterns";

        // Act
        var response = await SendChatRequestAsync(message, "cost-optimize-storage-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Budget Management

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task SetupBudget_WithAlerts_ShouldReturnConfiguration()
    {
        // Arrange
        var message = "Help me set up a budget of $10,000 per month with alerts at 50%, 75%, and 90% thresholds for the production resource group";

        // Act
        var response = await SendChatRequestAsync(message, "cost-budget-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task ReviewBudget_CurrentStatus_ShouldReturnStatus()
    {
        // Arrange
        var message = "Show current budget status and spending against configured budgets";

        // Act
        var response = await SendChatRequestAsync(message, "cost-budget-status-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Cost Forecasting

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task ForecastCosts_Next3Months_ShouldReturnProjection()
    {
        // Arrange
        var message = "Forecast my Azure costs for the next 3 months based on current usage trends and any planned resource changes";

        // Act
        var response = await SendChatRequestAsync(message, "cost-forecast-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task ForecastCosts_EndOfYear_ShouldReturnProjection()
    {
        // Arrange
        var message = "Project our Azure spend through end of fiscal year with confidence intervals";

        // Act
        var response = await SendChatRequestAsync(message, "cost-forecast-eoy-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Tag Compliance

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task CheckTagCompliance_CostAllocation_ShouldReturnReport()
    {
        // Arrange
        var message = "Identify resources that are missing required cost allocation tags (CostCenter, Environment, Owner) and show the untagged cost amount";

        // Act
        var response = await SendChatRequestAsync(message, "cost-tags-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task CheckTagCompliance_ChargebackReport_ShouldReturnReport()
    {
        // Arrange
        var message = "Generate a chargeback report showing costs by department based on CostCenter tags";

        // Act
        var response = await SendChatRequestAsync(message, "cost-chargeback-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Reserved Instances

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task AnalyzeReservedInstances_VMUsage_ShouldReturnRecommendations()
    {
        // Arrange
        var message = "Analyze my VM usage patterns and recommend which instances would benefit from reserved instance purchases with estimated savings";

        // Act
        var response = await SendChatRequestAsync(message, "cost-ri-vm-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task AnalyzeReservedInstances_SQLDatabase_ShouldReturnRecommendations()
    {
        // Arrange
        var message = "Analyze SQL Database reserved capacity opportunities and potential savings";

        // Act
        var response = await SendChatRequestAsync(message, "cost-ri-sql-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task ReviewReservedInstances_Utilization_ShouldReturnStatus()
    {
        // Arrange
        var message = "Show current reserved instance utilization and identify any underutilized reservations";

        // Act
        var response = await SendChatRequestAsync(message, "cost-ri-utilization-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Anomaly Detection

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task DetectCostAnomalies_Recent_ShouldReturnFindings()
    {
        // Arrange
        var message = "Detect any cost anomalies or unusual spending patterns in the last 30 days";

        // Act
        var response = await SendChatRequestAsync(message, "cost-anomaly-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "CostManagement")]
    public async Task DetectCostAnomalies_WithAlerts_ShouldReturnFindings()
    {
        // Arrange
        var message = "Show recent cost alerts and spending spikes with root cause analysis";

        // Act
        var response = await SendChatRequestAsync(message, "cost-anomaly-alerts-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion
}
