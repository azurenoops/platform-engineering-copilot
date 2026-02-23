using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Discovery;
using Platform.Engineering.Copilot.Agents.Discovery.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T134 — Unit tests for DiscoveryAgent.
/// </summary>
public class DiscoveryAgentTests
{
    private DiscoveryAgent CreateAgent() => new(
        new Mock<ILogger<DiscoveryAgent>>().Object,
        new DiscoverResourcesTool(new Mock<ILogger<DiscoverResourcesTool>>().Object),
        new GetResourceDependenciesTool(new Mock<ILogger<GetResourceDependenciesTool>>().Object),
        new CrossSubscriptionQueryTool(new Mock<ILogger<CrossSubscriptionQueryTool>>().Object),
        new GetResourceHealthTool(new Mock<ILogger<GetResourceHealthTool>>().Object),
        new GetNetworkTopologyTool(new Mock<ILogger<GetNetworkTopologyTool>>().Object),
        new AnalyzeTagsTool(new Mock<ILogger<AnalyzeTagsTool>>().Object),
        new GetResourceChangesTool(new Mock<ILogger<GetResourceChangesTool>>().Object),
        new GetOrphanedResourcesTool(new Mock<ILogger<GetOrphanedResourcesTool>>().Object),
        new GetResourceMetricsTool(new Mock<ILogger<GetResourceMetricsTool>>().Object));

    [Fact] public void AgentId_Returns_Discovery() => CreateAgent().AgentId.Should().Be("discovery");
    [Fact] public void AgentName_Returns_DiscoveryAgent() => CreateAgent().AgentName.Should().Be("Discovery Agent");
    [Fact] public void RequiredPimTier_Is_Read() => CreateAgent().RequiredPimTier.Should().Be(PimTier.Read);
    [Fact] public void Description_Mentions_Resources() => CreateAgent().Description.Should().Contain("resources");
    [Fact] public void Keywords_Contains_Discover() => CreateAgent().Keywords.Should().Contain("discover");
    [Fact] public void Keywords_Contains_Inventory() => CreateAgent().Keywords.Should().Contain("inventory");
    [Fact] public void Agent_Registers_Nine_Tools() => CreateAgent().GetToolMetadata().Should().HaveCount(9);
    [Fact] public void Agent_Is_BaseAgent() => CreateAgent().Should().BeAssignableTo<BaseAgent>();

    [Fact]
    public void SystemPrompt_Is_Loaded()
    {
        CreateAgent().GetSystemPrompt().Should().Contain("Discovery Agent");
    }

    [Fact]
    public void Tools_Have_Correct_Names()
    {
        var names = CreateAgent().GetToolMetadata().Select(t => t.Name).ToList();
        names.Should().Contain("discover_resources");
        names.Should().Contain("get_resource_dependencies");
        names.Should().Contain("cross_subscription_query");
        names.Should().Contain("get_resource_health");
        names.Should().Contain("get_network_topology");
        names.Should().Contain("analyze_tags");
        names.Should().Contain("get_resource_changes");
        names.Should().Contain("get_orphaned_resources");
        names.Should().Contain("get_resource_metrics");
    }

    [Fact]
    public void All_Tools_Require_Authentication()
    {
        var tools = CreateAgent().GetToolMetadata();
        tools.Should().AllSatisfy(t => t.RequiresAuthentication.Should().BeTrue());
    }

    [Fact]
    public void All_Tools_Require_PIM_Read()
    {
        var tools = CreateAgent().GetToolMetadata();
        tools.Should().AllSatisfy(t => t.PimTierRequired.Should().Be(PimTier.Read));
    }
}
