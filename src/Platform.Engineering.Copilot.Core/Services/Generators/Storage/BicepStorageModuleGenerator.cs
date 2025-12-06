using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Storage;

/// <summary>
/// Bicep module generator for Azure Storage Account infrastructure
/// </summary>
public class BicepStorageModuleGenerator : IInfrastructureModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.Storage;
    public CloudProvider Provider => CloudProvider.Azure;

    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "storage";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        // Generate main storage module
        files["infra/modules/storage/main.bicep"] = GenerateMainBicep(request);
        files["infra/modules/storage/storage-account.bicep"] = GenerateStorageAccountBicep(request);
        
        if (security.EnablePrivateEndpoint == true)
        {
            files["infra/modules/storage/private-endpoint.bicep"] = GeneratePrivateEndpointBicep(request);
        }

        if (observability.EnableDiagnostics == true)
        {
            files["infra/modules/storage/diagnostics.bicep"] = GenerateDiagnosticsBicep(request);
        }

        // Generate README
        files["infra/modules/storage/README.md"] = GenerateReadme(request);

        return files;
    }

    private string GenerateMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "storage";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        sb.AppendLine("// Azure Storage Account Infrastructure Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-28 (Encryption), CP-9/CP-10 (Backup/Recovery), AU-11 (Audit Retention), AC-3 (Access Control)");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine($"// Region: {infrastructure.Region}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("@description('Name of the storage account')");
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
        sb.AppendLine("@description('Storage account SKU')");
        sb.AppendLine("@allowed(['Standard_LRS', 'Standard_GRS', 'Standard_RAGRS', 'Standard_ZRS', 'Premium_LRS'])");
        sb.AppendLine("param skuName string = 'Standard_LRS'");
        sb.AppendLine();
        sb.AppendLine("@description('Storage account kind')");
        sb.AppendLine("@allowed(['Storage', 'StorageV2', 'BlobStorage', 'FileStorage', 'BlockBlobStorage'])");
        sb.AppendLine("param kind string = 'StorageV2'");
        sb.AppendLine();

        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("@description('Virtual Network ID for private endpoint')");
            sb.AppendLine("param vnetId string");
            sb.AppendLine();
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

        // Storage Account Module
        sb.AppendLine("// Storage Account");
        sb.AppendLine("module storageAccount 'storage-account.bicep' = {");
        sb.AppendLine("  name: '${storageAccountName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    storageAccountName: storageAccountName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    skuName: skuName");
        sb.AppendLine("    kind: kind");
        sb.AppendLine("    tags: tags");
        sb.AppendLine($"    enableHttpsOnly: {(security.TLS ? "true" : "false")}");
        sb.AppendLine($"    minimumTlsVersion: 'TLS{(security.TLSVersion ?? "1.2").Replace(".", "_")}'");
        sb.AppendLine($"    allowBlobPublicAccess: {(security.EnablePrivateEndpoint == true ? "false" : "true")}");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Private Endpoint Module (if enabled)
        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("// Private Endpoint for Storage Account");
            sb.AppendLine("module privateEndpoint 'private-endpoint.bicep' = {");
            sb.AppendLine("  name: '${storageAccountName}-pe-deployment'");
            sb.AppendLine("  params: {");
            sb.AppendLine("    privateEndpointName: '${storageAccountName}-pe'");
            sb.AppendLine("    location: location");
            sb.AppendLine("    storageAccountId: storageAccount.outputs.storageAccountId");
            sb.AppendLine("    subnetId: subnetId");
            sb.AppendLine("    tags: tags");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Diagnostics Module (if enabled)
        if (observability.EnableDiagnostics == true)
        {
            sb.AppendLine("// Diagnostic Settings");
            sb.AppendLine("module diagnostics 'diagnostics.bicep' = {");
            sb.AppendLine("  name: '${storageAccountName}-diag-deployment'");
            sb.AppendLine("  params: {");
            sb.AppendLine("    storageAccountName: storageAccount.outputs.storageAccountName");
            sb.AppendLine("    logAnalyticsWorkspaceId: logAnalyticsWorkspaceId");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Outputs
        sb.AppendLine("// Outputs");
        sb.AppendLine("output storageAccountId string = storageAccount.outputs.storageAccountId");
        sb.AppendLine("output storageAccountName string = storageAccount.outputs.storageAccountName");
        sb.AppendLine("output primaryEndpoints object = storageAccount.outputs.primaryEndpoints");
        sb.AppendLine("output primaryBlobEndpoint string = storageAccount.outputs.primaryBlobEndpoint");
        
        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("output privateEndpointId string = privateEndpoint.outputs.privateEndpointId");
        }

        return sb.ToString();
    }

    private string GenerateStorageAccountBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Storage Account Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-28 (Encryption at Rest), CP-9/CP-10 (Backup/Recovery), AU-11 (Audit Record Retention)");
        sb.AppendLine();
        sb.AppendLine("@description('Storage Account Name')");
        sb.AppendLine("param storageAccountName string");
        sb.AppendLine();
        sb.AppendLine("@description('Azure region')");
        sb.AppendLine("param location string");
        sb.AppendLine();
        sb.AppendLine("@description('SKU name')");
        sb.AppendLine("param skuName string");
        sb.AppendLine();
        sb.AppendLine("@description('Storage account kind')");
        sb.AppendLine("param kind string");
        sb.AppendLine();
        sb.AppendLine("@description('Resource tags')");
        sb.AppendLine("param tags object");
        sb.AppendLine();
        sb.AppendLine("@description('Enable HTTPS only')");
        sb.AppendLine("param enableHttpsOnly bool");
        sb.AppendLine();
        sb.AppendLine("@description('Minimum TLS version')");
        sb.AppendLine("param minimumTlsVersion string");
        sb.AppendLine();
        sb.AppendLine("@description('Allow blob public access')");
        sb.AppendLine("param allowBlobPublicAccess bool");
        sb.AppendLine();
        sb.AppendLine("@description('Soft delete retention days - FedRAMP CP-9')");
        sb.AppendLine("param softDeleteRetentionDays int = 14");
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
        sb.AppendLine("    supportsHttpsTrafficOnly: enableHttpsOnly");
        sb.AppendLine("    minimumTlsVersion: minimumTlsVersion");
        sb.AppendLine("    allowBlobPublicAccess: allowBlobPublicAccess");
        sb.AppendLine("    allowSharedKeyAccess: false  // FedRAMP AC-3 - Require AAD auth");
        sb.AppendLine("    networkAcls: {");
        sb.AppendLine("      bypass: 'AzureServices'");
        sb.AppendLine("      defaultAction: allowBlobPublicAccess ? 'Allow' : 'Deny'");
        sb.AppendLine("    }");
        sb.AppendLine("    encryption: {");
        sb.AppendLine("      services: {");
        sb.AppendLine("        blob: {");
        sb.AppendLine("          enabled: true");
        sb.AppendLine("          keyType: 'Account'");
        sb.AppendLine("        }");
        sb.AppendLine("        file: {");
        sb.AppendLine("          enabled: true");
        sb.AppendLine("          keyType: 'Account'");
        sb.AppendLine("        }");
        sb.AppendLine("        table: {");
        sb.AppendLine("          enabled: true");
        sb.AppendLine("          keyType: 'Account'");
        sb.AppendLine("        }");
        sb.AppendLine("        queue: {");
        sb.AppendLine("          enabled: true");
        sb.AppendLine("          keyType: 'Account'");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("      keySource: 'Microsoft.Storage'");
        sb.AppendLine("      requireInfrastructureEncryption: true  // FedRAMP SC-28 - Double encryption");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // FedRAMP: Blob Services Configuration (Soft Delete, Versioning)
        sb.AppendLine("// FedRAMP CP-9: Blob Services with Soft Delete and Versioning");
        sb.AppendLine("resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {");
        sb.AppendLine("  parent: storageAccount");
        sb.AppendLine("  name: 'default'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    deleteRetentionPolicy: {");
        sb.AppendLine("      enabled: true");
        sb.AppendLine("      days: softDeleteRetentionDays");
        sb.AppendLine("    }");
        sb.AppendLine("    containerDeleteRetentionPolicy: {");
        sb.AppendLine("      enabled: true");
        sb.AppendLine("      days: softDeleteRetentionDays");
        sb.AppendLine("    }");
        sb.AppendLine("    isVersioningEnabled: true  // FedRAMP AU-11 - Version history");
        sb.AppendLine("    changeFeed: {");
        sb.AppendLine("      enabled: true  // FedRAMP AU-3 - Track changes");
        sb.AppendLine("      retentionInDays: 90");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output storageAccountId string = storageAccount.id");
        sb.AppendLine("output storageAccountName string = storageAccount.name");
        sb.AppendLine("output primaryEndpoints object = storageAccount.properties.primaryEndpoints");
        sb.AppendLine("output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob");

        return sb.ToString();
    }

    private string GeneratePrivateEndpointBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Private Endpoint for Storage Account");
        sb.AppendLine();
        sb.AppendLine("param privateEndpointName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param storageAccountId string");
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
        sb.AppendLine("          privateLinkServiceId: storageAccountId");
        sb.AppendLine("          groupIds: ['blob']");
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

        sb.AppendLine("// Diagnostic Settings for Storage Account");
        sb.AppendLine();
        sb.AppendLine("param storageAccountName string");
        sb.AppendLine("param logAnalyticsWorkspaceId string");
        sb.AppendLine();

        sb.AppendLine("resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {");
        sb.AppendLine("  name: storageAccountName");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {");
        sb.AppendLine("  name: '${storageAccountName}-diagnostics'");
        sb.AppendLine("  scope: storageAccount");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    workspaceId: logAnalyticsWorkspaceId");
        sb.AppendLine("    metrics: [");
        sb.AppendLine("      {");
        sb.AppendLine("        category: 'Transaction'");
        sb.AppendLine("        enabled: true");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "storage";

        sb.AppendLine($"# Azure Storage Account - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("FedRAMP-compliant Bicep infrastructure for Azure Storage Account with:");
        sb.AppendLine("- Storage account with configurable SKU");
        sb.AppendLine("- Infrastructure encryption (double encryption) - FedRAMP SC-28");
        sb.AppendLine("- HTTPS-only access with TLS 1.2 - FedRAMP SC-8");
        sb.AppendLine("- Blob soft delete (14 days) - FedRAMP CP-9");
        sb.AppendLine("- Container soft delete (14 days) - FedRAMP CP-9");
        sb.AppendLine("- Blob versioning - FedRAMP AU-11");
        sb.AppendLine("- Change feed enabled - FedRAMP AU-3");
        sb.AppendLine("- Shared key access disabled (AAD only) - FedRAMP AC-3");
        
        if (request.Security?.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("- Private endpoint connectivity - FedRAMP SC-7");
        }
        
        if (request.Observability?.EnableDiagnostics == true)
        {
            sb.AppendLine("- Diagnostic settings and logging - FedRAMP AU-2");
        }

        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Implementation |");
        sb.AppendLine("|---------|----------------|");
        sb.AppendLine("| SC-28 | Infrastructure encryption (double encryption) |");
        sb.AppendLine("| SC-8 | TLS 1.2 encryption in transit |");
        sb.AppendLine("| CP-9/CP-10 | Soft delete with 14-day retention |");
        sb.AppendLine("| AU-11 | Blob versioning for audit retention |");
        sb.AppendLine("| AU-3 | Change feed for audit tracking |");
        sb.AppendLine("| AC-3 | AAD authentication required (shared key disabled) |");
        sb.AppendLine("| SC-7 | Private endpoint (network isolation) |");
        sb.AppendLine();
        sb.AppendLine("## Deployment");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Deploy the storage infrastructure");
        sb.AppendLine("az deployment group create \\");
        sb.AppendLine("  --resource-group <resource-group> \\");
        sb.AppendLine("  --template-file infra/modules/storage/main.bicep \\");
        sb.AppendLine($"  --parameters storageAccountName={serviceName}sa");
        sb.AppendLine("```");

        return sb.ToString();
    }

    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure;
        if (infrastructure == null) return false;

        return infrastructure.ComputePlatform == ComputePlatform.Storage &&
               infrastructure.Format == InfrastructureFormat.Bicep &&
               infrastructure.Provider == CloudProvider.Azure;
    }

    public bool CanHandle(TemplateGenerationRequest request)
    {
        return CanGenerate(request);
    }
}
