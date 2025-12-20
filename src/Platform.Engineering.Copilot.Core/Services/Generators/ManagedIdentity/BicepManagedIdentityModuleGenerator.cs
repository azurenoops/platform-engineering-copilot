using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.ManagedIdentity;

/// <summary>
/// Bicep module generator for Azure User-Assigned Managed Identity infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (RBAC) are handled by reusable generators
/// </summary>
public class BicepManagedIdentityModuleGenerator : IResourceModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.Security;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "managed-identity", "identity", "user-assigned-identity" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by Managed Identity
    /// Managed Identity is typically the principal for RBAC, not a target
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.RBACAssignment
    };
    
    /// <summary>
    /// Azure resource type for User-Assigned Managed Identity
    /// </summary>
    public string AzureResourceType => "Microsoft.ManagedIdentity/userAssignedIdentities";

    /// <summary>
    /// Generate ONLY the core Managed Identity resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "identity";

        // Generate only core Managed Identity module
        files["managed-identity.bicep"] = GenerateManagedIdentityBicep(request);
        files["main.bicep"] = GenerateCoreMainBicep(request);
        files["README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "managedIdentity", // Module name for cross-cutting references
            ResourceType = "Microsoft.ManagedIdentity/userAssignedIdentities",
            OutputNames = new List<string>
            {
                "identityId",
                "identityName",
                "principalId",
                "clientId",
                "tenantId",
                "resourceId",
                "resourceName"
            },
            SupportedCrossCutting = new List<CrossCuttingType>
            {
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
    /// Core main.bicep - only Managed Identity, no cross-cutting modules
    /// </summary>
    private string GenerateCoreMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "identity";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        sb.AppendLine("// Azure User-Assigned Managed Identity Core Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: IA-2 (Identification and Authentication), AC-2 (Account Management)");
        sb.AppendLine("// Cross-cutting concerns (RBAC) are composed separately");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("@description('Name of the Managed Identity')");
        sb.AppendLine("param identityName string");
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
        sb.AppendLine("@description('Enable federated identity credentials for workload identity')");
        sb.AppendLine("param enableFederatedCredentials bool = false");
        sb.AppendLine();
        sb.AppendLine("@description('OIDC issuer URL for federated credentials (e.g., AKS OIDC issuer)')");
        sb.AppendLine("param oidcIssuerUrl string = ''");
        sb.AppendLine();
        sb.AppendLine("@description('Subject identifier for federated credentials')");
        sb.AppendLine("param federatedSubject string = ''");
        sb.AppendLine();

        // Managed Identity Module
        sb.AppendLine("// Managed Identity Core Resource");
        sb.AppendLine("module managedIdentity './managed-identity.bicep' = {");
        sb.AppendLine("  name: '${identityName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    identityName: identityName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("    enableFederatedCredentials: enableFederatedCredentials");
        sb.AppendLine("    oidcIssuerUrl: oidcIssuerUrl");
        sb.AppendLine("    federatedSubject: federatedSubject");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// Outputs for cross-cutting module composition");
        sb.AppendLine("output identityId string = managedIdentity.outputs.identityId");
        sb.AppendLine("output identityName string = managedIdentity.outputs.identityName");
        sb.AppendLine("output principalId string = managedIdentity.outputs.principalId");
        sb.AppendLine("output clientId string = managedIdentity.outputs.clientId");
        sb.AppendLine("output tenantId string = managedIdentity.outputs.tenantId");
        sb.AppendLine("output resourceId string = managedIdentity.outputs.identityId");
        sb.AppendLine("output resourceName string = managedIdentity.outputs.identityName");

        return sb.ToString();
    }

    private string GenerateManagedIdentityBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// User-Assigned Managed Identity Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: IA-2 (Identification and Authentication), AC-2 (Account Management)");
        sb.AppendLine();
        sb.AppendLine("param identityName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param tags object");
        sb.AppendLine("param enableFederatedCredentials bool");
        sb.AppendLine("param oidcIssuerUrl string");
        sb.AppendLine("param federatedSubject string");
        sb.AppendLine();

        sb.AppendLine("resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {");
        sb.AppendLine("  name: identityName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: union(tags, {");
        sb.AppendLine("    'security-control': 'IA-2,AC-2'");
        sb.AppendLine("    'managed-by': 'bicep'");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();

        // Federated Identity Credential
        sb.AppendLine("// Federated Identity Credential (for AKS Workload Identity)");
        sb.AppendLine("resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = if (enableFederatedCredentials && !empty(oidcIssuerUrl)) {");
        sb.AppendLine("  parent: managedIdentity");
        sb.AppendLine("  name: '${identityName}-federated'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    issuer: oidcIssuerUrl");
        sb.AppendLine("    subject: federatedSubject");
        sb.AppendLine("    audiences: [");
        sb.AppendLine("      'api://AzureADTokenExchange'");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output identityId string = managedIdentity.id");
        sb.AppendLine("output identityName string = managedIdentity.name");
        sb.AppendLine("output principalId string = managedIdentity.properties.principalId");
        sb.AppendLine("output clientId string = managedIdentity.properties.clientId");
        sb.AppendLine("output tenantId string = managedIdentity.properties.tenantId");

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "identity";

        sb.AppendLine($"# Azure User-Assigned Managed Identity - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("FedRAMP-compliant Bicep infrastructure for Azure User-Assigned Managed Identity with:");
        sb.AppendLine("- User-assigned managed identity for workload authentication");
        sb.AppendLine("- Optional federated identity credentials for AKS Workload Identity");
        sb.AppendLine("- Azure AD integration for RBAC assignments");

        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Implementation |");
        sb.AppendLine("|---------|----------------|");
        sb.AppendLine("| IA-2 | Identification and authentication via Azure AD |");
        sb.AppendLine("| AC-2 | Account management through managed identity lifecycle |");
        sb.AppendLine();
        sb.AppendLine("## Deployment");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Deploy the Managed Identity infrastructure");
        sb.AppendLine("az deployment group create \\");
        sb.AppendLine("  --resource-group <resource-group> \\");
        sb.AppendLine("  --template-file infra/modules/identity/main.bicep \\");
        sb.AppendLine($"  --parameters identityName={serviceName}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## AKS Workload Identity Integration");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Deploy with federated credentials for AKS");
        sb.AppendLine("az deployment group create \\");
        sb.AppendLine("  --resource-group <resource-group> \\");
        sb.AppendLine("  --template-file infra/modules/identity/main.bicep \\");
        sb.AppendLine($"  --parameters identityName={serviceName} \\");
        sb.AppendLine("    enableFederatedCredentials=true \\");
        sb.AppendLine("    oidcIssuerUrl='https://eastus.oic.prod-aks.azure.com/...' \\");
        sb.AppendLine("    federatedSubject='system:serviceaccount:my-namespace:my-sa'");
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

        // Match managed identity specific requests
        var serviceName = request.ServiceName?.ToLowerInvariant() ?? "";
        return serviceName.Contains("identity") || 
               serviceName.Contains("managed-identity");
    }
}
