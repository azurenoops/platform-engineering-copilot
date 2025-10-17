using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Azure.Security;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.Security;

/// <summary>
/// Unit tests for AzureSecurityConfigurationService
/// Tests FedRAMP High/DoD IL5 security configuration generation
/// </summary>
public class AzureSecurityConfigurationServiceTests
{
    private readonly Mock<ILogger<AzureSecurityConfigurationService>> _mockLogger;
    private readonly Mock<IAzureResourceService> _mockAzureService;
    private readonly AzureSecurityConfigurationService _service;

    public AzureSecurityConfigurationServiceTests()
    {
        _mockLogger = new Mock<ILogger<AzureSecurityConfigurationService>>();
        _mockAzureService = new Mock<IAzureResourceService>();
        _service = new AzureSecurityConfigurationService(_mockLogger.Object, _mockAzureService.Object);
    }

    #region Key Vault Configuration Tests

    [Fact]
    public void GenerateKeyVaultConfig_FedRAMPHigh_EnablesSoftDelete()
    {
        // Act
        var result = _service.GenerateKeyVaultConfig(
            "test-kv", 
            new List<string> { "AppSubnet" }, 
            "FedRAMP-High");

        // Assert
        Assert.True(result.EnableSoftDelete);
        Assert.Equal(90, result.SoftDeleteRetentionDays);
    }

    [Fact]
    public void GenerateKeyVaultConfig_FedRAMPHigh_EnablesPurgeProtection()
    {
        // Act
        var result = _service.GenerateKeyVaultConfig(
            "test-kv", 
            new List<string> { "AppSubnet" }, 
            "FedRAMP-High");

        // Assert
        Assert.True(result.EnablePurgeProtection);
    }

    [Fact]
    public void GenerateKeyVaultConfig_FedRAMPHigh_UsesRBACAuthorization()
    {
        // Act
        var result = _service.GenerateKeyVaultConfig(
            "test-kv", 
            new List<string> { "AppSubnet" }, 
            "FedRAMP-High");

        // Assert
        Assert.True(result.EnableRbacAuthorization);
    }

    [Fact]
    public void GenerateKeyVaultConfig_FedRAMPHigh_DisablesPublicAccess()
    {
        // Act
        var result = _service.GenerateKeyVaultConfig(
            "test-kv", 
            new List<string> { "AppSubnet" }, 
            "FedRAMP-High");

        // Assert
        Assert.Equal("Disabled", result.PublicNetworkAccess);
    }

    [Fact]
    public void GenerateKeyVaultConfig_FedRAMPHigh_UsesPremiumSKU()
    {
        // Act
        var result = _service.GenerateKeyVaultConfig(
            "test-kv", 
            new List<string> { "AppSubnet" }, 
            "FedRAMP-High");

        // Assert
        Assert.Equal("Premium", result.Sku); // Premium for HSM-backed keys
    }

    [Fact]
    public void GenerateKeyVaultConfig_NetworkACLs_DenyByDefault()
    {
        // Act
        var result = _service.GenerateKeyVaultConfig(
            "test-kv", 
            new List<string> { "AppSubnet" }, 
            "FedRAMP-High");

        // Assert
        Assert.Equal("Deny", result.NetworkAcls.DefaultAction);
        Assert.Equal("AzureServices", result.NetworkAcls.Bypass);
    }

    [Fact]
    public void GenerateKeyVaultConfig_NetworkACLs_AllowsSpecifiedSubnets()
    {
        // Arrange
        var allowedSubnets = new List<string> { "AppSubnet", "DataSubnet" };

        // Act
        var result = _service.GenerateKeyVaultConfig(
            "test-kv", 
            allowedSubnets, 
            "FedRAMP-High");

        // Assert
        Assert.Equal(allowedSubnets, result.NetworkAcls.AllowedSubnets);
        Assert.Empty(result.NetworkAcls.AllowedIpRanges); // No public IPs for security
    }

    [Fact]
    public void GenerateKeyVaultConfig_FedRAMPHigh_DisablesDeploymentFeatures()
    {
        // Act
        var result = _service.GenerateKeyVaultConfig(
            "test-kv", 
            new List<string> { "AppSubnet" }, 
            "FedRAMP-High");

        // Assert
        Assert.False(result.EnabledForDeployment); // Disabled for compliance
        Assert.True(result.EnabledForDiskEncryption); // Enabled for disk encryption
        Assert.False(result.EnabledForTemplateDeployment); // Disabled for compliance
    }

    #endregion

    #region NSG Rules Tests - Application Tier

    [Fact]
    public void GenerateNsgRulesForTier_Application_AllowsHTTPSFromGateway()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(SubnetPurpose.Application, allowInternetAccess: false);

        // Assert
        var httpsRule = rules.FirstOrDefault(r => r.Name == "Allow-HTTPS-Inbound");
        Assert.NotNull(httpsRule);
        Assert.Equal("Allow", httpsRule.Access);
        Assert.Equal("Inbound", httpsRule.Direction);
        Assert.Equal("443", httpsRule.DestinationPortRange);
        Assert.Equal("GatewaySubnet", httpsRule.SourceAddressPrefix);
    }

    [Fact]
    public void GenerateNsgRulesForTier_Application_AllowsOutboundToDataTier()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(SubnetPurpose.Application, allowInternetAccess: false);

        // Assert
        var dataRule = rules.FirstOrDefault(r => r.Name == "Allow-DataTier-Outbound");
        Assert.NotNull(dataRule);
        Assert.Equal("Allow", dataRule.Access);
        Assert.Equal("Outbound", dataRule.Direction);
        Assert.Contains("1433", dataRule.DestinationPortRange); // SQL Server
        Assert.Contains("5432", dataRule.DestinationPortRange); // PostgreSQL
        Assert.Contains("3306", dataRule.DestinationPortRange); // MySQL
    }

    #endregion

    #region NSG Rules Tests - Database Tier

    [Fact]
    public void GenerateNsgRulesForTier_Database_AllowsOnlyFromAppTier()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(SubnetPurpose.Database, allowInternetAccess: false);

        // Assert
        var allowRule = rules.FirstOrDefault(r => r.Name == "Allow-AppTier-Inbound");
        Assert.NotNull(allowRule);
        Assert.Equal("Allow", allowRule.Access);
        Assert.Equal("Inbound", allowRule.Direction);
        Assert.Equal("ApplicationTierSubnet", allowRule.SourceAddressPrefix);
    }

    [Fact]
    public void GenerateNsgRulesForTier_Database_DeniesAllOtherInbound()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(SubnetPurpose.Database, allowInternetAccess: false);

        // Assert
        var denyRule = rules.FirstOrDefault(r => r.Name == "Deny-All-Inbound");
        Assert.NotNull(denyRule);
        Assert.Equal("Deny", denyRule.Access);
        Assert.Equal("Inbound", denyRule.Direction);
        Assert.Equal(4096, denyRule.Priority); // Low priority (evaluated last)
        Assert.Equal("*", denyRule.SourceAddressPrefix);
    }

    #endregion

    #region NSG Rules Tests - Application Gateway

    [Fact]
    public void GenerateNsgRulesForTier_Gateway_WithInternet_AllowsHTTPSFromInternet()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(
            SubnetPurpose.ApplicationGateway, 
            allowInternetAccess: true);

        // Assert
        var httpsRule = rules.FirstOrDefault(r => r.Name == "Allow-HTTPS-Internet");
        Assert.NotNull(httpsRule);
        Assert.Equal("Allow", httpsRule.Access);
        Assert.Equal("Internet", httpsRule.SourceAddressPrefix);
        Assert.Equal("443", httpsRule.DestinationPortRange);
    }

    [Fact]
    public void GenerateNsgRulesForTier_Gateway_WithoutInternet_AllowsHTTPSFromVNetOnly()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(
            SubnetPurpose.ApplicationGateway, 
            allowInternetAccess: false);

        // Assert
        var httpsRule = rules.FirstOrDefault(r => r.Name == "Allow-HTTPS-VirtualNetwork");
        Assert.NotNull(httpsRule);
        Assert.Equal("VirtualNetwork", httpsRule.SourceAddressPrefix);
        
        // Should not have internet rule
        Assert.DoesNotContain(rules, r => r.SourceAddressPrefix == "Internet");
    }

    [Fact]
    public void GenerateNsgRulesForTier_Gateway_AllowsGatewayManager()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(SubnetPurpose.ApplicationGateway, allowInternetAccess: true);

        // Assert
        var gatewayRule = rules.FirstOrDefault(r => r.Name == "Allow-GatewayManager");
        Assert.NotNull(gatewayRule);
        Assert.Equal("GatewayManager", gatewayRule.SourceAddressPrefix);
        Assert.Equal("65200-65535", gatewayRule.DestinationPortRange); // Azure management ports
    }

    #endregion

    #region NSG Rules Tests - Private Endpoints

    [Fact]
    public void GenerateNsgRulesForTier_PrivateEndpoints_AllowsVNetTraffic()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(SubnetPurpose.PrivateEndpoints, allowInternetAccess: false);

        // Assert
        var vnetRule = rules.FirstOrDefault(r => r.Name == "Allow-VNet-Inbound");
        Assert.NotNull(vnetRule);
        Assert.Equal("VirtualNetwork", vnetRule.SourceAddressPrefix);
        Assert.Equal("*", vnetRule.DestinationPortRange); // All ports for private endpoints
    }

    #endregion

    #region NSG Rules Tests - Common Rules

    [Fact]
    public void GenerateNsgRulesForTier_AllTiers_AllowAzureCloudOutbound()
    {
        // Arrange
        var purposes = new[] 
        { 
            SubnetPurpose.Application, 
            SubnetPurpose.Database, 
            SubnetPurpose.ApplicationGateway 
        };

        foreach (var purpose in purposes)
        {
            // Act
            var rules = _service.GenerateNsgRulesForTier(purpose, allowInternetAccess: false);

            // Assert
            var azureRule = rules.FirstOrDefault(r => r.Name == "Allow-AzureCloud-Outbound");
            Assert.NotNull(azureRule);
            Assert.Equal("Outbound", azureRule.Direction);
            Assert.Equal("AzureCloud", azureRule.DestinationAddressPrefix);
            Assert.Equal("443", azureRule.DestinationPortRange); // HTTPS only
        }
    }

    [Fact]
    public void GenerateNsgRulesForTier_WithoutInternetAccess_DeniesInternetOutbound()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(
            SubnetPurpose.Application, 
            allowInternetAccess: false);

        // Assert
        var denyRule = rules.FirstOrDefault(r => r.Name == "Deny-Internet-Outbound");
        Assert.NotNull(denyRule);
        Assert.Equal("Deny", denyRule.Access);
        Assert.Equal("Outbound", denyRule.Direction);
        Assert.Equal("Internet", denyRule.DestinationAddressPrefix);
    }

    [Fact]
    public void GenerateNsgRulesForTier_WithInternetAccess_NoInternetDenyRule()
    {
        // Act
        var rules = _service.GenerateNsgRulesForTier(
            SubnetPurpose.Application, 
            allowInternetAccess: true);

        // Assert
        var denyRule = rules.FirstOrDefault(r => r.Name == "Deny-Internet-Outbound");
        Assert.Null(denyRule); // Should not deny internet when access is allowed
    }

    #endregion

    #region Security Configuration Tests

    [Fact]
    public async Task GenerateSecurityConfigAsync_CreatesCompleteConfiguration()
    {
        // Act
        var result = await _service.GenerateSecurityConfigAsync(
            "test-rg", 
            "test-kv", 
            new List<string> { "Microsoft.Storage", "Microsoft.Sql" },
            "FedRAMP-High");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-rg", result.ResourceGroupName);
        Assert.Equal("FedRAMP-High", result.ComplianceFramework);
        Assert.NotNull(result.KeyVault);
        Assert.NotNull(result.Encryption);
        Assert.NotNull(result.NetworkSecurity);
        Assert.NotNull(result.IdentityAndAccess);
        Assert.NotNull(result.Monitoring);
    }

    [Fact]
    public async Task GenerateSecurityConfigAsync_Encryption_UsesCustomerManagedKeys()
    {
        // Act
        var result = await _service.GenerateSecurityConfigAsync(
            "test-rg", 
            "test-kv", 
            new List<string>(),
            "FedRAMP-High");

        // Assert
        Assert.Equal(EncryptionType.CustomerManagedKeys, result.Encryption.EncryptionType);
        Assert.True(result.Encryption.EnableAutomaticKeyRotation);
        Assert.Equal(90, result.Encryption.RotationPolicyDays); // FedRAMP requirement
    }

    [Fact]
    public async Task GenerateSecurityConfigAsync_Encryption_EnablesDoubleEncryption()
    {
        // Act
        var result = await _service.GenerateSecurityConfigAsync(
            "test-rg", 
            "test-kv", 
            new List<string>(),
            "DoD-IL5");

        // Assert
        Assert.True(result.Encryption.StorageAccountEncryption.RequireInfrastructureEncryption); // DoD IL5
        Assert.True(result.Encryption.DiskEncryption.EncryptionAtHost);
    }

    [Fact]
    public async Task GenerateSecurityConfigAsync_NetworkSecurity_EnablesDDoSProtection()
    {
        // Act
        var result = await _service.GenerateSecurityConfigAsync(
            "test-rg", 
            "test-kv", 
            new List<string>(),
            "FedRAMP-High");

        // Assert
        Assert.True(result.NetworkSecurity.EnableDDoSProtection);
        Assert.True(result.NetworkSecurity.EnableAzureFirewall);
        Assert.True(result.NetworkSecurity.DenyAllInbound);
    }

    [Fact]
    public async Task GenerateSecurityConfigAsync_Identity_RequiresMFA()
    {
        // Act
        var result = await _service.GenerateSecurityConfigAsync(
            "test-rg", 
            "test-kv", 
            new List<string>(),
            "FedRAMP-High");

        // Assert
        Assert.True(result.IdentityAndAccess.EnableManagedIdentity);
        Assert.True(result.IdentityAndAccess.RequireMultiFactor); // FedRAMP requirement
        Assert.True(result.IdentityAndAccess.EnableConditionalAccess);
        Assert.Equal("SystemAssigned", result.IdentityAndAccess.ManagedIdentityType);
    }

    [Fact]
    public async Task GenerateSecurityConfigAsync_Monitoring_Enables90DayRetention()
    {
        // Act
        var result = await _service.GenerateSecurityConfigAsync(
            "test-rg", 
            "test-kv", 
            new List<string>(),
            "FedRAMP-High");

        // Assert
        Assert.True(result.Monitoring.EnableDiagnosticSettings);
        Assert.Equal(90, result.Monitoring.LogRetentionDays); // FedRAMP minimum
        Assert.Contains("AuditEvent", result.Monitoring.EnabledLogs);
        Assert.Contains("SecurityEvent", result.Monitoring.EnabledLogs);
    }

    [Fact]
    public async Task GenerateSecurityConfigAsync_Monitoring_EnablesSecurityAlerts()
    {
        // Act
        var result = await _service.GenerateSecurityConfigAsync(
            "test-rg", 
            "test-kv", 
            new List<string>(),
            "FedRAMP-High");

        // Assert
        Assert.True(result.Monitoring.EnableAlerts);
        Assert.Contains("UnauthorizedKeyVaultAccess", result.Monitoring.AlertRules);
        Assert.Contains("NetworkSecurityGroupModified", result.Monitoring.AlertRules);
        Assert.Contains("FirewallRuleChanged", result.Monitoring.AlertRules);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task GenerateSecurityConfigAsync_FedRAMPHigh_MeetsAllRequirements()
    {
        // Act
        var result = await _service.GenerateSecurityConfigAsync(
            "test-rg", 
            "test-kv", 
            new List<string>(),
            "FedRAMP-High");

        // Assert - Key Vault
        Assert.True(result.KeyVault.EnableSoftDelete);
        Assert.True(result.KeyVault.EnablePurgeProtection);
        Assert.Equal("Disabled", result.KeyVault.PublicNetworkAccess);
        
        // Assert - Encryption
        Assert.Equal(EncryptionType.CustomerManagedKeys, result.Encryption.EncryptionType);
        Assert.True(result.Encryption.EnableAutomaticKeyRotation);
        
        // Assert - Network
        Assert.True(result.NetworkSecurity.DenyAllInbound);
        Assert.True(result.NetworkSecurity.EnableDDoSProtection);
        
        // Assert - Identity
        Assert.True(result.IdentityAndAccess.RequireMultiFactor);
        Assert.True(result.IdentityAndAccess.EnableConditionalAccess);
        
        // Assert - Monitoring
        Assert.Equal(90, result.Monitoring.LogRetentionDays);
        Assert.True(result.Monitoring.EnableAlerts);
    }

    #endregion
}
