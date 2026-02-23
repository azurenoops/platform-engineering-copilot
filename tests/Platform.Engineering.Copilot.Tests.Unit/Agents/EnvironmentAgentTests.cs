using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Environment;
using Platform.Engineering.Copilot.Agents.Environment.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T135 — Unit tests for EnvironmentAgent.
/// </summary>
public class EnvironmentAgentTests
{
    private EnvironmentAgent CreateAgent() => new(
        new Mock<ILogger<EnvironmentAgent>>().Object,
        new CloneEnvironmentTool(new Mock<ILogger<CloneEnvironmentTool>>().Object),
        new DetectDriftTool(new Mock<ILogger<DetectDriftTool>>().Object),
        new CompareEnvironmentsTool(new Mock<ILogger<CompareEnvironmentsTool>>().Object),
        new PromoteEnvironmentTool(new Mock<ILogger<PromoteEnvironmentTool>>().Object),
        new ListEnvironmentsTool(new Mock<ILogger<ListEnvironmentsTool>>().Object),
        new GetEnvironmentStatusTool(new Mock<ILogger<GetEnvironmentStatusTool>>().Object),
        new CreateEnvironmentTool(new Mock<ILogger<CreateEnvironmentTool>>().Object),
        new DeleteEnvironmentTool(new Mock<ILogger<DeleteEnvironmentTool>>().Object),
        new GetEnvironmentHistoryTool(new Mock<ILogger<GetEnvironmentHistoryTool>>().Object),
        new ValidateEnvironmentTool(new Mock<ILogger<ValidateEnvironmentTool>>().Object));

    [Fact] public void AgentId_Returns_Environment() => CreateAgent().AgentId.Should().Be("environment");
    [Fact] public void AgentName_Returns_EnvironmentAgent() => CreateAgent().AgentName.Should().Be("Environment Agent");
    [Fact] public void RequiredPimTier_Is_Write() => CreateAgent().RequiredPimTier.Should().Be(PimTier.Write);
    [Fact] public void Description_Mentions_Environment() => CreateAgent().Description.Should().Contain("environment");
    [Fact] public void Keywords_Contains_Clone() => CreateAgent().Keywords.Should().Contain("clone");
    [Fact] public void Keywords_Contains_Drift() => CreateAgent().Keywords.Should().Contain("drift");
    [Fact] public void Agent_Registers_Ten_Tools() => CreateAgent().GetToolMetadata().Should().HaveCount(10);
    [Fact] public void Agent_Is_BaseAgent() => CreateAgent().Should().BeAssignableTo<BaseAgent>();

    [Fact]
    public void SystemPrompt_Is_Loaded()
    {
        CreateAgent().GetSystemPrompt().Should().Contain("Environment Agent");
    }

    [Fact]
    public void Tools_Have_Correct_Names()
    {
        var names = CreateAgent().GetToolMetadata().Select(t => t.Name).ToList();
        names.Should().Contain("clone_environment");
        names.Should().Contain("detect_drift");
        names.Should().Contain("compare_environments");
        names.Should().Contain("promote_environment");
        names.Should().Contain("list_environments");
        names.Should().Contain("get_environment_status");
        names.Should().Contain("create_environment");
        names.Should().Contain("delete_environment");
        names.Should().Contain("get_environment_history");
        names.Should().Contain("validate_environment");
    }

    [Fact]
    public void All_Tools_Require_Authentication()
    {
        var tools = CreateAgent().GetToolMetadata();
        tools.Should().AllSatisfy(t => t.RequiresAuthentication.Should().BeTrue());
    }

    [Fact]
    public void Write_Tools_Require_PIM_Write()
    {
        var agent = CreateAgent();
        var writeTools = agent.GetToolMetadata()
            .Where(t => t.Name is "clone_environment" or "promote_environment" or "create_environment" or "delete_environment");
        writeTools.Should().AllSatisfy(t => t.PimTierRequired.Should().Be(PimTier.Write));
    }

    [Fact]
    public void Read_Tools_Require_PIM_Read()
    {
        var agent = CreateAgent();
        var readTools = agent.GetToolMetadata()
            .Where(t => t.Name is "detect_drift" or "compare_environments" or "list_environments" or "get_environment_status" or "get_environment_history" or "validate_environment");
        readTools.Should().AllSatisfy(t => t.PimTierRequired.Should().Be(PimTier.Read));
    }
}
