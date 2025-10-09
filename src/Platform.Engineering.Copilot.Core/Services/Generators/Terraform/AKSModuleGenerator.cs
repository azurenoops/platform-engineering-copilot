using System.Text;
using System.Linq;
using System.Collections.Generic;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

/// <summary>
/// Generates complete Terraform module for Azure AKS (Azure Kubernetes Service)
/// Follows the same modular pattern as EKS and GKE generators
/// </summary>
public class AKSModuleGenerator
{
    public Dictionary<string, string> GenerateAKSModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var serviceName = request.ServiceName ?? "aks-service";
        var deployment = request.Deployment ?? new DeploymentSpec();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var app = request.Application ?? new ApplicationSpec();
        
        // Generate all AKS module files
        files["infra/terraform/modules/aks/cluster.tf"] = GenerateClusterConfig(request);
        files["infra/terraform/modules/aks/node_pools.tf"] = GenerateNodePoolsConfig(request);
        files["infra/terraform/modules/aks/identity.tf"] = GenerateIdentityConfig(request);
        files["infra/terraform/modules/aks/network.tf"] = GenerateNetworkConfig(request);
        files["infra/terraform/modules/aks/security.tf"] = GenerateSecurityConfig(request);
        files["infra/terraform/modules/aks/acr.tf"] = GenerateACRConfig(request);
        files["infra/terraform/modules/aks/monitoring.tf"] = GenerateMonitoringConfig(request);
        files["infra/terraform/modules/aks/variables.tf"] = GenerateVariablesConfig(request);
        files["infra/terraform/modules/aks/outputs.tf"] = GenerateOutputsConfig(request);
        
        return files;
    }
    
    private string GenerateClusterConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "aks-service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();
        
        var region = infrastructure.Region ?? "eastus";
        
        sb.AppendLine("# AKS Cluster Configuration");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_kubernetes_cluster\" \"main\" {");
        sb.AppendLine("  name                = var.cluster_name");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  dns_prefix          = var.cluster_name");
        sb.AppendLine("  kubernetes_version  = var.kubernetes_version");
        sb.AppendLine();
        
        // Private Cluster
        sb.AppendLine("  # Private cluster (API server accessible only via private network)");
        sb.AppendLine("  private_cluster_enabled = var.enable_private_cluster");
        sb.AppendLine();
        
        // Authorized IP Ranges
        sb.AppendLine("  # Authorized IP ranges for API server access");
        sb.AppendLine("  api_server_authorized_ip_ranges = var.authorized_ip_ranges");
        sb.AppendLine();
        sb.AppendLine("  # Default node pool (required)");
        sb.AppendLine("  # We'll use a minimal system node pool and add user node pools separately");
        sb.AppendLine("  default_node_pool {");
        sb.AppendLine("    name                = \"system\"");
        sb.AppendLine("    node_count          = var.node_count");
        sb.AppendLine("    vm_size             = var.vm_size");
        sb.AppendLine("    vnet_subnet_id      = azurerm_subnet.aks.id");
        sb.AppendLine("    type                = \"VirtualMachineScaleSets\"");
        sb.AppendLine("    enable_auto_scaling = var.enable_auto_scaling");
        sb.AppendLine("    os_disk_size_gb     = var.os_disk_size_gb");
        sb.AppendLine("    os_disk_type        = \"Managed\"");
        sb.AppendLine();
        sb.AppendLine("    # Node labels");
        sb.AppendLine("    node_labels = {");
        sb.AppendLine("      \"node.kubernetes.io/role\" = \"system\"");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    # Only system workloads on this pool");
        sb.AppendLine("    only_critical_addons_enabled = true");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Identity
        sb.AppendLine("  # Managed identity");
        sb.AppendLine("  identity {");
        sb.AppendLine("    type = \"SystemAssigned\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Network profile
        sb.AppendLine("  # Network profile");
        sb.AppendLine("  network_profile {");
        sb.AppendLine("    network_plugin     = var.network_plugin");
        
        // Network policy (azure, calico, or null)
        var networkPolicy = infrastructure.NetworkPolicy ?? "azure";
        if (!string.IsNullOrEmpty(networkPolicy) && networkPolicy.ToLower() != "null")
        {
            sb.AppendLine($"    network_policy     = var.enable_network_policy");
        }
        
        sb.AppendLine("    dns_service_ip     = var.dns_service_ip");
        sb.AppendLine("    service_cidr       = var.service_cidr");
        sb.AppendLine("    load_balancer_sku  = \"standard\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Azure AD RBAC
        sb.AppendLine("  # Azure AD RBAC");
        sb.AppendLine("  azure_active_directory_role_based_access_control {");
        sb.AppendLine("    managed                = true");
        sb.AppendLine("    azure_rbac_enabled     = var.enable_azure_rbac");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Azure Policy
        sb.AppendLine("  # Azure Policy Add-on");
        sb.AppendLine("  azure_policy_enabled = var.enable_azure_policy");
        sb.AppendLine();
        
        // HTTP Application Routing
        sb.AppendLine("  # HTTP application routing");
        sb.AppendLine("  http_application_routing_enabled = var.enable_http_application_routing");
        sb.AppendLine();
        
        // Image Cleaner
        sb.AppendLine("  # Image Cleaner");
        sb.AppendLine("  image_cleaner_enabled = var.enable_image_cleaner");
        sb.AppendLine("  image_cleaner_interval_hours = var.image_cleaner_interval_hours");
        sb.AppendLine();
        
        // OMS Agent for monitoring
        sb.AppendLine("  # OMS Agent for Azure Monitor");
        sb.AppendLine("  oms_agent {");
        sb.AppendLine("    log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Microsoft Defender
        sb.AppendLine("  # Microsoft Defender for Containers");
        sb.AppendLine("  microsoft_defender {");
        sb.AppendLine("    log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Key Vault secrets provider
        sb.AppendLine("  # Key Vault Secrets Provider");
        sb.AppendLine("  key_vault_secrets_provider {");
        sb.AppendLine("    secret_rotation_enabled  = var.enable_secret_rotation");
        sb.AppendLine("    secret_rotation_interval = var.rotation_poll_interval");
        sb.AppendLine("  }");
        sb.AppendLine();
        
        // Workload identity (OIDC)
        sb.AppendLine("  # Workload Identity");
        sb.AppendLine("  oidc_issuer_enabled       = var.enable_oidc_issuer");
        sb.AppendLine("  workload_identity_enabled = var.enable_workload_identity");
        sb.AppendLine();
        
        // Disk encryption (only if disk_encryption_set_id is provided)
        var diskEncryptionSetId = infrastructure.DiskEncryptionSetId ?? infrastructure.DiskEncryptionSetIdTF;
        if (!string.IsNullOrEmpty(diskEncryptionSetId))
        {
            sb.AppendLine("  # Disk encryption with customer-managed keys");
            sb.AppendLine("  disk_encryption_set_id = var.disk_encryption_set_id");
            sb.AppendLine();
        }
        
        // Automatic upgrades
        var enableMaintenanceWindow = infrastructure.EnableMaintenanceWindow ?? infrastructure.EnableAutoScalingTF ?? true;
        sb.AppendLine("  # Automatic upgrades");
        sb.AppendLine("  automatic_channel_upgrade = \"patch\"");
        sb.AppendLine();
        
        if (enableMaintenanceWindow)
        {
            var maintenanceWindow = infrastructure.MaintenanceWindow;
            if (!string.IsNullOrEmpty(maintenanceWindow))
            {
                // Custom maintenance window (parse from infrastructure.MaintenanceWindow)
                sb.AppendLine("  # Custom maintenance window");
                sb.AppendLine("  maintenance_window {");
                sb.AppendLine("    allowed {");
                sb.AppendLine("      day   = \"Sunday\"");
                sb.AppendLine("      hours = [3, 4]");
                sb.AppendLine("    }");
                sb.AppendLine("  }");
            }
            else
            {
                // Default maintenance window
                sb.AppendLine("  # Maintenance window");
                sb.AppendLine("  maintenance_window {");
                sb.AppendLine("    allowed {");
                sb.AppendLine("      day   = \"Sunday\"");
                sb.AppendLine("      hours = [3, 4]");
                sb.AppendLine("    }");
                sb.AppendLine("  }");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateNodePoolsConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var deployment = request.Deployment ?? new DeploymentSpec();
        var resources = deployment.Resources ?? new ResourceRequirements();
        
        var minNodes = deployment.MinReplicas > 0 ? deployment.MinReplicas : 2;
        var maxNodes = deployment.MaxReplicas > 0 ? deployment.MaxReplicas : 10;
        var nodeCount = deployment.Replicas > 0 ? deployment.Replicas : 3;
        
        sb.AppendLine("# User Node Pool Configuration");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_kubernetes_cluster_node_pool\" \"user\" {");
        sb.AppendLine("  name                  = \"user\"");
        sb.AppendLine("  kubernetes_cluster_id = azurerm_kubernetes_cluster.main.id");
        
        // Determine VM size based on resource requirements
        var vmSize = DetermineVMSize(resources);
        sb.AppendLine($"  vm_size               = var.vm_size");
        sb.AppendLine();
        
        // Scaling configuration
        if (deployment.AutoScaling)
        {
            sb.AppendLine("  # Auto-scaling enabled");
            sb.AppendLine("  enable_auto_scaling = true");
            sb.AppendLine($"  min_count           = var.min_instances");
            sb.AppendLine($"  max_count           = var.max_instances");
            sb.AppendLine($"  node_count          = var.user_node_count");
        }
        else
        {
            sb.AppendLine("  # Fixed node count");
            sb.AppendLine("  enable_auto_scaling = false");
            sb.AppendLine($"  node_count          = var.user_node_count");
        }
        sb.AppendLine();
        
        sb.AppendLine("  # Virtual network");
        sb.AppendLine("  vnet_subnet_id = azurerm_subnet.aks.id");
        sb.AppendLine();
        sb.AppendLine("  # Scale set configuration");
        sb.AppendLine("  os_disk_size_gb = var.user_os_disk_size_gb");
        sb.AppendLine("  os_disk_type    = \"Managed\"");
        sb.AppendLine("  os_type         = \"Linux\"");
        sb.AppendLine();
        sb.AppendLine("  # Node labels");
        sb.AppendLine("  node_labels = {");
        sb.AppendLine("    role        = \"user\"");
        sb.AppendLine("    environment = var.environment");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Node taints (none for general workloads)");
        sb.AppendLine("  # node_taints = []");
        sb.AppendLine();
        sb.AppendLine("  # Upgrade settings");
        sb.AppendLine("  upgrade_settings {");
        sb.AppendLine("    max_surge = \"33%\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateIdentityConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# User Assigned Managed Identity for Workloads");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_user_assigned_identity\" \"workload\" {");
        sb.AppendLine("  name                = \"${var.cluster_name}-workload-identity\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Federated identity credential for Workload Identity");
        sb.AppendLine("resource \"azurerm_federated_identity_credential\" \"workload\" {");
        sb.AppendLine("  name                = \"${var.cluster_name}-workload-federated-identity\"");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  parent_id           = azurerm_user_assigned_identity.workload.id");
        sb.AppendLine("  audience            = [\"api://AzureADTokenExchange\"]");
        sb.AppendLine("  issuer              = azurerm_kubernetes_cluster.main.oidc_issuer_url");
        sb.AppendLine("  subject             = \"system:serviceaccount:${var.namespace}:${var.service_account_name}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Role assignments for cluster identity");
        sb.AppendLine("resource \"azurerm_role_assignment\" \"network_contributor\" {");
        sb.AppendLine("  scope                = azurerm_virtual_network.main.id");
        sb.AppendLine("  role_definition_name = \"Network Contributor\"");
        sb.AppendLine("  principal_id         = azurerm_kubernetes_cluster.main.identity[0].principal_id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_role_assignment\" \"acr_pull\" {");
        sb.AppendLine("  scope                = azurerm_container_registry.acr.id");
        sb.AppendLine("  role_definition_name = \"AcrPull\"");
        sb.AppendLine("  principal_id         = azurerm_kubernetes_cluster.main.kubelet_identity[0].object_id");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateNetworkConfig(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var networkConfig = infrastructure.NetworkConfig ?? CreateDefaultAKSNetworkConfig();
        
        // Check if using existing network or creating new
        if (networkConfig.Mode == NetworkMode.UseExisting)
        {
            return GenerateExistingNetworkReferences(networkConfig);
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("# Virtual Network Configuration for AKS");
        sb.AppendLine();
        
        // Virtual Network
        sb.AppendLine($@"resource ""azurerm_virtual_network"" ""main"" {{
  name                = ""{networkConfig.VNetName}""
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = [""{networkConfig.VNetAddressSpace}""]");
        
        // DDoS Protection
        if (networkConfig.EnableDDoSProtection && !string.IsNullOrEmpty(networkConfig.DDoSProtectionPlanId))
        {
            sb.AppendLine($@"
  ddos_protection_plan {{
    id     = ""{networkConfig.DDoSProtectionPlanId}""
    enable = true
  }}");
        }
        
        sb.AppendLine(@"
  tags = var.tags
}");
        sb.AppendLine();
        
        // AKS Subnet
        var aksSubnet = networkConfig.Subnets.FirstOrDefault(s => 
            s.Name.Contains("aks") || s.Name.Contains("kubernetes"));
        if (aksSubnet == null)
        {
            aksSubnet = new SubnetConfiguration
            {
                Name = "${{var.cluster_name}}-aks-subnet",
                AddressPrefix = "10.0.1.0/24",
                EnableServiceEndpoints = true,
                ServiceEndpoints = new List<string> 
                { 
                    "Microsoft.ContainerRegistry", 
                    "Microsoft.Storage", 
                    "Microsoft.KeyVault" 
                }
            };
        }
        
        sb.AppendLine($@"# Subnet for AKS
resource ""azurerm_subnet"" ""aks"" {{
  name                 = ""{aksSubnet.Name}""
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [""{aksSubnet.AddressPrefix}""]");
        
        // Service Endpoints
        if (aksSubnet.EnableServiceEndpoints && aksSubnet.ServiceEndpoints.Any())
        {
            sb.AppendLine($@"
  service_endpoints = [
    {string.Join(",\n    ", aksSubnet.ServiceEndpoints.Select(se => $"\"{se}\""))}
  ]");
        }
        
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Application Gateway Subnet (optional)
        var appgwSubnet = networkConfig.Subnets.FirstOrDefault(s => 
            s.Name.Contains("appgw") || s.Name.Contains("gateway"));
        if (appgwSubnet != null)
        {
            sb.AppendLine($@"# Subnet for Application Gateway
resource ""azurerm_subnet"" ""appgw"" {{
  name                 = ""{appgwSubnet.Name}""
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [""{appgwSubnet.AddressPrefix}""]");
            
            if (appgwSubnet.EnableServiceEndpoints && appgwSubnet.ServiceEndpoints.Any())
            {
                sb.AppendLine($@"
  service_endpoints = [{string.Join(", ", appgwSubnet.ServiceEndpoints.Select(se => $"\"{se}\""))}]");
            }
            
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        // Network Security Group (if enabled)
        if (networkConfig.EnableNetworkSecurityGroup)
        {
            if (networkConfig.NsgMode == "existing" && !string.IsNullOrEmpty(networkConfig.ExistingNsgResourceId))
            {
                // Use existing NSG
                sb.AppendLine($@"# Reference existing Network Security Group
data ""azurerm_network_security_group"" ""aks"" {{
  name                = element(split(""/"", ""{networkConfig.ExistingNsgResourceId}""), length(split(""/"", ""{networkConfig.ExistingNsgResourceId}"")) - 1)
  resource_group_name = element(split(""/"", ""{networkConfig.ExistingNsgResourceId}""), length(split(""/"", ""{networkConfig.ExistingNsgResourceId}"")) - 5)
}}");
            }
            else
            {
                // Create new NSG
                var nsgName = string.IsNullOrEmpty(networkConfig.NsgName) ? "${{var.cluster_name}}-aks-nsg" : networkConfig.NsgName;
                sb.AppendLine($@"# Network Security Group for AKS subnet
resource ""azurerm_network_security_group"" ""aks"" {{
  name                = ""{nsgName}""
  location            = var.location
  resource_group_name = var.resource_group_name

  tags = var.tags
}}");
            }
            sb.AppendLine();
            
            // NSG Rules
            if (networkConfig.NsgRules.Any())
            {
                foreach (var rule in networkConfig.NsgRules)
                {
                    sb.AppendLine($@"resource ""azurerm_network_security_rule"" ""{rule.Name.ToLowerInvariant().Replace(" ", "_")}"" {{
  name                        = ""{rule.Name}""
  priority                    = {rule.Priority}
  direction                   = ""{rule.Direction}""
  access                      = ""{rule.Access}""
  protocol                    = ""{rule.Protocol}""
  source_port_range           = ""{rule.SourcePortRange}""
  destination_port_range      = ""{rule.DestinationPortRange}""
  source_address_prefix       = ""{rule.SourceAddressPrefix}""
  destination_address_prefix  = ""{rule.DestinationAddressPrefix}""
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.aks.name
  description                 = ""{rule.Description}""
}}");
                    sb.AppendLine();
                }
            }
            else
            {
                // Default rule: Allow internal VNet communication
                sb.AppendLine($@"# Allow internal communication
resource ""azurerm_network_security_rule"" ""allow_internal"" {{
  name                        = ""AllowInternal""
  priority                    = 100
  direction                   = ""Inbound""
  access                      = ""Allow""
  protocol                    = ""*""
  source_port_range           = ""*""
  destination_port_range      = ""*""
  source_address_prefix       = ""VirtualNetwork""
  destination_address_prefix  = ""VirtualNetwork""
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.aks.name
}}");
                sb.AppendLine();
            }
            
            // Associate NSG with subnet
            sb.AppendLine($@"resource ""azurerm_subnet_network_security_group_association"" ""aks"" {{
  subnet_id                 = azurerm_subnet.aks.id
  network_security_group_id = azurerm_network_security_group.aks.id
}}");
            sb.AppendLine();
        }
        
        // Outputs
        sb.AppendLine($@"# Network Outputs
output ""vnet_id"" {{
  description = ""ID of the Virtual Network""
  value       = azurerm_virtual_network.main.id
}}

output ""vnet_name"" {{
  description = ""Name of the Virtual Network""
  value       = azurerm_virtual_network.main.name
}}

output ""aks_subnet_id"" {{
  description = ""ID of the AKS subnet""
  value       = azurerm_subnet.aks.id
}}");
        
        if (appgwSubnet != null)
        {
            sb.AppendLine($@"
output ""appgw_subnet_id"" {{
  description = ""ID of the Application Gateway subnet""
  value       = azurerm_subnet.appgw.id
}}");
        }
        
        return sb.ToString();
    }
    
    private string GenerateExistingNetworkReferences(NetworkingConfiguration networkConfig)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Reference Existing Network Resources for AKS");
        sb.AppendLine();
        
        // Data source for existing VNet
        sb.AppendLine($@"# Reference existing Virtual Network
data ""azurerm_virtual_network"" ""main"" {{
  name                = ""{networkConfig.ExistingVNetName}""
  resource_group_name = ""{networkConfig.ExistingVNetResourceGroup}""
}}");
        sb.AppendLine();
        
        // Find AKS subnet and AppGW subnet from existing subnets
        var aksSubnet = networkConfig.ExistingSubnets.FirstOrDefault(s => 
            s.Purpose == SubnetPurpose.Application || s.Name.Contains("aks"));
        var appgwSubnet = networkConfig.ExistingSubnets.FirstOrDefault(s => 
            s.Purpose == SubnetPurpose.ApplicationGateway || s.Name.Contains("appgw") || s.Name.Contains("gateway"));
        
        if (aksSubnet != null)
        {
            sb.AppendLine($@"# Reference existing AKS subnet
data ""azurerm_subnet"" ""aks"" {{
  name                 = ""{aksSubnet.Name}""
  virtual_network_name = data.azurerm_virtual_network.main.name
  resource_group_name  = ""{networkConfig.ExistingVNetResourceGroup}""
}}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("# WARNING: No AKS subnet found in existing network configuration");
            sb.AppendLine();
        }
        
        if (appgwSubnet != null)
        {
            sb.AppendLine($@"# Reference existing Application Gateway subnet
data ""azurerm_subnet"" ""appgw"" {{
  name                 = ""{appgwSubnet.Name}""
  virtual_network_name = data.azurerm_virtual_network.main.name
  resource_group_name  = ""{networkConfig.ExistingVNetResourceGroup}""
}}");
            sb.AppendLine();
        }
        
        // Optionally generate NSG if needed
        if (networkConfig.EnableNetworkSecurityGroup && networkConfig.NsgRules.Any())
        {
            sb.AppendLine($@"# Network Security Group for AKS
resource ""azurerm_network_security_group"" ""aks"" {{
  name                = ""nsg-${{var.cluster_name}}-aks""
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}}");
            sb.AppendLine();
            
            foreach (var rule in networkConfig.NsgRules)
            {
                sb.AppendLine($@"
resource ""azurerm_network_security_rule"" ""{rule.Name.ToLower().Replace(" ", "_")}"" {{
  name                        = ""{rule.Name}""
  priority                    = {rule.Priority}
  direction                   = ""{rule.Direction}""
  access                      = ""{rule.Access}""
  protocol                    = ""{rule.Protocol}""
  source_port_range           = ""{rule.SourcePortRange}""
  destination_port_range      = ""{rule.DestinationPortRange}""
  source_address_prefix       = ""{rule.SourceAddressPrefix}""
  destination_address_prefix  = ""{rule.DestinationAddressPrefix}""
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.aks.name
  description                 = ""{rule.Description}""
}}");
            }
            sb.AppendLine();
            
            // Associate NSG with existing AKS subnet
            if (aksSubnet != null)
            {
                sb.AppendLine($@"
resource ""azurerm_subnet_network_security_group_association"" ""aks"" {{
  subnet_id                 = data.azurerm_subnet.aks.id
  network_security_group_id = azurerm_network_security_group.aks.id
}}");
                sb.AppendLine();
            }
        }
        
        // Outputs
        sb.AppendLine($@"# Outputs
output ""vnet_id"" {{
  description = ""ID of the existing Virtual Network""
  value       = data.azurerm_virtual_network.main.id
}}");
        
        if (aksSubnet != null)
        {
            sb.AppendLine($@"
output ""aks_subnet_id"" {{
  description = ""ID of the existing AKS subnet""
  value       = data.azurerm_subnet.aks.id
}}");
        }
        
        if (appgwSubnet != null)
        {
            sb.AppendLine($@"
output ""appgw_subnet_id"" {{
  description = ""ID of the existing Application Gateway subnet""
  value       = data.azurerm_subnet.appgw.id
}}");
        }
        
        return sb.ToString();
    }
    
    private NetworkingConfiguration CreateDefaultAKSNetworkConfig()
    {
        return new NetworkingConfiguration
        {
            Mode = NetworkMode.CreateNew,
            VNetName = "${var.cluster_name}-vnet",
            VNetAddressSpace = "10.0.0.0/16",
            Subnets = new List<SubnetConfiguration>
            {
                new SubnetConfiguration
                {
                    Name = "${var.cluster_name}-aks-subnet",
                    AddressPrefix = "10.0.1.0/24",
                    Purpose = SubnetPurpose.Application,
                    EnableServiceEndpoints = true,
                    ServiceEndpoints = new List<string>
                    {
                        "Microsoft.ContainerRegistry",
                        "Microsoft.Storage",
                        "Microsoft.KeyVault"
                    }
                },
                new SubnetConfiguration
                {
                    Name = "${var.cluster_name}-appgw-subnet",
                    AddressPrefix = "10.0.2.0/24",
                    Purpose = SubnetPurpose.ApplicationGateway
                }
            },
            EnableNetworkSecurityGroup = true,
            EnablePrivateEndpoint = false,
            EnableServiceEndpoints = true,
            EnableDDoSProtection = false,
            EnablePrivateDns = false
        };
    }
    
    private string GenerateSecurityConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var security = request.Security ?? new SecuritySpec();
        
        sb.AppendLine("# Key Vault for secrets management");
        sb.AppendLine();
        sb.AppendLine("data \"azurerm_client_config\" \"current\" {}");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_key_vault\" \"main\" {");
        sb.AppendLine("  name                       = \"${var.cluster_name}-kv\"");
        sb.AppendLine("  location                   = var.location");
        sb.AppendLine("  resource_group_name        = var.resource_group_name");
        sb.AppendLine("  tenant_id                  = data.azurerm_client_config.current.tenant_id");
        sb.AppendLine("  sku_name                   = \"standard\"");
        sb.AppendLine("  soft_delete_retention_days = 7");
        sb.AppendLine("  purge_protection_enabled   = true");
        sb.AppendLine();
        sb.AppendLine("  # Enable RBAC authorization");
        sb.AppendLine("  enable_rbac_authorization = true");
        sb.AppendLine();
        sb.AppendLine("  # Network ACLs");
        sb.AppendLine("  network_acls {");
        sb.AppendLine("    bypass                     = \"AzureServices\"");
        sb.AppendLine("    default_action             = \"Deny\"");
        sb.AppendLine("    virtual_network_subnet_ids = [azurerm_subnet.aks.id]");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        sb.AppendLine("# Key Vault access for cluster identity");
        sb.AppendLine("resource \"azurerm_role_assignment\" \"kv_secrets_user\" {");
        sb.AppendLine("  scope                = azurerm_key_vault.main.id");
        sb.AppendLine("  role_definition_name = \"Key Vault Secrets User\"");
        sb.AppendLine("  principal_id         = azurerm_kubernetes_cluster.main.key_vault_secrets_provider[0].secret_identity[0].object_id");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateACRConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Azure Container Registry");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_container_registry\" \"acr\" {");
        sb.AppendLine("  name                = replace(\"${var.cluster_name}acr\", \"-\", \"\")");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  sku                 = \"Standard\"");
        sb.AppendLine("  admin_enabled       = false");
        sb.AppendLine();
        sb.AppendLine("  # Geo-replication (Premium SKU only)");
        sb.AppendLine("  # georeplications {");
        sb.AppendLine("  #   location = \"West US\"");
        sb.AppendLine("  #   tags     = var.tags");
        sb.AppendLine("  # }");
        sb.AppendLine();
        sb.AppendLine("  # Network rules");
        sb.AppendLine("  network_rule_set {");
        sb.AppendLine("    default_action = \"Allow\"");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Content trust");
        sb.AppendLine("  trust_policy {");
        sb.AppendLine("    enabled = false");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Retention policy (Premium SKU only)");
        sb.AppendLine("  # retention_policy {");
        sb.AppendLine("  #   days    = 7");
        sb.AppendLine("  #   enabled = true");
        sb.AppendLine("  # }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateMonitoringConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var observability = request.Observability ?? new ObservabilitySpec();
        
        sb.AppendLine("# Log Analytics Workspace");
        sb.AppendLine();
        sb.AppendLine("resource \"azurerm_log_analytics_workspace\" \"main\" {");
        sb.AppendLine("  name                = \"${var.cluster_name}-logs\"");
        sb.AppendLine("  location            = var.location");
        sb.AppendLine("  resource_group_name = var.resource_group_name");
        sb.AppendLine("  sku                 = \"PerGB2018\"");
        sb.AppendLine("  retention_in_days   = var.log_retention_days");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        if (observability.Prometheus)
        {
            sb.AppendLine("# Azure Monitor managed service for Prometheus");
            sb.AppendLine("resource \"azurerm_monitor_workspace\" \"prometheus\" {");
            sb.AppendLine("  name                = \"${var.cluster_name}-prometheus\"");
            sb.AppendLine("  location            = var.location");
            sb.AppendLine("  resource_group_name = var.resource_group_name");
            sb.AppendLine();
            sb.AppendLine("  tags = var.tags");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# Data collection endpoint");
            sb.AppendLine("resource \"azurerm_monitor_data_collection_endpoint\" \"prometheus\" {");
            sb.AppendLine("  name                = \"${var.cluster_name}-dce\"");
            sb.AppendLine("  location            = var.location");
            sb.AppendLine("  resource_group_name = var.resource_group_name");
            sb.AppendLine();
            sb.AppendLine("  tags = var.tags");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# Data collection rule");
            sb.AppendLine("resource \"azurerm_monitor_data_collection_rule\" \"prometheus\" {");
            sb.AppendLine("  name                        = \"${var.cluster_name}-dcr\"");
            sb.AppendLine("  location                    = var.location");
            sb.AppendLine("  resource_group_name         = var.resource_group_name");
            sb.AppendLine("  data_collection_endpoint_id = azurerm_monitor_data_collection_endpoint.prometheus.id");
            sb.AppendLine();
            sb.AppendLine("  destinations {");
            sb.AppendLine("    monitor_account {");
            sb.AppendLine("      monitor_account_id = azurerm_monitor_workspace.prometheus.id");
            sb.AppendLine("      name               = \"MonitoringAccount\"");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  data_flow {");
            sb.AppendLine("    streams      = [\"Microsoft-PrometheusMetrics\"]");
            sb.AppendLine("    destinations = [\"MonitoringAccount\"]");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  tags = var.tags");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("# Associate DCR with AKS cluster");
            sb.AppendLine("resource \"azurerm_monitor_data_collection_rule_association\" \"prometheus\" {");
            sb.AppendLine("  name                    = \"${var.cluster_name}-dcra\"");
            sb.AppendLine("  target_resource_id      = azurerm_kubernetes_cluster.main.id");
            sb.AppendLine("  data_collection_rule_id = azurerm_monitor_data_collection_rule.prometheus.id");
            sb.AppendLine("}");
        }
        
        return sb.ToString();
    }
    
    private string GenerateVariablesConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "aks-service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        sb.AppendLine("# Variables for AKS Module");
        sb.AppendLine();
        sb.AppendLine("variable \"cluster_name\" {");
        sb.AppendLine("  description = \"Name of the AKS cluster\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{serviceName}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"location\" {");
        sb.AppendLine("  description = \"Azure region for resources\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{infrastructure.Region ?? "eastus"}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_group_name\" {");
        sb.AppendLine("  description = \"Name of the resource group\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"kubernetes_version\" {");
        sb.AppendLine("  description = \"Kubernetes version\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"1.28\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"vnet_address_space\" {");
        sb.AppendLine("  description = \"Address space for the virtual network\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.0.0/16\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"aks_subnet_address_prefix\" {");
        sb.AppendLine("  description = \"Address prefix for AKS subnet\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.1.0/24\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"appgw_subnet_address_prefix\" {");
        sb.AppendLine("  description = \"Address prefix for Application Gateway subnet\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.0.2.0/24\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"environment\" {");
        sb.AppendLine("  description = \"Environment name (dev, staging, prod)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"dev\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"namespace\" {");
        sb.AppendLine("  description = \"Kubernetes namespace for workload identity\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"default\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"service_account_name\" {");
        sb.AppendLine("  description = \"Kubernetes service account name for workload identity\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"default\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // === NODE POOL CONFIGURATION ===
        sb.AppendLine("# === NODE POOL CONFIGURATION ===");
        sb.AppendLine();
        
        var deployment = request.Deployment ?? new DeploymentSpec();
        var resources = deployment.Resources ?? new ResourceRequirements();
        var vmSize = DetermineVMSize(resources);
        var minNodes = deployment.MinReplicas > 0 ? deployment.MinReplicas : 2;
        var maxNodes = deployment.MaxReplicas > 0 ? deployment.MaxReplicas : 10;
        var nodeCount = deployment.Replicas > 0 ? deployment.Replicas : 3;
        
        sb.AppendLine("variable \"vm_size\" {");
        sb.AppendLine("  description = \"VM size for default node pool\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{vmSize}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"node_count\" {");
        sb.AppendLine("  description = \"Initial node count for default node pool\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine($"  default     = 1");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"min_instances\" {");
        sb.AppendLine("  description = \"Minimum number of nodes for autoscaling\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine($"  default     = {minNodes}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"max_instances\" {");
        sb.AppendLine("  description = \"Maximum number of nodes for autoscaling\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine($"  default     = {maxNodes}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"user_node_count\" {");
        sb.AppendLine("  description = \"Initial node count for user node pool\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine($"  default     = {nodeCount}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"os_disk_size_gb\" {");
        sb.AppendLine("  description = \"OS disk size in GB\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 30");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"user_os_disk_size_gb\" {");
        sb.AppendLine("  description = \"OS disk size in GB for user node pool\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 100");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // === NETWORK CONFIGURATION ===
        sb.AppendLine("# === NETWORK CONFIGURATION ===");
        sb.AppendLine();
        sb.AppendLine("variable \"network_plugin\" {");
        sb.AppendLine("  description = \"Network plugin to use (azure or kubenet)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"azure\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"dns_service_ip\" {");
        sb.AppendLine("  description = \"IP address for Kubernetes DNS service\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.2.0.10\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"service_cidr\" {");
        sb.AppendLine("  description = \"CIDR block for Kubernetes services\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"10.2.0.0/16\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // === MONITORING CONFIGURATION ===
        sb.AppendLine("# === MONITORING CONFIGURATION ===");
        sb.AppendLine();
        sb.AppendLine("variable \"log_retention_days\" {");
        sb.AppendLine("  description = \"Log Analytics retention in days\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 30");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // === ZERO TRUST SECURITY PARAMETERS (Mirror Bicep AKS) ===
        sb.AppendLine("# === ZERO TRUST SECURITY PARAMETERS ===");
        sb.AppendLine();
        
        var enablePrivateCluster = infrastructure.EnablePrivateCluster ?? infrastructure.EnablePrivateClusterTF ?? true;
        sb.AppendLine("variable \"enable_private_cluster\" {");
        sb.AppendLine("  description = \"Enable private cluster (API server accessible only via private network)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enablePrivateCluster.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableWorkloadIdentity = infrastructure.EnableWorkloadIdentity ?? infrastructure.EnableWorkloadIdentityTF ?? true;
        sb.AppendLine("variable \"enable_workload_identity\" {");
        sb.AppendLine("  description = \"Enable Azure AD Workload Identity for pod-level authentication\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableWorkloadIdentity.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableAzurePolicy = infrastructure.EnableAzurePolicy ?? infrastructure.EnableAzurePolicyTF ?? true;
        sb.AppendLine("variable \"enable_azure_policy\" {");
        sb.AppendLine("  description = \"Enable Azure Policy for Kubernetes\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableAzurePolicy.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableImageCleaner = infrastructure.EnableImageCleaner ?? infrastructure.EnableImageCleanerTF ?? true;
        sb.AppendLine("variable \"enable_image_cleaner\" {");
        sb.AppendLine("  description = \"Enable Image Cleaner to remove unused container images\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableImageCleaner.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var imageCleanerInterval = infrastructure.ImageCleanerIntervalHours ?? 24;
        sb.AppendLine("variable \"image_cleaner_interval_hours\" {");
        sb.AppendLine("  description = \"Interval in hours for Image Cleaner\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine($"  default     = {imageCleanerInterval}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableDiskEncryption = true; // Default to true for security
        sb.AppendLine("variable \"enable_disk_encryption\" {");
        sb.AppendLine("  description = \"Enable disk encryption at host level\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableDiskEncryption.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var diskEncryptionSetId = infrastructure.DiskEncryptionSetId ?? infrastructure.DiskEncryptionSetIdTF ?? "";
        sb.AppendLine("variable \"disk_encryption_set_id\" {");
        sb.AppendLine("  description = \"ID of disk encryption set for customer-managed keys\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{diskEncryptionSetId}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableDefender = infrastructure.EnableDefender ?? true;
        sb.AppendLine("variable \"enable_defender\" {");
        sb.AppendLine("  description = \"Enable Microsoft Defender for Containers\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableDefender.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableACRPrivateEndpoint = true; // Default to true for security
        sb.AppendLine("variable \"enable_acr_private_endpoint\" {");
        sb.AppendLine("  description = \"Enable private endpoint for Azure Container Registry\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableACRPrivateEndpoint.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var acrSubnetId = "";
        sb.AppendLine("variable \"acr_private_endpoint_subnet_id\" {");
        sb.AppendLine("  description = \"Subnet ID for ACR private endpoint\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{acrSubnetId}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var security = request.Security ?? new SecuritySpec();
        var enableAzureRBAC = infrastructure.EnableAzureRBAC ?? security.RBAC;
        sb.AppendLine("variable \"enable_azure_rbac\" {");
        sb.AppendLine("  description = \"Enable Azure RBAC for Kubernetes authorization\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableAzureRBAC.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableOIDCIssuer = infrastructure.EnableOIDCIssuer ?? true;
        sb.AppendLine("variable \"enable_oidc_issuer\" {");
        sb.AppendLine("  description = \"Enable OIDC issuer for workload identity federation\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableOIDCIssuer.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enablePodSecurityPolicy = false; // Default to false (deprecated)
        sb.AppendLine("variable \"enable_pod_security_policy\" {");
        sb.AppendLine("  description = \"Enable Pod Security Policy (deprecated, use Azure Policy instead)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enablePodSecurityPolicy.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var networkPolicy = infrastructure.NetworkPolicy ?? "azure";
        sb.AppendLine("variable \"enable_network_policy\" {");
        sb.AppendLine("  description = \"Network policy implementation: 'azure', 'calico', or null\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{networkPolicy}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableHTTPRouting = infrastructure.EnableHTTPApplicationRouting ?? false;
        sb.AppendLine("variable \"enable_http_application_routing\" {");
        sb.AppendLine("  description = \"Enable HTTP application routing (not recommended for production)\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableHTTPRouting.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableKeyVaultProvider = infrastructure.EnableKeyVaultSecretsProvider ?? true;
        sb.AppendLine("variable \"enable_azure_keyvault_secrets_provider\" {");
        sb.AppendLine("  description = \"Enable Azure Key Vault secrets provider\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableKeyVaultProvider.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableSecretRotation = infrastructure.EnableSecretRotation ?? true;
        sb.AppendLine("variable \"enable_secret_rotation\" {");
        sb.AppendLine("  description = \"Enable automatic secret rotation for Key Vault\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableSecretRotation.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var rotationInterval = infrastructure.SecretRotationPollInterval ?? infrastructure.RotationPollInterval ?? "2m";
        sb.AppendLine("variable \"rotation_poll_interval\" {");
        sb.AppendLine("  description = \"Secret rotation poll interval (e.g., '2m')\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{rotationInterval}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var authorizedIPRanges = infrastructure.AuthorizedIPRanges ?? infrastructure.AuthorizedIPRangesTF ?? "";
        var ipRangesList = string.IsNullOrEmpty(authorizedIPRanges) ? "[]" : $"[\"{string.Join("\", \"", authorizedIPRanges.Split(',').Select(s => s.Trim()))}\"]";
        sb.AppendLine("variable \"authorized_ip_ranges\" {");
        sb.AppendLine("  description = \"Authorized IP ranges for API server access\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine($"  default     = {ipRangesList}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableAutoScaling = deployment.AutoScaling || (infrastructure.EnableAutoScalingTF ?? true);
        sb.AppendLine("variable \"enable_auto_scaling\" {");
        sb.AppendLine("  description = \"Enable cluster autoscaler\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableAutoScaling.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        var enableMaintenanceWindow = infrastructure.EnableMaintenanceWindow ?? true;
        sb.AppendLine("variable \"enable_maintenance_window\" {");
        sb.AppendLine("  description = \"Enable maintenance window for updates\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine($"  default     = {enableMaintenanceWindow.ToString().ToLower()}");
        sb.AppendLine("}");
        sb.AppendLine();
        // Tags variable
        var tags = infrastructure.Tags ?? new Dictionary<string, string>();
        string tagsDefault;
        if (tags.Any())
        {
            var tagPairs = tags.Select(kvp => $"\"{kvp.Key}\" = \"{kvp.Value}\"");
            tagsDefault = $"{{{string.Join(", ", tagPairs)}}}";
        }
        else
        {
            tagsDefault = "{}";
        }
        
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Tags to apply to all resources\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine($"  default     = {tagsDefault}");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateOutputsConfig(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Outputs for AKS Module");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_id\" {");
        sb.AppendLine("  description = \"AKS cluster ID\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_name\" {");
        sb.AppendLine("  description = \"AKS cluster name\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_fqdn\" {");
        sb.AppendLine("  description = \"AKS cluster FQDN\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.fqdn");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"kube_config\" {");
        sb.AppendLine("  description = \"Kubernetes config for cluster access\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.kube_config_raw");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"client_certificate\" {");
        sb.AppendLine("  description = \"Client certificate for cluster authentication\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.kube_config[0].client_certificate");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"cluster_ca_certificate\" {");
        sb.AppendLine("  description = \"Cluster CA certificate\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.kube_config[0].cluster_ca_certificate");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"oidc_issuer_url\" {");
        sb.AppendLine("  description = \"OIDC issuer URL for workload identity\"");
        sb.AppendLine("  value       = azurerm_kubernetes_cluster.main.oidc_issuer_url");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"acr_login_server\" {");
        sb.AppendLine("  description = \"Azure Container Registry login server\"");
        sb.AppendLine("  value       = azurerm_container_registry.acr.login_server");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"acr_id\" {");
        sb.AppendLine("  description = \"Azure Container Registry ID\"");
        sb.AppendLine("  value       = azurerm_container_registry.acr.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"vnet_id\" {");
        sb.AppendLine("  description = \"Virtual network ID\"");
        sb.AppendLine("  value       = azurerm_virtual_network.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"aks_subnet_id\" {");
        sb.AppendLine("  description = \"AKS subnet ID\"");
        sb.AppendLine("  value       = azurerm_subnet.aks.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"workload_identity_client_id\" {");
        sb.AppendLine("  description = \"Client ID of workload identity\"");
        sb.AppendLine("  value       = azurerm_user_assigned_identity.workload.client_id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"log_analytics_workspace_id\" {");
        sb.AppendLine("  description = \"Log Analytics workspace ID\"");
        sb.AppendLine("  value       = azurerm_log_analytics_workspace.main.id");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string DetermineVMSize(ResourceRequirements resources)
    {
        var cpuLimit = resources.CpuLimit?.ToLower() ?? "1 vcpu";
        var memoryLimit = resources.MemoryLimit?.ToLower() ?? "2 gb";
        
        // Parse CPU requirements
        var cpuValue = 1.0;
        if (cpuLimit.Contains("vcpu"))
        {
            var parts = cpuLimit.Split(' ');
            if (parts.Length > 0 && double.TryParse(parts[0], out var cpu))
            {
                cpuValue = cpu;
            }
        }
        
        // Determine VM size based on CPU and memory
        // Standard_D series VMs
        if (cpuValue <= 2)
        {
            return "Standard_DS2_v2"; // 2 vCPU, 7 GB
        }
        else if (cpuValue <= 4)
        {
            return "Standard_DS3_v2"; // 4 vCPU, 14 GB
        }
        else if (cpuValue <= 8)
        {
            return "Standard_DS4_v2"; // 8 vCPU, 28 GB
        }
        else
        {
            return "Standard_DS5_v2"; // 16 vCPU, 56 GB
        }
    }
}
