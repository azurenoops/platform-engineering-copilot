using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Compliance;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T044 — ComplianceAgent unit tests: constructor, tool registration, system prompt, keywords, metadata.
/// </summary>
public class ComplianceAgentTests
{
    private readonly Mock<ILogger<ComplianceAgent>> _loggerMock = new();
    private readonly Mock<INistService> _nistServiceMock = new();

    private ComplianceAgent CreateAgent(IEnumerable<BaseTool>? tools = null)
    {
        return new ComplianceAgent(_loggerMock.Object, tools ?? []);
    }

    [Fact]
    public void Agent_HasCorrectId()
    {
        var agent = CreateAgent();
        agent.AgentId.Should().Be("compliance");
    }

    [Fact]
    public void Agent_HasCorrectName()
    {
        var agent = CreateAgent();
        agent.AgentName.Should().Be("Compliance Agent");
    }

    [Fact]
    public void Agent_HasMeaningfulDescription()
    {
        var agent = CreateAgent();
        agent.Description.Should().NotBeNullOrWhiteSpace();
        agent.Description.Should().Contain("compliance");
    }

    [Fact]
    public void Agent_RequiresReadPimTier()
    {
        var agent = CreateAgent();
        agent.RequiredPimTier.Should().Be(PimTier.Read);
    }

    [Fact]
    public void Agent_HasComplianceKeywords()
    {
        var agent = CreateAgent();
        agent.Keywords.Should().NotBeEmpty();
        agent.Keywords.Should().Contain("compliance");
        agent.Keywords.Should().Contain("nist");
        agent.Keywords.Should().Contain("fedramp");
        agent.Keywords.Should().Contain("assessment");
    }

    [Fact]
    public void Agent_KeywordsIncludeDocumentTypes()
    {
        var agent = CreateAgent();
        agent.Keywords.Should().Contain("ssp");
        agent.Keywords.Should().Contain("sar");
        agent.Keywords.Should().Contain("poam");
    }

    [Fact]
    public void Agent_HasSystemPrompt()
    {
        var agent = CreateAgent();
        var prompt = agent.GetSystemPrompt();
        prompt.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Agent_RegistersProvidedTools()
    {
        var tool1 = BaseToolTestHelper.CreateTool("tool1", "Tool 1");
        var tool2 = BaseToolTestHelper.CreateTool("tool2", "Tool 2");

        var agent = CreateAgent([tool1, tool2]);

        var metadata = agent.GetToolMetadata();
        metadata.Should().HaveCount(2);
        metadata.Select(m => m.Name).Should().Contain("tool1");
        metadata.Select(m => m.Name).Should().Contain("tool2");
    }

    [Fact]
    public void Agent_RegistersAllComplianceTools()
    {
        var tools = CreateAllComplianceTools();
        var agent = CreateAgent(tools);

        var metadata = agent.GetToolMetadata();
        metadata.Should().HaveCountGreaterThanOrEqualTo(12);
    }

    [Fact]
    public void Agent_ToolMetadataContainsCorrectAgentId()
    {
        var tool = BaseToolTestHelper.CreateTool("test", "Test");
        var agent = CreateAgent([tool]);

        var metadata = agent.GetToolMetadata();
        metadata.Should().ContainSingle();
        metadata.First().AgentId.Should().Be("compliance");
    }

    [Fact]
    public async Task Agent_ExecuteToolAsync_CallsTool()
    {
        var recorder = BaseToolTestHelper.CreateRecordingTool("compliance_test");
        var agent = CreateAgent([recorder]);

        var result = await agent.ExecuteToolAsync("compliance_test", new Dictionary<string, object?> { { "key", "value" } });

        result.Should().NotBeNullOrWhiteSpace();
        recorder.WasInvoked.Should().BeTrue();
        recorder.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task Agent_ExecuteToolAsync_UnknownTool_Throws()
    {
        var agent = CreateAgent();

        Func<Task> act = () => agent.ExecuteToolAsync("nonexistent_tool", new Dictionary<string, object?>());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Agent_IsBaseAgent()
    {
        var agent = CreateAgent();
        agent.Should().BeAssignableTo<BaseAgent>();
    }

    [Fact]
    public void Agent_NoTools_HasEmptyMetadata()
    {
        var agent = CreateAgent();
        var metadata = agent.GetToolMetadata();
        metadata.Should().BeEmpty();
    }

    private IEnumerable<BaseTool> CreateAllComplianceTools()
    {
        var nistLogger = new Mock<ILogger<ComplianceAssessTool>>().Object;
        var familyLogger = new Mock<ILogger<ComplianceGetControlFamilyTool>>().Object;
        var statusLogger = new Mock<ILogger<ComplianceStatusTool>>().Object;
        var historyLogger = new Mock<ILogger<ComplianceHistoryTool>>().Object;
        var remediateLogger = new Mock<ILogger<ComplianceRemediateTool>>().Object;
        var validateLogger = new Mock<ILogger<ComplianceValidateRemediationTool>>().Object;
        var planLogger = new Mock<ILogger<ComplianceGeneratePlanTool>>().Object;
        var evidenceLogger = new Mock<ILogger<ComplianceCollectEvidenceTool>>().Object;
        var docLogger = new Mock<ILogger<ComplianceGenerateDocumentTool>>().Object;
        var auditLogger = new Mock<ILogger<ComplianceAuditLogTool>>().Object;
        var chatLogger = new Mock<ILogger<ComplianceChatTool>>().Object;
        var mapLogger = new Mock<ILogger<ComplianceMapControlsTool>>().Object;
        var compareLogger = new Mock<ILogger<ComplianceCompareFrameworksTool>>().Object;
        var dashboardLogger = new Mock<ILogger<ComplianceDashboardTool>>().Object;
        var exportLogger = new Mock<ILogger<ComplianceExportTool>>().Object;
        var monitoringLogger = new Mock<ILogger<ComplianceMonitoringTool>>().Object;

        yield return new ComplianceAssessTool(_nistServiceMock.Object, nistLogger);
        yield return new ComplianceGetControlFamilyTool(_nistServiceMock.Object, familyLogger);
        yield return new ComplianceStatusTool(statusLogger);
        yield return new ComplianceHistoryTool(historyLogger);
        yield return new ComplianceRemediateTool(remediateLogger);
        yield return new ComplianceValidateRemediationTool(validateLogger);
        yield return new ComplianceGeneratePlanTool(planLogger);
        yield return new ComplianceCollectEvidenceTool(evidenceLogger);
        yield return new ComplianceGenerateDocumentTool(docLogger);
        yield return new ComplianceAuditLogTool(auditLogger);
        yield return new ComplianceChatTool(chatLogger);
        yield return new ComplianceMapControlsTool(_nistServiceMock.Object, mapLogger);
        yield return new ComplianceCompareFrameworksTool(_nistServiceMock.Object, compareLogger);
        yield return new ComplianceDashboardTool(dashboardLogger);
        yield return new ComplianceExportTool(exportLogger);
        yield return new ComplianceMonitoringTool(monitoringLogger);
    }
}
