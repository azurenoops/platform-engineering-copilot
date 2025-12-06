using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.KeyVault;

/// <summary>
/// Bicep module generator for Azure Key Vault infrastructure
/// </summary>
public class BicepKeyVaultModuleGenerator : IInfrastructureModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.Security;
    public CloudProvider Provider => CloudProvider.Azure;

    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "keyvault";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        // Generate main Key Vault module
        files["infra/modules/keyvault/main.bicep"] = GenerateMainBicep(request);
        files["infra/modules/keyvault/key-vault.bicep"] = GenerateKeyVaultBicep(request);
        
        if (security.EnablePrivateEndpoint == true)
        {
            files["infra/modules/keyvault/private-endpoint.bicep"] = GeneratePrivateEndpointBicep(request);
        }

        if (observability.EnableDiagnostics == true)
        {
            files["infra/modules/keyvault/diagnostics.bicep"] = GenerateDiagnosticsBicep(request);
        }

        if (security.RBAC)
        {
            files["infra/modules/keyvault/rbac.bicep"] = GenerateRBACBicep(request);
        }

        // Generate README
        files["infra/modules/keyvault/README.md"] = GenerateReadme(request);

        return files;
    }

    private string GenerateMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "keyvault";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        sb.AppendLine("// Azure Key Vault Infrastructure Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-12 (Cryptographic Key Management), SC-28 (Encryption at Rest), AU-2 (Audit Events)");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine($"// Region: {infrastructure.Region}");
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
        sb.AppendLine("@description('Object ID of the user/service principal to grant access')");
        sb.AppendLine("param objectId string");
        sb.AppendLine();
        sb.AppendLine("@description('Key Vault SKU')");
        sb.AppendLine("@allowed(['standard', 'premium'])");
        sb.AppendLine("param skuName string = 'standard'");
        sb.AppendLine();

        if (security.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("@description('Subnet ID for private endpoint')");
            sb.AppendLine("param subnetId string");
            sb.AppendLine();
        }

        if (observability.EnableDiagnostics == true)
        {
            sb.AppendLine("@description('Log Analytics Workspace ID')");
            sb.AppendLine("param logAnalyticsWorkspaceId string");
            sb.AppendLine();
        }

        // Key Vault Module - FedRAMP Compliant
        sb.AppendLine("// Key Vault");
        sb.AppendLine("module keyVault 'key-vault.bicep' = {");
        sb.AppendLine("  name: '${keyVaultName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    keyVaultName: keyVaultName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    tenantId: tenantId");
        sb.AppendLine("    skuName: skuName");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("    enableRbacAuthorization: true  // FedRAMP AC-3 - RBAC for access control");
        sb.AppendLine("    enableSoftDelete: true  // FedRAMP CP-9 - Recovery capability");
        sb.AppendLine("    softDeleteRetentionInDays: 90  // FedRAMP AU-11 - Audit retention");
        sb.AppendLine("    enablePurgeProtection: true  // FedRAMP CP-9 - Prevent permanent deletion");
        sb.AppendLine($"    publicNetworkAccess: '{(security.EnablePrivateEndpoint == true ? "Disabled" : "Enabled")}'");
        sb.AppendLine("    enabledForDeployment: true  // FedRAMP CM-3 - Configuration management");
        sb.AppendLine("    enabledForDiskEncryption: true  // FedRAMP SC-28 - Encryption at rest");
        sb.AppendLine("    enabledForTemplateDeployment: true  // FedRAMP CM-3 - Configuration management");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // RBAC Module (if enabled)
        if (security.RBAC)
        {
            sb.AppendLine("// RBAC Role Assignment");
            sb.AppendLine("module rbac 'rbac.bicep' = {");
            sb.AppendLine("  name: '${keyVaultName}-rbac-deployment'");
            sb.AppendLine("  params: {");
            sb.AppendLine("    keyVaultName: keyVault.outputs.keyVaultName");
            sb.AppendLine("    principalId: objectId");
            sb.AppendLine("  }");
            sb.AppendLine("  dependsOn: [");
            sb.AppendLine("    keyVault");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Private Endpoint Module (if enabled)
        if (security.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("// Private Endpoint for Key Vault");
            sb.AppendLine("module privateEndpoint 'private-endpoint.bicep' = {");
            sb.AppendLine("  name: '${keyVaultName}-pe-deployment'");
            sb.AppendLine("  params: {");
            sb.AppendLine("    privateEndpointName: '${keyVaultName}-pe'");
            sb.AppendLine("    location: location");
            sb.AppendLine("    keyVaultId: keyVault.outputs.keyVaultId");
            sb.AppendLine("    subnetId: subnetId");
            sb.AppendLine("    tags: tags");
            sb.AppendLine("  }");
            sb.AppendLine("  dependsOn: [");
            sb.AppendLine("    keyVault");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Diagnostics Module (if enabled)
        if (observability.EnableDiagnostics == true)
        {
            sb.AppendLine("// Diagnostic Settings");
            sb.AppendLine("module diagnostics 'diagnostics.bicep' = {");
            sb.AppendLine("  name: '${keyVaultName}-diag-deployment'");
            sb.AppendLine("  params: {");
            sb.AppendLine("    keyVaultName: keyVault.outputs.keyVaultName");
            sb.AppendLine("    logAnalyticsWorkspaceId: logAnalyticsWorkspaceId");
            sb.AppendLine("  }");
            sb.AppendLine("  dependsOn: [");
            sb.AppendLine("    keyVault");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Outputs
        sb.AppendLine("// Outputs");
        sb.AppendLine("output keyVaultId string = keyVault.outputs.keyVaultId");
        sb.AppendLine("output keyVaultName string = keyVault.outputs.keyVaultName");
        sb.AppendLine("output keyVaultUri string = keyVault.outputs.keyVaultUri");
        
        if (security.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("output privateEndpointId string = privateEndpoint.outputs.privateEndpointId");
        }

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

    private string GeneratePrivateEndpointBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Private Endpoint for Key Vault");
        sb.AppendLine();
        sb.AppendLine("param privateEndpointName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param keyVaultId string");
        sb.AppendLine("param subnetId string");
        sb.AppendLine("param tags object");
        sb.AppendLine();

        sb.AppendLine("resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-04-01' = {");
        sb.AppendLine("  name: privateEndpointName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    subnet: {");
        sb.AppendLine("      id: subnetId");
        sb.AppendLine("    }");
        sb.AppendLine("    privateLinkServiceConnections: [");
        sb.AppendLine("      {");
        sb.AppendLine("        name: '${privateEndpointName}-connection'");
        sb.AppendLine("        properties: {");
        sb.AppendLine("          privateLinkServiceId: keyVaultId");
        sb.AppendLine("          groupIds: ['vault']");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output privateEndpointId string = privateEndpoint.id");

        return sb.ToString();
    }

    private string GenerateDiagnosticsBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Diagnostic Settings for Key Vault");
        sb.AppendLine();
        sb.AppendLine("param keyVaultName string");
        sb.AppendLine("param logAnalyticsWorkspaceId string");
        sb.AppendLine();

        sb.AppendLine("resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' existing = {");
        sb.AppendLine("  name: keyVaultName");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {");
        sb.AppendLine("  name: '${keyVaultName}-diagnostics'");
        sb.AppendLine("  scope: keyVault");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    workspaceId: logAnalyticsWorkspaceId");
        sb.AppendLine("    logs: [");
        sb.AppendLine("      {");
        sb.AppendLine("        category: 'AuditEvent'");
        sb.AppendLine("        enabled: true");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("    metrics: [");
        sb.AppendLine("      {");
        sb.AppendLine("        category: 'AllMetrics'");
        sb.AppendLine("        enabled: true");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateRBACBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// RBAC Role Assignment for Key Vault");
        sb.AppendLine();
        sb.AppendLine("param keyVaultName string");
        sb.AppendLine("param principalId string");
        sb.AppendLine();

        sb.AppendLine("resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' existing = {");
        sb.AppendLine("  name: keyVaultName");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("// Key Vault Administrator role");
        sb.AppendLine("var keyVaultAdministratorRoleId = '00482a5a-887f-4fb3-b363-3b7fe8e74483'");
        sb.AppendLine();

        sb.AppendLine("resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {");
        sb.AppendLine("  name: guid(keyVault.id, principalId, keyVaultAdministratorRoleId)");
        sb.AppendLine("  scope: keyVault");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultAdministratorRoleId)");
        sb.AppendLine("    principalId: principalId");
        sb.AppendLine("    principalType: 'ServicePrincipal'");
        sb.AppendLine("  }");
        sb.AppendLine("}");

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

    public bool CanHandle(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure;
        if (infrastructure == null) return false;

        return infrastructure.ComputePlatform == ComputePlatform.Security &&
               infrastructure.Format == InfrastructureFormat.Bicep &&
               infrastructure.Provider == CloudProvider.Azure;
    }
}
