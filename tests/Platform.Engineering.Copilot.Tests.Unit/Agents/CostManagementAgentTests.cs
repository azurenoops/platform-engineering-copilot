using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.CostManagement;
using Platform.Engineering.Copilot.Agents.CostManagement.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T115 — Unit tests for CostManagementAgent.
/// </summary>
public class CostManagementAgentTests
{
    private CostManagementAgent CreateAgent() => new(
        new Mock<ILogger<CostManagementAgent>>().Object,
        new GetCostAnalysisTool(new Mock<ILogger<GetCostAnalysisTool>>().Object),
        new GetCostForecastTool(new Mock<ILogger<GetCostForecastTool>>().Object),
        new GetOptimizationSuggestionsTool(new Mock<ILogger<GetOptimizationSuggestionsTool>>().Object),
        new GetCachedCostReportTool(new Mock<ILogger<GetCachedCostReportTool>>().Object),
        new GetBudgetStatusTool(new Mock<ILogger<GetBudgetStatusTool>>().Object),
        new GetCostAnomaliesTool(new Mock<ILogger<GetCostAnomaliesTool>>().Object));

    [Fact] public void AgentId_Returns_CostManagement() => CreateAgent().AgentId.Should().Be("costmanagement");
    [Fact] public void AgentName_Returns_CostManagementAgent() => CreateAgent().AgentName.Should().Be("Cost Management Agent");
    [Fact] public void RequiredPimTier_Is_Read() => CreateAgent().RequiredPimTier.Should().Be(PimTier.Read);
    [Fact] public void Description_Mentions_Spending() => CreateAgent().Description.Should().Contain("spending");
    [Fact] public void Keywords_Contains_Cost() => CreateAgent().Keywords.Should().Contain("cost");
    [Fact] public void Keywords_Contains_Budget() => CreateAgent().Keywords.Should().Contain("budget");
    [Fact] public void Agent_Registers_Six_Tools() => CreateAgent().GetToolMetadata().Should().HaveCount(6);
    [Fact] public void Agent_Is_BaseAgent() => CreateAgent().Should().BeAssignableTo<BaseAgent>();

    [Fact]
    public void SystemPrompt_Is_Loaded()
    {
        CreateAgent().GetSystemPrompt().Should().Contain("Cost Management Agent");
    }

    [Fact]
    public void Tools_Have_Correct_Names()
    {
        var names = CreateAgent().GetToolMetadata().Select(t => t.Name).ToList();
        names.Should().Contain("get_cost_analysis");
        names.Should().Contain("get_cost_forecast");
        names.Should().Contain("get_optimization_suggestions");
        names.Should().Contain("get_cached_cost_report");
        names.Should().Contain("get_budget_status");
        names.Should().Contain("get_cost_anomalies");
    }
}
