using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.KeyVault;

/// <summary>
/// Bicep module generator for Azure Key Vault infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (PE, diagnostics, RBAC) are handled by reusable generators
/// </summary>
public class BicepKeyVaultModuleGenerator : IResourceModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.Security;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "keyvault", "key-vault", "vault" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by Key Vault
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.PrivateEndpoint,
        CrossCuttingType.DiagnosticSettings,
        CrossCuttingType.RBACAssignment
    };
    
    /// <summary>
    /// Azure resource type for Key Vault
    /// </summary>
    public string AzureResourceType => "Microsoft.KeyVault/vaults";

    /// <summary>
    /// Generate ONLY the core Key Vault resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "keyvault";

        // Generate only core Key Vault module - no PE, diagnostics, or RBAC
        files["key-vault.bicep"] = GenerateKeyVaultBicep(request);
        files["main.bicep"] = GenerateCoreMainBicep(request);
        files["README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "keyVault", // Module name for cross-cutting references
            ResourceType = "Microsoft.KeyVault/vaults",
            OutputNames = new List<string>
            {
                "keyVaultId",
                "keyVaultName",
                "keyVaultUri",
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
        // Delegate to new composition pattern
        var result = GenerateCoreResource(request);
        return result.Files;
    }

    /// <summary>
    /// Core main.bicep - only KeyVault, no cross-cutting modules
    /// Cross-cutting modules are composed by the orchestrator
    /// </summary>
    private string GenerateCoreMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "keyvault";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        sb.AppendLine("// Azure Key Vault Core Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-12 (Cryptographic Key Management), SC-28 (Encryption at Rest)");
        sb.AppendLine("// Cross-cutting concerns (PE, diagnostics, RBAC) are composed separately");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("@description('Name of the Key Vault')");
        sb.AppendLine("param keyVaultName string");
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
        sb.AppendLine("@description('Tenant ID for Azure AD')");
        sb.AppendLine("param tenantId string = subscription().tenantId");
        sb.AppendLine();
        sb.AppendLine("@description('Key Vault SKU')");
        sb.AppendLine("@allowed(['standard', 'premium'])");
        sb.AppendLine("param skuName string = 'standard'");
        sb.AppendLine();
        sb.AppendLine("@description('Enable public network access')");
        sb.AppendLine("param enablePublicNetworkAccess bool = false");
        sb.AppendLine();

        // Key Vault Module
        sb.AppendLine("// Key Vault Core Resource");
        sb.AppendLine("module keyVault './key-vault.bicep' = {");
        sb.AppendLine("  name: '${keyVaultName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    keyVaultName: keyVaultName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    tenantId: tenantId");
        sb.AppendLine("    skuName: skuName");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("    enableRbacAuthorization: true  // FedRAMP AC-3");
        sb.AppendLine("    enableSoftDelete: true  // FedRAMP CP-9");
        sb.AppendLine("    softDeleteRetentionInDays: 90  // FedRAMP AU-11");
        sb.AppendLine("    enablePurgeProtection: true  // FedRAMP CP-9");
        sb.AppendLine("    publicNetworkAccess: enablePublicNetworkAccess ? 'Enabled' : 'Disabled'");
        sb.AppendLine("    enabledForDeployment: true");
        sb.AppendLine("    enabledForDiskEncryption: true  // FedRAMP SC-28");
        sb.AppendLine("    enabledForTemplateDeployment: true");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// Outputs for cross-cutting module composition");
        sb.AppendLine("output keyVaultId string = keyVault.outputs.keyVaultId");
        sb.AppendLine("output keyVaultName string = keyVault.outputs.keyVaultName");
        sb.AppendLine("output keyVaultUri string = keyVault.outputs.keyVaultUri");
        sb.AppendLine("output resourceId string = keyVault.outputs.keyVaultId");
        sb.AppendLine("output resourceName string = keyVault.outputs.keyVaultName");

        return sb.ToString();
    }

    private string GenerateKeyVaultBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Key Vault Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-12 (Cryptographic Key Management), AC-3 (Access Control), CP-9 (Recovery)");
        sb.AppendLine();
        sb.AppendLine("param keyVaultName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param tenantId string");
        sb.AppendLine("param skuName string");
        sb.AppendLine("param tags object");
        sb.AppendLine("param enableRbacAuthorization bool");
        sb.AppendLine("param enableSoftDelete bool");
        sb.AppendLine("param softDeleteRetentionInDays int");
        sb.AppendLine("param enablePurgeProtection bool");
        sb.AppendLine("param publicNetworkAccess string");
        sb.AppendLine("param enabledForDeployment bool");
        sb.AppendLine("param enabledForDiskEncryption bool");
        sb.AppendLine("param enabledForTemplateDeployment bool");
        sb.AppendLine();

        sb.AppendLine("resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {");
        sb.AppendLine("  name: keyVaultName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    tenantId: tenantId");
        sb.AppendLine("    sku: {");
        sb.AppendLine("      family: 'A'");
        sb.AppendLine("      name: skuName");
        sb.AppendLine("    }");
        sb.AppendLine("    enableRbacAuthorization: enableRbacAuthorization");
        sb.AppendLine("    enableSoftDelete: enableSoftDelete");
        sb.AppendLine("    softDeleteRetentionInDays: softDeleteRetentionInDays");
        sb.AppendLine("    enablePurgeProtection: enablePurgeProtection ? true : null");
        sb.AppendLine("    publicNetworkAccess: publicNetworkAccess");
        sb.AppendLine("    enabledForDeployment: enabledForDeployment");
        sb.AppendLine("    enabledForDiskEncryption: enabledForDiskEncryption");
        sb.AppendLine("    enabledForTemplateDeployment: enabledForTemplateDeployment");
        sb.AppendLine("    networkAcls: {");
        sb.AppendLine("      bypass: 'AzureServices'");
        sb.AppendLine("      defaultAction: publicNetworkAccess == 'Enabled' ? 'Allow' : 'Deny'");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output keyVaultId string = keyVault.id");
        sb.AppendLine("output keyVaultName string = keyVault.name");
        sb.AppendLine("output keyVaultUri string = keyVault.properties.vaultUri");

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "keyvault";

        sb.AppendLine($"# Azure Key Vault - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("FedRAMP-compliant Bicep infrastructure for Azure Key Vault with:");
        sb.AppendLine("- Key Vault with configurable SKU");
        sb.AppendLine("- Soft delete with 90-day retention - FedRAMP CP-9");
        sb.AppendLine("- Purge protection enabled - FedRAMP CP-9");
        sb.AppendLine("- RBAC authorization - FedRAMP AC-3");
        sb.AppendLine("- Network access controls - FedRAMP SC-7");
        sb.AppendLine("- Enabled for disk encryption - FedRAMP SC-28");
        sb.AppendLine("- Enabled for deployment and templates - FedRAMP CM-3");
        
        if (request.Security?.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("- Private endpoint connectivity - FedRAMP SC-7");
        }
        
        if (request.Observability?.EnableDiagnostics == true)
        {
            sb.AppendLine("- Diagnostic settings and audit logging - FedRAMP AU-2");
        }

        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Implementation |");
        sb.AppendLine("|---------|----------------|");
        sb.AppendLine("| SC-12 | Cryptographic key establishment and management |");
        sb.AppendLine("| SC-28 | Encryption at rest (disk encryption enabled) |");
        sb.AppendLine("| AC-3 | RBAC for access control enforcement |");
        sb.AppendLine("| CP-9 | Soft delete and purge protection |");
        sb.AppendLine("| AU-2 | Audit event logging enabled |");
        sb.AppendLine("| AU-11 | 90-day retention for soft deleted items |");
        sb.AppendLine("| SC-7 | Network isolation (private endpoint optional) |");
        sb.AppendLine("| CM-3 | Configuration management via ARM/template deployment |");
        sb.AppendLine();
        sb.AppendLine("## Deployment");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Deploy the Key Vault infrastructure");
        sb.AppendLine("az deployment group create \\");
        sb.AppendLine("  --resource-group <resource-group> \\");
        sb.AppendLine("  --template-file infra/modules/keyvault/main.bicep \\");
        sb.AppendLine($"  --parameters keyVaultName={serviceName}kv objectId=<your-object-id>");
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

        return infrastructure.ComputePlatform == ComputePlatform.Security &&
               infrastructure.Format == InfrastructureFormat.Bicep &&
               infrastructure.Provider == CloudProvider.Azure;
    }
}
