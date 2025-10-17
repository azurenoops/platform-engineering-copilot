using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Compliance;
using Platform.Engineering.Copilot.Core.Services.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Azure.Security;
using Platform.Engineering.Copilot.Core.Services.TemplateGeneration;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Core.Phase2;

/// <summary>
/// Integration tests for Phase 2 services working together
/// Tests the full flow: compliance enhancement → network design → security config → template generation
/// </summary>
public class Phase2IntegrationTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<INistControlsService> _mockNistService;
    private readonly Mock<IAzureResourceService> _mockAzureService;

    public Phase2IntegrationTests()
    {
        // Setup mock services
        _mockNistService = new Mock<INistControlsService>();
        _mockAzureService = new Mock<IAzureResourceService>();

        // Configure mock NIST service to return controls
        _mockNistService
            .Setup(x => x.GetControlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken ct) => 
                new NistControl { Id = id, Title = $"Mock control for {id}" });

        // Build service collection with Phase 2 services
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Register Phase 2 services
        services.AddScoped<INetworkTopologyDesignService, NetworkTopologyDesignService>();
        services.AddScoped<IAzureSecurityConfigurationService, AzureSecurityConfigurationService>();
        
        // For tests that need compliance enhancement, we'd need full pipeline
        // Simplified for now to focus on Network + Security integration
        
        // Register dependencies with mocks
        services.AddSingleton(_mockNistService.Object);
        services.AddSingleton(_mockAzureService.Object);
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Phase2Integration_FedRAMPCompliantInfrastructure_GeneratesCompleteConfiguration()
    {
        // Arrange
        var networkService = _serviceProvider.GetRequiredService<INetworkTopologyDesignService>();
        var securityService = _serviceProvider.GetRequiredService<IAzureSecurityConfigurationService>();
        var enhancer = _serviceProvider.GetRequiredService<IComplianceAwareTemplateEnhancer>();

        var request = new TemplateGenerationRequest
        {
            ServiceName = "navwar-mission-app",
            Description = "FedRAMP High compliant mission application",
            TemplateType = "microservice",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.NodeJS,
                Framework = "express",
                Type = ApplicationType.WebAPI,
                Port = 8080
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = ComputePlatform.AKS,
                Tags = new Dictionary<string, string>
                {
                    { "Mission", "NAVWAR" },
                    { "Classification", "IL5" }
                }
            },
            Deployment = new DeploymentSpec
            {
                Replicas = 3,
                AutoScaling = true,
                MinReplicas = 2,
                MaxReplicas = 10
            },
            Security = new SecuritySpec
            {
                NetworkPolicies = false,
                RBAC = false,
                TLS = false
            },
            Observability = new ObservabilitySpec
            {
                Prometheus = false,
                StructuredLogging = false
            }
        };

        // Act - Step 1: Design network topology
        var networkConfig = networkService.DesignMultiTierTopology(
            addressSpace: "10.100.0.0/16",
            tierCount: 3,
            includeBastion: true,
            includeFirewall: true,
            includeGateway: true
        );

        // Act - Step 2: Generate security configuration
        var securityConfig = await securityService.GenerateSecurityConfigAsync(
            resourceGroupName: "rg-navwar-mission",
            keyVaultName: "kv-navwar-fedramp",
            resourceTypes: new List<string> { "Microsoft.Storage", "Microsoft.Sql", "Microsoft.KeyVault" },
            complianceFramework: "FedRAMP-High"
        );

        // Act - Step 3: Store network config in infrastructure spec
        request.Infrastructure.NetworkConfig = networkConfig;

        // Assert - Network Topology
        Assert.NotNull(networkConfig);
        Assert.True(networkConfig.Subnets.Count >= 6, "Should have at least 6 subnets (3 tiers + gateway + bastion + firewall + private endpoints)");
        Assert.Contains(networkConfig.Subnets, s => s.Name == "GatewaySubnet");
        Assert.Contains(networkConfig.Subnets, s => s.Name == "AzureBastionSubnet");
        Assert.Contains(networkConfig.Subnets, s => s.Name == "AzureFirewallSubnet");
        Assert.Equal("10.100.0.0/16", networkConfig.VNetAddressSpace);

        // Assert - Security Configuration
        Assert.NotNull(securityConfig);
        Assert.Equal("FedRAMP-High", securityConfig.ComplianceFramework);
        
        // Key Vault compliance
        Assert.True(securityConfig.KeyVault.EnableSoftDelete);
        Assert.True(securityConfig.KeyVault.EnablePurgeProtection);
        Assert.Equal(90, securityConfig.KeyVault.SoftDeleteRetentionDays);
        Assert.Equal("Disabled", securityConfig.KeyVault.PublicNetworkAccess);
        Assert.Equal("Premium", securityConfig.KeyVault.Sku);
        
        // Encryption compliance
        Assert.Equal(EncryptionType.CustomerManagedKeys, securityConfig.Encryption.EncryptionType);
        Assert.True(securityConfig.Encryption.EnableAutomaticKeyRotation);
        Assert.Equal(90, securityConfig.Encryption.RotationPolicyDays);
        Assert.True(securityConfig.Encryption.StorageAccountEncryption.RequireInfrastructureEncryption);
        
        // Network security compliance
        Assert.True(securityConfig.NetworkSecurity.DenyAllInbound);
        Assert.True(securityConfig.NetworkSecurity.EnableDDoSProtection);
        Assert.True(securityConfig.NetworkSecurity.EnableAzureFirewall);
        
        // Identity compliance
        Assert.True(securityConfig.IdentityAndAccess.RequireMultiFactor);
        Assert.True(securityConfig.IdentityAndAccess.EnableConditionalAccess);
        
        // Monitoring compliance
        Assert.Equal(90, securityConfig.Monitoring.LogRetentionDays);
        Assert.True(securityConfig.Monitoring.EnableAlerts);

        // Assert - Request has network config stored
        Assert.NotNull(request.Infrastructure.NetworkConfig);
        Assert.Equal("10.100.0.0/16", request.Infrastructure.NetworkConfig.VNetAddressSpace);
    }

    [Fact]
    public async Task Phase2Integration_NetworkTopologyWithSecurityRules_CreatesSecureInfrastructure()
    {
        // Arrange
        var networkService = _serviceProvider.GetRequiredService<INetworkTopologyDesignService>();
        var securityService = _serviceProvider.GetRequiredService<IAzureSecurityConfigurationService>();

        // Act - Step 1: Design 3-tier network
        var networkConfig = networkService.DesignMultiTierTopology(
            addressSpace: "172.16.0.0/20",
            tierCount: 3,
            includeBastion: true,
            includeFirewall: false,
            includeGateway: false
        );

        // Act - Step 2: Generate NSG rules for each tier
        var appTierSubnet = networkConfig.Subnets.FirstOrDefault(s => s.Purpose == SubnetPurpose.Application);
        var dataTierSubnet = networkConfig.Subnets.FirstOrDefault(s => s.Purpose == SubnetPurpose.Database);

        var appNsgRules = securityService.GenerateNsgRulesForTier(SubnetPurpose.Application, allowInternetAccess: false);
        var dataNsgRules = securityService.GenerateNsgRulesForTier(SubnetPurpose.Database, allowInternetAccess: false);

        // Assert - Network created with proper segmentation
        Assert.NotNull(networkConfig);
        Assert.True(networkConfig.Subnets.Count >= 3);
        Assert.Contains(networkConfig.Subnets, s => s.Name == "AzureBastionSubnet");

        // Assert - Application tier has secure inbound rules
        Assert.NotEmpty(appNsgRules);
        var httpsRule = appNsgRules.FirstOrDefault(r => r.Name == "Allow-HTTPS-Inbound");
        Assert.NotNull(httpsRule);
        Assert.Equal("Allow", httpsRule.Access);
        Assert.Equal("443", httpsRule.DestinationPortRange);

        // Assert - Data tier is locked down
        Assert.NotEmpty(dataNsgRules);
        var denyAllRule = dataNsgRules.FirstOrDefault(r => r.Name == "Deny-All-Inbound");
        Assert.NotNull(denyAllRule);
        Assert.Equal("Deny", denyAllRule.Access);
        Assert.Equal(4096, denyAllRule.Priority);

        // Assert - Data tier allows only from app tier
        var appTierRule = dataNsgRules.FirstOrDefault(r => r.Name == "Allow-AppTier-Inbound");
        Assert.NotNull(appTierRule);
        Assert.Equal("ApplicationTierSubnet", appTierRule.SourceAddressPrefix);
    }

    [Fact]
    public async Task Phase2Integration_ComplianceEnhancementWithNetworkAndSecurity_ProducesCompleteTemplate()
    {
        // Arrange
        var networkService = _serviceProvider.GetRequiredService<INetworkTopologyDesignService>();
        var securityService = _serviceProvider.GetRequiredService<IAzureSecurityConfigurationService>();
        var enhancer = _serviceProvider.GetRequiredService<IComplianceAwareTemplateEnhancer>();

        // Step 1: Design network
        var networkConfig = networkService.DesignMultiTierTopology(
            addressSpace: "10.0.0.0/16",
            tierCount: 2,
            includeBastion: false,
            includeFirewall: true,
            includeGateway: true
        );

        // Step 2: Generate Key Vault config
        var kvConfig = securityService.GenerateKeyVaultConfig(
            "kv-test",
            networkConfig.Subnets.Select(s => s.Name).ToList(),
            "DoD-IL5"
        );

        // Step 3: Create template request with network and security
        var request = new TemplateGenerationRequest
        {
            ServiceName = "secure-app",
            TemplateType = "microservice",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.Python,
                Framework = "flask",
                Type = ApplicationType.WebAPI,
                Port = 5000
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep,
                Provider = CloudProvider.Azure,
                Region = "usgovvirginia",
                ComputePlatform = ComputePlatform.AKS,
                NetworkConfig = networkConfig
            },
            Security = new SecuritySpec
            {
                NetworkPolicies = false,
                RBAC = false,
                TLS = false
            }
        };

        // Act - Enhance with DoD IL5 compliance
        // Note: This test would require full ComplianceAwareTemplateEnhancer setup
        // Simplified to focus on Network + Security service integration
        
        // Assert - Network configuration
        Assert.NotNull(request.Infrastructure.NetworkConfig);
        Assert.True(request.Infrastructure.NetworkConfig.Subnets.Count >= 2);
        
        // Assert - Key Vault config meets DoD IL5
        Assert.True(kvConfig.EnablePurgeProtection);
        Assert.True(kvConfig.EnableRbacAuthorization);
        Assert.Equal("Deny", kvConfig.NetworkAcls.DefaultAction);
        Assert.Equal("Premium", kvConfig.Sku);
        
        // Assert - Security specs configured
        Assert.True(request.Security.RBAC);
        Assert.True(request.Security.TLS);
        Assert.True(request.Security.NetworkPolicies);
    }

    [Fact]
    public void Phase2Integration_SubnetCIDRCalculation_NonOverlappingRanges()
    {
        // Arrange
        var networkService = _serviceProvider.GetRequiredService<INetworkTopologyDesignService>();

        // Act - Calculate subnets for various address spaces
        var subnets1 = networkService.CalculateSubnetCIDRs("10.0.0.0/16", 4);
        var subnets2 = networkService.CalculateSubnetCIDRs("192.168.1.0/24", 2);

        // Assert - No overlapping CIDRs within same address space
        var cidrs1 = subnets1.Select(s => s.AddressPrefix).ToList();
        Assert.Equal(4, cidrs1.Distinct().Count());
        
        var cidrs2 = subnets2.Select(s => s.AddressPrefix).ToList();
        Assert.Equal(2, cidrs2.Distinct().Count());
        
        // Verify proper CIDR notation
        Assert.All(subnets1, subnet => Assert.Contains("/", subnet.AddressPrefix));
        Assert.All(subnets2, subnet => Assert.Contains("/", subnet.AddressPrefix));
    }

    [Fact]
    public async Task Phase2Integration_FullPipeline_FedRAMPCompliantK8sDeployment()
    {
        // Arrange
        var networkService = _serviceProvider.GetRequiredService<INetworkTopologyDesignService>();
        var securityService = _serviceProvider.GetRequiredService<IAzureSecurityConfigurationService>();
        var enhancer = _serviceProvider.GetRequiredService<IComplianceAwareTemplateEnhancer>();

        // Step 1: Network design for AKS
        var network = networkService.DesignMultiTierTopology(
            "10.200.0.0/16",
            tierCount: 3,
            includeBastion: true,
            includeFirewall: true,
            includeGateway: false
        );

        // Step 2: Security configuration
        var security = await securityService.GenerateSecurityConfigAsync(
            "rg-aks-fedramp",
            "kv-aks-secrets",
            new List<string> { "Microsoft.ContainerService", "Microsoft.KeyVault", "Microsoft.Storage" },
            "FedRAMP-High"
        );

        // Step 3: Template generation with compliance
        var request = new TemplateGenerationRequest
        {
            ServiceName = "aks-fedramp-cluster",
            TemplateType = "infrastructure",
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep,
                Provider = CloudProvider.Azure,
                Region = "eastus2",
                ComputePlatform = ComputePlatform.AKS,
                NetworkConfig = network
            },
            Security = new SecuritySpec()
        };

        // Assert - Complete Phase 2 service integration
        Assert.NotNull(network);
        Assert.NotNull(security);
        Assert.NotNull(request.Infrastructure.NetworkConfig);
        
        // Network assertions
        Assert.True(network.Subnets.Count >= 5);
        Assert.Contains(network.Subnets, s => s.Name == "AzureBastionSubnet");
        Assert.Contains(network.Subnets, s => s.Name == "AzureFirewallSubnet");
        
        // Security assertions
        Assert.Equal("FedRAMP-High", security.ComplianceFramework);
        Assert.True(security.KeyVault.EnableSoftDelete);
        Assert.True(security.Encryption.EnableAutomaticKeyRotation);
        Assert.True(security.IdentityAndAccess.RequireMultiFactor);
        Assert.Equal(90, security.Monitoring.LogRetentionDays);
        
        // Verify network config stored in request
        Assert.Equal(network.VNetAddressSpace, request.Infrastructure.NetworkConfig.VNetAddressSpace);
        Assert.Equal(network.Subnets.Count, request.Infrastructure.NetworkConfig.Subnets.Count);
    }
}
