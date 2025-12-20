using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual.Agents;

/// <summary>
/// Manual tests for Discovery Agent via MCP HTTP API.
/// Tests resource discovery, inventory, relationship mapping, and orphan detection.
/// 
/// Prerequisites:
/// - MCP server running in HTTP mode: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5100
/// - Azure CLI authenticated with Reader permissions
/// </summary>
public class DiscoveryAgentManualTests : McpHttpTestBase
{
    private const string ExpectedIntent = "discovery";

    public DiscoveryAgentManualTests(ITestOutputHelper output) : base(output) { }

    #region Resource Discovery

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task DiscoverResources_FullSubscription_ShouldReturnInventory()
    {
        // Arrange
        var message = "Discover all resources in my Azure subscription and provide a summary grouped by resource type and location";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-full-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task DiscoverResources_ByResourceGroup_ShouldReturnInventory()
    {
        // Arrange
        var message = "List all resources in the production resource group with their current status";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-rg-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task DiscoverResources_ComputeOnly_ShouldReturnFilteredInventory()
    {
        // Arrange
        var message = "Discover all compute resources (VMs, VMSS, AKS clusters, App Services) in the subscription";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-compute-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task DiscoverResources_DatabasesOnly_ShouldReturnFilteredInventory()
    {
        // Arrange
        var message = "List all database resources including SQL databases, Cosmos DB, and Redis caches";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-databases-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Network Resource Mapping

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task MapNetworkResources_AllNetworking_ShouldReturnTopology()
    {
        // Arrange
        var message = "Map all network resources including virtual networks, subnets, NSGs, and show their relationships and peering connections";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-network-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task MapNetworkResources_LoadBalancers_ShouldReturnConfiguration()
    {
        // Arrange
        var message = "List all load balancers and application gateways with their backend pool configurations";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-lb-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task MapNetworkResources_PublicEndpoints_ShouldReturnList()
    {
        // Arrange
        var message = "Show all public IP addresses and their associated resources";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-publicip-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Orphaned Resource Detection

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task DetectOrphanedResources_All_ShouldReturnFindings()
    {
        // Arrange
        var message = "Find orphaned resources like unattached disks, unused public IPs, and empty network security groups";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-orphaned-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task DetectOrphanedResources_Disks_ShouldReturnFindings()
    {
        // Arrange
        var message = "Find all unattached managed disks and their sizes";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-orphaned-disks-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task DetectOrphanedResources_NICs_ShouldReturnFindings()
    {
        // Arrange
        var message = "Find network interfaces that are not attached to any virtual machine";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-orphaned-nics-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Resource Dependency Analysis

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task AnalyzeDependencies_ResourceGroup_ShouldReturnDependencyMap()
    {
        // Arrange
        var message = "Analyze dependencies for the web application resource group and show what would be affected if we deleted the primary database";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-deps-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task AnalyzeDependencies_SpecificResource_ShouldReturnDependencyMap()
    {
        // Arrange
        var message = "Show all resources that depend on the primary key vault";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-deps-keyvault-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task AnalyzeDependencies_VirtualNetwork_ShouldReturnDependencyMap()
    {
        // Arrange
        var message = "Show all resources connected to the production virtual network and their network dependencies";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-deps-vnet-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Tag Inventory

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task InventoryTags_AllTags_ShouldReturnSummary()
    {
        // Arrange
        var message = "Show me all unique tags in use across my subscription and how many resources have each tag";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-tags-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task InventoryTags_MissingRequired_ShouldReturnReport()
    {
        // Arrange
        var message = "Find all resources missing the required Environment, Owner, or CostCenter tags";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-tags-missing-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task InventoryTags_ByEnvironment_ShouldReturnReport()
    {
        // Arrange
        var message = "Show resource count by Environment tag value (dev, staging, production)";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-tags-env-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Resource Health

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task CheckResourceHealth_All_ShouldReturnStatus()
    {
        // Arrange
        var message = "Show resource health status for all critical resources in the subscription";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-health-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Discovery")]
    public async Task CheckResourceHealth_Degraded_ShouldReturnFindings()
    {
        // Arrange
        var message = "Find all resources with degraded or unhealthy status";

        // Act
        var response = await SendChatRequestAsync(message, "discovery-health-degraded-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion
}
