using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Discovery.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Discovery;

/// <summary>
/// T134 — Unit tests for Discovery tools.
/// </summary>
public class DiscoveryToolTests
{
    private readonly DiscoverResourcesTool _discoverTool = new(new Mock<ILogger<DiscoverResourcesTool>>().Object);
    private readonly GetResourceDependenciesTool _depsTool = new(new Mock<ILogger<GetResourceDependenciesTool>>().Object);
    private readonly CrossSubscriptionQueryTool _crossSubTool = new(new Mock<ILogger<CrossSubscriptionQueryTool>>().Object);
    private readonly GetResourceHealthTool _healthTool = new(new Mock<ILogger<GetResourceHealthTool>>().Object);
    private readonly GetNetworkTopologyTool _topoTool = new(new Mock<ILogger<GetNetworkTopologyTool>>().Object);
    private readonly AnalyzeTagsTool _tagsTool = new(new Mock<ILogger<AnalyzeTagsTool>>().Object);
    private readonly GetResourceChangesTool _changesTool = new(new Mock<ILogger<GetResourceChangesTool>>().Object);
    private readonly GetOrphanedResourcesTool _orphanedTool = new(new Mock<ILogger<GetOrphanedResourcesTool>>().Object);
    private readonly GetResourceMetricsTool _metricsTool = new(new Mock<ILogger<GetResourceMetricsTool>>().Object);

    // ─── discover_resources ───
    [Fact]
    public async Task DiscoverResources_Returns_Success_With_Resources()
    {
        var result = await _discoverTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("resources").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DiscoverResources_Filters_By_ResourceType()
    {
        var result = await _discoverTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001",
            ["resourceType"] = "virtualMachines"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("filter").GetString().Should().Be("virtualMachines");
    }

    [Fact]
    public async Task DiscoverResources_Missing_Subscription_Returns_Error()
    {
        var result = await _discoverTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = ""
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    [Fact]
    public async Task DiscoverResources_Reports_Progress()
    {
        var progress = new List<ProgressUpdate>();
        await _discoverTool.ExecuteAsync(
            new Dictionary<string, object?> { ["subscriptionId"] = "sub-001" },
            new Progress<ProgressUpdate>(p => progress.Add(p)));
        await Task.Delay(50); // Allow progress callback
        progress.Should().NotBeEmpty();
    }

    // ─── get_resource_dependencies ───
    [Fact]
    public async Task GetDependencies_Returns_Dependencies()
    {
        var result = await _depsTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = "/subscriptions/sub-001/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm-01"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("dependencies").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetDependencies_Invalid_Depth_Returns_Error()
    {
        var result = await _depsTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = "/subscriptions/sub-001/vm-01",
            ["depth"] = 10
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString().Should().Be("INVALID_DEPTH");
    }

    // ─── cross_subscription_query ───
    [Fact]
    public async Task CrossSubscription_Returns_Results()
    {
        var result = await _crossSubTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionIds"] = new[] { "sub-001", "sub-002" }
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("subscriptionCount").GetInt32().Should().Be(2);
    }

    // ─── get_resource_health ───
    [Fact]
    public async Task GetHealth_Returns_Healthy_Status()
    {
        var result = await _healthTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = "/subscriptions/sub-001/vm-01"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("healthStatus").GetString().Should().Be("Healthy");
    }

    // ─── get_network_topology ───
    [Fact]
    public async Task GetTopology_Returns_VNets_And_NSGs()
    {
        var result = await _topoTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("virtualNetworks").GetArrayLength().Should().BeGreaterThan(0);
        doc.RootElement.GetProperty("data").GetProperty("nsgs").GetArrayLength().Should().BeGreaterThan(0);
    }

    // ─── analyze_tags ───
    [Fact]
    public async Task AnalyzeTags_Returns_Coverage_Stats()
    {
        var result = await _tagsTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("compliancePercentage").GetDouble().Should().BeGreaterThan(0);
    }

    // ─── get_resource_changes ───
    [Fact]
    public async Task GetChanges_Returns_Change_History()
    {
        var result = await _changesTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = "/subscriptions/sub-001/vm-01"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("changes").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetChanges_Invalid_Lookback_Returns_Error()
    {
        var result = await _changesTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = "/subscriptions/sub-001/vm-01",
            ["lookbackHours"] = 500
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    // ─── get_orphaned_resources ───
    [Fact]
    public async Task GetOrphaned_Returns_Orphaned_Resources()
    {
        var result = await _orphanedTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("totalOrphaned").GetInt32().Should().BeGreaterThan(0);
    }

    // ─── get_resource_metrics ───
    [Fact]
    public async Task GetMetrics_Returns_Metrics()
    {
        var result = await _metricsTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = "/subscriptions/sub-001/vm-01"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("metrics").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetMetrics_Missing_ResourceId_Returns_Error()
    {
        var result = await _metricsTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceId"] = ""
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    // ─── Tool metadata verification ───
    [Fact] public void DiscoverResources_RequiresAuth() => _discoverTool.RequiresAuthentication.Should().BeTrue();
    [Fact] public void DiscoverResources_PIM_Read() => _discoverTool.PimTierRequired.Should().Be(PimTier.Read);
    [Fact] public void Dependencies_RequiresAuth() => _depsTool.RequiresAuthentication.Should().BeTrue();
    [Fact] public void CrossSub_RequiresAuth() => _crossSubTool.RequiresAuthentication.Should().BeTrue();
    [Fact] public void Health_RequiresAuth() => _healthTool.RequiresAuthentication.Should().BeTrue();
    [Fact] public void Topology_RequiresAuth() => _topoTool.RequiresAuthentication.Should().BeTrue();
    [Fact] public void Tags_RequiresAuth() => _tagsTool.RequiresAuthentication.Should().BeTrue();
    [Fact] public void Changes_RequiresAuth() => _changesTool.RequiresAuthentication.Should().BeTrue();
    [Fact] public void Orphaned_RequiresAuth() => _orphanedTool.RequiresAuthentication.Should().BeTrue();
    [Fact] public void Metrics_RequiresAuth() => _metricsTool.RequiresAuthentication.Should().BeTrue();
}
