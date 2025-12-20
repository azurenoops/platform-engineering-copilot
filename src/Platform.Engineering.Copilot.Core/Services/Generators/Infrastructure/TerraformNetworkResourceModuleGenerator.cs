using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;

/// <summary>
/// Terraform module generator for Azure Virtual Network infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (NSG, diagnostics) are handled by reusable generators
/// </summary>
public class TerraformNetworkResourceModuleGenerator : IResourceModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public ComputePlatform Platform => ComputePlatform.Network;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "vnet", "network", "virtual-network", "networking" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by Virtual Network
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.DiagnosticSettings,
        CrossCuttingType.NetworkSecurityGroup
    };
    
    /// <summary>
    /// Azure resource type for Virtual Network
    /// </summary>
    public string AzureResourceType => "Microsoft.Network/virtualNetworks";

    /// <summary>
    /// Generate ONLY the core VNet resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "network";

        // Generate only core Network module - no NSG, diagnostics
        files["modules/network/main.tf"] = GenerateCoreMainTf(request);
        files["modules/network/variables.tf"] = GenerateCoreVariablesTf(request);
        files["modules/network/outputs.tf"] = GenerateCoreOutputsTf(request);
        files["modules/network/README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "azurerm_virtual_network.network", // Terraform resource reference
            ResourceType = "Microsoft.Network/virtualNetworks",
            OutputNames = new List<string>
            {
                "vnet_id",
                "vnet_name",
                "vnet_address_space",
                "subnet_ids",
                "private_endpoint_subnet_id"
            },
            SupportedCrossCutting = new List<CrossCuttingType>
            {
                CrossCuttingType.DiagnosticSettings,
                CrossCuttingType.NetworkSecurityGroup
            }
        };
    }

    /// <summary>
    /// Legacy GenerateModule - delegates to legacy generator for full module
    /// </summary>
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        // Delegate to new composition pattern
        var result = GenerateCoreResource(request);
        return result.Files;
    }
    
    /// <summary>
    /// Check if this generator can handle the request
    /// </summary>
    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        return infrastructure.Format == InfrastructureFormat.Terraform &&
               infrastructure.Provider == CloudProvider.Azure &&
               (infrastructure.ComputePlatform == ComputePlatform.Network ||
                infrastructure.IncludeNetworking == true);
    }

    /// <summary>
    /// Core main.tf - only VNet and Subnets, no cross-cutting
    /// </summary>
    private string GenerateCoreMainTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "network";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var networkConfig = infrastructure.NetworkConfig ?? new NetworkingConfiguration();

        sb.AppendLine("# Azure Virtual Network Infrastructure Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: SC-7 (Boundary Protection), AC-4 (Information Flow), SC-8 (Transmission Confidentiality)");
        sb.AppendLine($"# Service: {serviceName}");
        sb.AppendLine($"# Region: {infrastructure.Region}");
        sb.AppendLine("# NOTE: Cross-cutting concerns (NSG, diagnostics) are composed via separate modules");
        sb.AppendLine();

        // Virtual Network
        sb.AppendLine("# Virtual Network");
        sb.AppendLine("resource \"azurerm_virtual_network\" \"network\" {");
        sb.AppendLine("  name                = var.vnet_name");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  address_space       = var.address_space");
        sb.AppendLine();

        // DDoS protection (if enabled)
        if (networkConfig.EnableDDoSProtection)
        {
            sb.AppendLine("  ddos_protection_plan {");
            sb.AppendLine("    id     = var.ddos_protection_plan_id");
            sb.AppendLine("    enable = true");
            sb.AppendLine("  }");
            sb.AppendLine();
        }

        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();

        // Default subnets
        sb.AppendLine("# Subnet: Private Endpoints");
        sb.AppendLine("resource \"azurerm_subnet\" \"private_endpoints\" {");
        sb.AppendLine("  name                 = \"snet-private-endpoints\"");
        sb.AppendLine("  resource_group_name  = var.resource_group_name");
        sb.AppendLine("  virtual_network_name = azurerm_virtual_network.network.name");
        sb.AppendLine("  address_prefixes     = [var.private_endpoint_subnet_prefix]");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("# Subnet: Application");
        sb.AppendLine("resource \"azurerm_subnet\" \"application\" {");
        sb.AppendLine("  name                 = \"snet-application\"");
        sb.AppendLine("  resource_group_name  = var.resource_group_name");
        sb.AppendLine("  virtual_network_name = azurerm_virtual_network.network.name");
        sb.AppendLine("  address_prefixes     = [var.application_subnet_prefix]");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("# Subnet: Data");
        sb.AppendLine("resource \"azurerm_subnet\" \"data\" {");
        sb.AppendLine("  name                 = \"snet-data\"");
        sb.AppendLine("  resource_group_name  = var.resource_group_name");
        sb.AppendLine("  virtual_network_name = azurerm_virtual_network.network.name");
        sb.AppendLine("  address_prefixes     = [var.data_subnet_prefix]");
        sb.AppendLine();
        sb.AppendLine("  service_endpoints = [");
        sb.AppendLine("    \"Microsoft.Storage\",");
        sb.AppendLine("    \"Microsoft.Sql\",");
        sb.AppendLine("    \"Microsoft.KeyVault\"");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();

        // Additional subnets from config
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
                    var delegationName = subnet.Delegation.Split('/').Last();
                    sb.AppendLine();
                    sb.AppendLine("  delegation {");
                    sb.AppendLine($"    name = \"{delegationName}-delegation\"");
                    sb.AppendLine("    service_delegation {");
                    sb.AppendLine($"      name = \"{subnet.Delegation}\"");
                    sb.AppendLine("    }");
                    sb.AppendLine("  }");
                }

                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Core variables.tf - only variables for core resource
    /// </summary>
    private string GenerateCoreVariablesTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Virtual Network Variables - FedRAMP Compliant");
        sb.AppendLine("# Cross-cutting variables are defined in their respective modules");
        sb.AppendLine();
        sb.AppendLine("variable \"vnet_name\" {");
        sb.AppendLine("  description = \"Name of the virtual network\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_group_name\" {");
        sb.AppendLine("  description = \"Name of the resource group\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"location\" {");
        sb.AppendLine("  description = \"Azure region for resources\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"address_space\" {");
        sb.AppendLine("  description = \"Address space for the virtual network\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = [\"10.0.0.0/16\"]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"private_endpoint_subnet_prefix\" {");
        sb.AppendLine("  description = \"Address prefix for private endpoints subnet\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.1.0/24\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"application_subnet_prefix\" {");
        sb.AppendLine("  description = \"Address prefix for application subnet\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.2.0/24\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"data_subnet_prefix\" {");
        sb.AppendLine("  description = \"Address prefix for data subnet\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.3.0/24\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"ddos_protection_plan_id\" {");
        sb.AppendLine("  description = \"DDoS protection plan ID (optional)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Tags to apply to resources\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Core outputs.tf - resource outputs for cross-cutting composition
    /// </summary>
    private string GenerateCoreOutputsTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Virtual Network Outputs");
        sb.AppendLine("# Used by cross-cutting modules for composition");
        sb.AppendLine();
        sb.AppendLine("output \"vnet_id\" {");
        sb.AppendLine("  description = \"The ID of the virtual network\"");
        sb.AppendLine("  value       = azurerm_virtual_network.network.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"vnet_name\" {");
        sb.AppendLine("  description = \"The name of the virtual network\"");
        sb.AppendLine("  value       = azurerm_virtual_network.network.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"vnet_address_space\" {");
        sb.AppendLine("  description = \"The address space of the virtual network\"");
        sb.AppendLine("  value       = azurerm_virtual_network.network.address_space");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"private_endpoint_subnet_id\" {");
        sb.AppendLine("  description = \"The ID of the private endpoints subnet\"");
        sb.AppendLine("  value       = azurerm_subnet.private_endpoints.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"application_subnet_id\" {");
        sb.AppendLine("  description = \"The ID of the application subnet\"");
        sb.AppendLine("  value       = azurerm_subnet.application.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"data_subnet_id\" {");
        sb.AppendLine("  description = \"The ID of the data subnet\"");
        sb.AppendLine("  value       = azurerm_subnet.data.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"subnet_ids\" {");
        sb.AppendLine("  description = \"Map of all subnet IDs\"");
        sb.AppendLine("  value = {");
        sb.AppendLine("    private_endpoints = azurerm_subnet.private_endpoints.id");
        sb.AppendLine("    application       = azurerm_subnet.application.id");
        sb.AppendLine("    data              = azurerm_subnet.data.id");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate README documentation
    /// </summary>
    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "network";

        sb.AppendLine($"# Azure Virtual Network Module - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("This Terraform module creates an Azure Virtual Network with FedRAMP-compliant security settings.");
        sb.AppendLine("Cross-cutting concerns (NSG, Diagnostic Settings) are composed via separate modules.");
        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Description | Implementation |");
        sb.AppendLine("|---------|-------------|----------------|");
        sb.AppendLine("| SC-7 | Boundary Protection | VNet isolation, service endpoints |");
        sb.AppendLine("| AC-4 | Information Flow | Subnet segmentation |");
        sb.AppendLine("| SC-8 | Transmission Confidentiality | Private endpoints support |");
        sb.AppendLine();
        sb.AppendLine("## Subnets");
        sb.AppendLine();
        sb.AppendLine("| Subnet | Purpose | Default CIDR |");
        sb.AppendLine("|--------|---------|--------------|");
        sb.AppendLine("| snet-private-endpoints | Private endpoints for PaaS services | 10.0.1.0/24 |");
        sb.AppendLine("| snet-application | Application workloads | 10.0.2.0/24 |");
        sb.AppendLine("| snet-data | Data tier with service endpoints | 10.0.3.0/24 |");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"network\" {");
        sb.AppendLine("  source = \"./modules/network\"");
        sb.AppendLine();
        sb.AppendLine("  vnet_name           = \"vnet-myapp\"");
        sb.AppendLine("  resource_group_name = azurerm_resource_group.main.name");
        sb.AppendLine("  location            = azurerm_resource_group.main.location");
        sb.AppendLine("  address_space       = [\"10.0.0.0/16\"]");
        sb.AppendLine("  tags                = local.common_tags");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Composition");
        sb.AppendLine();
        sb.AppendLine("To add cross-cutting concerns, use the appropriate modules:");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"network_nsg\" {");
        sb.AppendLine("  source = \"./modules/cross-cutting/network-security-group\"");
        sb.AppendLine("  ");
        sb.AppendLine("  name                = \"nsg-${var.vnet_name}\"");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  subnet_ids          = module.network.subnet_ids");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }
}
