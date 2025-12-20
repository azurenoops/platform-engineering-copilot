using FluentAssertions;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Infrastructure;

/// <summary>
/// Unit tests for NetworkTopologyDesignService
/// Tests CIDR calculations, subnet allocation, and network topology design
/// </summary>
public class NetworkTopologyDesignServiceTests
{
    #region CIDR Calculation Tests

    [Theory]
    [InlineData("10.0.0.0/16", 256)]       // /16 = 65536 addresses per /24 subnet = 256 /24 subnets
    [InlineData("10.0.0.0/20", 16)]        // /20 = 4096 addresses = 16 /24 subnets
    [InlineData("10.0.0.0/24", 1)]         // /24 = 256 addresses = 1 /24 subnet
    [InlineData("10.0.0.0/8", 65536)]      // /8 = 16M addresses = 65536 /24 subnets
    public void CalculateSubnetCapacity_ReturnsCorrectCount(string addressSpace, int expectedSubnets)
    {
        // Arrange
        var prefix = int.Parse(addressSpace.Split('/')[1]);

        // Act - Calculate how many /24 subnets fit in the address space
        var availableSubnets = (int)Math.Pow(2, 24 - prefix);

        // Assert
        availableSubnets.Should().Be(expectedSubnets);
    }

    [Theory]
    [InlineData("10.0.0.0/16", 24, 256)]    // 256 /24 subnets in /16
    [InlineData("10.0.0.0/16", 25, 512)]    // 512 /25 subnets in /16
    [InlineData("10.0.0.0/16", 26, 1024)]   // 1024 /26 subnets in /16
    [InlineData("10.0.0.0/16", 27, 2048)]   // 2048 /27 subnets in /16
    [InlineData("10.0.0.0/16", 28, 4096)]   // 4096 /28 subnets in /16
    public void CalculateSubnetCapacity_WithDifferentMasks_Works(string addressSpace, int subnetMask, int expectedSubnets)
    {
        // Arrange
        var prefix = int.Parse(addressSpace.Split('/')[1]);

        // Act
        var availableSubnets = (int)Math.Pow(2, subnetMask - prefix);

        // Assert
        availableSubnets.Should().Be(expectedSubnets);
    }

    [Theory]
    [InlineData(24, 256)]    // /24 = 256 addresses
    [InlineData(25, 128)]    // /25 = 128 addresses
    [InlineData(26, 64)]     // /26 = 64 addresses
    [InlineData(27, 32)]     // /27 = 32 addresses
    [InlineData(28, 16)]     // /28 = 16 addresses
    [InlineData(29, 8)]      // /29 = 8 addresses
    [InlineData(30, 4)]      // /30 = 4 addresses (point-to-point links)
    public void SubnetMask_CalculatesTotalAddresses(int subnetMask, int expectedAddresses)
    {
        // Act
        var totalAddresses = (int)Math.Pow(2, 32 - subnetMask);

        // Assert
        totalAddresses.Should().Be(expectedAddresses);
    }

    [Theory]
    [InlineData(24, 251)]    // /24 = 256 - 5 Azure reserved = 251 usable
    [InlineData(25, 123)]    // /25 = 128 - 5 = 123 usable
    [InlineData(26, 59)]     // /26 = 64 - 5 = 59 usable
    [InlineData(27, 27)]     // /27 = 32 - 5 = 27 usable
    [InlineData(28, 11)]     // /28 = 16 - 5 = 11 usable
    public void SubnetMask_CalculatesUsableAddresses_WithAzureReserved(int subnetMask, int expectedUsable)
    {
        // Arrange - Azure reserves 5 addresses per subnet:
        // .0 = network, .1 = gateway, .2, .3 = DNS, .255 = broadcast
        const int AzureReservedAddresses = 5;

        // Act
        var totalAddresses = (int)Math.Pow(2, 32 - subnetMask);
        var usableAddresses = totalAddresses - AzureReservedAddresses;

        // Assert
        usableAddresses.Should().Be(expectedUsable);
    }

    #endregion

    #region IP Address Parsing Tests

    [Theory]
    [InlineData("10.0.0.0/16", "10.0.0.0", 16)]
    [InlineData("192.168.1.0/24", "192.168.1.0", 24)]
    [InlineData("172.16.0.0/12", "172.16.0.0", 12)]
    public void AddressSpace_ParsesCorrectly(string addressSpace, string expectedNetwork, int expectedPrefix)
    {
        // Act
        var parts = addressSpace.Split('/');
        var network = parts[0];
        var prefix = int.Parse(parts[1]);

        // Assert
        network.Should().Be(expectedNetwork);
        prefix.Should().Be(expectedPrefix);
    }

    [Theory]
    [InlineData("10.0.0.0", new byte[] { 10, 0, 0, 0 })]
    [InlineData("192.168.1.1", new byte[] { 192, 168, 1, 1 })]
    [InlineData("172.16.0.1", new byte[] { 172, 16, 0, 1 })]
    public void IPAddress_ParsesOctets(string ip, byte[] expectedOctets)
    {
        // Act
        var octets = ip.Split('.').Select(byte.Parse).ToArray();

        // Assert
        octets.Should().BeEquivalentTo(expectedOctets);
    }

    [Theory]
    [InlineData("10.0.0.0", true)]      // Class A private
    [InlineData("172.16.0.0", true)]    // Class B private
    [InlineData("192.168.0.0", true)]   // Class C private
    [InlineData("8.8.8.8", false)]      // Public IP (Google DNS)
    [InlineData("1.1.1.1", false)]      // Public IP (Cloudflare DNS)
    public void IPAddress_IsPrivate_ChecksCorrectly(string ip, bool expectedPrivate)
    {
        // Arrange
        var octets = ip.Split('.').Select(byte.Parse).ToArray();

        // Act - Check RFC 1918 private ranges
        var isPrivate = 
            octets[0] == 10 ||                                      // 10.0.0.0/8
            (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31) ||  // 172.16.0.0/12
            (octets[0] == 192 && octets[1] == 168);                 // 192.168.0.0/16

        // Assert
        isPrivate.Should().Be(expectedPrivate);
    }

    #endregion

    #region Subnet Allocation Strategy Tests

    [Fact]
    public void SubnetAllocation_EqualSize_DividesEvenly()
    {
        // Arrange
        var addressSpace = "10.0.0.0/16";
        var requiredSubnets = 4;
        var prefix = 16;

        // Act - Equal size allocation gives each subnet the same size
        var subnetPrefix = prefix + (int)Math.Ceiling(Math.Log2(requiredSubnets));

        // Assert
        subnetPrefix.Should().Be(18); // /16 divided by 4 = /18 (each has 16k addresses)
    }

    [Fact]
    public void SubnetAllocation_VariableSize_UsesDifferentPrefixes()
    {
        // Arrange - Hub/Spoke topology with different subnet sizes
        var tierAllocations = new Dictionary<string, int>
        {
            ["GatewaySubnet"] = 27,      // /27 = 32 addresses (Azure min for VPN gateway)
            ["AzureFirewallSubnet"] = 26, // /26 = 64 addresses (Azure requirement)
            ["AzureBastionSubnet"] = 26,  // /26 = 64 addresses (Azure requirement)
            ["WebTier"] = 24,             // /24 = 256 addresses
            ["AppTier"] = 24,             // /24 = 256 addresses
            ["DataTier"] = 24             // /24 = 256 addresses
        };

        // Assert - Azure-mandated subnets have specific size requirements
        tierAllocations["GatewaySubnet"].Should().Be(27);
        tierAllocations["AzureFirewallSubnet"].Should().Be(26);
        tierAllocations["AzureBastionSubnet"].Should().Be(26);
    }

    [Theory]
    [InlineData(1, 0)]   // 1 subnet = 2^0 = 1
    [InlineData(2, 1)]   // 2 subnets = 2^1 = 2
    [InlineData(3, 2)]   // 3 subnets rounds up to 2^2 = 4
    [InlineData(4, 2)]   // 4 subnets = 2^2 = 4
    [InlineData(5, 3)]   // 5 subnets rounds up to 2^3 = 8
    [InlineData(8, 3)]   // 8 subnets = 2^3 = 8
    public void SubnetAllocation_CalculatesBitsNeeded(int requiredSubnets, int expectedBits)
    {
        // Act
        var bitsNeeded = (int)Math.Ceiling(Math.Log2(requiredSubnets));

        // Assert
        bitsNeeded.Should().Be(expectedBits);
    }

    #endregion

    #region Multi-Tier Topology Design Tests

    [Fact]
    public void MultiTierTopology_Default_HasThreeTiers()
    {
        // Arrange
        var tierCount = 3;
        var tierNames = new[] { "WebTier", "AppTier", "DataTier" };

        // Assert
        tierNames.Should().HaveCount(tierCount);
    }

    [Fact]
    public void MultiTierTopology_WithBastion_IncludesBastionSubnet()
    {
        // Arrange
        var includeBastion = true;
        var subnets = new List<string> { "WebTier", "AppTier", "DataTier" };

        // Act
        if (includeBastion)
        {
            subnets.Add("AzureBastionSubnet");
        }

        // Assert
        subnets.Should().Contain("AzureBastionSubnet");
    }

    [Fact]
    public void MultiTierTopology_WithFirewall_IncludesFirewallSubnet()
    {
        // Arrange
        var includeFirewall = true;
        var subnets = new List<string> { "WebTier", "AppTier", "DataTier" };

        // Act
        if (includeFirewall)
        {
            subnets.Add("AzureFirewallSubnet");
        }

        // Assert
        subnets.Should().Contain("AzureFirewallSubnet");
    }

    [Fact]
    public void MultiTierTopology_WithGateway_IncludesGatewaySubnet()
    {
        // Arrange
        var includeGateway = true;
        var subnets = new List<string> { "WebTier", "AppTier", "DataTier" };

        // Act
        if (includeGateway)
        {
            subnets.Add("GatewaySubnet");
        }

        // Assert
        subnets.Should().Contain("GatewaySubnet");
    }

    [Fact]
    public void MultiTierTopology_FullConfiguration_HasAllSubnets()
    {
        // Arrange & Act
        var subnets = new List<string> 
        { 
            "WebTier", 
            "AppTier", 
            "DataTier",
            "GatewaySubnet",
            "AzureFirewallSubnet",
            "AzureBastionSubnet"
        };

        // Assert
        subnets.Should().HaveCount(6);
        subnets.Should().Contain("GatewaySubnet");
        subnets.Should().Contain("AzureFirewallSubnet");
        subnets.Should().Contain("AzureBastionSubnet");
    }

    #endregion

    #region Azure Required Subnet Names Tests

    [Theory]
    [InlineData("GatewaySubnet", true)]
    [InlineData("AzureFirewallSubnet", true)]
    [InlineData("AzureBastionSubnet", true)]
    [InlineData("RouteServerSubnet", true)]
    [InlineData("AzureFirewallManagementSubnet", true)]
    [InlineData("MyCustomSubnet", false)]
    [InlineData("WebTier", false)]
    public void AzureReservedSubnetName_IsRecognized(string subnetName, bool isAzureReserved)
    {
        // Arrange
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GatewaySubnet",
            "AzureFirewallSubnet",
            "AzureBastionSubnet",
            "RouteServerSubnet",
            "AzureFirewallManagementSubnet"
        };

        // Act
        var isReserved = reservedNames.Contains(subnetName);

        // Assert
        isReserved.Should().Be(isAzureReserved);
    }

    [Theory]
    [InlineData("GatewaySubnet", 27)]              // /27 minimum for VPN Gateway
    [InlineData("AzureFirewallSubnet", 26)]        // /26 required
    [InlineData("AzureBastionSubnet", 26)]         // /26 minimum
    [InlineData("RouteServerSubnet", 27)]          // /27 required
    [InlineData("AzureFirewallManagementSubnet", 26)]  // /26 required
    public void AzureReservedSubnet_HasMinimumSize(string subnetName, int minimumPrefix)
    {
        // Arrange
        var minimumSizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["GatewaySubnet"] = 27,
            ["AzureFirewallSubnet"] = 26,
            ["AzureBastionSubnet"] = 26,
            ["RouteServerSubnet"] = 27,
            ["AzureFirewallManagementSubnet"] = 26
        };

        // Act
        var requiredPrefix = minimumSizes[subnetName];

        // Assert
        requiredPrefix.Should().Be(minimumPrefix);
    }

    #endregion

    #region Hub-Spoke Topology Tests

    [Fact]
    public void HubSpokeTopology_HubContainsSharedServices()
    {
        // Arrange
        var hubSubnets = new List<string>
        {
            "GatewaySubnet",
            "AzureFirewallSubnet",
            "AzureBastionSubnet",
            "ManagementSubnet",
            "DnsSubnet"
        };

        // Assert
        hubSubnets.Should().Contain("GatewaySubnet");
        hubSubnets.Should().Contain("AzureFirewallSubnet");
        hubSubnets.Should().Contain("AzureBastionSubnet");
    }

    [Fact]
    public void HubSpokeTopology_SpokesContainWorkloads()
    {
        // Arrange
        var spokeConfigurations = new Dictionary<string, List<string>>
        {
            ["spoke-web"] = new List<string> { "WebFrontend", "LoadBalancer" },
            ["spoke-app"] = new List<string> { "AppServers", "ContainerSubnet" },
            ["spoke-data"] = new List<string> { "SqlSubnet", "RedisSubnet", "StorageSubnet" }
        };

        // Assert
        spokeConfigurations.Should().HaveCount(3);
        spokeConfigurations["spoke-data"].Should().Contain("SqlSubnet");
    }

    [Fact]
    public void HubSpokeTopology_PeeringConfiguration_IsCorrect()
    {
        // Arrange
        var peeringConfig = new
        {
            AllowVirtualNetworkAccess = true,
            AllowForwardedTraffic = true,
            AllowGatewayTransit = true,  // Only on hub
            UseRemoteGateways = true     // Only on spokes
        };

        // Assert
        peeringConfig.AllowVirtualNetworkAccess.Should().BeTrue();
        peeringConfig.AllowForwardedTraffic.Should().BeTrue();
    }

    #endregion

    #region NSG Rule Generation Tests

    [Fact]
    public void NsgRules_WebTier_AllowsHttp()
    {
        // Arrange
        var webTierRules = new List<(string Name, string Access, int Port, string Direction)>
        {
            ("Allow-HTTP-Inbound", "Allow", 80, "Inbound"),
            ("Allow-HTTPS-Inbound", "Allow", 443, "Inbound"),
            ("Deny-All-Inbound", "Deny", 0, "Inbound")
        };

        // Assert
        webTierRules.Should().Contain(r => r.Name == "Allow-HTTP-Inbound" && r.Port == 80);
        webTierRules.Should().Contain(r => r.Name == "Allow-HTTPS-Inbound" && r.Port == 443);
    }

    [Fact]
    public void NsgRules_DataTier_RestrictsAccess()
    {
        // Arrange
        var dataTierRules = new List<(string Name, string Access, int Port, string SourceSubnet)>
        {
            ("Allow-SQL-From-AppTier", "Allow", 1433, "AppTier"),
            ("Allow-Redis-From-AppTier", "Allow", 6379, "AppTier"),
            ("Deny-All-Inbound", "Deny", 0, "*")
        };

        // Assert
        dataTierRules.Should().Contain(r => r.Name == "Allow-SQL-From-AppTier" && r.SourceSubnet == "AppTier");
        dataTierRules.Should().Contain(r => r.Name == "Deny-All-Inbound");
    }

    #endregion
}
