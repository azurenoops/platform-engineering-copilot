using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.ContainerRegistry;

/// <summary>
/// Bicep module generator for Azure Container Registry infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (PE, diagnostics) are handled by reusable generators
/// </summary>
public class BicepContainerRegistryModuleGenerator : IResourceModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.Storage;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "container-registry", "acr", "registry" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by ACR
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.PrivateEndpoint,
        CrossCuttingType.DiagnosticSettings,
        CrossCuttingType.RBACAssignment
    };
    
    /// <summary>
    /// Azure resource type for Container Registry
    /// </summary>
    public string AzureResourceType => "Microsoft.ContainerRegistry/registries";

    /// <summary>
    /// Generate ONLY the core ACR resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "acr";

        // Generate only core ACR module - no PE, diagnostics, or RBAC
        files["container-registry.bicep"] = GenerateContainerRegistryBicep(request);
        files["main.bicep"] = GenerateCoreMainBicep(request);
        files["README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "acr", // Module name for cross-cutting references
            ResourceType = "Microsoft.ContainerRegistry/registries",
            OutputNames = new List<string>
            {
                "acrId",
                "acrName",
                "loginServer",
                "resourceId",
                "resourceName"
            },
            SupportedCrossCutting = new List<CrossCuttingType>
            {
                CrossCuttingType.PrivateEndpoint,
                CrossCuttingType.DiagnosticSettings,
                CrossCuttingType.RBACAssignment
            }
        };
    }

    /// <summary>
    /// Legacy GenerateModule - delegates to GenerateCoreResource for composition pattern
    /// </summary>
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var result = GenerateCoreResource(request);
        return result.Files;
    }

    /// <summary>
    /// Core main.bicep - only ACR, no cross-cutting modules
    /// </summary>
    private string GenerateCoreMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "acr";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        // ACR names must be alphanumeric only
        var acrName = serviceName.Replace("-", "").Replace("_", "");

        sb.AppendLine("// Azure Container Registry Core Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-7 (Boundary Protection), SI-7 (Software Integrity), CM-7 (Least Functionality)");
        sb.AppendLine("// Cross-cutting concerns (PE, diagnostics, RBAC) are composed separately");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("@description('Name of the Container Registry (alphanumeric only)')");
        sb.AppendLine("param acrName string");
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
        sb.AppendLine("@description('Container Registry SKU')");
        sb.AppendLine("@allowed(['Basic', 'Standard', 'Premium'])");
        sb.AppendLine("param sku string = 'Premium'");
        sb.AppendLine();
        sb.AppendLine("@description('Enable admin user')");
        sb.AppendLine("param adminUserEnabled bool = false");
        sb.AppendLine();
        sb.AppendLine("@description('Enable public network access')");
        sb.AppendLine("param publicNetworkAccess bool = true");
        sb.AppendLine();

        // ACR Module
        sb.AppendLine("// Container Registry Core Resource");
        sb.AppendLine("module acr './container-registry.bicep' = {");
        sb.AppendLine("  name: '${acrName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    acrName: acrName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    sku: sku");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("    adminUserEnabled: adminUserEnabled  // FedRAMP CM-7 - Least functionality");
        sb.AppendLine("    publicNetworkAccess: publicNetworkAccess ? 'Enabled' : 'Disabled'");
        sb.AppendLine("    enableContentTrust: true  // FedRAMP SI-7 - Software integrity");
        sb.AppendLine("    dataEndpointEnabled: !publicNetworkAccess  // Enable for private link");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// Outputs for cross-cutting module composition");
        sb.AppendLine("output acrId string = acr.outputs.acrId");
        sb.AppendLine("output acrName string = acr.outputs.acrName");
        sb.AppendLine("output loginServer string = acr.outputs.loginServer");
        sb.AppendLine("output resourceId string = acr.outputs.acrId");
        sb.AppendLine("output resourceName string = acr.outputs.acrName");

        return sb.ToString();
    }

    private string GenerateContainerRegistryBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Container Registry Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-7 (Boundary Protection), SI-7 (Software Integrity), CM-7 (Least Functionality)");
        sb.AppendLine();
        sb.AppendLine("param acrName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param sku string");
        sb.AppendLine("param tags object");
        sb.AppendLine("param adminUserEnabled bool");
        sb.AppendLine("param publicNetworkAccess string");
        sb.AppendLine("param enableContentTrust bool");
        sb.AppendLine("param dataEndpointEnabled bool");
        sb.AppendLine();

        sb.AppendLine("resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {");
        sb.AppendLine("  name: acrName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: union(tags, {");
        sb.AppendLine("    'security-control': 'SC-7,SI-7,CM-7'");
        sb.AppendLine("    'managed-by': 'bicep'");
        sb.AppendLine("  })");
        sb.AppendLine("  sku: {");
        sb.AppendLine("    name: sku");
        sb.AppendLine("  }");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    adminUserEnabled: adminUserEnabled");
        sb.AppendLine("    publicNetworkAccess: publicNetworkAccess");
        sb.AppendLine("    zoneRedundancy: sku == 'Premium' ? 'Disabled' : 'Disabled'");
        sb.AppendLine("    dataEndpointEnabled: dataEndpointEnabled");
        sb.AppendLine("    policies: {");
        sb.AppendLine("      trustPolicy: {");
        sb.AppendLine("        type: 'Notary'");
        sb.AppendLine("        status: enableContentTrust ? 'enabled' : 'disabled'");
        sb.AppendLine("      }");
        sb.AppendLine("      quarantinePolicy: {");
        sb.AppendLine("        status: 'disabled'");
        sb.AppendLine("      }");
        sb.AppendLine("      retentionPolicy: {");
        sb.AppendLine("        days: 30");
        sb.AppendLine("        status: 'enabled'");
        sb.AppendLine("      }");
        sb.AppendLine("      exportPolicy: {");
        sb.AppendLine("        status: 'enabled'");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("    encryption: {");
        sb.AppendLine("      status: 'disabled'  // Enable with customer-managed key if required");
        sb.AppendLine("    }");
        sb.AppendLine("    networkRuleBypassOptions: 'AzureServices'");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output acrId string = acr.id");
        sb.AppendLine("output acrName string = acr.name");
        sb.AppendLine("output loginServer string = acr.properties.loginServer");

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "acr";

        sb.AppendLine($"# Azure Container Registry - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("FedRAMP-compliant Bicep infrastructure for Azure Container Registry with:");
        sb.AppendLine("- Premium SKU for enterprise features");
        sb.AppendLine("- Content trust enabled - FedRAMP SI-7");
        sb.AppendLine("- Admin user disabled by default - FedRAMP CM-7");
        sb.AppendLine("- Network access controls - FedRAMP SC-7");
        sb.AppendLine("- 30-day retention policy for untagged images");
        sb.AppendLine("- Azure services bypass for trusted access");
        
        if (request.Security?.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("- Private endpoint connectivity - FedRAMP SC-7");
        }
        
        if (request.Observability?.EnableDiagnostics == true)
        {
            sb.AppendLine("- Diagnostic settings for audit logging - FedRAMP AU-2");
        }

        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Implementation |");
        sb.AppendLine("|---------|----------------|");
        sb.AppendLine("| SC-7 | Network boundary protection (private endpoint optional) |");
        sb.AppendLine("| SI-7 | Software integrity via content trust |");
        sb.AppendLine("| CM-7 | Least functionality (admin user disabled) |");
        sb.AppendLine("| AU-2 | Audit events via diagnostic settings |");
        sb.AppendLine();
        sb.AppendLine("## Deployment");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Deploy the Container Registry infrastructure");
        sb.AppendLine("az deployment group create \\");
        sb.AppendLine("  --resource-group <resource-group> \\");
        sb.AppendLine("  --template-file infra/modules/acr/main.bicep \\");
        sb.AppendLine($"  --parameters acrName={serviceName.Replace("-", "")}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    /// <summary>
    /// Check if this generator can handle the specified request
    /// </summary>
    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure;
        if (infrastructure == null) return false;

        // Check format and provider match
        if (infrastructure.Format != InfrastructureFormat.Bicep ||
            infrastructure.Provider != CloudProvider.Azure)
            return false;

        // Match ACR-specific requests
        var serviceName = request.ServiceName?.ToLowerInvariant() ?? "";
        return serviceName.Contains("acr") || 
               serviceName.Contains("registry") || 
               serviceName.Contains("container");
    }
}
