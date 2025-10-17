using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Azure.Security;

/// <summary>
/// Azure security configuration service with FedRAMP High and DoD IL5 compliant defaults
/// Generates security configurations for Key Vault, Encryption, NSG rules, and Azure Firewall
/// </summary>
public interface IAzureSecurityConfigurationService
{
    /// <summary>
    /// Generate comprehensive security configuration for Azure resources
    /// </summary>
    Task<AzureSecurityConfiguration> GenerateSecurityConfigAsync(
        string resourceGroupName,
        string keyVaultName,
        List<string> resourceTypes,
        string complianceFramework = "FedRAMP-High",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate NSG rules for a specific tier/subnet
    /// </summary>
    List<NetworkSecurityRule> GenerateNsgRulesForTier(
        SubnetPurpose tierPurpose,
        bool allowInternetAccess = false);

    /// <summary>
    /// Generate Key Vault configuration with compliance settings
    /// </summary>
    KeyVaultConfiguration GenerateKeyVaultConfig(
        string keyVaultName,
        List<string> allowedSubnets,
        string complianceFramework = "FedRAMP-High");
}

public class AzureSecurityConfigurationService : IAzureSecurityConfigurationService
{
    private readonly ILogger<AzureSecurityConfigurationService> _logger;
    private readonly IAzureResourceService _azureResourceService;

    public AzureSecurityConfigurationService(
        ILogger<AzureSecurityConfigurationService> logger,
        IAzureResourceService azureResourceService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
    }

    public async Task<AzureSecurityConfiguration> GenerateSecurityConfigAsync(
        string resourceGroupName,
        string keyVaultName,
        List<string> resourceTypes,
        string complianceFramework = "FedRAMP-High",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating security configuration for RG={ResourceGroup}, KV={KeyVault}, Framework={Framework}",
            resourceGroupName, keyVaultName, complianceFramework);

        await Task.CompletedTask; // Placeholder for async operations

        var config = new AzureSecurityConfiguration
        {
            ResourceGroupName = resourceGroupName,
            ComplianceFramework = complianceFramework,
            KeyVault = GenerateKeyVaultConfig(keyVaultName, new List<string> { "AppSubnet", "DataSubnet" }, complianceFramework),
            Encryption = GenerateEncryptionConfig(keyVaultName),
            NetworkSecurity = GenerateNetworkSecurityConfig(),
            IdentityAndAccess = GenerateIdentityConfig(),
            Monitoring = GenerateMonitoringConfig()
        };

        _logger.LogInformation("Security configuration generated with {ComponentCount} components", 5);
        return config;
    }

    public List<NetworkSecurityRule> GenerateNsgRulesForTier(
        SubnetPurpose tierPurpose,
        bool allowInternetAccess = false)
    {
        _logger.LogDebug("Generating NSG rules for tier={Tier}, AllowInternet={AllowInternet}", tierPurpose, allowInternetAccess);

        var rules = new List<NetworkSecurityRule>();
        int priority = 100;

        switch (tierPurpose)
        {
            case SubnetPurpose.Application:
                // Allow HTTPS from Application Gateway
                rules.Add(new NetworkSecurityRule
                {
                    Name = "Allow-HTTPS-Inbound",
                    Priority = priority++,
                    Direction = "Inbound",
                    Access = "Allow",
                    Protocol = "Tcp",
                    SourcePortRange = "*",
                    DestinationPortRange = "443",
                    SourceAddressPrefix = "GatewaySubnet",
                    DestinationAddressPrefix = "*",
                    Description = "Allow HTTPS traffic from Application Gateway"
                });

                // Allow outbound to Data tier
                rules.Add(new NetworkSecurityRule
                {
                    Name = "Allow-DataTier-Outbound",
                    Priority = priority++,
                    Direction = "Outbound",
                    Access = "Allow",
                    Protocol = "Tcp",
                    SourcePortRange = "*",
                    DestinationPortRange = "1433,5432,3306", // SQL Server, PostgreSQL, MySQL
                    SourceAddressPrefix = "*",
                    DestinationAddressPrefix = "DataTierSubnet",
                    Description = "Allow database connections to Data tier"
                });

                break;

            case SubnetPurpose.Database:
                // Allow from Application tier only
                rules.Add(new NetworkSecurityRule
                {
                    Name = "Allow-AppTier-Inbound",
                    Priority = priority++,
                    Direction = "Inbound",
                    Access = "Allow",
                    Protocol = "Tcp",
                    SourcePortRange = "*",
                    DestinationPortRange = "1433,5432,3306",
                    SourceAddressPrefix = "ApplicationTierSubnet",
                    DestinationAddressPrefix = "*",
                    Description = "Allow database access from Application tier"
                });

                // Deny all other inbound
                rules.Add(new NetworkSecurityRule
                {
                    Name = "Deny-All-Inbound",
                    Priority = 4096,
                    Direction = "Inbound",
                    Access = "Deny",
                    Protocol = "*",
                    SourcePortRange = "*",
                    DestinationPortRange = "*",
                    SourceAddressPrefix = "*",
                    DestinationAddressPrefix = "*",
                    Description = "Deny all other inbound traffic"
                });

                break;

            case SubnetPurpose.ApplicationGateway:
                // Allow HTTPS from internet (if allowed)
                if (allowInternetAccess)
                {
                    rules.Add(new NetworkSecurityRule
                    {
                        Name = "Allow-HTTPS-Internet",
                        Priority = priority++,
                        Direction = "Inbound",
                        Access = "Allow",
                        Protocol = "Tcp",
                        SourcePortRange = "*",
                        DestinationPortRange = "443",
                        SourceAddressPrefix = "Internet",
                        DestinationAddressPrefix = "*",
                        Description = "Allow HTTPS from Internet"
                    });
                }
                else
                {
                    rules.Add(new NetworkSecurityRule
                    {
                        Name = "Allow-HTTPS-VirtualNetwork",
                        Priority = priority++,
                        Direction = "Inbound",
                        Access = "Allow",
                        Protocol = "Tcp",
                        SourcePortRange = "*",
                        DestinationPortRange = "443",
                        SourceAddressPrefix = "VirtualNetwork",
                        DestinationAddressPrefix = "*",
                        Description = "Allow HTTPS from VirtualNetwork only"
                    });
                }

                // Allow GatewayManager for Azure management
                rules.Add(new NetworkSecurityRule
                {
                    Name = "Allow-GatewayManager",
                    Priority = priority++,
                    Direction = "Inbound",
                    Access = "Allow",
                    Protocol = "Tcp",
                    SourcePortRange = "*",
                    DestinationPortRange = "65200-65535",
                    SourceAddressPrefix = "GatewayManager",
                    DestinationAddressPrefix = "*",
                    Description = "Allow Azure Gateway Manager"
                });

                break;

            case SubnetPurpose.PrivateEndpoints:
                // Allow all traffic within VNet for private endpoints
                rules.Add(new NetworkSecurityRule
                {
                    Name = "Allow-VNet-Inbound",
                    Priority = priority++,
                    Direction = "Inbound",
                    Access = "Allow",
                    Protocol = "*",
                    SourcePortRange = "*",
                    DestinationPortRange = "*",
                    SourceAddressPrefix = "VirtualNetwork",
                    DestinationAddressPrefix = "*",
                    Description = "Allow all traffic from VirtualNetwork to private endpoints"
                });

                break;

            default:
                // Default deny all
                rules.Add(new NetworkSecurityRule
                {
                    Name = "Deny-All-Inbound",
                    Priority = 4096,
                    Direction = "Inbound",
                    Access = "Deny",
                    Protocol = "*",
                    SourcePortRange = "*",
                    DestinationPortRange = "*",
                    SourceAddressPrefix = "*",
                    DestinationAddressPrefix = "*",
                    Description = "Deny all inbound traffic by default"
                });
                break;
        }

        // Add common outbound rules
        rules.Add(new NetworkSecurityRule
        {
            Name = "Allow-AzureCloud-Outbound",
            Priority = 100,
            Direction = "Outbound",
            Access = "Allow",
            Protocol = "Tcp",
            SourcePortRange = "*",
            DestinationPortRange = "443",
            SourceAddressPrefix = "*",
            DestinationAddressPrefix = "AzureCloud",
            Description = "Allow HTTPS to Azure services"
        });

        // Deny internet outbound by default (unless explicitly allowed)
        if (!allowInternetAccess)
        {
            rules.Add(new NetworkSecurityRule
            {
                Name = "Deny-Internet-Outbound",
                Priority = 4095,
                Direction = "Outbound",
                Access = "Deny",
                Protocol = "*",
                SourcePortRange = "*",
                DestinationPortRange = "*",
                SourceAddressPrefix = "*",
                DestinationAddressPrefix = "Internet",
                Description = "Deny all outbound traffic to Internet"
            });
        }

        _logger.LogDebug("Generated {RuleCount} NSG rules for tier {Tier}", rules.Count, tierPurpose);
        return rules;
    }

    public KeyVaultConfiguration GenerateKeyVaultConfig(
        string keyVaultName,
        List<string> allowedSubnets,
        string complianceFramework = "FedRAMP-High")
    {
        _logger.LogDebug("Generating Key Vault configuration for {KeyVaultName} with {Framework}", keyVaultName, complianceFramework);

        var config = new KeyVaultConfiguration
        {
            Name = keyVaultName,
            EnableSoftDelete = true,
            EnablePurgeProtection = true,
            SoftDeleteRetentionDays = 90, // FedRAMP/DoD requirement
            EnableRbacAuthorization = true,
            PublicNetworkAccess = "Disabled", // No public access for FedRAMP/DoD
            NetworkAcls = new KeyVaultNetworkAcls
            {
                DefaultAction = "Deny", // Deny by default
                Bypass = "AzureServices", // Allow Azure services
                AllowedSubnets = allowedSubnets,
                AllowedIpRanges = new List<string>() // No IP whitelist for max security
            },
            EnabledForDeployment = false, // Disable for compliance
            EnabledForDiskEncryption = true, // Enable for disk encryption
            EnabledForTemplateDeployment = false, // Disable for compliance
            Sku = "Premium", // Premium SKU for HSM-backed keys (FedRAMP requirement)
            CreateMode = "default"
        };

        _logger.LogInformation("Key Vault configuration generated: SoftDelete={SoftDelete}, RBAC={RBAC}, NetworkAccess={NetworkAccess}",
            config.EnableSoftDelete, config.EnableRbacAuthorization, config.PublicNetworkAccess);

        return config;
    }

    #region Private Helper Methods

    private EncryptionConfiguration GenerateEncryptionConfig(string keyVaultName)
    {
        return new EncryptionConfiguration
        {
            EncryptionType = EncryptionType.CustomerManagedKeys,
            KeyVaultName = keyVaultName,
            KeyName = "cmk-encryption-key",
            KeyVersion = "latest",
            EnableAutomaticKeyRotation = true,
            RotationPolicyDays = 90, // Rotate every 90 days (FedRAMP best practice)
            
            // Storage Account Encryption
            StorageAccountEncryption = new StorageEncryptionConfig
            {
                Enabled = true,
                EncryptionType = EncryptionType.CustomerManagedKeys,
                KeyVaultKeyId = $"/subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroup}}/providers/Microsoft.KeyVault/vaults/{keyVaultName}/keys/cmk-encryption-key",
                RequireInfrastructureEncryption = true // Double encryption for DoD IL5
            },

            // SQL Database TDE
            SqlDatabaseTDE = new SqlEncryptionConfig
            {
                Enabled = true,
                UseCustomerManagedKey = true,
                KeyVaultKeyId = $"/subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroup}}/providers/Microsoft.KeyVault/vaults/{keyVaultName}/keys/cmk-encryption-key"
            },

            // Disk Encryption
            DiskEncryption = new DiskEncryptionConfig
            {
                Enabled = true,
                EncryptionAtHost = true, // FedRAMP requirement
                DiskEncryptionSetId = $"/subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroup}}/providers/Microsoft.Compute/diskEncryptionSets/des-{keyVaultName}"
            }
        };
    }

    private NetworkSecurityConfiguration GenerateNetworkSecurityConfig()
    {
        return new NetworkSecurityConfiguration
        {
            DenyAllInbound = true, // Default deny
            AllowedPorts = new List<int> { 443 }, // HTTPS only
            SourceAddressPrefix = "VirtualNetwork", // No internet access by default
            EnableDDoSProtection = true,
            EnableAzureFirewall = true,
            FirewallRules = new List<FirewallRule>
            {
                new FirewallRule
                {
                    Name = "Allow-AzureCloud",
                    Priority = 100,
                    Action = "Allow",
                    DestinationPorts = new List<string> { "443" },
                    Destinations = new List<string> { "AzureCloud" },
                    Protocols = new List<string> { "HTTPS" }
                },
                new FirewallRule
                {
                    Name = "Deny-Internet",
                    Priority = 200,
                    Action = "Deny",
                    DestinationPorts = new List<string> { "*" },
                    Destinations = new List<string> { "Internet" },
                    Protocols = new List<string> { "*" }
                }
            }
        };
    }

    private IdentityConfiguration GenerateIdentityConfig()
    {
        return new IdentityConfiguration
        {
            EnableManagedIdentity = true,
            ManagedIdentityType = "SystemAssigned",
            EnableAzureADIntegration = true,
            RequireMultiFactor = true, // FedRAMP/DoD requirement
            EnableConditionalAccess = true,
            RbacAssignments = new List<RbacAssignment>
            {
                new RbacAssignment
                {
                    RoleName = "Key Vault Secrets User",
                    Scope = "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}",
                    PrincipalType = "ServicePrincipal"
                },
                new RbacAssignment
                {
                    RoleName = "Storage Blob Data Contributor",
                    Scope = "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}",
                    PrincipalType = "ServicePrincipal"
                }
            }
        };
    }

    private MonitoringConfiguration GenerateMonitoringConfig()
    {
        return new MonitoringConfiguration
        {
            EnableDiagnosticSettings = true,
            LogAnalyticsWorkspaceId = "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.OperationalInsights/workspaces/log-analytics-workspace",
            EnabledLogs = new List<string>
            {
                "AuditEvent",
                "AzurePolicyEvaluationDetails",
                "SecurityEvent",
                "NetworkSecurityGroupEvent",
                "NetworkSecurityGroupRuleCounter"
            },
            LogRetentionDays = 90, // FedRAMP minimum
            EnableMetrics = true,
            EnableAlerts = true,
            AlertRules = new List<string>
            {
                "UnauthorizedKeyVaultAccess",
                "NetworkSecurityGroupModified",
                "FirewallRuleChanged",
                "EncryptionKeyAccessed"
            }
        };
    }

    #endregion
}

#region Configuration Models

/// <summary>
/// Comprehensive Azure security configuration
/// </summary>
public class AzureSecurityConfiguration
{
    public string ResourceGroupName { get; set; } = string.Empty;
    public string ComplianceFramework { get; set; } = "FedRAMP-High";
    public KeyVaultConfiguration KeyVault { get; set; } = new();
    public EncryptionConfiguration Encryption { get; set; } = new();
    public NetworkSecurityConfiguration NetworkSecurity { get; set; } = new();
    public IdentityConfiguration IdentityAndAccess { get; set; } = new();
    public MonitoringConfiguration Monitoring { get; set; } = new();
}

/// <summary>
/// Key Vault configuration
/// </summary>
public class KeyVaultConfiguration
{
    public string Name { get; set; } = string.Empty;
    public bool EnableSoftDelete { get; set; } = true;
    public bool EnablePurgeProtection { get; set; } = true;
    public int SoftDeleteRetentionDays { get; set; } = 90;
    public bool EnableRbacAuthorization { get; set; } = true;
    public string PublicNetworkAccess { get; set; } = "Disabled";
    public KeyVaultNetworkAcls NetworkAcls { get; set; } = new();
    public bool EnabledForDeployment { get; set; } = false;
    public bool EnabledForDiskEncryption { get; set; } = true;
    public bool EnabledForTemplateDeployment { get; set; } = false;
    public string Sku { get; set; } = "Premium";
    public string CreateMode { get; set; } = "default";
}

public class KeyVaultNetworkAcls
{
    public string DefaultAction { get; set; } = "Deny";
    public string Bypass { get; set; } = "AzureServices";
    public List<string> AllowedSubnets { get; set; } = new();
    public List<string> AllowedIpRanges { get; set; } = new();
}

/// <summary>
/// Encryption configuration
/// </summary>
public class EncryptionConfiguration
{
    public EncryptionType EncryptionType { get; set; } = EncryptionType.CustomerManagedKeys;
    public string KeyVaultName { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public string KeyVersion { get; set; } = "latest";
    public bool EnableAutomaticKeyRotation { get; set; } = true;
    public int RotationPolicyDays { get; set; } = 90;
    public StorageEncryptionConfig StorageAccountEncryption { get; set; } = new();
    public SqlEncryptionConfig SqlDatabaseTDE { get; set; } = new();
    public DiskEncryptionConfig DiskEncryption { get; set; } = new();
}

public class StorageEncryptionConfig
{
    public bool Enabled { get; set; } = true;
    public EncryptionType EncryptionType { get; set; }
    public string KeyVaultKeyId { get; set; } = string.Empty;
    public bool RequireInfrastructureEncryption { get; set; } = true;
}

public class SqlEncryptionConfig
{
    public bool Enabled { get; set; } = true;
    public bool UseCustomerManagedKey { get; set; } = true;
    public string KeyVaultKeyId { get; set; } = string.Empty;
}

public class DiskEncryptionConfig
{
    public bool Enabled { get; set; } = true;
    public bool EncryptionAtHost { get; set; } = true;
    public string DiskEncryptionSetId { get; set; } = string.Empty;
}

public enum EncryptionType
{
    MicrosoftManaged,
    CustomerManagedKeys
}

/// <summary>
/// Network security configuration
/// </summary>
public class NetworkSecurityConfiguration
{
    public bool DenyAllInbound { get; set; } = true;
    public List<int> AllowedPorts { get; set; } = new();
    public string SourceAddressPrefix { get; set; } = "VirtualNetwork";
    public bool EnableDDoSProtection { get; set; } = true;
    public bool EnableAzureFirewall { get; set; } = true;
    public List<FirewallRule> FirewallRules { get; set; } = new();
}

public class FirewallRule
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Action { get; set; } = "Deny";
    public List<string> DestinationPorts { get; set; } = new();
    public List<string> Destinations { get; set; } = new();
    public List<string> Protocols { get; set; } = new();
}

/// <summary>
/// Identity and access configuration
/// </summary>
public class IdentityConfiguration
{
    public bool EnableManagedIdentity { get; set; } = true;
    public string ManagedIdentityType { get; set; } = "SystemAssigned";
    public bool EnableAzureADIntegration { get; set; } = true;
    public bool RequireMultiFactor { get; set; } = true;
    public bool EnableConditionalAccess { get; set; } = true;
    public List<RbacAssignment> RbacAssignments { get; set; } = new();
}

public class RbacAssignment
{
    public string RoleName { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string PrincipalType { get; set; } = "ServicePrincipal";
}

/// <summary>
/// Monitoring and logging configuration
/// </summary>
public class MonitoringConfiguration
{
    public bool EnableDiagnosticSettings { get; set; } = true;
    public string LogAnalyticsWorkspaceId { get; set; } = string.Empty;
    public List<string> EnabledLogs { get; set; } = new();
    public int LogRetentionDays { get; set; } = 90;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableAlerts { get; set; } = true;
    public List<string> AlertRules { get; set; } = new();
}

#endregion
