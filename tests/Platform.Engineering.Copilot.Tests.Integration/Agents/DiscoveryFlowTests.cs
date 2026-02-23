using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Agents.Discovery;
using Platform.Engineering.Copilot.Agents.Discovery.Tools;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// T134 — Integration tests for Discovery agent multi-step flows.
/// </summary>
public class DiscoveryFlowTests
{
    private readonly DiscoverResourcesTool _discover = new(new Mock<ILogger<DiscoverResourcesTool>>().Object);
    private readonly GetResourceDependenciesTool _deps = new(new Mock<ILogger<GetResourceDependenciesTool>>().Object);
    private readonly GetResourceHealthTool _health = new(new Mock<ILogger<GetResourceHealthTool>>().Object);
    private readonly GetNetworkTopologyTool _network = new(new Mock<ILogger<GetNetworkTopologyTool>>().Object);
    private readonly AnalyzeTagsTool _tags = new(new Mock<ILogger<AnalyzeTagsTool>>().Object);
    private readonly GetResourceChangesTool _changes = new(new Mock<ILogger<GetResourceChangesTool>>().Object);
    private readonly GetOrphanedResourcesTool _orphaned = new(new Mock<ILogger<GetOrphanedResourcesTool>>().Object);
    private readonly GetResourceMetricsTool _metrics = new(new Mock<ILogger<GetResourceMetricsTool>>().Object);
    private readonly CrossSubscriptionQueryTool _crossSub = new(new Mock<ILogger<CrossSubscriptionQueryTool>>().Object);

    [Fact]
    public async Task Discover_Then_Health_Then_Dependencies_Flow()
    {
        // Step 1: Discover resources in subscription
        var discoverResult = await _discover.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var discovered = JsonDocument.Parse(discoverResult);
        discovered.RootElement.GetProperty("status").GetString().Should().Be("success");
        discovered.RootElement.GetProperty("data").GetProperty("resourceCount").GetInt32().Should().BeGreaterThan(0);

        // Step 2: Check health of a discovered resource
        var healthResult = await _health.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = "/subscriptions/sub-001/resourceGroups/rg-platform/providers/Microsoft.Compute/virtualMachines/vm-app-01"
        });
        var health = JsonDocument.Parse(healthResult);
        health.RootElement.GetProperty("status").GetString().Should().Be("success");

        // Step 3: Get dependency graph
        var depResult = await _deps.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = "/subscriptions/sub-001/resourceGroups/rg-platform/providers/Microsoft.Compute/virtualMachines/vm-app-01",
            ["depth"] = 3
        });
        var deps = JsonDocument.Parse(depResult);
        deps.RootElement.GetProperty("status").GetString().Should().Be("success");
        deps.RootElement.GetProperty("data").GetProperty("blastRadius")
            .GetProperty("directDependents").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Network_Topology_And_Tags_Analysis_Flow()
    {
        // Step 1: Get network topology
        var networkResult = await _network.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var net = JsonDocument.Parse(networkResult);
        net.RootElement.GetProperty("status").GetString().Should().Be("success");
        net.RootElement.GetProperty("data").GetProperty("virtualNetworks").GetArrayLength().Should().BeGreaterThan(0);

        // Step 2: Analyze tags for governance compliance
        var tagResult = await _tags.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var tags = JsonDocument.Parse(tagResult);
        tags.RootElement.GetProperty("status").GetString().Should().Be("success");
        tags.RootElement.GetProperty("data").GetProperty("compliancePercentage").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Orphaned_Resources_And_Metrics_Flow()
    {
        // Step 1: Find orphaned resources
        var orphanResult = await _orphaned.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var orphans = JsonDocument.Parse(orphanResult);
        orphans.RootElement.GetProperty("status").GetString().Should().Be("success");
        orphans.RootElement.GetProperty("data").GetProperty("totalOrphaned").GetInt32().Should().BeGreaterThan(0);

        // Step 2: Get metrics for a resource
        var metricResult = await _metrics.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = "/subscriptions/sub-001/resourceGroups/rg-platform/providers/Microsoft.Compute/virtualMachines/vm-app-01"
        });
        var metrics = JsonDocument.Parse(metricResult);
        metrics.RootElement.GetProperty("status").GetString().Should().Be("success");
        metrics.RootElement.GetProperty("data").GetProperty("metrics").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Cross_Subscription_Query_Flow()
    {
        var csResult = await _crossSub.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionIds"] = new[] { "sub-001", "sub-002" },
            ["resourceType"] = "Microsoft.Compute/virtualMachines"
        });
        var cs = JsonDocument.Parse(csResult);
        cs.RootElement.GetProperty("status").GetString().Should().Be("success");
        cs.RootElement.GetProperty("data").GetProperty("subscriptionCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Agent_Registers_All_Nine_Tools()
    {
        var agent = new DiscoveryAgent(
            new Mock<ILogger<DiscoveryAgent>>().Object,
            new BaseTool[] { _discover, _deps, _crossSub, _health, _network, _tags, _changes, _orphaned, _metrics });

        var tools = agent.GetToolMetadata();
        tools.Should().HaveCount(9);
        tools.Select(t => t.Name).Should().Contain("discover_resources");
        tools.Select(t => t.Name).Should().Contain("get_resource_dependencies");
        tools.Select(t => t.Name).Should().Contain("cross_subscription_query");
    }
}
