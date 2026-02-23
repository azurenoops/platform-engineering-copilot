using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.KnowledgeBase;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T093 — Unit tests for KnowledgeBaseAgent.
/// Verifies: extends BaseAgent, registers 8 tools, loads prompt, no auth.
/// </summary>
public class KnowledgeBaseAgentTests
{
    private readonly Mock<INistService> _nistServiceMock = new();

    private KnowledgeBaseAgent CreateAgent()
    {
        var logger = new Mock<ILogger<KnowledgeBaseAgent>>().Object;
        return new KnowledgeBaseAgent(logger,
            new ExplainControlTool(_nistServiceMock.Object, new Mock<ILogger<ExplainControlTool>>().Object),
            new CompareFrameworksTool(_nistServiceMock.Object, new Mock<ILogger<CompareFrameworksTool>>().Object),
            new SearchControlsTool(_nistServiceMock.Object, new Mock<ILogger<SearchControlsTool>>().Object),
            new GetStigGuidanceTool(_nistServiceMock.Object, new Mock<ILogger<GetStigGuidanceTool>>().Object),
            new GetAtoChecklistTool(_nistServiceMock.Object, new Mock<ILogger<GetAtoChecklistTool>>().Object),
            new FrameworkSummaryTool(_nistServiceMock.Object, new Mock<ILogger<FrameworkSummaryTool>>().Object),
            new ControlMappingTool(_nistServiceMock.Object, new Mock<ILogger<ControlMappingTool>>().Object),
            new ImplementationExamplesTool(_nistServiceMock.Object, new Mock<ILogger<ImplementationExamplesTool>>().Object));
    }

    [Fact]
    public void AgentId_Returns_KnowledgeBase()
    {
        var agent = CreateAgent();
        agent.AgentId.Should().Be("knowledgebase");
    }

    [Fact]
    public void AgentName_Returns_KnowledgeBaseAgent()
    {
        var agent = CreateAgent();
        agent.AgentName.Should().Be("Knowledge Base Agent");
    }

    [Fact]
    public void RequiredPimTier_Is_None()
    {
        var agent = CreateAgent();
        agent.RequiredPimTier.Should().Be(PimTier.None);
    }

    [Fact]
    public void Description_Mentions_NIST_And_Offline()
    {
        var agent = CreateAgent();
        agent.Description.Should().Contain("NIST");
        agent.Description.Should().Contain("offline");
    }

    [Fact]
    public void Keywords_Contains_Expected_Routing_Terms()
    {
        var agent = CreateAgent();
        agent.Keywords.Should().Contain("explain");
        agent.Keywords.Should().Contain("stig");
    }

    [Fact]
    public void Agent_Registers_Eight_Tools()
    {
        var agent = CreateAgent();
        agent.GetToolMetadata().Should().HaveCount(8);
    }

    [Fact]
    public void Agent_Registers_Tools_With_Correct_Names()
    {
        var agent = CreateAgent();
        var toolNames = agent.GetToolMetadata().Select(t => t.Name).ToList();
        toolNames.Should().Contain("explain_control");
        toolNames.Should().Contain("compare_frameworks");
        toolNames.Should().Contain("search_controls");
        toolNames.Should().Contain("get_stig_guidance");
        toolNames.Should().Contain("get_ato_checklist");
        toolNames.Should().Contain("framework_summary");
        toolNames.Should().Contain("control_mapping");
        toolNames.Should().Contain("implementation_examples");
    }

    [Fact]
    public void SystemPrompt_Is_Loaded()
    {
        var agent = CreateAgent();
        var prompt = agent.GetSystemPrompt();
        prompt.Should().NotBeNullOrWhiteSpace();
        prompt.Should().Contain("Knowledge Base Agent");
    }

    [Fact]
    public void Agent_Is_BaseAgent()
    {
        var agent = CreateAgent();
        agent.Should().BeAssignableTo<BaseAgent>();
    }
}
