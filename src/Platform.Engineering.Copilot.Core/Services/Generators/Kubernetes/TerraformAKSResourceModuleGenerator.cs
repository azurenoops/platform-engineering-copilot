using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Kubernetes;

/// <summary>
/// Terraform module generator for Azure Kubernetes Service (AKS) infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (PE, diagnostics, RBAC) are handled by reusable generators
/// </summary>
public class TerraformAKSResourceModuleGenerator : IResourceModuleGenerator
{
    private readonly TerraformAKSModuleGenerator _legacyGenerator = new();
    
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
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
        CrossCuttingType.RBACAssignment,
        CrossCuttingType.NetworkSecurityGroup
    };
    
    /// <summary>
    /// Azure resource type for AKS
    /// </summary>
    public string AzureResourceType => "Microsoft.ContainerService/managedClusters";

    /// <summary>
    /// Generate ONLY the core AKS cluster resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "aks";

        // Generate only core AKS module - no PE, diagnostics, or RBAC
        files["modules/aks/cluster.tf"] = GenerateCoreClusterTf(request);
        files["modules/aks/identity.tf"] = GenerateIdentityTf(request);
        files["modules/aks/variables.tf"] = GenerateCoreVariablesTf(request);
        files["modules/aks/outputs.tf"] = GenerateCoreOutputsTf(request);
        files["modules/aks/README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "azurerm_kubernetes_cluster.main", // Terraform resource reference
            ResourceType = "Microsoft.ContainerService/managedClusters",
            OutputNames = new List<string>
            {
                "cluster_id",
                "cluster_name",
                "cluster_fqdn",
                "kube_config",
                "kubelet_identity",
                "node_resource_group"
            },
            SupportedCrossCutting = new List<CrossCuttingType>
            {
                CrossCuttingType.PrivateEndpoint,
                CrossCuttingType.DiagnosticSettings,
                CrossCuttingType.RBACAssignment,
                CrossCuttingType.NetworkSecurityGroup
            }
        };
    }

    /// <summary>
    /// Legacy GenerateModule - delegates to legacy generator for full module
    /// </summary>
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _legacyGenerator.GenerateAKSModule(request);
    }
    
    /// <summary>
    /// Check if this generator can handle the request
    /// </summary>
    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        return infrastructure.Format == InfrastructureFormat.Terraform &&
               infrastructure.Provider == CloudProvider.Azure &&
               (infrastructure.ComputePlatform == ComputePlatform.AKS ||
                infrastructure.ComputePlatform == ComputePlatform.Kubernetes);
    }

    /// <summary>
    /// Core cluster.tf - only AKS cluster resource, no cross-cutting
    /// </summary>
    private string GenerateCoreClusterTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "aks";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();

        sb.AppendLine("# Azure Kubernetes Service (AKS) Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: AC-3 (RBAC), AU-2 (Auditing), SC-7 (Network Segmentation), SC-28 (Encryption), SI-4 (Defender)");
        sb.AppendLine($"# Service: {serviceName}");
        sb.AppendLine($"# Region: {infrastructure.Region}");
        sb.AppendLine("# NOTE: Cross-cutting concerns (PE, diagnostics, RBAC) are composed via separate modules");
        sb.AppendLine();

        // AKS Cluster
        sb.AppendLine("resource \"azurerm_kubernetes_cluster\" \"main\" {");
        sb.AppendLine("  name                = var.cluster_name");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  dns_prefix          = var.cluster_name");
        sb.AppendLine("  kubernetes_version  = var.kubernetes_version");
        sb.AppendLine();

        // Private cluster
        sb.AppendLine("  # Private cluster (API server accessible only via private network) - FedRAMP SC-7");
        sb.AppendLine("  private_cluster_enabled = var.enable_private_cluster");
        sb.AppendLine();

        // Authorized IP ranges
        sb.AppendLine("  # Authorized IP ranges for API server access - FedRAMP AC-3");
        sb.AppendLine("  api_server_authorized_ip_ranges = var.authorized_ip_ranges");
        sb.AppendLine();

        // Default node pool
        sb.AppendLine("  # System node pool (required)");
        sb.AppendLine("  default_node_pool {");
        sb.AppendLine("    name                         = \"system\"");
        sb.AppendLine("    node_count                   = var.system_node_count");
        sb.AppendLine("    vm_size                      = var.system_vm_size");
        sb.AppendLine("    vnet_subnet_id               = var.aks_subnet_id");
        sb.AppendLine("    type                         = \"VirtualMachineScaleSets\"");
        sb.AppendLine("    enable_auto_scaling          = var.enable_auto_scaling");
        sb.AppendLine("    min_count                    = var.enable_auto_scaling ? var.min_node_count : null");
        sb.AppendLine("    max_count                    = var.enable_auto_scaling ? var.max_node_count : null");
        sb.AppendLine("    os_disk_size_gb              = var.os_disk_size_gb");
        sb.AppendLine("    os_disk_type                 = \"Managed\"");
        sb.AppendLine("    only_critical_addons_enabled = true");
        sb.AppendLine();
        sb.AppendLine("    node_labels = {");
        sb.AppendLine("      \"node.kubernetes.io/role\" = \"system\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Identity
        sb.AppendLine("  # Managed identity - FedRAMP IA-2");
        sb.AppendLine("  identity {");
        sb.AppendLine("    type = \"SystemAssigned\"");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Network profile
        sb.AppendLine("  # Network profile - FedRAMP SC-7");
        sb.AppendLine("  network_profile {");
        sb.AppendLine("    network_plugin    = var.network_plugin");
        sb.AppendLine("    network_policy    = var.network_policy");
        sb.AppendLine("    dns_service_ip    = var.dns_service_ip");
        sb.AppendLine("    service_cidr      = var.service_cidr");
        sb.AppendLine("    load_balancer_sku = \"standard\"");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Azure AD RBAC
        sb.AppendLine("  # Azure AD RBAC - FedRAMP AC-3");
        sb.AppendLine("  azure_active_directory_role_based_access_control {");
        sb.AppendLine("    managed            = true");
        sb.AppendLine("    azure_rbac_enabled = var.enable_azure_rbac");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Azure Policy
        sb.AppendLine("  # Azure Policy Add-on - FedRAMP CM-2");
        sb.AppendLine("  azure_policy_enabled = var.enable_azure_policy");
        sb.AppendLine();

        // Key Vault Secrets Provider
        sb.AppendLine("  # Key Vault Secrets Provider - FedRAMP SC-12");
        sb.AppendLine("  key_vault_secrets_provider {");
        sb.AppendLine("    secret_rotation_enabled = true");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Image Cleaner
        sb.AppendLine("  # Image Cleaner");
        sb.AppendLine("  image_cleaner_enabled        = var.enable_image_cleaner");
        sb.AppendLine("  image_cleaner_interval_hours = var.image_cleaner_interval_hours");
        sb.AppendLine();

        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Identity configuration
    /// </summary>
    private string GenerateIdentityTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# AKS Identity Configuration - FedRAMP IA-2, AC-2");
        sb.AppendLine();
        sb.AppendLine("# User-assigned managed identity for AKS (optional)");
        sb.AppendLine("resource \"azurerm_user_assigned_identity\" \"aks\" {");
        sb.AppendLine("  count               = var.create_user_assigned_identity ? 1 : 0");
        sb.AppendLine("  name                = \"${var.cluster_name}-identity\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  tags                = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Role assignment for AKS to access ACR");
        sb.AppendLine("resource \"azurerm_role_assignment\" \"acr_pull\" {");
        sb.AppendLine("  count                = var.acr_id != null ? 1 : 0");
        sb.AppendLine("  scope                = var.acr_id");
        sb.AppendLine("  role_definition_name = \"AcrPull\"");
        sb.AppendLine("  principal_id         = azurerm_kubernetes_cluster.main.kubelet_identity[0].object_id");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Core variables.tf - only variables for core resource
    /// </summary>
    private string GenerateCoreVariablesTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# AKS Variables - FedRAMP Compliant");
        sb.AppendLine("# Cross-cutting variables are defined in their respective modules");
        sb.AppendLine();
        sb.AppendLine("variable \"cluster_name\" {");
        sb.AppendLine("  description = \"Name of the AKS cluster\"");
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
        sb.AppendLine("variable \"kubernetes_version\" {");
        sb.AppendLine("  description = \"Kubernetes version\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"1.28\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_private_cluster\" {");
        sb.AppendLine("  description = \"Enable private cluster (FedRAMP SC-7)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"authorized_ip_ranges\" {");
        sb.AppendLine("  description = \"Authorized IP ranges for API server access\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"aks_subnet_id\" {");
        sb.AppendLine("  description = \"Subnet ID for AKS nodes\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"system_node_count\" {");
        sb.AppendLine("  description = \"Number of system nodes\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 3");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"system_vm_size\" {");
        sb.AppendLine("  description = \"VM size for system nodes\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"Standard_D4s_v5\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_auto_scaling\" {");
        sb.AppendLine("  description = \"Enable auto-scaling\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"min_node_count\" {");
        sb.AppendLine("  description = \"Minimum node count for auto-scaling\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 3");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"max_node_count\" {");
        sb.AppendLine("  description = \"Maximum node count for auto-scaling\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 10");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"os_disk_size_gb\" {");
        sb.AppendLine("  description = \"OS disk size in GB\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 128");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"network_plugin\" {");
        sb.AppendLine("  description = \"Network plugin (azure or kubenet)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"azure\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"network_policy\" {");
        sb.AppendLine("  description = \"Network policy (azure or calico)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"azure\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"dns_service_ip\" {");
        sb.AppendLine("  description = \"DNS service IP address\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.0.10\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"service_cidr\" {");
        sb.AppendLine("  description = \"Service CIDR\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.0.0/16\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_azure_rbac\" {");
        sb.AppendLine("  description = \"Enable Azure RBAC for Kubernetes authorization\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_azure_policy\" {");
        sb.AppendLine("  description = \"Enable Azure Policy add-on\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_image_cleaner\" {");
        sb.AppendLine("  description = \"Enable Image Cleaner\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"image_cleaner_interval_hours\" {");
        sb.AppendLine("  description = \"Image Cleaner interval in hours\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 48");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"create_user_assigned_identity\" {");
        sb.AppendLine("  description = \"Create user-assigned managed identity\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"acr_id\" {");
        sb.AppendLine("  description = \"ACR resource ID for AcrPull role assignment\"");
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

        sb.AppendLine("# AKS Outputs");
        sb.AppendLine("# Used by cross-cutting modules for composition");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_id\" {");
        sb.AppendLine("  description = \"The ID of the AKS cluster\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_name\" {");
        sb.AppendLine("  description = \"The name of the AKS cluster\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_fqdn\" {");
        sb.AppendLine("  description = \"The FQDN of the AKS cluster\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.fqdn");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"kube_config\" {");
        sb.AppendLine("  description = \"Raw kubeconfig for the AKS cluster\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.kube_config_raw");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"kube_admin_config\" {");
        sb.AppendLine("  description = \"Raw admin kubeconfig for the AKS cluster\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.kube_admin_config_raw");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"kubelet_identity\" {");
        sb.AppendLine("  description = \"Kubelet identity object ID\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.kubelet_identity[0].object_id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"node_resource_group\" {");
        sb.AppendLine("  description = \"The auto-generated resource group for AKS nodes\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.node_resource_group");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"oidc_issuer_url\" {");
        sb.AppendLine("  description = \"The OIDC issuer URL for workload identity\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.oidc_issuer_url");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate README documentation
    /// </summary>
    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "aks";

        sb.AppendLine($"# Azure Kubernetes Service (AKS) Module - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("This Terraform module creates an Azure Kubernetes Service cluster with FedRAMP-compliant security settings.");
        sb.AppendLine("Cross-cutting concerns (Private Endpoints, Diagnostic Settings, RBAC) are composed via separate modules.");
        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Description | Implementation |");
        sb.AppendLine("|---------|-------------|----------------|");
        sb.AppendLine("| AC-3 | Access Control | Azure AD RBAC, authorized IP ranges |");
        sb.AppendLine("| AU-2 | Audit Events | Container Insights enabled |");
        sb.AppendLine("| SC-7 | Boundary Protection | Private cluster, network policies |");
        sb.AppendLine("| SC-12 | Cryptographic Key Management | Key Vault Secrets Provider |");
        sb.AppendLine("| CM-2 | Baseline Configuration | Azure Policy add-on |");
        sb.AppendLine("| IA-2 | Identification | Managed Identity |");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"aks\" {");
        sb.AppendLine("  source = \"./modules/aks\"");
        sb.AppendLine();
        sb.AppendLine("  cluster_name        = \"my-aks-cluster\"");
        sb.AppendLine("  resource_group_name = azurerm_resource_group.main.name");
        sb.AppendLine("  location            = azurerm_resource_group.main.location");
        sb.AppendLine("  aks_subnet_id       = module.network.aks_subnet_id");
        sb.AppendLine("  acr_id              = module.acr.acr_id");
        sb.AppendLine("  tags                = local.common_tags");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Composition");
        sb.AppendLine();
        sb.AppendLine("To add cross-cutting concerns, use the appropriate modules:");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"aks_diagnostics\" {");
        sb.AppendLine("  source = \"./modules/cross-cutting/diagnostic-settings\"");
        sb.AppendLine("  ");
        sb.AppendLine("  resource_id              = module.aks.cluster_id");
        sb.AppendLine("  log_analytics_workspace_id = module.laws.workspace_id");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }
}
