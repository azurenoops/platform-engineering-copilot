using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

/// <summary>
/// Generates Terraform templates for pure network infrastructure (VNet, Subnets, NSG, DDoS, Peering)
/// Used for network-only deployments without compute resources
/// </summary>
public class TerraformNetworkModuleGenerator
{
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var networkConfig = request.Infrastructure?.NetworkConfig ?? new NetworkingConfiguration();

        // Generate main network module
        files["modules/network/main.tf"] = GenerateMainTf(request, networkConfig);
        files["modules/network/variables.tf"] = GenerateVariablesTf(networkConfig);
        files["modules/network/outputs.tf"] = GenerateOutputsTf(networkConfig);
        files["modules/network/README.md"] = GenerateReadme(networkConfig);

        return files;
    }

    private string GenerateMainTf(TemplateGenerationRequest request, NetworkingConfiguration networkConfig)
    {
        var sb = new StringBuilder();

        // Provider configuration
        sb.AppendLine("# Network Infrastructure Module");
        sb.AppendLine();

        // Virtual Network
        sb.AppendLine("# Virtual Network");
        sb.AppendLine("resource \"azurerm_virtual_network\" \"network\" {");
        sb.AppendLine($"  name                = var.vnet_name");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  address_space       = [var.address_space]");
        
        if (networkConfig.EnableDDoSProtection)
        {
            if (networkConfig.DdosMode == "existing" && !string.IsNullOrEmpty(networkConfig.DDoSProtectionPlanId))
            {
                sb.AppendLine();
                sb.AppendLine("  ddos_protection_plan {");
                sb.AppendLine($"    id     = \"{networkConfig.DDoSProtectionPlanId}\"");
                sb.AppendLine("    enable = true");
                sb.AppendLine("  }");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("  ddos_protection_plan {");
                sb.AppendLine("    id     = azurerm_network_ddos_protection_plan.network.id");
                sb.AppendLine("    enable = true");
                sb.AppendLine("  }");
            }
        }

        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();

        // DDoS Protection Plan (if creating new)
        if (networkConfig.EnableDDoSProtection && networkConfig.DdosMode != "existing")
        {
            sb.AppendLine("# DDoS Protection Plan");
            sb.AppendLine("resource \"azurerm_network_ddos_protection_plan\" \"network\" {");
            sb.AppendLine("  name                = \"${var.vnet_name}-ddos-plan\"");
            sb.AppendLine("  location            = var.location");
            sb.AppendLine("  resource_group_name = var.resource_group_name");
            sb.AppendLine("  tags                = var.tags");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Subnets
        if (networkConfig.Subnets != null && networkConfig.Subnets.Any())
        {
            foreach (var subnet in networkConfig.Subnets)
            {
                var subnetSafeName = subnet.Name.Replace("-", "_").ToLower();
                
                sb.AppendLine($"# Subnet: {subnet.Name}");
                sb.AppendLine($"resource \"azurerm_subnet\" \"{subnetSafeName}\" {{");
                sb.AppendLine($"  name                 = \"{subnet.Name}\"");
                sb.AppendLine("  resource_group_name  = var.resource_group_name");
                sb.AppendLine("  virtual_network_name = azurerm_virtual_network.network.name");
                sb.AppendLine($"  address_prefixes     = [\"{subnet.AddressPrefix}\"]");

                if (subnet.ServiceEndpoints != null && subnet.ServiceEndpoints.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("  service_endpoints = [");
                    foreach (var endpoint in subnet.ServiceEndpoints)
                    {
                        sb.AppendLine($"    \"{endpoint}\",");
                    }
                    sb.AppendLine("  ]");
                }

                if (!string.IsNullOrEmpty(subnet.Delegation))
                {
                    sb.AppendLine();
                    sb.AppendLine("  delegation {");
                    sb.AppendLine($"    name = \"{subnet.Delegation.Split('/').Last()}-delegation\"");
                    sb.AppendLine("    service_delegation {");
                    sb.AppendLine($"      name = \"{subnet.Delegation}\"");
                    sb.AppendLine("    }");
                    sb.AppendLine("  }");
                }

                sb.AppendLine("}");
                sb.AppendLine();

                // NSG Association for this subnet
                if (networkConfig.EnableNetworkSecurityGroup)
                {
                    sb.AppendLine($"# NSG Association for {subnet.Name}");
                    sb.AppendLine($"resource \"azurerm_subnet_network_security_group_association\" \"{subnetSafeName}_nsg\" {{");
                    sb.AppendLine($"  subnet_id                 = azurerm_subnet.{subnetSafeName}.id");
                    
                    if (networkConfig.NsgMode == "existing" && !string.IsNullOrEmpty(networkConfig.ExistingNsgResourceId))
                    {
                        sb.AppendLine("  network_security_group_id = data.azurerm_network_security_group.existing.id");
                    }
                    else
                    {
                        sb.AppendLine("  network_security_group_id = azurerm_network_security_group.network.id");
                    }
                    
                    sb.AppendLine("}");
                    sb.AppendLine();
                }
            }
        }

        // Network Security Group
        if (networkConfig.EnableNetworkSecurityGroup)
        {
            if (networkConfig.NsgMode == "existing" && !string.IsNullOrEmpty(networkConfig.ExistingNsgResourceId))
            {
                // Reference existing NSG
                var nsgName = ExtractResourceName(networkConfig.ExistingNsgResourceId);
                var nsgResourceGroup = ExtractResourceGroupName(networkConfig.ExistingNsgResourceId);
                
                sb.AppendLine("# Existing Network Security Group");
                sb.AppendLine("data \"azurerm_network_security_group\" \"existing\" {");
                sb.AppendLine($"  name                = \"{nsgName}\"");
                sb.AppendLine($"  resource_group_name = \"{nsgResourceGroup}\"");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            else
            {
                // Create new NSG
                var nsgName = string.IsNullOrEmpty(networkConfig.NsgName) ? "${var.vnet_name}-nsg" : networkConfig.NsgName;
                
                sb.AppendLine("# Network Security Group");
                sb.AppendLine("resource \"azurerm_network_security_group\" \"network\" {");
                sb.AppendLine($"  name                = \"{nsgName}\"");
                sb.AppendLine("  location            = var.location");
                sb.AppendLine("  resource_group_name = var.resource_group_name");

                if (networkConfig.NsgRules != null && networkConfig.NsgRules.Any())
                {
                    sb.AppendLine();
                    foreach (var rule in networkConfig.NsgRules)
                    {
                        sb.AppendLine($"  security_rule {{");
                        sb.AppendLine($"    name                       = \"{rule.Name}\"");
                        sb.AppendLine($"    priority                   = {rule.Priority}");
                        sb.AppendLine($"    direction                  = \"{rule.Direction}\"");
                        sb.AppendLine($"    access                     = \"{rule.Access}\"");
                        sb.AppendLine($"    protocol                   = \"{rule.Protocol}\"");
                        sb.AppendLine($"    source_port_range          = \"{rule.SourcePortRange ?? "*"}\"");
                        sb.AppendLine($"    destination_port_range     = \"{rule.DestinationPortRange ?? "*"}\"");
                        sb.AppendLine($"    source_address_prefix      = \"{rule.SourceAddressPrefix ?? "*"}\"");
                        sb.AppendLine($"    destination_address_prefix = \"{rule.DestinationAddressPrefix ?? "*"}\"");
                        sb.AppendLine("  }");
                        sb.AppendLine();
                    }
                }

                sb.AppendLine("  tags = var.tags");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // VNet Peering
        if (networkConfig.EnableVNetPeering && networkConfig.VNetPeerings != null && networkConfig.VNetPeerings.Any())
        {
            foreach (var peering in networkConfig.VNetPeerings)
            {
                var peeringSafeName = peering.Name.Replace("-", "_").ToLower();
                
                sb.AppendLine($"# VNet Peering: {peering.Name}");
                sb.AppendLine($"resource \"azurerm_virtual_network_peering\" \"{peeringSafeName}\" {{");
                sb.AppendLine($"  name                      = \"{peering.Name}\"");
                sb.AppendLine("  resource_group_name       = var.resource_group_name");
                sb.AppendLine("  virtual_network_name      = azurerm_virtual_network.network.name");
                sb.AppendLine($"  remote_virtual_network_id = \"{peering.RemoteVNetResourceId}\"");
                sb.AppendLine();
                sb.AppendLine($"  allow_virtual_network_access = {peering.AllowVirtualNetworkAccess.ToString().ToLower()}");
                sb.AppendLine($"  allow_forwarded_traffic      = {peering.AllowForwardedTraffic.ToString().ToLower()}");
                sb.AppendLine($"  allow_gateway_transit        = {peering.AllowGatewayTransit.ToString().ToLower()}");
                sb.AppendLine($"  use_remote_gateways          = {peering.UseRemoteGateways.ToString().ToLower()}");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // Private DNS Zone
        if (networkConfig.EnablePrivateDns)
        {
            if (networkConfig.PrivateDnsMode == "existing" && !string.IsNullOrEmpty(networkConfig.ExistingPrivateDnsZoneResourceId))
            {
                var dnsZoneName = ExtractResourceName(networkConfig.ExistingPrivateDnsZoneResourceId);
                var dnsResourceGroup = ExtractResourceGroupName(networkConfig.ExistingPrivateDnsZoneResourceId);
                
                sb.AppendLine("# Existing Private DNS Zone");
                sb.AppendLine("data \"azurerm_private_dns_zone\" \"existing\" {");
                sb.AppendLine($"  name                = \"{dnsZoneName}\"");
                sb.AppendLine($"  resource_group_name = \"{dnsResourceGroup}\"");
                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine("# Link Existing Private DNS Zone to VNet");
                sb.AppendLine("resource \"azurerm_private_dns_zone_virtual_network_link\" \"network\" {");
                sb.AppendLine("  name                  = \"${var.vnet_name}-link\"");
                sb.AppendLine("  resource_group_name   = var.resource_group_name");
                sb.AppendLine("  private_dns_zone_name = data.azurerm_private_dns_zone.existing.name");
                sb.AppendLine("  virtual_network_id    = azurerm_virtual_network.network.id");
                sb.AppendLine("  tags                  = var.tags");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            else if (!string.IsNullOrEmpty(networkConfig.PrivateDnsZoneName))
            {
                sb.AppendLine("# Private DNS Zone");
                sb.AppendLine("resource \"azurerm_private_dns_zone\" \"network\" {");
                sb.AppendLine($"  name                = \"{networkConfig.PrivateDnsZoneName}\"");
                sb.AppendLine("  resource_group_name = var.resource_group_name");
                sb.AppendLine("  tags                = var.tags");
                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine("# Link Private DNS Zone to VNet");
                sb.AppendLine("resource \"azurerm_private_dns_zone_virtual_network_link\" \"network\" {");
                sb.AppendLine("  name                  = \"${var.vnet_name}-link\"");
                sb.AppendLine("  resource_group_name   = var.resource_group_name");
                sb.AppendLine("  private_dns_zone_name = azurerm_private_dns_zone.network.name");
                sb.AppendLine("  virtual_network_id    = azurerm_virtual_network.network.id");
                sb.AppendLine("  tags                  = var.tags");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private string GenerateVariablesTf(NetworkingConfiguration networkConfig)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Network Module Variables");
        sb.AppendLine();

        sb.AppendLine("variable \"vnet_name\" {");
        sb.AppendLine("  description = \"Name of the virtual network\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{networkConfig.VNetName ?? "vnet"}\"");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("variable \"address_space\" {");
        sb.AppendLine("  description = \"Address space for the virtual network\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{networkConfig.VNetAddressSpace ?? "10.0.0.0/16"}\"");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("variable \"location\" {");
        sb.AppendLine("  description = \"Azure region for resources\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("variable \"resource_group_name\" {");
        sb.AppendLine("  description = \"Name of the resource group\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Tags to apply to resources\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateOutputsTf(NetworkingConfiguration networkConfig)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Network Module Outputs");
        sb.AppendLine();

        sb.AppendLine("output \"vnet_id\" {");
        sb.AppendLine("  description = \"ID of the virtual network\"");
        sb.AppendLine("  value       = azurerm_virtual_network.network.id");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output \"vnet_name\" {");
        sb.AppendLine("  description = \"Name of the virtual network\"");
        sb.AppendLine("  value       = azurerm_virtual_network.network.name");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output \"address_space\" {");
        sb.AppendLine("  description = \"Address space of the virtual network\"");
        sb.AppendLine("  value       = azurerm_virtual_network.network.address_space");
        sb.AppendLine("}");
        sb.AppendLine();

        if (networkConfig.Subnets != null && networkConfig.Subnets.Any())
        {
            sb.AppendLine("output \"subnet_ids\" {");
            sb.AppendLine("  description = \"Map of subnet names to IDs\"");
            sb.AppendLine("  value = {");
            foreach (var subnet in networkConfig.Subnets)
            {
                var subnetSafeName = subnet.Name.Replace("-", "_").ToLower();
                sb.AppendLine($"    \"{subnet.Name}\" = azurerm_subnet.{subnetSafeName}.id");
            }
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        if (networkConfig.EnableNetworkSecurityGroup && networkConfig.NsgMode != "existing")
        {
            sb.AppendLine("output \"nsg_id\" {");
            sb.AppendLine("  description = \"ID of the network security group\"");
            sb.AppendLine("  value       = azurerm_network_security_group.network.id");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        if (networkConfig.EnableVNetPeering && networkConfig.VNetPeerings != null && networkConfig.VNetPeerings.Any())
        {
            sb.AppendLine("output \"peering_ids\" {");
            sb.AppendLine("  description = \"Map of peering names to IDs\"");
            sb.AppendLine("  value = {");
            foreach (var peering in networkConfig.VNetPeerings)
            {
                var peeringSafeName = peering.Name.Replace("-", "_").ToLower();
                sb.AppendLine($"    \"{peering.Name}\" = azurerm_virtual_network_peering.{peeringSafeName}.id");
            }
            sb.AppendLine("  }");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private string GenerateReadme(NetworkingConfiguration networkConfig)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Network Infrastructure Module");
        sb.AppendLine();
        sb.AppendLine("This Terraform module deploys network infrastructure including:");
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
        sb.AppendLine("## Usage");
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"network\" {");
        sb.AppendLine("  source = \"./modules/network\"");
        sb.AppendLine();
        sb.AppendLine("  vnet_name           = \"my-vnet\"");
        sb.AppendLine("  address_space       = \"10.0.0.0/16\"");
        sb.AppendLine("  location            = \"eastus\"");
        sb.AppendLine("  resource_group_name = \"my-resource-group\"");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    private string ExtractResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return string.Empty;
        var parts = resourceId.Split('/');
        return parts.Length > 0 ? parts[^1] : string.Empty;
    }

    private string ExtractResourceGroupName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return string.Empty;
        var parts = resourceId.Split('/');
        var rgIndex = Array.IndexOf(parts, "resourceGroups");
        return rgIndex >= 0 && rgIndex + 1 < parts.Length ? parts[rgIndex + 1] : string.Empty;
    }
}
