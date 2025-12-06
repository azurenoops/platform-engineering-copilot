# Variables for Platform Engineering Copilot Infrastructure

variable "project_name" {
  description = "Project name that will be used as prefix for resource names"
  type        = string
  default     = "platsup"
  
  validation {
    condition     = can(regex("^[a-z0-9]{3,8}$", var.project_name))
    error_message = "Project name must be 3-8 characters, lowercase letters and numbers only."
  }
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
  
  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be one of: dev, staging, prod."
  }
}

variable "location" {
  description = "Azure region for all resources"
  type        = string
  default     = "East US"
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "sql_admin_login" {
  description = "SQL Server administrator login"
  type        = string
  default     = "platformadmin"
}

variable "sql_admin_password" {
  description = "SQL Server administrator password"
  type        = string
  sensitive   = true
  
  validation {
    condition     = can(regex("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[@$!%*?&])[A-Za-z\\d@$!%*?&]{12,}$", var.sql_admin_password))
    error_message = "Password must be at least 12 characters with uppercase, lowercase, number, and special character."
  }
}

variable "key_vault_admin_object_id" {
  description = "Object ID of the user or service principal for Key Vault admin access"
  type        = string
}

variable "azure_ad_admin_object_id" {
  description = "Azure AD Admin Object ID for SQL Server"
  type        = string
  default     = ""
}

variable "azure_ad_admin_login" {
  description = "Azure AD Admin Login Name for SQL Server"
  type        = string
  default     = ""
}

variable "app_service_sku" {
  description = "App Service Plan SKU"
  type        = string
  default     = "B1"
  
  validation {
    condition     = contains(["F1", "B1", "B2", "B3", "S1", "S2", "S3", "P1V2", "P2V2", "P3V2"], var.app_service_sku)
    error_message = "App Service SKU must be one of: F1, B1, B2, B3, S1, S2, S3, P1V2, P2V2, P3V2."
  }
}

variable "sql_database_sku" {
  description = "SQL Database SKU"
  type        = string
  default     = "S0"
  
  validation {
    condition     = contains(["Basic", "S0", "S1", "S2", "S3", "P1", "P2", "P4", "P6", "P11", "P15"], var.sql_database_sku)
    error_message = "SQL Database SKU must be one of: Basic, S0, S1, S2, S3, P1, P2, P4, P6, P11, P15."
  }
}

# Network Configuration
variable "vnet_address_prefix" {
  description = "The address prefix for the virtual network"
  type        = string
  default     = "10.0.0.0/16"
}

variable "app_service_subnet_prefix" {
  description = "The address prefix for the app service subnet"
  type        = string
  default     = "10.0.1.0/24"
}

variable "private_endpoint_subnet_prefix" {
  description = "The address prefix for the private endpoint subnet"
  type        = string
  default     = "10.0.2.0/24"
}

variable "management_subnet_prefix" {
  description = "The address prefix for the management subnet"
  type        = string
  default     = "10.0.3.0/24"
}

# Monitoring Configuration
variable "alert_email_addresses" {
  description = "List of email addresses to send alerts to"
  type        = list(string)
  default     = []
}

# ==============================================================================
# App Service Deployment Configuration
# ==============================================================================

variable "deploy_admin_api" {
  description = "Deploy Admin API app service"
  type        = bool
  default     = true
}

variable "deploy_chat" {
  description = "Deploy Chat app service"
  type        = bool
  default     = true
}

# ==============================================================================
# Existing Resource Configuration
# ==============================================================================

variable "use_existing_network" {
  description = "Use existing Virtual Network instead of creating new one"
  type        = bool
  default     = false
}

variable "existing_vnet_name" {
  description = "Name of existing Virtual Network (required if use_existing_network is true)"
  type        = string
  default     = ""
}

variable "existing_vnet_resource_group" {
  description = "Resource group of existing Virtual Network (defaults to current resource group)"
  type        = string
  default     = ""
}

variable "existing_app_service_subnet_name" {
  description = "Name of existing App Service subnet (required if use_existing_network is true)"
  type        = string
  default     = ""
}

variable "existing_private_endpoint_subnet_name" {
  description = "Name of existing Private Endpoint subnet (required if use_existing_network is true)"
  type        = string
  default     = ""
}

variable "use_existing_log_analytics" {
  description = "Use existing Log Analytics Workspace instead of creating new one"
  type        = bool
  default     = false
}

variable "existing_log_analytics_workspace_name" {
  description = "Name of existing Log Analytics Workspace (required if use_existing_log_analytics is true)"
  type        = string
  default     = ""
}

variable "existing_log_analytics_resource_group" {
  description = "Resource group of existing Log Analytics Workspace (defaults to current resource group)"
  type        = string
  default     = ""
}

variable "use_existing_keyvault" {
  description = "Use existing Key Vault instead of creating new one"
  type        = bool
  default     = false
}

variable "existing_keyvault_name" {
  description = "Name of existing Key Vault (required if use_existing_keyvault is true)"
  type        = string
  default     = ""
}

variable "existing_keyvault_resource_group" {
  description = "Resource group of existing Key Vault (defaults to current resource group)"
  type        = string
  default     = ""
}

# ==============================================================================
# Container Infrastructure Configuration
# ==============================================================================

variable "enable_container_infrastructure" {
  description = "Enable container infrastructure (ACR, AKS, ACI)"
  type        = bool
  default     = false
}

# Azure Container Registry (ACR) Variables
variable "acr_sku" {
  description = "SKU for Azure Container Registry (Basic, Standard, Premium)"
  type        = string
  default     = "Premium"
  
  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.acr_sku)
    error_message = "ACR SKU must be Basic, Standard, or Premium."
  }
}

variable "acr_admin_enabled" {
  description = "Enable admin user for ACR (not recommended for production)"
  type        = bool
  default     = false
}

variable "acr_public_network_access_enabled" {
  description = "Enable public network access to ACR"
  type        = bool
  default     = false
}

variable "acr_zone_redundancy_enabled" {
  description = "Enable zone redundancy for ACR (Premium SKU only)"
  type        = bool
  default     = true
}

variable "acr_enable_private_endpoint" {
  description = "Enable private endpoint for ACR"
  type        = bool
  default     = true
}

variable "acr_network_rule_set_enabled" {
  description = "Enable network rule set for ACR"
  type        = bool
  default     = true
}

variable "acr_network_rule_default_action" {
  description = "Default action for ACR network rules (Allow or Deny)"
  type        = string
  default     = "Deny"
}

variable "acr_allowed_ip_ranges" {
  description = "List of allowed IP ranges for ACR access"
  type        = list(string)
  default     = []
}

variable "acr_allowed_subnet_ids" {
  description = "List of allowed subnet IDs for ACR access"
  type        = list(string)
  default     = []
}

variable "acr_content_trust_enabled" {
  description = "Enable content trust (Notary v2) for ACR"
  type        = bool
  default     = true
}

variable "acr_quarantine_enabled" {
  description = "Enable quarantine policy for ACR"
  type        = bool
  default     = true
}

variable "acr_retention_policy_enabled" {
  description = "Enable retention policy for untagged manifests"
  type        = bool
  default     = true
}

variable "acr_retention_days" {
  description = "Number of days to retain untagged manifests"
  type        = number
  default     = 30
}

variable "acr_georeplications" {
  description = "List of geo-replication locations for ACR (Premium SKU only)"
  type = list(object({
    location                = string
    zone_redundancy_enabled = bool
  }))
  default = []
}

variable "acr_encryption_enabled" {
  description = "Enable customer-managed encryption for ACR"
  type        = bool
  default     = false
}

variable "acr_encryption_key_vault_key_id" {
  description = "Key Vault key ID for ACR encryption"
  type        = string
  default     = ""
}

variable "acr_encryption_identity_client_id" {
  description = "Client ID of the managed identity for ACR encryption"
  type        = string
  default     = ""
}

# Azure Kubernetes Service (AKS) Variables
variable "enable_aks" {
  description = "Enable Azure Kubernetes Service"
  type        = bool
  default     = false
}

variable "aks_kubernetes_version" {
  description = "Kubernetes version for AKS"
  type        = string
  default     = "1.28.3"
}

variable "aks_enable_private_cluster" {
  description = "Enable private cluster for AKS"
  type        = bool
  default     = true
}

variable "aks_system_node_vm_size" {
  description = "VM size for AKS system node pool"
  type        = string
  default     = "Standard_D4s_v5"
}

variable "aks_system_node_count" {
  description = "Number of nodes in AKS system node pool"
  type        = number
  default     = 3
}

variable "aks_availability_zones" {
  description = "Availability zones for AKS nodes"
  type        = list(string)
  default     = ["1", "2", "3"]
}

variable "aks_enable_user_node_pool" {
  description = "Enable user node pool for AKS"
  type        = bool
  default     = true
}

variable "aks_user_node_vm_size" {
  description = "VM size for AKS user node pool"
  type        = string
  default     = "Standard_D4s_v5"
}

variable "aks_user_node_min_count" {
  description = "Minimum number of nodes in AKS user node pool"
  type        = number
  default     = 2
}

variable "aks_user_node_max_count" {
  description = "Maximum number of nodes in AKS user node pool"
  type        = number
  default     = 10
}

variable "aks_enable_azure_rbac" {
  description = "Enable Azure RBAC for AKS"
  type        = bool
  default     = true
}

variable "aks_aad_admin_group_object_ids" {
  description = "List of Azure AD group object IDs for AKS admin access"
  type        = list(string)
  default     = []
}

variable "aks_network_plugin" {
  description = "Network plugin for AKS (azure or kubenet)"
  type        = string
  default     = "azure"
}

variable "aks_network_policy" {
  description = "Network policy for AKS (azure, calico, or cilium)"
  type        = string
  default     = "azure"
}

variable "aks_service_cidr" {
  description = "Service CIDR for AKS"
  type        = string
  default     = "10.1.0.0/16"
}

variable "aks_dns_service_ip" {
  description = "DNS service IP for AKS (must be within service CIDR)"
  type        = string
  default     = "10.1.0.10"
}

variable "aks_pod_cidr" {
  description = "Pod CIDR for AKS (only used with kubenet)"
  type        = string
  default     = ""
}

variable "aks_outbound_type" {
  description = "Outbound type for AKS (loadBalancer, userDefinedRouting, or managedNATGateway)"
  type        = string
  default     = "loadBalancer"
}

variable "aks_enable_azure_policy" {
  description = "Enable Azure Policy for AKS"
  type        = bool
  default     = true
}

variable "aks_enable_defender" {
  description = "Enable Microsoft Defender for Containers"
  type        = bool
  default     = true
}

# Azure Container Instances (ACI) Variables
variable "enable_aci" {
  description = "Enable Azure Container Instances"
  type        = bool
  default     = false
}

variable "aci_mcp_container_image" {
  description = "Container image for ACI MCP server"
  type        = string
  default     = "mcr.microsoft.com/azuredocs/aci-helloworld:latest"
}

variable "aci_mcp_cpu_cores" {
  description = "Number of CPU cores for ACI MCP server"
  type        = number
  default     = 2
}

variable "aci_mcp_memory_in_gb" {
  description = "Memory in GB for ACI MCP server"
  type        = number
  default     = 4
}

variable "aci_mcp_port" {
  description = "Port for ACI MCP server"
  type        = number
  default     = 5100
}

variable "aci_restart_policy" {
  description = "Restart policy for ACI (Always, OnFailure, Never)"
  type        = string
  default     = "Always"
}

variable "aci_enable_vnet_integration" {
  description = "Enable VNet integration for ACI"
  type        = bool
  default     = true
}

variable "aci_mcp_environment_variables" {
  description = "Environment variables for ACI MCP server"
  type = list(object({
    name  = string
    value = string
  }))
  default = []
}

variable "aci_mcp_secure_environment_variables" {
  description = "Secure environment variables for ACI MCP server"
  type = list(object({
    name  = string
    value = string
  }))
  default   = []
  sensitive = true
}

variable "aci_enable_liveness_probe" {
  description = "Enable liveness probe for ACI"
  type        = bool
  default     = true
}

variable "aci_liveness_probe_path" {
  description = "Path for ACI liveness probe"
  type        = string
  default     = "/health"
}

variable "aci_enable_readiness_probe" {
  description = "Enable readiness probe for ACI"
  type        = bool
  default     = true
}

variable "aci_readiness_probe_path" {
  description = "Path for ACI readiness probe"
  type        = string
  default     = "/ready"
}