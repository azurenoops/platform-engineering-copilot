using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Security;
using Platform.Engineering.Copilot.Agents.Security.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T136 — Unit tests for SecurityAgent.
/// </summary>
public class SecurityAgentTests
{
    private SecurityAgent CreateAgent() => new(
        new Mock<ILogger<SecurityAgent>>().Object,
        new GetSecureScoreTool(new Mock<ILogger<GetSecureScoreTool>>().Object),
        new GetSecurityRecommendationsTool(new Mock<ILogger<GetSecurityRecommendationsTool>>().Object),
        new ManageSecurityPolicyTool(new Mock<ILogger<ManageSecurityPolicyTool>>().Object));

    [Fact] public void AgentId_Returns_Security() => CreateAgent().AgentId.Should().Be("security");
    [Fact] public void AgentName_Returns_SecurityAgent() => CreateAgent().AgentName.Should().Be("Security Agent");
    [Fact] public void RequiredPimTier_Is_Read() => CreateAgent().RequiredPimTier.Should().Be(PimTier.Read);
    [Fact] public void Description_Mentions_Security() => CreateAgent().Description.Should().Contain("security");
    [Fact] public void Keywords_Contains_Security() => CreateAgent().Keywords.Should().Contain("security");
    [Fact] public void Keywords_Contains_Defender() => CreateAgent().Keywords.Should().Contain("defender");
    [Fact] public void Agent_Registers_Three_Tools() => CreateAgent().GetToolMetadata().Should().HaveCount(3);
    [Fact] public void Agent_Is_BaseAgent() => CreateAgent().Should().BeAssignableTo<BaseAgent>();

    [Fact]
    public void SystemPrompt_Is_Loaded()
    {
        CreateAgent().GetSystemPrompt().Should().Contain("Security Agent");
    }

    [Fact]
    public void Tools_Have_Correct_Names()
    {
        var names = CreateAgent().GetToolMetadata().Select(t => t.Name).ToList();
        names.Should().Contain("get_secure_score");
        names.Should().Contain("get_security_recommendations");
        names.Should().Contain("manage_security_policy");
    }

    [Fact]
    public void All_Tools_Require_Authentication()
    {
        var tools = CreateAgent().GetToolMetadata();
        tools.Should().AllSatisfy(t => t.RequiresAuthentication.Should().BeTrue());
    }

    [Fact]
    public void All_Tools_Require_At_Least_PIM_Read()
    {
        var tools = CreateAgent().GetToolMetadata();
        tools.Should().AllSatisfy(t =>
            t.PimTierRequired.Should().BeOneOf(PimTier.Read, PimTier.Write));
    }
}
