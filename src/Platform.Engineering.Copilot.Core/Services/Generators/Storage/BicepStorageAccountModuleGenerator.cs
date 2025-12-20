using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Storage;

/// <summary>
/// Bicep module generator for Azure Storage Account infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (PE, diagnostics, RBAC) are handled by reusable generators
/// </summary>
public class BicepStorageAccountModuleGenerator : IResourceModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.Storage;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "storage-account", "storage", "blob", "datalake" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by Storage Account
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.PrivateEndpoint,
        CrossCuttingType.DiagnosticSettings,
        CrossCuttingType.RBACAssignment
    };
    
    /// <summary>
    /// Azure resource type for Storage Account
    /// </summary>
    public string AzureResourceType => "Microsoft.Storage/storageAccounts";

    /// <summary>
    /// Generate ONLY the core Storage Account resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "storage";

        // Generate only core Storage module - no PE, diagnostics, or RBAC
        files["storage-account.bicep"] = GenerateStorageAccountBicep(request);
        files["main.bicep"] = GenerateCoreMainBicep(request);
        files["README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "storageAccount", // Module name for cross-cutting references
            ResourceType = "Microsoft.Storage/storageAccounts",
            OutputNames = new List<string>
            {
                "storageAccountId",
                "storageAccountName",
                "primaryBlobEndpoint",
                "primaryEndpoints",
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
    /// Check if this generator can handle the request
    /// </summary>
    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        return infrastructure.Format == InfrastructureFormat.Bicep &&
               infrastructure.Provider == CloudProvider.Azure &&
               (infrastructure.ComputePlatform == ComputePlatform.Storage ||
                infrastructure.IncludeStorage == true);
    }

    /// <summary>
    /// Core main.bicep - only Storage Account, no cross-cutting modules
    /// Cross-cutting modules are composed by the orchestrator
    /// </summary>
    private string GenerateCoreMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "storage";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        sb.AppendLine("// Azure Storage Account Core Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-28 (Encryption at Rest), CP-9/CP-10 (Backup/Recovery), AU-11 (Audit Retention)");
        sb.AppendLine("// Cross-cutting concerns (PE, diagnostics, RBAC) are composed separately");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("@description('Name of the Storage Account')");
        sb.AppendLine("param storageAccountName string");
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
        sb.AppendLine("@description('Storage Account SKU')");
        sb.AppendLine("@allowed(['Standard_LRS', 'Standard_GRS', 'Standard_RAGRS', 'Standard_ZRS', 'Premium_LRS'])");
        sb.AppendLine("param skuName string = 'Standard_LRS'");
        sb.AppendLine();
        sb.AppendLine("@description('Storage Account kind')");
        sb.AppendLine("@allowed(['Storage', 'StorageV2', 'BlobStorage', 'FileStorage', 'BlockBlobStorage'])");
        sb.AppendLine("param kind string = 'StorageV2'");
        sb.AppendLine();
        sb.AppendLine("@description('Enable hierarchical namespace for Data Lake')");
        sb.AppendLine("param isHnsEnabled bool = false");
        sb.AppendLine();
        sb.AppendLine("@description('Enable public network access')");
        sb.AppendLine("param enablePublicNetworkAccess bool = false");
        sb.AppendLine();

        // Storage Account Module
        sb.AppendLine("// Storage Account Core Resource");
        sb.AppendLine("module storageAccount './storage-account.bicep' = {");
        sb.AppendLine("  name: '${storageAccountName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    storageAccountName: storageAccountName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    skuName: skuName");
        sb.AppendLine("    kind: kind");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("    isHnsEnabled: isHnsEnabled");
        sb.AppendLine("    enableHttpsOnly: true  // FedRAMP SC-8");
        sb.AppendLine("    minimumTlsVersion: 'TLS1_2'  // FedRAMP SC-8");
        sb.AppendLine("    allowBlobPublicAccess: false  // FedRAMP AC-3");
        sb.AppendLine("    publicNetworkAccess: enablePublicNetworkAccess ? 'Enabled' : 'Disabled'");
        sb.AppendLine("    softDeleteRetentionDays: 14  // FedRAMP CP-9");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// Outputs for cross-cutting module composition");
        sb.AppendLine("output storageAccountId string = storageAccount.outputs.storageAccountId");
        sb.AppendLine("output storageAccountName string = storageAccount.outputs.storageAccountName");
        sb.AppendLine("output primaryBlobEndpoint string = storageAccount.outputs.primaryBlobEndpoint");
        sb.AppendLine("output primaryEndpoints object = storageAccount.outputs.primaryEndpoints");
        sb.AppendLine("output resourceId string = storageAccount.outputs.storageAccountId");
        sb.AppendLine("output resourceName string = storageAccount.outputs.storageAccountName");

        return sb.ToString();
    }

    private string GenerateStorageAccountBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Storage Account Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-28 (Encryption at Rest), CP-9 (Recovery), SC-8 (Transmission Confidentiality)");
        sb.AppendLine();
        sb.AppendLine("param storageAccountName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param skuName string");
        sb.AppendLine("param kind string");
        sb.AppendLine("param tags object");
        sb.AppendLine("param isHnsEnabled bool");
        sb.AppendLine("param enableHttpsOnly bool");
        sb.AppendLine("param minimumTlsVersion string");
        sb.AppendLine("param allowBlobPublicAccess bool");
        sb.AppendLine("param publicNetworkAccess string");
        sb.AppendLine("param softDeleteRetentionDays int");
        sb.AppendLine();

        sb.AppendLine("resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {");
        sb.AppendLine("  name: storageAccountName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  sku: {");
        sb.AppendLine("    name: skuName");
        sb.AppendLine("  }");
        sb.AppendLine("  kind: kind");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    isHnsEnabled: isHnsEnabled");
        sb.AppendLine("    supportsHttpsTrafficOnly: enableHttpsOnly");
        sb.AppendLine("    minimumTlsVersion: minimumTlsVersion");
        sb.AppendLine("    allowBlobPublicAccess: allowBlobPublicAccess");
        sb.AppendLine("    publicNetworkAccess: publicNetworkAccess");
        sb.AppendLine("    networkAcls: {");
        sb.AppendLine("      bypass: 'AzureServices'");
        sb.AppendLine("      defaultAction: publicNetworkAccess == 'Enabled' ? 'Allow' : 'Deny'");
        sb.AppendLine("    }");
        sb.AppendLine("    encryption: {");
        sb.AppendLine("      services: {");
        sb.AppendLine("        blob: {");
        sb.AppendLine("          enabled: true  // SC-28");
        sb.AppendLine("        }");
        sb.AppendLine("        file: {");
        sb.AppendLine("          enabled: true  // SC-28");
        sb.AppendLine("        }");
        sb.AppendLine("        table: {");
        sb.AppendLine("          enabled: true  // SC-28");
        sb.AppendLine("        }");
        sb.AppendLine("        queue: {");
        sb.AppendLine("          enabled: true  // SC-28");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("      keySource: 'Microsoft.Storage'");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Blob Services with soft delete
        sb.AppendLine("// Blob Services - FedRAMP CP-9 (Recovery)");
        sb.AppendLine("resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {");
        sb.AppendLine("  parent: storageAccount");
        sb.AppendLine("  name: 'default'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    containerDeleteRetentionPolicy: {");
        sb.AppendLine("      enabled: true");
        sb.AppendLine("      days: softDeleteRetentionDays");
        sb.AppendLine("    }");
        sb.AppendLine("    deleteRetentionPolicy: {");
        sb.AppendLine("      enabled: true");
        sb.AppendLine("      days: softDeleteRetentionDays");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("output storageAccountId string = storageAccount.id");
        sb.AppendLine("output storageAccountName string = storageAccount.name");
        sb.AppendLine("output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob");
        sb.AppendLine("output primaryEndpoints object = storageAccount.properties.primaryEndpoints");

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "storage";

        sb.AppendLine($"# Azure Storage Account Module - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## FedRAMP Compliance");
        sb.AppendLine();
        sb.AppendLine("This module implements the following NIST 800-53 controls:");
        sb.AppendLine();
        sb.AppendLine("| Control | Description | Implementation |");
        sb.AppendLine("|---------|-------------|----------------|");
        sb.AppendLine("| SC-28 | Protection of Information at Rest | Storage Service Encryption (SSE) enabled for all services |");
        sb.AppendLine("| SC-8 | Transmission Confidentiality | HTTPS-only, TLS 1.2 minimum |");
        sb.AppendLine("| CP-9 | Information System Backup | Soft delete enabled with 14-day retention |");
        sb.AppendLine("| AC-3 | Access Enforcement | Public blob access disabled |");
        sb.AppendLine();
        sb.AppendLine("## Architecture");
        sb.AppendLine();
        sb.AppendLine("This is a **core resource module** that generates only the Storage Account resource.");
        sb.AppendLine("Cross-cutting concerns are composed separately by the infrastructure orchestrator:");
        sb.AppendLine();
        sb.AppendLine("- **Private Endpoint**: Network isolation via Azure Private Link");
        sb.AppendLine("- **Diagnostic Settings**: Audit logging to Log Analytics");
        sb.AppendLine("- **RBAC**: Role-based access control assignments");
        sb.AppendLine();
        sb.AppendLine("## Parameters");
        sb.AppendLine();
        sb.AppendLine("| Parameter | Type | Default | Description |");
        sb.AppendLine("|-----------|------|---------|-------------|");
        sb.AppendLine("| storageAccountName | string | required | Name of the storage account |");
        sb.AppendLine("| location | string | resourceGroup | Azure region |");
        sb.AppendLine("| skuName | string | Standard_LRS | Storage SKU |");
        sb.AppendLine("| kind | string | StorageV2 | Storage account kind |");
        sb.AppendLine("| isHnsEnabled | bool | false | Enable hierarchical namespace (Data Lake) |");
        sb.AppendLine();
        sb.AppendLine("## Outputs");
        sb.AppendLine();
        sb.AppendLine("| Output | Description |");
        sb.AppendLine("|--------|-------------|");
        sb.AppendLine("| storageAccountId | Resource ID of the storage account |");
        sb.AppendLine("| storageAccountName | Name of the storage account |");
        sb.AppendLine("| primaryBlobEndpoint | Primary blob service endpoint |");
        sb.AppendLine("| primaryEndpoints | All primary endpoints |");

        return sb.ToString();
    }
}
