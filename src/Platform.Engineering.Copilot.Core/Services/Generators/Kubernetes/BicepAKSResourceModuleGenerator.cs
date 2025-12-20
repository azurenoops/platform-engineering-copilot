using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Kubernetes;

/// <summary>
/// Bicep module generator for Azure Kubernetes Service (AKS) infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (PE, diagnostics, RBAC) are handled by reusable generators
/// </summary>
public class BicepAKSResourceModuleGenerator : IResourceModuleGenerator
{
    
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.AKS;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "aks", "kubernetes", "k8s", "azure-kubernetes" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by AKS
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.PrivateEndpoint,
        CrossCuttingType.DiagnosticSettings,
        CrossCuttingType.RBACAssignment
    };
    
    /// <summary>
    /// Azure resource type for AKS
    /// </summary>
    public string AzureResourceType => "Microsoft.ContainerService/managedClusters";

    /// <summary>
    /// Generate ONLY the core AKS resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "aks";

        // Generate core AKS module files
        files["aks-cluster.bicep"] = GenerateAKSClusterBicep(request);
        files["main.bicep"] = GenerateCoreMainBicep(request);
        files["README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "aksCluster", // Module name for cross-cutting references
            ResourceType = "Microsoft.ContainerService/managedClusters",
            OutputNames = new List<string>
            {
                "aksClusterId",
                "aksClusterName",
                "aksClusterFqdn",
                "kubeletIdentityId",
                "oidcIssuerUrl",
                "nodeResourceGroup",
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
    /// Check if this generator can handle the request
    /// </summary>
    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        return infrastructure.Format == InfrastructureFormat.Bicep &&
               infrastructure.Provider == CloudProvider.Azure &&
               (infrastructure.ComputePlatform == ComputePlatform.AKS ||
                infrastructure.ComputePlatform == ComputePlatform.Kubernetes);
    }

    /// <summary>
    /// Core main.bicep - only AKS cluster, no cross-cutting modules
    /// Cross-cutting modules are composed by the orchestrator
    /// </summary>
    private string GenerateCoreMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "aks";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();

        var kubernetesVersion = infrastructure.KubernetesVersion ?? "1.30";
        var nodeCount = infrastructure.NodeCount > 0 ? infrastructure.NodeCount : 3;
        var vmSize = infrastructure.VmSize ?? "Standard_D4s_v3";

        sb.AppendLine("// Azure Kubernetes Service Core Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: AC-3 (RBAC), SC-7 (Network Segmentation), SC-28 (Encryption), SI-4 (Defender)");
        sb.AppendLine("// Cross-cutting concerns (PE, diagnostics, RBAC) are composed separately");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("@description('Name of the AKS cluster')");
        sb.AppendLine("param aksClusterName string");
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
        sb.AppendLine("@description('Kubernetes version')");
        sb.AppendLine($"param kubernetesVersion string = '{kubernetesVersion}'");
        sb.AppendLine();
        sb.AppendLine("@description('System node pool size')");
        sb.AppendLine($"param nodeCount int = {nodeCount}");
        sb.AppendLine();
        sb.AppendLine("@description('System node VM size')");
        sb.AppendLine($"param nodeVmSize string = '{vmSize}'");
        sb.AppendLine();
        sb.AppendLine("@description('Subnet ID for AKS nodes')");
        sb.AppendLine("param subnetId string");
        sb.AppendLine();
        sb.AppendLine("@description('Enable Workload Identity')");
        sb.AppendLine($"param enableWorkloadIdentity bool = {security.EnableWorkloadIdentity.ToString().ToLower()}");
        sb.AppendLine();
        sb.AppendLine("@description('Enable private cluster')");
        sb.AppendLine($"param enablePrivateCluster bool = {(security.EnablePrivateCluster == true).ToString().ToLower()}");
        sb.AppendLine();
        sb.AppendLine("@description('Enable Azure Policy')");
        sb.AppendLine($"param enableAzurePolicy bool = {security.EnableAzurePolicy.ToString().ToLower()}");
        sb.AppendLine();
        sb.AppendLine("@description('Enable Microsoft Defender')");
        sb.AppendLine($"param enableDefender bool = {security.EnableDefender.ToString().ToLower()}");
        sb.AppendLine();

        // AKS Cluster Module
        sb.AppendLine("// AKS Cluster Core Resource");
        sb.AppendLine("module aksCluster './aks-cluster.bicep' = {");
        sb.AppendLine("  name: '${aksClusterName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    aksClusterName: aksClusterName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    kubernetesVersion: kubernetesVersion");
        sb.AppendLine("    nodeCount: nodeCount");
        sb.AppendLine("    nodeVmSize: nodeVmSize");
        sb.AppendLine("    subnetId: subnetId");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("    enableWorkloadIdentity: enableWorkloadIdentity");
        sb.AppendLine("    enablePrivateCluster: enablePrivateCluster");
        sb.AppendLine("    enableAzurePolicy: enableAzurePolicy");
        sb.AppendLine("    enableDefender: enableDefender");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// Outputs for cross-cutting module composition");
        sb.AppendLine("output aksClusterId string = aksCluster.outputs.aksClusterId");
        sb.AppendLine("output aksClusterName string = aksCluster.outputs.aksClusterName");
        sb.AppendLine("output aksClusterFqdn string = aksCluster.outputs.aksClusterFqdn");
        sb.AppendLine("output kubeletIdentityId string = aksCluster.outputs.kubeletIdentityId");
        sb.AppendLine("output oidcIssuerUrl string = aksCluster.outputs.oidcIssuerUrl");
        sb.AppendLine("output nodeResourceGroup string = aksCluster.outputs.nodeResourceGroup");
        sb.AppendLine("output resourceId string = aksCluster.outputs.aksClusterId");
        sb.AppendLine("output resourceName string = aksCluster.outputs.aksClusterName");

        return sb.ToString();
    }

    private string GenerateAKSClusterBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();

        sb.AppendLine("// AKS Cluster Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: AC-3 (RBAC), SC-7 (Network), SC-28 (Encryption), SI-4 (Defender)");
        sb.AppendLine();
        sb.AppendLine("param aksClusterName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param kubernetesVersion string");
        sb.AppendLine("param nodeCount int");
        sb.AppendLine("param nodeVmSize string");
        sb.AppendLine("param subnetId string");
        sb.AppendLine("param tags object");
        sb.AppendLine("param enableWorkloadIdentity bool");
        sb.AppendLine("param enablePrivateCluster bool");
        sb.AppendLine("param enableAzurePolicy bool");
        sb.AppendLine("param enableDefender bool");
        sb.AppendLine();

        sb.AppendLine("resource aksCluster 'Microsoft.ContainerService/managedClusters@2024-01-01' = {");
        sb.AppendLine("  name: aksClusterName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  identity: {");
        sb.AppendLine("    type: 'SystemAssigned'");
        sb.AppendLine("  }");
        sb.AppendLine("  sku: {");
        sb.AppendLine("    name: 'Base'");
        sb.AppendLine("    tier: 'Standard'  // FedRAMP production requirement");
        sb.AppendLine("  }");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    kubernetesVersion: kubernetesVersion");
        sb.AppendLine("    dnsPrefix: '${aksClusterName}-dns'");
        sb.AppendLine("    enableRBAC: true  // FedRAMP AC-3");
        sb.AppendLine("    aadProfile: {");
        sb.AppendLine("      managed: true");
        sb.AppendLine("      enableAzureRBAC: true  // FedRAMP AC-3");
        sb.AppendLine("    }");
        sb.AppendLine("    oidcIssuerProfile: {");
        sb.AppendLine("      enabled: enableWorkloadIdentity");
        sb.AppendLine("    }");
        sb.AppendLine("    securityProfile: {");
        sb.AppendLine("      workloadIdentity: {");
        sb.AppendLine("        enabled: enableWorkloadIdentity");
        sb.AppendLine("      }");
        sb.AppendLine("      defender: {");
        sb.AppendLine("        securityMonitoring: {");
        sb.AppendLine("          enabled: enableDefender  // FedRAMP SI-4");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("      imageCleaner: {");
        sb.AppendLine("        enabled: true");
        sb.AppendLine("        intervalHours: 24");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("    networkProfile: {");
        sb.AppendLine("      networkPlugin: 'azure'");
        sb.AppendLine("      networkPolicy: 'azure'  // FedRAMP SC-7");
        sb.AppendLine("      loadBalancerSku: 'standard'");
        sb.AppendLine("      outboundType: 'loadBalancer'");
        sb.AppendLine("      serviceCidr: '10.0.0.0/16'");
        sb.AppendLine("      dnsServiceIP: '10.0.0.10'");
        sb.AppendLine("    }");
        sb.AppendLine("    agentPoolProfiles: [");
        sb.AppendLine("      {");
        sb.AppendLine("        name: 'system'");
        sb.AppendLine("        count: nodeCount");
        sb.AppendLine("        vmSize: nodeVmSize");
        sb.AppendLine("        mode: 'System'");
        sb.AppendLine("        osType: 'Linux'");
        sb.AppendLine("        osSKU: 'AzureLinux'  // FedRAMP compliant OS");
        sb.AppendLine("        vnetSubnetID: subnetId");
        sb.AppendLine("        enableAutoScaling: true");
        sb.AppendLine("        minCount: 1");
        sb.AppendLine("        maxCount: 5");
        sb.AppendLine("        maxPods: 110");
        sb.AppendLine("        enableEncryptionAtHost: true  // FedRAMP SC-28");
        sb.AppendLine("        enableFIPS: true  // FedRAMP SC-13");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("    apiServerAccessProfile: {");
        sb.AppendLine("      enablePrivateCluster: enablePrivateCluster");
        sb.AppendLine("    }");
        sb.AppendLine("    disableLocalAccounts: true  // FedRAMP AC-3");
        sb.AppendLine("    addonProfiles: {");
        sb.AppendLine("      azurepolicy: {");
        sb.AppendLine("        enabled: enableAzurePolicy  // FedRAMP CM-7");
        sb.AppendLine("        config: {");
        sb.AppendLine("          version: 'v2'");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("      azureKeyvaultSecretsProvider: {");
        sb.AppendLine("        enabled: true");
        sb.AppendLine("        config: {");
        sb.AppendLine("          enableSecretRotation: 'true'");
        sb.AppendLine("          rotationPollInterval: '2m'");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("    autoUpgradeProfile: {");
        sb.AppendLine("      upgradeChannel: 'stable'");
        sb.AppendLine("      nodeOSUpgradeChannel: 'SecurityPatch'  // FedRAMP SI-2");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("output aksClusterId string = aksCluster.id");
        sb.AppendLine("output aksClusterName string = aksCluster.name");
        sb.AppendLine("output aksClusterFqdn string = enablePrivateCluster ? aksCluster.properties.privateFQDN : aksCluster.properties.fqdn");
        sb.AppendLine("output kubeletIdentityId string = aksCluster.properties.identityProfile.kubeletidentity.objectId");
        sb.AppendLine("output oidcIssuerUrl string = enableWorkloadIdentity ? aksCluster.properties.oidcIssuerProfile.issuerURL : ''");
        sb.AppendLine("output nodeResourceGroup string = aksCluster.properties.nodeResourceGroup");

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "aks";

        sb.AppendLine($"# Azure Kubernetes Service Module - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## FedRAMP Compliance");
        sb.AppendLine();
        sb.AppendLine("This module implements the following NIST 800-53 controls:");
        sb.AppendLine();
        sb.AppendLine("| Control | Description | Implementation |");
        sb.AppendLine("|---------|-------------|----------------|");
        sb.AppendLine("| AC-3 | Access Enforcement | Azure RBAC, AAD integration, local accounts disabled |");
        sb.AppendLine("| SC-7 | Boundary Protection | Azure CNI, Network Policies |");
        sb.AppendLine("| SC-13 | Cryptographic Protection | FIPS-enabled nodes |");
        sb.AppendLine("| SC-28 | Protection of Information at Rest | Host encryption enabled |");
        sb.AppendLine("| SI-2 | Flaw Remediation | Auto-upgrade, security patches |");
        sb.AppendLine("| SI-4 | Information System Monitoring | Microsoft Defender for Containers |");
        sb.AppendLine("| CM-7 | Least Functionality | Azure Policy addon |");
        sb.AppendLine();
        sb.AppendLine("## Architecture");
        sb.AppendLine();
        sb.AppendLine("This is a **core resource module** that generates only the AKS cluster resource.");
        sb.AppendLine("Cross-cutting concerns are composed separately by the infrastructure orchestrator:");
        sb.AppendLine();
        sb.AppendLine("- **Private Endpoint**: API server network isolation");
        sb.AppendLine("- **Diagnostic Settings**: Control plane logging to Log Analytics");
        sb.AppendLine("- **RBAC**: Role-based access control assignments");
        sb.AppendLine();
        sb.AppendLine("## Parameters");
        sb.AppendLine();
        sb.AppendLine("| Parameter | Type | Default | Description |");
        sb.AppendLine("|-----------|------|---------|-------------|");
        sb.AppendLine("| aksClusterName | string | required | Name of the cluster |");
        sb.AppendLine("| kubernetesVersion | string | 1.30 | Kubernetes version |");
        sb.AppendLine("| nodeCount | int | 3 | System node pool size |");
        sb.AppendLine("| nodeVmSize | string | Standard_D4s_v3 | VM size |");
        sb.AppendLine("| subnetId | string | required | Subnet for nodes |");
        sb.AppendLine("| enableWorkloadIdentity | bool | true | Enable OIDC/Workload Identity |");
        sb.AppendLine("| enablePrivateCluster | bool | false | Enable private API server |");
        sb.AppendLine();
        sb.AppendLine("## Outputs");
        sb.AppendLine();
        sb.AppendLine("| Output | Description |");
        sb.AppendLine("|--------|-------------|");
        sb.AppendLine("| aksClusterId | Resource ID of the cluster |");
        sb.AppendLine("| aksClusterName | Name of the cluster |");
        sb.AppendLine("| aksClusterFqdn | FQDN for kubectl access |");
        sb.AppendLine("| kubeletIdentityId | Kubelet managed identity |");
        sb.AppendLine("| oidcIssuerUrl | OIDC issuer for Workload Identity |");

        return sb.ToString();
    }
}
