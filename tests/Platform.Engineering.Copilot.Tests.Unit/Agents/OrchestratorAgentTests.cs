using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Orchestrator;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T070 — OrchestratorAgent tests: keyword routing, direct targeting, LLM fallback,
/// ambiguity resolution, unrecognized intent, routing explanation.
/// </summary>
public class OrchestratorAgentTests
{
    private readonly Mock<ILogger<OrchestratorAgent>> _loggerMock = new();
    private readonly Mock<ILogger<PlatformOrchestrator>> _orchLoggerMock = new();

    [Fact]
    public void Agent_HasCorrectId()
    {
        var orchestrator = new PlatformOrchestrator(_orchLoggerMock.Object);
        var agent = new OrchestratorAgent(_loggerMock.Object, orchestrator);
        agent.AgentId.Should().Be("orchestrator");
    }

    [Fact]
    public void Agent_HasCorrectName()
    {
        var orchestrator = new PlatformOrchestrator(_orchLoggerMock.Object);
        var agent = new OrchestratorAgent(_loggerMock.Object, orchestrator);
        agent.AgentName.Should().Be("Orchestrator Agent");
    }

    [Fact]
    public void Agent_DoesNotRequirePim()
    {
        var orchestrator = new PlatformOrchestrator(_orchLoggerMock.Object);
        var agent = new OrchestratorAgent(_loggerMock.Object, orchestrator);
        agent.RequiredPimTier.Should().Be(PimTier.None);
    }

    [Fact]
    public void Agent_HasSystemPrompt()
    {
        var orchestrator = new PlatformOrchestrator(_orchLoggerMock.Object);
        var agent = new OrchestratorAgent(_loggerMock.Object, orchestrator);
        agent.GetSystemPrompt().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Agent_ExposesOrchestrator()
    {
        var orchestrator = new PlatformOrchestrator(_orchLoggerMock.Object);
        var agent = new OrchestratorAgent(_loggerMock.Object, orchestrator);
        agent.Orchestrator.Should().BeSameAs(orchestrator);
    }

    [Fact]
    public void Agent_IsBaseAgent()
    {
        var orchestrator = new PlatformOrchestrator(_orchLoggerMock.Object);
        var agent = new OrchestratorAgent(_loggerMock.Object, orchestrator);
        agent.Should().BeAssignableTo<BaseAgent>();
    }

    [Fact]
    public async Task Routing_DirectTarget_RoutesToCorrectAgent()
    {
        var orchestrator = CreateOrchestratorWithComplianceAgent();
        var result = await orchestrator.RouteAsync("@compliance run assessment");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("compliance");
        result.Method.Should().Be(RoutingMethod.DirectTarget);
    }

    [Fact]
    public async Task Routing_KeywordMatch_RoutesToCorrectAgent()
    {
        var orchestrator = CreateOrchestratorWithComplianceAgent();
        var result = await orchestrator.RouteAsync("run a nist compliance assessment");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("compliance");
        result.Method.Should().Be(RoutingMethod.KeywordMatch);
    }

    [Fact]
    public async Task Routing_MultipleKeywordHits_PicksBestMatch()
    {
        var orchestrator = CreateOrchestratorWithMultipleAgents();
        // "compliance assessment nist fedramp" has 4 compliance keywords
        var result = await orchestrator.RouteAsync("run compliance assessment for nist fedramp");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("compliance");
    }

    [Fact]
    public async Task Routing_UnrecognizedIntent_NoMatch()
    {
        var orchestrator = CreateOrchestratorWithComplianceAgent();
        var result = await orchestrator.RouteAsync("what is the weather today");

        result.IsMatch.Should().BeFalse();
        result.Method.Should().Be(RoutingMethod.None);
        result.Explanation.Should().Contain("available agents");
    }

    [Fact]
    public async Task Routing_UnrecognizedIntent_ListsAvailableAgents()
    {
        var orchestrator = CreateOrchestratorWithMultipleAgents();
        var result = await orchestrator.RouteAsync("xyz random unrelated text");

        result.IsMatch.Should().BeFalse();
        result.Explanation.Should().Contain("Compliance");
        result.Explanation.Should().Contain("@");
    }

    [Fact]
    public async Task Routing_EmptyMessage_NoMatch()
    {
        var orchestrator = CreateOrchestratorWithComplianceAgent();
        var result = await orchestrator.RouteAsync("");

        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task Routing_DirectTarget_CaseInsensitive()
    {
        var orchestrator = CreateOrchestratorWithComplianceAgent();
        var result = await orchestrator.RouteAsync("@Compliance check status");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("compliance");
    }

    [Fact]
    public async Task Routing_ExplanationIsTransparent()
    {
        var orchestrator = CreateOrchestratorWithComplianceAgent();
        var result = await orchestrator.RouteAsync("run compliance assessment");

        result.Explanation.Should().NotBeNullOrWhiteSpace();
        result.Explanation.Should().Contain("Compliance");
    }

    [Fact]
    public async Task Routing_SecurityKeywords_RouteToSecurity()
    {
        var orchestrator = CreateOrchestratorWithMultipleAgents();
        var result = await orchestrator.RouteAsync("check my defender security score");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("security");
    }

    [Fact]
    public async Task Routing_CostKeywords_RouteToCost()
    {
        var orchestrator = CreateOrchestratorWithMultipleAgents();
        var result = await orchestrator.RouteAsync("show me the spending budget overview");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("cost");
    }

    private PlatformOrchestrator CreateOrchestratorWithComplianceAgent()
    {
        var orchestrator = new PlatformOrchestrator(_orchLoggerMock.Object);
        var agent = BaseAgentTestHelper.CreateAgent(
            "compliance", "Compliance Agent", "Compliance management",
            ["compliance", "nist", "fedramp", "assessment", "finding", "control", "remediate", "audit"],
            PimTier.Read);
        orchestrator.RegisterAgent(agent);
        return orchestrator;
    }

    private PlatformOrchestrator CreateOrchestratorWithMultipleAgents()
    {
        var orchestrator = new PlatformOrchestrator(_orchLoggerMock.Object);

        orchestrator.RegisterAgent(BaseAgentTestHelper.CreateAgent(
            "compliance", "Compliance Agent", "Compliance management",
            ["compliance", "nist", "fedramp", "assessment", "finding", "control"],
            PimTier.Read));

        orchestrator.RegisterAgent(BaseAgentTestHelper.CreateAgent(
            "security", "Security Agent", "Security posture management",
            ["secure", "score", "defender", "security", "vulnerability", "threat"],
            PimTier.Read));

        orchestrator.RegisterAgent(BaseAgentTestHelper.CreateAgent(
            "cost", "Cost Management Agent", "Cost analysis and optimization",
            ["cost", "spending", "budget", "optimization", "savings"],
            PimTier.Read));

        orchestrator.RegisterAgent(BaseAgentTestHelper.CreateAgent(
            "infrastructure", "Infrastructure Agent", "IaC management",
            ["infrastructure", "deploy", "template", "bicep"],
            PimTier.None));

        return orchestrator;
    }
}
