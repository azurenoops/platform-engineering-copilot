namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// SCCA-compliant and 3-tier network configuration patterns
/// Provides pre-built subnet layouts and NSG rules for common architectures
/// </summary>
public static class SccaNetworkConfiguration
{
    /// <summary>
    /// Get 3-tier subnet configuration (web/app/data) with SCCA-compliant address ranges
    /// </summary>
    /// <param name="vnetAddressPrefix">VNet CIDR block (default: 10.0.0.0/16)</param>
    /// <returns>List of subnet configurations for web, app, and data tiers</returns>
    public static List<SubnetDefinition> GetThreeTierSubnets(string vnetAddressPrefix = "10.0.0.0/16")
    {
        // Parse the base address to generate subnet ranges
        var baseOctets = vnetAddressPrefix.Split('/')[0].Split('.');
        var baseNetwork = $"{baseOctets[0]}.{baseOctets[1]}";

        return new List<SubnetDefinition>
        {
            new SubnetDefinition
            {
                Name = "web-tier",
                AddressPrefix = $"{baseNetwork}.1.0/24",
                Purpose = SubnetPurpose.WebTier,
                Description = "Web/presentation tier - public-facing load balancers and web servers",
                ServiceEndpoints = new List<string> { "Microsoft.Web", "Microsoft.KeyVault" },
                NsgRules = GetWebTierNsgRules()
            },
            new SubnetDefinition
            {
                Name = "app-tier",
                AddressPrefix = $"{baseNetwork}.2.0/24",
                Purpose = SubnetPurpose.ApplicationTier,
                Description = "Application tier - business logic, APIs, and microservices",
                ServiceEndpoints = new List<string> { "Microsoft.Sql", "Microsoft.Storage", "Microsoft.KeyVault" },
                NsgRules = GetAppTierNsgRules()
            },
            new SubnetDefinition
            {
                Name = "data-tier",
                AddressPrefix = $"{baseNetwork}.3.0/24",
                Purpose = SubnetPurpose.DataTier,
                Description = "Data tier - databases and storage services",
                ServiceEndpoints = new List<string> { "Microsoft.Sql", "Microsoft.Storage" },
                NsgRules = GetDataTierNsgRules()
            }
        };
    }

    /// <summary>
    /// Get AKS-specific subnet layout with system and user node pools
    /// </summary>
    public static List<SubnetDefinition> GetAksSubnets(string vnetAddressPrefix = "10.0.0.0/16")
    {
        var baseOctets = vnetAddressPrefix.Split('/')[0].Split('.');
        var baseNetwork = $"{baseOctets[0]}.{baseOctets[1]}";

        return new List<SubnetDefinition>
        {
            new SubnetDefinition
            {
                Name = "aks-system",
                AddressPrefix = $"{baseNetwork}.4.0/23",  // /23 for system pods
                Purpose = SubnetPurpose.AksSystemNodePool,
                Description = "AKS system node pool subnet",
                ServiceEndpoints = new List<string> { "Microsoft.ContainerRegistry", "Microsoft.Storage", "Microsoft.KeyVault" },
                NsgRules = GetAksNsgRules()
            },
            new SubnetDefinition
            {
                Name = "aks-user",
                AddressPrefix = $"{baseNetwork}.8.0/22",  // /22 for user workloads (larger)
                Purpose = SubnetPurpose.AksUserNodePool,
                Description = "AKS user node pool subnet for workloads",
                ServiceEndpoints = new List<string> { "Microsoft.ContainerRegistry", "Microsoft.Storage", "Microsoft.Sql", "Microsoft.KeyVault" },
                NsgRules = GetAksNsgRules()
            },
            new SubnetDefinition
            {
                Name = "aks-ingress",
                AddressPrefix = $"{baseNetwork}.12.0/24",
                Purpose = SubnetPurpose.AksIngress,
                Description = "AKS ingress controller subnet",
                ServiceEndpoints = new List<string> { "Microsoft.Web" },
                NsgRules = GetAksIngressNsgRules()
            }
        };
    }

    /// <summary>
    /// Get SCCA-compliant Landing Zone subnets (management, shared services, workload)
    /// </summary>
    public static List<SubnetDefinition> GetLandingZoneSubnets(string vnetAddressPrefix = "10.0.0.0/16")
    {
        var baseOctets = vnetAddressPrefix.Split('/')[0].Split('.');
        var baseNetwork = $"{baseOctets[0]}.{baseOctets[1]}";

        return new List<SubnetDefinition>
        {
            new SubnetDefinition
            {
                Name = "management",
                AddressPrefix = $"{baseNetwork}.0.0/26",
                Purpose = SubnetPurpose.Management,
                Description = "Management subnet for bastion, jump boxes, and admin access",
                ServiceEndpoints = new List<string> { "Microsoft.KeyVault" },
                NsgRules = GetManagementNsgRules()
            },
            new SubnetDefinition
            {
                Name = "shared-services",
                AddressPrefix = $"{baseNetwork}.0.64/26",
                Purpose = SubnetPurpose.SharedServices,
                Description = "Shared services - DNS, AD, monitoring, logging",
                ServiceEndpoints = new List<string> { "Microsoft.Storage", "Microsoft.KeyVault" },
                NsgRules = GetSharedServicesNsgRules()
            },
            new SubnetDefinition
            {
                Name = "workload",
                AddressPrefix = $"{baseNetwork}.1.0/24",
                Purpose = SubnetPurpose.Workload,
                Description = "Primary workload subnet for applications",
                ServiceEndpoints = new List<string> { "Microsoft.Sql", "Microsoft.Storage", "Microsoft.KeyVault", "Microsoft.ContainerRegistry" },
                NsgRules = GetWorkloadNsgRules()
            },
            new SubnetDefinition
            {
                Name = "AzureFirewallSubnet",  // Required exact name for Azure Firewall
                AddressPrefix = $"{baseNetwork}.255.0/26",
                Purpose = SubnetPurpose.Firewall,
                Description = "Azure Firewall subnet (required name)",
                ServiceEndpoints = new List<string>(),
                NsgRules = new List<NsgRuleDefinition>() // Firewall subnet cannot have NSG
            },
            new SubnetDefinition
            {
                Name = "AzureBastionSubnet",  // Required exact name for Bastion
                AddressPrefix = $"{baseNetwork}.255.64/26",
                Purpose = SubnetPurpose.Bastion,
                Description = "Azure Bastion subnet (required name)",
                ServiceEndpoints = new List<string>(),
                NsgRules = GetBastionNsgRules()
            }
        };
    }

    /// <summary>
    /// Get combined 3-tier + AKS subnet layout for AKS Landing Zone pattern
    /// </summary>
    public static List<SubnetDefinition> GetAksLandingZoneSubnets(string vnetAddressPrefix = "10.0.0.0/16")
    {
        var subnets = new List<SubnetDefinition>();
        
        // Add 3-tier subnets
        subnets.AddRange(GetThreeTierSubnets(vnetAddressPrefix));
        
        // Add AKS subnets
        subnets.AddRange(GetAksSubnets(vnetAddressPrefix));
        
        return subnets;
    }

    // ===== NSG RULE DEFINITIONS BY TIER =====

    /// <summary>
    /// NSG rules for web tier - allows inbound HTTP/HTTPS, outbound to app tier
    /// </summary>
    public static List<NsgRuleDefinition> GetWebTierNsgRules()
    {
        return new List<NsgRuleDefinition>
        {
            // Inbound rules
            new NsgRuleDefinition
            {
                Name = "Allow-HTTP-Inbound",
                Description = "Allow HTTP from internet",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "Internet",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "80"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-HTTPS-Inbound",
                Description = "Allow HTTPS from internet",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "Internet",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "443"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-AzureLoadBalancer",
                Description = "Allow Azure Load Balancer health probes",
                Priority = 120,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "AzureLoadBalancer",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            },
            new NsgRuleDefinition
            {
                Name = "Deny-All-Inbound",
                Description = "Deny all other inbound traffic",
                Priority = 4096,
                Direction = "Inbound",
                Access = "Deny",
                Protocol = "*",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            },
            
            // Outbound rules
            new NsgRuleDefinition
            {
                Name = "Allow-AppTier-Outbound",
                Description = "Allow outbound to app tier",
                Priority = 100,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "10.0.2.0/24",  // App tier
                DestinationPortRange = "8080-8443"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-AzureCloud-Outbound",
                Description = "Allow outbound to Azure services",
                Priority = 200,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "AzureCloud",
                DestinationPortRange = "*"
            }
        };
    }

    /// <summary>
    /// NSG rules for app tier - allows from web tier, outbound to data tier
    /// </summary>
    public static List<NsgRuleDefinition> GetAppTierNsgRules()
    {
        return new List<NsgRuleDefinition>
        {
            // Inbound rules
            new NsgRuleDefinition
            {
                Name = "Allow-WebTier-Inbound",
                Description = "Allow inbound from web tier",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "10.0.1.0/24",  // Web tier
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "8080-8443"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-AzureLoadBalancer",
                Description = "Allow Azure Load Balancer health probes",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "AzureLoadBalancer",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            },
            new NsgRuleDefinition
            {
                Name = "Deny-All-Inbound",
                Description = "Deny all other inbound traffic",
                Priority = 4096,
                Direction = "Inbound",
                Access = "Deny",
                Protocol = "*",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            },
            
            // Outbound rules
            new NsgRuleDefinition
            {
                Name = "Allow-DataTier-Outbound",
                Description = "Allow outbound to data tier (SQL)",
                Priority = 100,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "10.0.3.0/24",  // Data tier
                DestinationPortRange = "1433"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-DataTier-Redis",
                Description = "Allow outbound to data tier (Redis)",
                Priority = 110,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "10.0.3.0/24",
                DestinationPortRange = "6379-6380"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-AzureCloud-Outbound",
                Description = "Allow outbound to Azure services",
                Priority = 200,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "AzureCloud",
                DestinationPortRange = "*"
            }
        };
    }

    /// <summary>
    /// NSG rules for data tier - only allows from app tier, denies internet
    /// </summary>
    public static List<NsgRuleDefinition> GetDataTierNsgRules()
    {
        return new List<NsgRuleDefinition>
        {
            // Inbound rules
            new NsgRuleDefinition
            {
                Name = "Allow-AppTier-SQL",
                Description = "Allow SQL from app tier",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "10.0.2.0/24",  // App tier
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "1433"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-AppTier-Redis",
                Description = "Allow Redis from app tier",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "10.0.2.0/24",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "6379-6380"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-AzureLoadBalancer",
                Description = "Allow Azure Load Balancer health probes",
                Priority = 120,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "AzureLoadBalancer",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            },
            new NsgRuleDefinition
            {
                Name = "Deny-All-Inbound",
                Description = "Deny all other inbound traffic (including internet)",
                Priority = 4096,
                Direction = "Inbound",
                Access = "Deny",
                Protocol = "*",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            },
            
            // Outbound rules
            new NsgRuleDefinition
            {
                Name = "Allow-AzureStorage-Outbound",
                Description = "Allow outbound to Azure Storage for backups",
                Priority = 100,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "Storage",
                DestinationPortRange = "443"
            },
            new NsgRuleDefinition
            {
                Name = "Deny-Internet-Outbound",
                Description = "Deny direct internet outbound from data tier",
                Priority = 4096,
                Direction = "Outbound",
                Access = "Deny",
                Protocol = "*",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "Internet",
                DestinationPortRange = "*"
            }
        };
    }

    /// <summary>
    /// NSG rules for AKS node pools
    /// </summary>
    public static List<NsgRuleDefinition> GetAksNsgRules()
    {
        return new List<NsgRuleDefinition>
        {
            new NsgRuleDefinition
            {
                Name = "Allow-AzureLoadBalancer",
                Description = "Allow Azure Load Balancer",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "AzureLoadBalancer",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-VNet-Inbound",
                Description = "Allow intra-VNet communication",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "VirtualNetwork",
                SourcePortRange = "*",
                DestinationAddressPrefix = "VirtualNetwork",
                DestinationPortRange = "*"
            },
            new NsgRuleDefinition
            {
                Name = "Deny-All-Inbound",
                Description = "Deny all other inbound",
                Priority = 4096,
                Direction = "Inbound",
                Access = "Deny",
                Protocol = "*",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            }
        };
    }

    /// <summary>
    /// NSG rules for AKS ingress subnet
    /// </summary>
    public static List<NsgRuleDefinition> GetAksIngressNsgRules()
    {
        return new List<NsgRuleDefinition>
        {
            new NsgRuleDefinition
            {
                Name = "Allow-HTTP",
                Description = "Allow HTTP inbound",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "Internet",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "80"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-HTTPS",
                Description = "Allow HTTPS inbound",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "Internet",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "443"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-AzureLoadBalancer",
                Description = "Allow Azure Load Balancer",
                Priority = 120,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "AzureLoadBalancer",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            }
        };
    }

    /// <summary>
    /// NSG rules for management subnet
    /// </summary>
    public static List<NsgRuleDefinition> GetManagementNsgRules()
    {
        return new List<NsgRuleDefinition>
        {
            new NsgRuleDefinition
            {
                Name = "Allow-Bastion-SSH",
                Description = "Allow SSH from Bastion",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "10.0.255.64/26", // Bastion subnet
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "22"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-Bastion-RDP",
                Description = "Allow RDP from Bastion",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "10.0.255.64/26",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "3389"
            },
            new NsgRuleDefinition
            {
                Name = "Deny-All-Inbound",
                Description = "Deny all other inbound",
                Priority = 4096,
                Direction = "Inbound",
                Access = "Deny",
                Protocol = "*",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            }
        };
    }

    /// <summary>
    /// NSG rules for shared services subnet
    /// </summary>
    public static List<NsgRuleDefinition> GetSharedServicesNsgRules()
    {
        return new List<NsgRuleDefinition>
        {
            new NsgRuleDefinition
            {
                Name = "Allow-VNet-DNS",
                Description = "Allow DNS from VNet",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "VirtualNetwork",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "53"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-VNet-LDAP",
                Description = "Allow LDAP from VNet",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "VirtualNetwork",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "389"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-VNet-LDAPS",
                Description = "Allow LDAPS from VNet",
                Priority = 120,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "VirtualNetwork",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "636"
            }
        };
    }

    /// <summary>
    /// NSG rules for workload subnet
    /// </summary>
    public static List<NsgRuleDefinition> GetWorkloadNsgRules()
    {
        return new List<NsgRuleDefinition>
        {
            new NsgRuleDefinition
            {
                Name = "Allow-AzureLoadBalancer",
                Description = "Allow Azure Load Balancer",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "AzureLoadBalancer",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-VNet-Inbound",
                Description = "Allow intra-VNet communication",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "VirtualNetwork",
                SourcePortRange = "*",
                DestinationAddressPrefix = "VirtualNetwork",
                DestinationPortRange = "*"
            },
            new NsgRuleDefinition
            {
                Name = "Deny-All-Inbound",
                Description = "Deny all other inbound",
                Priority = 4096,
                Direction = "Inbound",
                Access = "Deny",
                Protocol = "*",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "*"
            }
        };
    }

    /// <summary>
    /// NSG rules for Azure Bastion subnet (required rules per Azure documentation)
    /// </summary>
    public static List<NsgRuleDefinition> GetBastionNsgRules()
    {
        return new List<NsgRuleDefinition>
        {
            // Inbound rules
            new NsgRuleDefinition
            {
                Name = "Allow-HTTPS-Inbound",
                Description = "Allow HTTPS inbound for Bastion",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "Internet",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "443"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-GatewayManager",
                Description = "Allow Gateway Manager for control plane",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "GatewayManager",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "443"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-AzureLoadBalancer",
                Description = "Allow Azure Load Balancer",
                Priority = 120,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "AzureLoadBalancer",
                SourcePortRange = "*",
                DestinationAddressPrefix = "*",
                DestinationPortRange = "443"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-BastionHostCommunication",
                Description = "Allow Bastion data plane",
                Priority = 130,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "VirtualNetwork",
                SourcePortRange = "*",
                DestinationAddressPrefix = "VirtualNetwork",
                DestinationPortRange = "8080,5701"
            },
            
            // Outbound rules
            new NsgRuleDefinition
            {
                Name = "Allow-SSH-Outbound",
                Description = "Allow SSH to VMs",
                Priority = 100,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "VirtualNetwork",
                DestinationPortRange = "22"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-RDP-Outbound",
                Description = "Allow RDP to VMs",
                Priority = 110,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "VirtualNetwork",
                DestinationPortRange = "3389"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-AzureCloud-Outbound",
                Description = "Allow outbound to Azure Cloud",
                Priority = 120,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = "AzureCloud",
                DestinationPortRange = "443"
            },
            new NsgRuleDefinition
            {
                Name = "Allow-BastionCommunication-Outbound",
                Description = "Allow Bastion data plane outbound",
                Priority = 130,
                Direction = "Outbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = "VirtualNetwork",
                SourcePortRange = "*",
                DestinationAddressPrefix = "VirtualNetwork",
                DestinationPortRange = "8080,5701"
            }
        };
    }
}

/// <summary>
/// Subnet definition with address range and associated NSG rules
/// </summary>
public class SubnetDefinition
{
    public string Name { get; set; } = string.Empty;
    public string AddressPrefix { get; set; } = string.Empty;
    public SubnetPurpose Purpose { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> ServiceEndpoints { get; set; } = new();
    public List<NsgRuleDefinition> NsgRules { get; set; } = new();
    public bool DelegateToService { get; set; }
    public string? ServiceDelegation { get; set; }
}

// Note: SubnetPurpose enum is defined in TemplateGenerationModels.cs to avoid duplication

/// <summary>
/// NSG rule definition
/// </summary>
public class NsgRuleDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Direction { get; set; } = "Inbound";  // Inbound or Outbound
    public string Access { get; set; } = "Allow";       // Allow or Deny
    public string Protocol { get; set; } = "Tcp";       // Tcp, Udp, Icmp, *
    public string SourceAddressPrefix { get; set; } = "*";
    public string SourcePortRange { get; set; } = "*";
    public string DestinationAddressPrefix { get; set; } = "*";
    public string DestinationPortRange { get; set; } = "*";
}
