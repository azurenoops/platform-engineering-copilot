using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Agents.CostManagement;
using Platform.Engineering.Copilot.Agents.CostManagement.Tools;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// T117 — Integration test for cost analysis flow.
/// Live query → view results → optimization suggestions → cached report without auth.
/// </summary>
public class CostManagementFlowTests
{
    private readonly GetCostAnalysisTool _costAnalysis = new(new Mock<ILogger<GetCostAnalysisTool>>().Object);
    private readonly GetOptimizationSuggestionsTool _optimizations = new(new Mock<ILogger<GetOptimizationSuggestionsTool>>().Object);
    private readonly GetCachedCostReportTool _cachedReport = new(new Mock<ILogger<GetCachedCostReportTool>>().Object);
    private readonly GetBudgetStatusTool _budget = new(new Mock<ILogger<GetBudgetStatusTool>>().Object);
    private readonly GetCostAnomaliesTool _anomalies = new(new Mock<ILogger<GetCostAnomaliesTool>>().Object);

    [Fact]
    public async Task CostAnalysis_Then_Optimization_Flow()
    {
        // Step 1: Get cost analysis (30d, by resourceType)
        var analysisResult = await _costAnalysis.ExecuteAsync(new Dictionary<string, object?>
        {
            ["timeframe"] = "30d",
            ["groupBy"] = "resourceType"
        });
        var analysis = JsonDocument.Parse(analysisResult);
        analysis.RootElement.GetProperty("status").GetString().Should().Be("success");
        analysis.RootElement.GetProperty("data").GetProperty("totalCost").GetDouble()
            .Should().BeGreaterThan(0);

        // Step 2: Get optimization suggestions based on current spend
        var optResult = await _optimizations.ExecuteAsync(new Dictionary<string, object?>
        {
            ["category"] = "all"
        });
        var opts = JsonDocument.Parse(optResult);
        opts.RootElement.GetProperty("status").GetString().Should().Be("success");
        opts.RootElement.GetProperty("data").GetProperty("totalEstimatedSavings").GetDouble()
            .Should().BeGreaterThan(0);

        // Step 3: Get cached report (no auth)
        var cacheResult = await _cachedReport.ExecuteAsync([]);
        var cache = JsonDocument.Parse(cacheResult);
        cache.RootElement.GetProperty("status").GetString().Should().Be("success");
        cache.RootElement.GetProperty("data").GetProperty("summary").GetProperty("totalCost").GetDouble()
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BudgetStatus_Shows_ThresholdAlerts()
    {
        var result = await _budget.ExecuteAsync([]);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");

        var budgets = doc.RootElement.GetProperty("data").GetProperty("budgets");
        budgets.GetArrayLength().Should().BeGreaterThan(0);

        // At least one budget should have a triggered alert
        var hasTriggered = false;
        foreach (var b in budgets.EnumerateArray())
        {
            foreach (var alert in b.GetProperty("alerts").EnumerateArray())
            {
                if (alert.GetProperty("triggered").GetBoolean())
                    hasTriggered = true;
            }
        }
        hasTriggered.Should().BeTrue("at least one budget alert should be triggered");
    }

    [Fact]
    public async Task CostAnomalies_Detect_Spending_Spikes()
    {
        var result = await _anomalies.ExecuteAsync(new Dictionary<string, object?>
        {
            ["sensitivity"] = "high"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("totalAnomalies").GetInt32()
            .Should().BeGreaterThan(0);

        // Should include critical anomaly
        doc.RootElement.GetProperty("data").GetProperty("summary")
            .GetProperty("critical").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Agent_Registers_All_Six_Tools()
    {
        var agent = new CostManagementAgent(
            new Mock<ILogger<CostManagementAgent>>().Object,
            new BaseTool[]
            {
                _costAnalysis,
                new GetCostForecastTool(new Mock<ILogger<GetCostForecastTool>>().Object),
                _optimizations,
                _cachedReport,
                _budget,
                _anomalies
            });

        var tools = agent.GetToolMetadata();
        tools.Should().HaveCount(6);

        // Verify cached report has no auth requirement
        var cachedTool = tools.First(t => t.Name == "get_cached_cost_report");
        cachedTool.RequiresAuthentication.Should().BeFalse();
    }
}
