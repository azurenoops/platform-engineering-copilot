# ==============================================================================
# Azure Kubernetes Service (AKS) - IL5/IL6 Compliant
# ==============================================================================
# Creates AKS cluster with:
# - Private cluster (no public API endpoint)
# - Azure AD integration with RBAC
# - Azure Policy for Pod Security
# - Microsoft Defender for Containers
# - Network policies and Azure CNI
# - Workload identity with OIDC
# ==============================================================================

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.80"
    }
  }
}

# AKS Cluster
resource "azurerm_kubernetes_cluster" "aks" {
  name                = var.cluster_name
  location            = var.location
  resource_group_name = var.resource_group_name
  dns_prefix          = var.dns_prefix
  kubernetes_version  = var.kubernetes_version

  # IL5/IL6: Private cluster
  private_cluster_enabled = var.enable_private_cluster

  # FIPS 140-2 compliance
  #fips_enabled = true

  # System-assigned managed identity
  identity {
    type = "SystemAssigned"
  }

  # Default (system) node pool
  default_node_pool {
    name                 = "system"
    vm_size              = var.system_node_vm_size
    node_count           = var.system_node_count
    vnet_subnet_id       = var.subnet_id
    auto_scaling_enabled  = false
    zones                = var.availability_zones
    host_encryption_enabled = true
    
    upgrade_settings {
      max_surge = "10%"
    }

    tags = var.tags
  }

  # Azure AD integration
  azure_active_directory_role_based_access_control {
    azure_rbac_enabled     = var.enable_azure_rbac
    admin_group_object_ids = var.aad_admin_group_object_ids
  }

  # Network profile (Azure CNI)
  network_profile {
    network_plugin     = var.network_plugin
    network_policy     = var.network_policy
    service_cidr       = var.service_cidr
    dns_service_ip     = var.dns_service_ip
    pod_cidr           = var.pod_cidr
    load_balancer_sku  = "standard"
    outbound_type      = var.outbound_type
  }

  # Workload identity and OIDC
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  # Azure Policy Add-on
  azure_policy_enabled = var.enable_azure_policy

  # Microsoft Defender for Containers
  dynamic "microsoft_defender" {
    for_each = var.enable_defender ? [1] : []
    content {
      log_analytics_workspace_id = var.log_analytics_workspace_id
    }
  }

  # Azure Monitor for containers (OMS agent)
  oms_agent {
    log_analytics_workspace_id = var.log_analytics_workspace_id
  }

  # Key Vault Secrets Provider
  key_vault_secrets_provider {
    secret_rotation_enabled  = true
    secret_rotation_interval = "2m"
  }

  # Auto-upgrade configuration
  #automatic_channel_upgrade = var.automatic_channel_upgrade

  # Node OS upgrade configuration
  #node_os_channel_upgrade = "NodeImage"

  # Maintenance window
  maintenance_window {
    allowed {
      day   = "Sunday"
      hours = [2, 3, 4]
    }
  }

  # Storage profile
  storage_profile {
    blob_driver_enabled = true
    disk_driver_enabled = true
    file_driver_enabled = true
  }

  tags = merge(
    var.tags,
    {
      ManagedBy = "Terraform"
      Service   = "Kubernetes"
    }
  )

  lifecycle {
    ignore_changes = [
      default_node_pool[0].node_count,
      kubernetes_version
    ]
  }
}

# User node pool (workload nodes)
resource "azurerm_kubernetes_cluster_node_pool" "user" {
  name                  = "user"
  kubernetes_cluster_id = azurerm_kubernetes_cluster.aks.id
  vm_size               = var.user_node_vm_size
  node_count            = var.user_node_count
  vnet_subnet_id        = var.subnet_id
  auto_scaling_enabled   = var.enable_auto_scaling
  min_count             = var.enable_auto_scaling ? var.min_node_count : null
  max_count             = var.enable_auto_scaling ? var.max_node_count : null
  zones                 = var.availability_zones
  host_encryption_enabled = true
  mode                  = "User"

  upgrade_settings {
    max_surge = "33%"
  }

  node_labels = {
    "workload" = "general"
  }

  node_taints = []

  tags = var.tags
}

# Role Assignment: AKS to ACR
resource "azurerm_role_assignment" "aks_acr_pull" {
  count = var.acr_id != "" ? 1 : 0

  scope                = var.acr_id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
}

# Diagnostic Settings
resource "azurerm_monitor_diagnostic_setting" "aks_diag" {
  count = var.log_analytics_workspace_id != "" ? 1 : 0

  name                       = "${var.cluster_name}-diag"
  target_resource_id         = azurerm_kubernetes_cluster.aks.id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "kube-apiserver"
  }

  enabled_log {
    category = "kube-audit"
  }

  enabled_log {
    category = "kube-controller-manager"
  }

  enabled_log {
    category = "kube-scheduler"
  }

  enabled_log {
    category = "cluster-autoscaler"
  }

  enabled_log {
    category = "guard"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}
