using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.CrossCutting;

/// <summary>
/// Reusable Bicep Private Endpoint Generator
/// Creates private endpoints for any supported Azure resource type
/// Implements FedRAMP SC-7 (Boundary Protection)
/// </summary>
public class BicepPrivateEndpointGenerator : ICrossCuttingModuleGenerator
{
    public CrossCuttingType Type => CrossCuttingType.PrivateEndpoint;
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public CloudProvider Provider => CloudProvider.Azure;

    public Dictionary<string, string> GenerateModule(CrossCuttingRequest request)
    {
        var files = new Dictionary<string, string>();
        var groupId = CrossCuttingCapabilityMap.GetGroupId(request.ResourceType);
        
        if (string.IsNullOrEmpty(groupId))
        {
            throw new ArgumentException($"Resource type '{request.ResourceType}' does not support private endpoints");
        }

        var modulePath = string.IsNullOrEmpty(request.ModulePath) 
            ? "modules/private-endpoint" 
            : request.ModulePath;

        files[$"{modulePath}/private-endpoint.bicep"] = GeneratePrivateEndpointBicep(request, groupId);
        
        // Generate DNS zone module if requested
        var config = GetConfig(request);
        if (config.CreateDnsZone)
        {
            files[$"{modulePath}/private-dns-zone.bicep"] = GeneratePrivateDnsZoneBicep(request);
        }

        return files;
    }

    public bool CanGenerate(string resourceType)
    {
        return CrossCuttingCapabilityMap.SupportsCapability(resourceType, CrossCuttingType.PrivateEndpoint);
    }

    public string GenerateModuleInvocation(CrossCuttingRequest request, string dependsOn)
    {
        var sb = new StringBuilder();
        var moduleName = $"{request.ResourceName.Replace("-", "_")}_pe";
        var modulePath = string.IsNullOrEmpty(request.ModulePath) 
            ? "modules/private-endpoint" 
            : request.ModulePath;
        var config = GetConfig(request);

        sb.AppendLine($"// Private Endpoint for {request.ResourceName} - FedRAMP SC-7");
        sb.AppendLine($"module {moduleName} './{modulePath}/private-endpoint.bicep' = {{");
        sb.AppendLine($"  name: '{request.ResourceName}-pe-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine($"    privateEndpointName: '{request.ResourceName}-pe'");
        sb.AppendLine("    location: location");
        sb.AppendLine($"    resourceId: {request.ResourceReference}");
        sb.AppendLine($"    subnetId: {(string.IsNullOrEmpty(config.SubnetId) ? "privateEndpointSubnetId" : $"'{config.SubnetId}'")}");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("  }");
        
        if (!string.IsNullOrEmpty(dependsOn))
        {
            sb.AppendLine($"  dependsOn: [{dependsOn}]");
        }
        
        sb.AppendLine("}");

        return sb.ToString();
    }

    private PrivateEndpointConfig GetConfig(CrossCuttingRequest request)
    {
        if (request.Config.TryGetValue("privateEndpoint", out var configObj) && configObj is PrivateEndpointConfig peConfig)
        {
            return peConfig;
        }

        // Build config from individual properties
        return new PrivateEndpointConfig
        {
            SubnetId = request.Config.TryGetValue("subnetId", out var subnet) ? subnet?.ToString() ?? "" : "",
            CreateDnsZone = request.Config.TryGetValue("createDnsZone", out var dns) && dns is bool createDns && createDns,
            VNetId = request.Config.TryGetValue("vnetId", out var vnet) ? vnet?.ToString() : null
        };
    }

    private string GeneratePrivateEndpointBicep(CrossCuttingRequest request, string groupId)
    {
        var sb = new StringBuilder();
        var dnsZoneName = CrossCuttingCapabilityMap.GetDnsZoneName(request.ResourceType);

        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// Private Endpoint Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-7 (Boundary Protection), AC-4 (Information Flow Enforcement)");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine();
        
        // Parameters
        sb.AppendLine("@description('Name of the private endpoint')");
        sb.AppendLine("param privateEndpointName string");
        sb.AppendLine();
        sb.AppendLine("@description('Azure region for deployment')");
        sb.AppendLine("param location string");
        sb.AppendLine();
        sb.AppendLine("@description('Resource ID of the target resource')");
        sb.AppendLine("param resourceId string");
        sb.AppendLine();
        sb.AppendLine("@description('Subnet ID for the private endpoint')");
        sb.AppendLine("param subnetId string");
        sb.AppendLine();
        sb.AppendLine("@description('Resource tags')");
        sb.AppendLine("param tags object = {}");
        sb.AppendLine();
        
        if (dnsZoneName != null)
        {
            sb.AppendLine("@description('Existing private DNS zone ID (optional)')");
            sb.AppendLine("param privateDnsZoneId string = ''");
            sb.AppendLine();
        }

        // Private Endpoint resource
        sb.AppendLine("// Private Endpoint Resource");
        sb.AppendLine("resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = {");
        sb.AppendLine("  name: privateEndpointName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: union(tags, {");
        sb.AppendLine("    'security-control': 'SC-7'");
        sb.AppendLine("    'managed-by': 'bicep'");
        sb.AppendLine("  })");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    subnet: {");
        sb.AppendLine("      id: subnetId");
        sb.AppendLine("    }");
        sb.AppendLine("    privateLinkServiceConnections: [");
        sb.AppendLine("      {");
        sb.AppendLine("        name: '${privateEndpointName}-connection'");
        sb.AppendLine("        properties: {");
        sb.AppendLine("          privateLinkServiceId: resourceId");
        sb.AppendLine($"          groupIds: ['{groupId}']");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // DNS Zone Group (if DNS zone provided)
        if (dnsZoneName != null)
        {
            sb.AppendLine("// Private DNS Zone Group (links PE to DNS zone)");
            sb.AppendLine("resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-05-01' = if (!empty(privateDnsZoneId)) {");
            sb.AppendLine("  parent: privateEndpoint");
            sb.AppendLine("  name: 'default'");
            sb.AppendLine("  properties: {");
            sb.AppendLine("    privateDnsZoneConfigs: [");
            sb.AppendLine("      {");
            sb.AppendLine($"        name: '{groupId}-config'");
            sb.AppendLine("        properties: {");
            sb.AppendLine("          privateDnsZoneId: privateDnsZoneId");
            sb.AppendLine("        }");
            sb.AppendLine("      }");
            sb.AppendLine("    ]");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Outputs
        sb.AppendLine("// ===== OUTPUTS =====");
        sb.AppendLine("output privateEndpointId string = privateEndpoint.id");
        sb.AppendLine("output privateEndpointName string = privateEndpoint.name");
        sb.AppendLine("output networkInterfaceId string = privateEndpoint.properties.networkInterfaces[0].id");

        return sb.ToString();
    }

    private string GeneratePrivateDnsZoneBicep(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();
        var dnsZoneName = CrossCuttingCapabilityMap.GetDnsZoneName(request.ResourceType) ?? "privatelink.azure.net";

        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// Private DNS Zone Module");
        sb.AppendLine("// Provides DNS resolution for private endpoints");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine();
        
        // Parameters
        sb.AppendLine("@description('Name of the private DNS zone')");
        sb.AppendLine($"param privateDnsZoneName string = '{dnsZoneName}'");
        sb.AppendLine();
        sb.AppendLine("@description('VNet ID to link the DNS zone to')");
        sb.AppendLine("param vnetId string");
        sb.AppendLine();
        sb.AppendLine("@description('Resource tags')");
        sb.AppendLine("param tags object = {}");
        sb.AppendLine();

        // DNS Zone
        sb.AppendLine("// Private DNS Zone");
        sb.AppendLine("resource privateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {");
        sb.AppendLine("  name: privateDnsZoneName");
        sb.AppendLine("  location: 'global'");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("}");
        sb.AppendLine();

        // VNet Link
        sb.AppendLine("// VNet Link");
        sb.AppendLine("resource vnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {");
        sb.AppendLine("  parent: privateDnsZone");
        sb.AppendLine("  name: '${privateDnsZoneName}-link'");
        sb.AppendLine("  location: 'global'");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    registrationEnabled: false");
        sb.AppendLine("    virtualNetwork: {");
        sb.AppendLine("      id: vnetId");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// ===== OUTPUTS =====");
        sb.AppendLine("output privateDnsZoneId string = privateDnsZone.id");
        sb.AppendLine("output privateDnsZoneName string = privateDnsZone.name");

        return sb.ToString();
    }
}
