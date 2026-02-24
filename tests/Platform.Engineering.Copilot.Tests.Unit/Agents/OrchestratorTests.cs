using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for PlatformOrchestrator — keyword routing, LLM fallback,
/// direct targeting, ambiguity handling, transparent routing explanation.
/// </summary>
public class OrchestratorTests
{
    private readonly Mock<ILogger<PlatformOrchestrator>> _loggerMock = new();

    private PlatformOrchestrator CreateOrchestrator(IChatClient? chatClient = null)
    {
        return new PlatformOrchestrator(_loggerMock.Object, chatClient);
    }

    // ─── Test Agent Implementations ─────────────────────────────────

    private class TestComplianceAgent : BaseAgent
    {
        public override string AgentId => "compliance";
        public override string AgentName => "Compliance Agent";
        public override string Description => "Handles compliance assessments, NIST controls, and FedRAMP audits.";
        public override IReadOnlyList<string> Keywords => ["compliance", "nist", "fedramp", "assessment", "stig", "controls"];

        public TestComplianceAgent() : base(Mock.Of<ILogger>()) { }
        public override string GetSystemPrompt() => "You are the compliance agent.";
    }

    private class TestCostAgent : BaseAgent
    {
        public override string AgentId => "cost-management";
        public override string AgentName => "Cost Management Agent";
        public override string Description => "Handles cost analysis, spending reports, and budget tracking.";
        public override IReadOnlyList<string> Keywords => ["cost", "spending", "budget", "pricing", "billing"];

        public TestCostAgent() : base(Mock.Of<ILogger>()) { }
        public override string GetSystemPrompt() => "You are the cost management agent.";
    }

    private class TestIaCAgent : BaseAgent
    {
        public override string AgentId => "iac";
        public override string AgentName => "IaC Agent";
        public override string Description => "Handles infrastructure-as-code template generation for Azure.";
        public override IReadOnlyList<string> Keywords => ["terraform", "bicep", "template", "infrastructure", "iac"];
        public override PimTier RequiredPimTier => PimTier.Write;

        public TestIaCAgent() : base(Mock.Of<ILogger>()) { }
        public override string GetSystemPrompt() => "You are the IaC agent.";
    }

    private PlatformOrchestrator CreateWithAgents()
    {
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent(new TestComplianceAgent());
        orchestrator.RegisterAgent(new TestCostAgent());
        orchestrator.RegisterAgent(new TestIaCAgent());
        return orchestrator;
    }

    // ─── Agent Registration ─────────────────────────────────────────

    [Fact]
    public void RegisterAgent_AddsAgentToList()
    {
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent(new TestComplianceAgent());

        orchestrator.Agents.Should().HaveCount(1);
        orchestrator.Agents[0].AgentId.Should().Be("compliance");
    }

    [Fact]
    public void RegisterAgent_DuplicateId_SkipsDuplicate()
    {
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent(new TestComplianceAgent());
        orchestrator.RegisterAgent(new TestComplianceAgent()); // duplicate

        orchestrator.Agents.Should().HaveCount(1);
    }

    [Fact]
    public void RegisterAgent_NullAgent_Throws()
    {
        var orchestrator = CreateOrchestrator();

        var act = () => orchestrator.RegisterAgent(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ─── Direct Targeting ───────────────────────────────────────────

    [Fact]
    public async Task RouteAsync_DirectTargetById_RoutesToAgent()
    {
        var orchestrator = CreateWithAgents();

        var result = await orchestrator.RouteAsync("@compliance check my NIST controls");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("compliance");
        result.Method.Should().Be(RoutingMethod.DirectTarget);
        result.Explanation.Should().Contain("Compliance Agent");
        result.Explanation.Should().Contain("@compliance");
    }

    [Fact]
    public async Task RouteAsync_DirectTargetByName_RoutesToAgent()
    {
        var orchestrator = CreateWithAgents();

        var result = await orchestrator.RouteAsync("@iac generate a Bicep template");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("iac");
        result.Method.Should().Be(RoutingMethod.DirectTarget);
    }

    [Fact]
    public async Task RouteAsync_DirectTargetUnknown_FallsToKeyword()
    {
        var orchestrator = CreateWithAgents();

        var result = await orchestrator.RouteAsync("@unknown check compliance status");

        // Should fall through to keyword matching (compliance keyword)
        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("compliance");
        result.Method.Should().Be(RoutingMethod.KeywordMatch);
    }

    [Fact]
    public async Task RouteAsync_DirectTargetCaseInsensitive_RoutesToAgent()
    {
        var orchestrator = CreateWithAgents();

        var result = await orchestrator.RouteAsync("@Compliance check my controls");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("compliance");
        result.Method.Should().Be(RoutingMethod.DirectTarget);
    }

    // ─── Keyword Fast-Path ──────────────────────────────────────────

    [Theory]
    [InlineData("What is the compliance status?", "compliance")]
    [InlineData("Show me the NIST controls for AC-2", "compliance")]
    [InlineData("What's the FedRAMP assessment result?", "compliance")]
    [InlineData("Show me spending for this month", "cost-management")]
    [InlineData("What's the budget for Q1?", "cost-management")]
    [InlineData("Generate a Terraform template", "iac")]
    [InlineData("Create a Bicep template for storage", "iac")]
    public async Task RouteAsync_KeywordMatch_RoutesToCorrectAgent(string message, string expectedAgentId)
    {
        var orchestrator = CreateWithAgents();

        var result = await orchestrator.RouteAsync(message);

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be(expectedAgentId);
        result.Method.Should().Be(RoutingMethod.KeywordMatch);
    }

    [Fact]
    public async Task RouteAsync_KeywordMatch_TransparentExplanation()
    {
        var orchestrator = CreateWithAgents();

        var result = await orchestrator.RouteAsync("Run a compliance assessment");

        result.Explanation.Should().Contain("Compliance Agent");
        result.Explanation.Should().Contain("keyword");
    }

    [Fact]
    public async Task RouteAsync_MultipleKeywordHits_SelectsMostHits()
    {
        var orchestrator = CreateWithAgents();

        // "compliance assessment controls" has 3 keywords for compliance
        var result = await orchestrator.RouteAsync("Run a compliance assessment and check controls");

        result.Agent!.AgentId.Should().Be("compliance");
        result.Method.Should().Be(RoutingMethod.KeywordMatch);
    }

    [Fact]
    public async Task RouteAsync_AmbiguousKeywords_ExplainsOtherCandidates()
    {
        var orchestrator = CreateWithAgents();

        // "compliance" + "cost" → 2 agents, each with 1 hit
        var result = await orchestrator.RouteAsync("compliance cost analysis");

        result.IsMatch.Should().BeTrue();
        // Should mention other candidates in explanation
        result.Explanation.Should().ContainAny("candidates", "keyword");
    }

    // ─── No Match ───────────────────────────────────────────────────

    [Fact]
    public async Task RouteAsync_NoKeywordNoLlm_ReturnsNoMatch()
    {
        var orchestrator = CreateWithAgents();

        var result = await orchestrator.RouteAsync("Hello, how are you today?");

        result.IsMatch.Should().BeFalse();
        result.Agent.Should().BeNull();
        result.Method.Should().Be(RoutingMethod.None);
        result.Explanation.Should().Contain("available agents");
    }

    [Fact]
    public async Task RouteAsync_EmptyMessage_ReturnsNoMatch()
    {
        var orchestrator = CreateWithAgents();

        var result = await orchestrator.RouteAsync("");

        result.IsMatch.Should().BeFalse();
        result.Explanation.Should().Contain("Empty message");
    }

    [Fact]
    public async Task RouteAsync_WhitespaceMessage_ReturnsNoMatch()
    {
        var orchestrator = CreateWithAgents();

        var result = await orchestrator.RouteAsync("   ");

        result.IsMatch.Should().BeFalse();
    }

    // ─── LLM Fallback ──────────────────────────────────────────────

    [Fact]
    public async Task RouteAsync_LlmFallback_RoutesWhenKeywordMisses()
    {
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "compliance")));

        var orchestrator = new PlatformOrchestrator(_loggerMock.Object, mockChatClient.Object);
        orchestrator.RegisterAgent(new TestComplianceAgent());
        orchestrator.RegisterAgent(new TestCostAgent());

        // "audit my environment" has no keywords but should route to compliance via LLM
        var result = await orchestrator.RouteAsync("audit my environment");

        result.IsMatch.Should().BeTrue();
        result.Agent!.AgentId.Should().Be("compliance");
        result.Method.Should().Be(RoutingMethod.LlmClassification);
        result.Explanation.Should().Contain("intent classification");
    }

    [Fact]
    public async Task RouteAsync_LlmReturnsNone_ReturnsNoMatch()
    {
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "none")));

        var orchestrator = new PlatformOrchestrator(_loggerMock.Object, mockChatClient.Object);
        orchestrator.RegisterAgent(new TestComplianceAgent());

        var result = await orchestrator.RouteAsync("tell me a joke");

        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task RouteAsync_LlmThrows_ReturnsNoMatch()
    {
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var orchestrator = new PlatformOrchestrator(_loggerMock.Object, mockChatClient.Object);
        orchestrator.RegisterAgent(new TestComplianceAgent());

        var result = await orchestrator.RouteAsync("audit my system");

        result.IsMatch.Should().BeFalse();
    }

    // ─── Keyword Priority Over LLM ─────────────────────────────────

    [Fact]
    public async Task RouteAsync_KeywordMatchSkipsLlm()
    {
        var mockChatClient = new Mock<IChatClient>();

        var orchestrator = new PlatformOrchestrator(_loggerMock.Object, mockChatClient.Object);
        orchestrator.RegisterAgent(new TestComplianceAgent());

        var result = await orchestrator.RouteAsync("Check NIST compliance");

        result.Method.Should().Be(RoutingMethod.KeywordMatch);

        // LLM should not have been called
        mockChatClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Direct Target Priority Over Keyword ────────────────────────

    [Fact]
    public async Task RouteAsync_DirectTargetSkipsKeyword()
    {
        var orchestrator = CreateWithAgents();

        // Message has @cost targeting but "compliance" keyword — direct target should win
        var result = await orchestrator.RouteAsync("@cost-management what is the compliance cost?");

        result.Agent!.AgentId.Should().Be("cost-management");
        result.Method.Should().Be(RoutingMethod.DirectTarget);
    }

    // ─── Agent Metadata ─────────────────────────────────────────────

    [Fact]
    public void Agent_RequiredPimTier_DefaultsToNone()
    {
        var agent = new TestComplianceAgent();
        agent.RequiredPimTier.Should().Be(PimTier.None);
    }

    [Fact]
    public void Agent_RequiredPimTier_CanBeOverridden()
    {
        var agent = new TestIaCAgent();
        agent.RequiredPimTier.Should().Be(PimTier.Write);
    }

    // ─── Tool Registration on Agent ─────────────────────────────────

    private class TestTool : BaseTool
    {
        public override string Name => "test_tool";
        public override string Description => "A test tool";
        public override string Parameters => """{"type":"object","properties":{"input":{"type":"string"}}}""";

        public TestTool() : base(Mock.Of<ILogger>()) { }

        public override Task<string> ExecuteAsync(
            Dictionary<string, object?> parameters,
            IProgress<ProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("{}");
        }
    }

    [Fact]
    public void RegisterTool_AddsTool()
    {
        var agent = new TestComplianceAgent();
        agent.RegisterTool(new TestTool());

        agent.Tools.Should().HaveCount(1);
        agent.Tools[0].Name.Should().Be("test_tool");
    }

    [Fact]
    public void RegisterTool_DuplicateName_SkipsDuplicate()
    {
        var agent = new TestComplianceAgent();
        agent.RegisterTool(new TestTool());
        agent.RegisterTool(new TestTool()); // same name

        agent.Tools.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteToolAsync_ValidTool_ExecutesAndReturnsResult()
    {
        var agent = new TestComplianceAgent();
        agent.RegisterTool(new TestTool());

        var result = await agent.ExecuteToolAsync("test_tool", new Dictionary<string, object?>());

        result.Should().Be("{}");
    }

    [Fact]
    public async Task ExecuteToolAsync_UnknownTool_Throws()
    {
        var agent = new TestComplianceAgent();

        var act = async () => await agent.ExecuteToolAsync("nonexistent", []);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public void GetToolMetadata_ReturnsCorrectMetadata()
    {
        var agent = new TestComplianceAgent();
        agent.RegisterTool(new TestTool());

        var metadata = agent.GetToolMetadata();

        metadata.Should().HaveCount(1);
        metadata[0].Name.Should().Be("test_tool");
        metadata[0].AgentId.Should().Be("compliance");
        metadata[0].RequiresAuthentication.Should().BeTrue();
        metadata[0].PimTierRequired.Should().Be(PimTier.None);
    }

    // ─── ResponseEnvelope ───────────────────────────────────────────

    [Fact]
    public void ResponseEnvelope_Success_HasCorrectStructure()
    {
        var envelope = ResponseEnvelope<string>.Success("data", "test_tool", 42);

        envelope.Status.Should().Be("success");
        envelope.Data.Should().Be("data");
        envelope.Metadata.ToolName.Should().Be("test_tool");
        envelope.Metadata.ExecutionTimeMs.Should().Be(42);
        envelope.Error.Should().BeNull();
    }

    [Fact]
    public void ResponseEnvelope_Fail_HasCorrectStructure()
    {
        var envelope = ResponseEnvelope<string>.Fail("test_tool", "AUTH_REQUIRED", "CAC required", 10);

        envelope.Status.Should().Be("error");
        envelope.Data.Should().BeNull();
        envelope.Error.Should().NotBeNull();
        envelope.Error!.Code.Should().Be("AUTH_REQUIRED");
        envelope.Error.Message.Should().Be("CAC required");
    }

    [Fact]
    public void ResponseEnvelope_ToJson_ProducesValidJson()
    {
        var envelope = ResponseEnvelope<string>.Success("test", "tool", 100);

        var json = envelope.ToJson();

        json.Should().Contain("\"status\"");
        json.Should().Contain("\"data\"");
        json.Should().Contain("\"metadata\"");
    }

    [Fact]
    public void PaginationInfo_CalculatesCorrectly()
    {
        var pagination = new PaginationInfo
        {
            Page = 2,
            PageSize = 10,
            TotalItems = 25
        };

        pagination.TotalPages.Should().Be(3);
        pagination.HasNextPage.Should().BeTrue();
        pagination.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void PaginationInfo_FirstPage_NoPreviousPage()
    {
        var pagination = new PaginationInfo
        {
            Page = 1,
            PageSize = 10,
            TotalItems = 25
        };

        pagination.HasPreviousPage.Should().BeFalse();
        pagination.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void PaginationInfo_LastPage_NoNextPage()
    {
        var pagination = new PaginationInfo
        {
            Page = 3,
            PageSize = 10,
            TotalItems = 25
        };

        pagination.HasNextPage.Should().BeFalse();
        pagination.HasPreviousPage.Should().BeTrue();
    }
}
