using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.CrossCutting;

/// <summary>
/// Reusable Bicep Network Security Group Generator
/// Creates NSGs with FedRAMP-compliant default rules
/// Implements FedRAMP SC-7 (Boundary Protection), AC-4 (Information Flow Enforcement)
/// </summary>
public class BicepNSGGenerator : ICrossCuttingModuleGenerator
{
    public CrossCuttingType Type => CrossCuttingType.NetworkSecurityGroup;
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public CloudProvider Provider => CloudProvider.Azure;

    public Dictionary<string, string> GenerateModule(CrossCuttingRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var modulePath = string.IsNullOrEmpty(request.ModulePath) 
            ? "modules/nsg" 
            : request.ModulePath;

        files[$"{modulePath}/nsg.bicep"] = GenerateNSGBicep(request);

        return files;
    }

    public bool CanGenerate(string resourceType)
    {
        return CrossCuttingCapabilityMap.SupportsCapability(resourceType, CrossCuttingType.NetworkSecurityGroup);
    }

    public string GenerateModuleInvocation(CrossCuttingRequest request, string dependsOn)
    {
        var sb = new StringBuilder();
        var moduleName = $"{request.ResourceName.Replace("-", "_")}_nsg";
        var modulePath = string.IsNullOrEmpty(request.ModulePath) 
            ? "modules/nsg" 
            : request.ModulePath;

        sb.AppendLine($"// Network Security Group for {request.ResourceName} - FedRAMP SC-7, AC-4");
        sb.AppendLine($"module {moduleName} './{modulePath}/nsg.bicep' = {{");
        sb.AppendLine($"  name: '{request.ResourceName}-nsg-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine($"    nsgName: '{request.ResourceName}-nsg'");
        sb.AppendLine("    location: location");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("  }");
        
        if (!string.IsNullOrEmpty(dependsOn))
        {
            sb.AppendLine($"  dependsOn: [{dependsOn}]");
        }
        
        sb.AppendLine("}");

        return sb.ToString();
    }

    private NSGConfig GetConfig(CrossCuttingRequest request)
    {
        if (request.Config.TryGetValue("nsg", out var configObj) && configObj is NSGConfig nsgConfig)
        {
            return nsgConfig;
        }

        // Build config from individual properties or use defaults
        var config = new NSGConfig();
        
        if (request.Config.TryGetValue("rules", out var rules) && rules is List<NSGRuleConfig> ruleList)
        {
            config.Rules = ruleList;
        }
        
        if (request.Config.TryGetValue("subnetAssociations", out var subnets) && subnets is List<string> subnetList)
        {
            config.SubnetAssociations = subnetList;
        }

        return config;
    }

    private string GenerateNSGBicep(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();
        var config = GetConfig(request);

        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// Network Security Group Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-7 (Boundary Protection), AC-4 (Information Flow Enforcement)");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine();
        
        // Parameters
        sb.AppendLine("@description('Name of the Network Security Group')");
        sb.AppendLine("param nsgName string");
        sb.AppendLine();
        sb.AppendLine("@description('Azure region for deployment')");
        sb.AppendLine("param location string");
        sb.AppendLine();
        sb.AppendLine("@description('Resource tags')");
        sb.AppendLine("param tags object = {}");
        sb.AppendLine();
        sb.AppendLine("@description('Custom security rules (optional)')");
        sb.AppendLine("param customRules array = []");
        sb.AppendLine();

        // NSG Resource with FedRAMP default rules
        sb.AppendLine("// FedRAMP-compliant default security rules");
        sb.AppendLine("var defaultSecurityRules = [");
        
        // Default deny all inbound (SC-7)
        sb.AppendLine("  {");
        sb.AppendLine("    name: 'DenyAllInbound'");
        sb.AppendLine("    properties: {");
        sb.AppendLine("      priority: 4096");
        sb.AppendLine("      direction: 'Inbound'");
        sb.AppendLine("      access: 'Deny'");
        sb.AppendLine("      protocol: '*'");
        sb.AppendLine("      sourceAddressPrefix: '*'");
        sb.AppendLine("      sourcePortRange: '*'");
        sb.AppendLine("      destinationAddressPrefix: '*'");
        sb.AppendLine("      destinationPortRange: '*'");
        sb.AppendLine("      description: 'FedRAMP SC-7 - Default deny all inbound'");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        
        // Allow Azure Load Balancer
        sb.AppendLine("  {");
        sb.AppendLine("    name: 'AllowAzureLoadBalancerInbound'");
        sb.AppendLine("    properties: {");
        sb.AppendLine("      priority: 4095");
        sb.AppendLine("      direction: 'Inbound'");
        sb.AppendLine("      access: 'Allow'");
        sb.AppendLine("      protocol: '*'");
        sb.AppendLine("      sourceAddressPrefix: 'AzureLoadBalancer'");
        sb.AppendLine("      sourcePortRange: '*'");
        sb.AppendLine("      destinationAddressPrefix: '*'");
        sb.AppendLine("      destinationPortRange: '*'");
        sb.AppendLine("      description: 'Allow Azure Load Balancer health probes'");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        
        // Allow VNet internal traffic
        sb.AppendLine("  {");
        sb.AppendLine("    name: 'AllowVNetInbound'");
        sb.AppendLine("    properties: {");
        sb.AppendLine("      priority: 4094");
        sb.AppendLine("      direction: 'Inbound'");
        sb.AppendLine("      access: 'Allow'");
        sb.AppendLine("      protocol: '*'");
        sb.AppendLine("      sourceAddressPrefix: 'VirtualNetwork'");
        sb.AppendLine("      sourcePortRange: '*'");
        sb.AppendLine("      destinationAddressPrefix: 'VirtualNetwork'");
        sb.AppendLine("      destinationPortRange: '*'");
        sb.AppendLine("      description: 'Allow VNet internal traffic'");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        
        sb.AppendLine("]");
        sb.AppendLine();

        // Merge custom rules with defaults
        sb.AppendLine("// Merge custom rules with default rules");
        sb.AppendLine("var securityRules = concat(customRules, defaultSecurityRules)");
        sb.AppendLine();

        // NSG Resource
        sb.AppendLine("// Network Security Group Resource");
        sb.AppendLine("resource nsg 'Microsoft.Network/networkSecurityGroups@2023-05-01' = {");
        sb.AppendLine("  name: nsgName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: union(tags, {");
        sb.AppendLine("    'security-control': 'SC-7,AC-4'");
        sb.AppendLine("    'managed-by': 'bicep'");
        sb.AppendLine("  })");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    securityRules: securityRules");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// ===== OUTPUTS =====");
        sb.AppendLine("output nsgId string = nsg.id");
        sb.AppendLine("output nsgName string = nsg.name");

        return sb.ToString();
    }

    /// <summary>
    /// Generate common security rule sets for specific scenarios
    /// </summary>
    public static List<NSGRuleConfig> GetWebTierRules(string sourceAddressPrefix = "*")
    {
        return new List<NSGRuleConfig>
        {
            new NSGRuleConfig
            {
                Name = "AllowHTTPS",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = sourceAddressPrefix,
                DestinationPortRange = "443",
                Description = "Allow HTTPS traffic"
            },
            new NSGRuleConfig
            {
                Name = "AllowHTTP",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = sourceAddressPrefix,
                DestinationPortRange = "80",
                Description = "Allow HTTP traffic (redirect to HTTPS)"
            }
        };
    }

    public static List<NSGRuleConfig> GetAppTierRules(string webTierPrefix)
    {
        return new List<NSGRuleConfig>
        {
            new NSGRuleConfig
            {
                Name = "AllowWebTier",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = webTierPrefix,
                DestinationPortRange = "8080",
                Description = "Allow traffic from web tier"
            }
        };
    }

    public static List<NSGRuleConfig> GetDataTierRules(string appTierPrefix)
    {
        return new List<NSGRuleConfig>
        {
            new NSGRuleConfig
            {
                Name = "AllowAppTier",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = appTierPrefix,
                DestinationPortRange = "1433",
                Description = "Allow SQL traffic from app tier"
            }
        };
    }

    public static List<NSGRuleConfig> GetAKSRules(string aksSubnetPrefix)
    {
        return new List<NSGRuleConfig>
        {
            new NSGRuleConfig
            {
                Name = "AllowKubeAPIServer",
                Priority = 100,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "Tcp",
                SourceAddressPrefix = "AzureCloud",
                DestinationAddressPrefix = aksSubnetPrefix,
                DestinationPortRange = "443",
                Description = "Allow Kubernetes API server"
            },
            new NSGRuleConfig
            {
                Name = "AllowNodeCommunication",
                Priority = 110,
                Direction = "Inbound",
                Access = "Allow",
                Protocol = "*",
                SourceAddressPrefix = aksSubnetPrefix,
                DestinationAddressPrefix = aksSubnetPrefix,
                DestinationPortRange = "*",
                Description = "Allow node-to-node communication"
            }
        };
    }
}
