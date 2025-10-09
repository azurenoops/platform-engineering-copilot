using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Bicep;

/// <summary>
/// Generates Bicep templates for pure network infrastructure (VNet, Subnets, NSG, DDoS, Peering)
/// Used for network-only deployments without compute resources
/// </summary>
public class BicepNetworkModuleGenerator
{
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var networkConfig = request.Infrastructure?.NetworkConfig ?? new NetworkingConfiguration();

        // Generate main network module
        files["modules/network/main.bicep"] = GenerateNetworkModule(request, networkConfig);
        files["modules/network/README.md"] = GenerateReadme(networkConfig);

        return files;
    }

    private string GenerateNetworkModule(TemplateGenerationRequest request, NetworkingConfiguration networkConfig)
    {
        var sb = new StringBuilder();

        // Parameters
        sb.AppendLine("@description('The name of the virtual network')");
        sb.AppendLine($"param vnetName string = '{networkConfig.VNetName}'");
        sb.AppendLine();

        sb.AppendLine("@description('The address space for the virtual network')");
        sb.AppendLine($"param addressSpace string = '{networkConfig.VNetAddressSpace}'");
        sb.AppendLine();

        sb.AppendLine("@description('Azure region for resources')");
        sb.AppendLine($"param location string = resourceGroup().location");
        sb.AppendLine();

        sb.AppendLine("@description('Tags to apply to resources')");
        sb.AppendLine("param tags object = {}");
        sb.AppendLine();

        // Virtual Network
        sb.AppendLine("// Virtual Network");
        sb.AppendLine("resource vnet 'Microsoft.Network/virtualNetworks@2023-05-01' = {");
        sb.AppendLine($"  name: vnetName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    addressSpace: {");
        sb.AppendLine("      addressPrefixes: [");
        sb.AppendLine("        addressSpace");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");

        // Subnets
        if (networkConfig.Subnets != null && networkConfig.Subnets.Any())
        {
            sb.AppendLine("    subnets: [");
            foreach (var subnet in networkConfig.Subnets)
            {
                sb.AppendLine("      {");
                sb.AppendLine($"        name: '{subnet.Name}'");
                sb.AppendLine("        properties: {");
                sb.AppendLine($"          addressPrefix: '{subnet.AddressPrefix}'");

                if (subnet.ServiceEndpoints != null && subnet.ServiceEndpoints.Any())
                {
                    sb.AppendLine("          serviceEndpoints: [");
                    foreach (var endpoint in subnet.ServiceEndpoints)
                    {
                        sb.AppendLine($"            {{ service: '{endpoint}' }}");
                    }
                    sb.AppendLine("          ]");
                }

                sb.AppendLine("        }");
                sb.AppendLine("      }");
            }
            sb.AppendLine("    ]");
        }

        // DDoS Protection
        if (networkConfig.EnableDDoSProtection)
        {
            if (networkConfig.DdosMode == "existing" && !string.IsNullOrEmpty(networkConfig.DDoSProtectionPlanId))
            {
                sb.AppendLine("    enableDdosProtection: true");
                sb.AppendLine("    ddosProtectionPlan: {");
                sb.AppendLine($"      id: '{networkConfig.DDoSProtectionPlanId}'");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine("    enableDdosProtection: true");
                // Note: When creating new DDoS plan, it needs to be a separate resource
            }
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Network Security Groups
        if (networkConfig.EnableNetworkSecurityGroup && networkConfig.NsgMode != "existing")
        {
            var nsgName = string.IsNullOrEmpty(networkConfig.NsgName) ? "${vnetName}-nsg" : networkConfig.NsgName;
            sb.AppendLine("// Network Security Group");
            sb.AppendLine("resource nsg 'Microsoft.Network/networkSecurityGroups@2023-05-01' = {");
            sb.AppendLine($"  name: '{nsgName}'");
            sb.AppendLine("  location: location");
            sb.AppendLine("  tags: tags");
            sb.AppendLine("  properties: {");
            
            if (networkConfig.NsgRules != null && networkConfig.NsgRules.Any())
            {
                sb.AppendLine("    securityRules: [");
                foreach (var rule in networkConfig.NsgRules)
                {
                    sb.AppendLine("      {");
                    sb.AppendLine($"        name: '{rule.Name}'");
                    sb.AppendLine("        properties: {");
                    sb.AppendLine($"          priority: {rule.Priority}");
                    sb.AppendLine($"          direction: '{rule.Direction}'");
                    sb.AppendLine($"          access: '{rule.Access}'");
                    sb.AppendLine($"          protocol: '{rule.Protocol}'");
                    sb.AppendLine($"          sourceAddressPrefix: '{rule.SourceAddressPrefix ?? "*"}'");
                    sb.AppendLine($"          sourcePortRange: '{rule.SourcePortRange ?? "*"}'");
                    sb.AppendLine($"          destinationAddressPrefix: '{rule.DestinationAddressPrefix ?? "*"}'");
                    sb.AppendLine($"          destinationPortRange: '{rule.DestinationPortRange ?? "*"}'");
                    sb.AppendLine("        }");
                    sb.AppendLine("      }");
                }
                sb.AppendLine("    ]");
            }
            
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // VNet Peering
        if (networkConfig.EnableVNetPeering && networkConfig.VNetPeerings != null && networkConfig.VNetPeerings.Any())
        {
            foreach (var peering in networkConfig.VNetPeerings)
            {
                sb.AppendLine($"// VNet Peering: {peering.Name}");
                sb.AppendLine($"resource peering_{peering.Name.Replace("-", "_")} 'Microsoft.Network/virtualNetworks/virtualNetworkPeerings@2023-05-01' = {{");
                sb.AppendLine($"  parent: vnet");
                sb.AppendLine($"  name: '{peering.Name}'");
                sb.AppendLine("  properties: {");
                sb.AppendLine($"    allowVirtualNetworkAccess: {peering.AllowVirtualNetworkAccess.ToString().ToLower()}");
                sb.AppendLine($"    allowForwardedTraffic: {peering.AllowForwardedTraffic.ToString().ToLower()}");
                sb.AppendLine($"    allowGatewayTransit: {peering.AllowGatewayTransit.ToString().ToLower()}");
                sb.AppendLine($"    useRemoteGateways: {peering.UseRemoteGateways.ToString().ToLower()}");
                sb.AppendLine("    remoteVirtualNetwork: {");
                sb.AppendLine($"      id: '{peering.RemoteVNetResourceId}'");
                sb.AppendLine("    }");
                sb.AppendLine("  }");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // Private DNS Zone
        if (networkConfig.EnablePrivateDns && networkConfig.PrivateDnsMode != "existing")
        {
            if (!string.IsNullOrEmpty(networkConfig.PrivateDnsZoneName))
            {
                sb.AppendLine($"// Private DNS Zone");
                sb.AppendLine("resource privateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {");
                sb.AppendLine($"  name: '{networkConfig.PrivateDnsZoneName}'");
                sb.AppendLine("  location: 'global'");
                sb.AppendLine("  tags: tags");
                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine("// Link Private DNS Zone to VNet");
                sb.AppendLine("resource privateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {");
                sb.AppendLine("  parent: privateDnsZone");
                sb.AppendLine("  name: '${vnetName}-link'");
                sb.AppendLine("  location: 'global'");
                sb.AppendLine("  properties: {");
                sb.AppendLine("    registrationEnabled: false");
                sb.AppendLine("    virtualNetwork: {");
                sb.AppendLine("      id: vnet.id");
                sb.AppendLine("    }");
                sb.AppendLine("  }");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // Outputs
        sb.AppendLine("// Outputs");
        sb.AppendLine("output vnetId string = vnet.id");
        sb.AppendLine("output vnetName string = vnet.name");
        sb.AppendLine("output addressSpace string = addressSpace");
        
        if (networkConfig.Subnets != null && networkConfig.Subnets.Any())
        {
            sb.AppendLine("output subnets array = [for subnet in vnet.properties.subnets: {");
            sb.AppendLine("  name: subnet.name");
            sb.AppendLine("  id: subnet.id");
            sb.AppendLine("  addressPrefix: subnet.properties.addressPrefix");
            sb.AppendLine("}]");
        }

        if (networkConfig.EnableNetworkSecurityGroup && networkConfig.NsgMode != "existing")
        {
            sb.AppendLine("output nsgId string = nsg.id");
        }

        return sb.ToString();
    }

    private string GenerateReadme(NetworkingConfiguration networkConfig)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Network Infrastructure Module");
        sb.AppendLine();
        sb.AppendLine("This Bicep module deploys network infrastructure including:");
        sb.AppendLine();
        sb.AppendLine("## Resources");
        sb.AppendLine("- Virtual Network (VNet)");
        
        if (networkConfig.Subnets != null && networkConfig.Subnets.Any())
        {
            sb.AppendLine($"- {networkConfig.Subnets.Count} Subnet(s)");
        }

        if (networkConfig.EnableNetworkSecurityGroup)
        {
            sb.AppendLine($"- Network Security Group ({(networkConfig.NsgMode == "existing" ? "Existing" : "New")})");
        }

        if (networkConfig.EnableDDoSProtection)
        {
            sb.AppendLine($"- DDoS Protection ({(networkConfig.DdosMode == "existing" ? "Existing Plan" : "New Plan")})");
        }

        if (networkConfig.EnableVNetPeering && networkConfig.VNetPeerings != null)
        {
            sb.AppendLine($"- {networkConfig.VNetPeerings.Count} VNet Peering(s)");
        }

        if (networkConfig.EnablePrivateDns)
        {
            sb.AppendLine($"- Private DNS Zone ({(networkConfig.PrivateDnsMode == "existing" ? "Existing" : "New")})");
        }

        sb.AppendLine();
        sb.AppendLine("## Deployment");
        sb.AppendLine("```bash");
        sb.AppendLine("az deployment group create \\");
        sb.AppendLine("  --resource-group <resource-group-name> \\");
        sb.AppendLine("  --template-file modules/network/main.bicep");
        sb.AppendLine("```");

        return sb.ToString();
    }
}
