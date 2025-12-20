using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;

/// <summary>
/// Bicep module generator for Azure Virtual Network infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (NSG, diagnostics) are handled by reusable generators
/// </summary>
public class BicepNetworkResourceModuleGenerator : IResourceModuleGenerator
{    
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.Networking;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "vnet", "network", "virtual-network", "networking" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by VNet
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

        // Generate core VNet module files
        files["virtual-network.bicep"] = GenerateVirtualNetworkBicep(request);
        files["main.bicep"] = GenerateCoreMainBicep(request);
        files["README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "vnet", // Module name for cross-cutting references
            ResourceType = "Microsoft.Network/virtualNetworks",
            OutputNames = new List<string>
            {
                "vnetId",
                "vnetName",
                "addressSpace",
                "subnets",
                "resourceId",
                "resourceName"
            },
            SupportedCrossCutting = new List<CrossCuttingType>
            {
                CrossCuttingType.DiagnosticSettings,
                CrossCuttingType.NetworkSecurityGroup
            }
        };
    }

    /// <summary>
    /// Legacy GenerateModule - delegates to existing generator for backward compatibility
    /// </summary>
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        // For full VNet modules, delegate to core resource generation
        var result = GenerateCoreResource(request);
        return result.Files;
    }
    
    /// <summary>
    /// Check if this generator can handle the request
    /// </summary>
    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        return infrastructure.Format == InfrastructureFormat.Bicep &&
               infrastructure.Provider == CloudProvider.Azure &&
               infrastructure.ComputePlatform == ComputePlatform.Networking;
    }

    /// <summary>
    /// Core main.bicep - only VNet and subnets, no cross-cutting modules
    /// Cross-cutting modules are composed by the orchestrator
    /// </summary>
    private string GenerateCoreMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "network";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var networkConfig = infrastructure.NetworkConfig ?? new NetworkingConfiguration();

        sb.AppendLine("// Azure Virtual Network Core Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-7 (Boundary Protection), AC-4 (Information Flow), SC-8 (Transmission Confidentiality)");
        sb.AppendLine("// Cross-cutting concerns (NSG, diagnostics) are composed separately");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("@description('Name of the Virtual Network')");
        sb.AppendLine("param vnetName string");
        sb.AppendLine();
        sb.AppendLine("@description('Azure region for deployment')");
        sb.AppendLine($"param location string = '{infrastructure.Region}'");
        sb.AppendLine();
        sb.AppendLine("@description('Environment name')");
        sb.AppendLine("param environment string = 'dev'");
        sb.AppendLine();
        sb.AppendLine("@description('Resource tags')");
        sb.AppendLine("param tags object = {}");
        sb.AppendLine();
        sb.AppendLine("@description('VNet address space')");
        sb.AppendLine($"param addressSpace string = '{networkConfig.VNetAddressSpace ?? "10.0.0.0/16"}'");
        sb.AppendLine();
        sb.AppendLine("@description('Enable DDoS protection')");
        sb.AppendLine($"param enableDdosProtection bool = {networkConfig.EnableDDoSProtection.ToString().ToLower()}");
        sb.AppendLine();

        // VNet Module
        sb.AppendLine("// Virtual Network Core Resource");
        sb.AppendLine("module vnet './virtual-network.bicep' = {");
        sb.AppendLine("  name: '${vnetName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    vnetName: vnetName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    addressSpace: addressSpace");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("    enableDdosProtection: enableDdosProtection");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// Outputs for cross-cutting module composition");
        sb.AppendLine("output vnetId string = vnet.outputs.vnetId");
        sb.AppendLine("output vnetName string = vnet.outputs.vnetName");
        sb.AppendLine("output addressSpace string = vnet.outputs.addressSpace");
        sb.AppendLine("output subnets array = vnet.outputs.subnets");
        sb.AppendLine("output resourceId string = vnet.outputs.vnetId");
        sb.AppendLine("output resourceName string = vnet.outputs.vnetName");

        return sb.ToString();
    }

    private string GenerateVirtualNetworkBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var networkConfig = infrastructure.NetworkConfig ?? new NetworkingConfiguration();

        sb.AppendLine("// Virtual Network Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-7 (Boundary Protection), AC-4 (Information Flow)");
        sb.AppendLine();
        sb.AppendLine("param vnetName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param addressSpace string");
        sb.AppendLine("param tags object");
        sb.AppendLine("param enableDdosProtection bool");
        sb.AppendLine();

        // Default subnets if not specified
        var subnets = networkConfig.Subnets ?? new List<SubnetConfiguration>
        {
            new() { Name = "default", AddressPrefix = "10.0.0.0/24" }
        };

        sb.AppendLine("resource vnet 'Microsoft.Network/virtualNetworks@2023-05-01' = {");
        sb.AppendLine("  name: vnetName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    addressSpace: {");
        sb.AppendLine("      addressPrefixes: [");
        sb.AppendLine("        addressSpace");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine("    subnets: [");

        foreach (var subnet in subnets)
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
        sb.AppendLine("    enableDdosProtection: enableDdosProtection");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("output vnetId string = vnet.id");
        sb.AppendLine("output vnetName string = vnet.name");
        sb.AppendLine("output addressSpace string = vnet.properties.addressSpace.addressPrefixes[0]");
        sb.AppendLine("output subnets array = [for subnet in vnet.properties.subnets: {");
        sb.AppendLine("  name: subnet.name");
        sb.AppendLine("  id: subnet.id");
        sb.AppendLine("  addressPrefix: subnet.properties.addressPrefix");
        sb.AppendLine("}]");

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "network";

        sb.AppendLine($"# Azure Virtual Network Module - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## FedRAMP Compliance");
        sb.AppendLine();
        sb.AppendLine("This module implements the following NIST 800-53 controls:");
        sb.AppendLine();
        sb.AppendLine("| Control | Description | Implementation |");
        sb.AppendLine("|---------|-------------|----------------|");
        sb.AppendLine("| SC-7 | Boundary Protection | VNet segmentation, subnets |");
        sb.AppendLine("| AC-4 | Information Flow Enforcement | Subnet isolation |");
        sb.AppendLine("| SC-5 | Denial of Service Protection | DDoS Protection Plan support |");
        sb.AppendLine();
        sb.AppendLine("## Architecture");
        sb.AppendLine();
        sb.AppendLine("This is a **core resource module** that generates only the Virtual Network resource.");
        sb.AppendLine("Cross-cutting concerns are composed separately by the infrastructure orchestrator:");
        sb.AppendLine();
        sb.AppendLine("- **NSG**: Network Security Groups for subnet protection");
        sb.AppendLine("- **Diagnostic Settings**: Flow logging to Log Analytics");
        sb.AppendLine();
        sb.AppendLine("## Parameters");
        sb.AppendLine();
        sb.AppendLine("| Parameter | Type | Default | Description |");
        sb.AppendLine("|-----------|------|---------|-------------|");
        sb.AppendLine("| vnetName | string | required | Name of the VNet |");
        sb.AppendLine("| addressSpace | string | 10.0.0.0/16 | CIDR address space |");
        sb.AppendLine("| enableDdosProtection | bool | false | Enable DDoS protection |");
        sb.AppendLine();
        sb.AppendLine("## Outputs");
        sb.AppendLine();
        sb.AppendLine("| Output | Description |");
        sb.AppendLine("|--------|-------------|");
        sb.AppendLine("| vnetId | Resource ID of the VNet |");
        sb.AppendLine("| vnetName | Name of the VNet |");
        sb.AppendLine("| addressSpace | Address space of the VNet |");
        sb.AppendLine("| subnets | Array of subnet information |");

        return sb.ToString();
    }
}
