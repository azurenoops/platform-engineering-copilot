using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Infrastructure;
using Platform.Engineering.Copilot.Agents.Infrastructure.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T105 — Unit tests for InfrastructureAgent.
/// </summary>
public class InfrastructureAgentTests
{
    private InfrastructureAgent CreateAgent() => new(
        new Mock<ILogger<InfrastructureAgent>>().Object,
        new GenerateInfrastructureTemplateTool(new Mock<ILogger<GenerateInfrastructureTemplateTool>>().Object),
        new ProvisionInfrastructureTool(new Mock<ILogger<ProvisionInfrastructureTool>>().Object),
        new ValidateTemplateTool(new Mock<ILogger<ValidateTemplateTool>>().Object),
        new ListDeploymentsTool(new Mock<ILogger<ListDeploymentsTool>>().Object),
        new GetDeploymentStatusTool(new Mock<ILogger<GetDeploymentStatusTool>>().Object),
        new RollbackDeploymentTool(new Mock<ILogger<RollbackDeploymentTool>>().Object));

    [Fact] public void AgentId_Returns_Infrastructure() => CreateAgent().AgentId.Should().Be("infrastructure");
    [Fact] public void AgentName_Returns_InfrastructureAgent() => CreateAgent().AgentName.Should().Be("Infrastructure Agent");
    [Fact] public void RequiredPimTier_Is_Read() => CreateAgent().RequiredPimTier.Should().Be(PimTier.Read);
    [Fact] public void Description_Mentions_Templates() => CreateAgent().Description.Should().Contain("template");
    [Fact] public void Keywords_Contains_Deploy() => CreateAgent().Keywords.Should().Contain("deploy");
    [Fact] public void Keywords_Contains_Bicep() => CreateAgent().Keywords.Should().Contain("bicep");
    [Fact] public void Agent_Registers_Six_Tools() => CreateAgent().GetToolMetadata().Should().HaveCount(6);
    [Fact] public void Agent_Is_BaseAgent() => CreateAgent().Should().BeAssignableTo<BaseAgent>();

    [Fact]
    public void SystemPrompt_Is_Loaded()
    {
        CreateAgent().GetSystemPrompt().Should().Contain("Infrastructure Agent");
    }

    [Fact]
    public void Tools_Have_Correct_Names()
    {
        var names = CreateAgent().GetToolMetadata().Select(t => t.Name).ToList();
        names.Should().Contain("generate_infrastructure_template");
        names.Should().Contain("provision_infrastructure");
        names.Should().Contain("validate_template");
        names.Should().Contain("list_deployments");
        names.Should().Contain("get_deployment_status");
        names.Should().Contain("rollback_deployment");
    }
}
