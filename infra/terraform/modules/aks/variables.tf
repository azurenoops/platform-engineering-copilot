# ==============================================================================
# Variables for Azure Kubernetes Service Module
# ==============================================================================

variable "cluster_name" {
  description = "Name of the AKS cluster"
  type        = string
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "location" {
  description = "Azure region for the cluster"
  type        = string
}

variable "kubernetes_version" {
  description = "Kubernetes version"
  type        = string
  default     = "1.28.3"
}

variable "dns_prefix" {
  description = "DNS prefix for the cluster"
  type        = string
}

variable "enable_private_cluster" {
  description = "Enable private cluster"
  type        = bool
  default     = true
}

variable "system_node_vm_size" {
  description = "VM size for system node pool"
  type        = string
  default     = "Standard_D4s_v5"
}

variable "system_node_count" {
  description = "Number of system nodes"
  type        = number
  default     = 3
}

variable "user_node_vm_size" {
  description = "VM size for user node pool"
  type        = string
  default     = "Standard_D8s_v5"
}

variable "user_node_count" {
  description = "Initial number of user nodes"
  type        = number
  default     = 3
}

variable "enable_auto_scaling" {
  description = "Enable auto-scaling for user node pool"
  type        = bool
  default     = true
}

variable "min_node_count" {
  description = "Minimum node count for auto-scaling"
  type        = number
  default     = 3
}

variable "max_node_count" {
  description = "Maximum node count for auto-scaling"
  type        = number
  default     = 10
}

variable "subnet_id" {
  description = "Subnet ID for AKS nodes"
  type        = string
}

variable "availability_zones" {
  description = "Availability zones for node pools"
  type        = list(string)
  default     = ["1", "2", "3"]
}

variable "enable_azure_rbac" {
  description = "Enable Azure RBAC for Kubernetes authorization"
  type        = bool
  default     = true
}

variable "aad_admin_group_object_ids" {
  description = "Azure AD admin group object IDs"
  type        = list(string)
  default     = []
}

variable "network_plugin" {
  description = "Network plugin (azure or kubenet)"
  type        = string
  default     = "azure"
}

variable "network_policy" {
  description = "Network policy (azure, calico, or none)"
  type        = string
  default     = "azure"
}

variable "service_cidr" {
  description = "CIDR for Kubernetes services"
  type        = string
  default     = "10.0.0.0/16"
}

variable "dns_service_ip" {
  description = "DNS service IP address"
  type        = string
  default     = "10.0.0.10"
}

variable "pod_cidr" {
  description = "CIDR for pods (kubenet only)"
  type        = string
  default     = null
}

variable "outbound_type" {
  description = "Outbound traffic type"
  type        = string
  default     = "loadBalancer"
}

variable "enable_azure_policy" {
  description = "Enable Azure Policy add-on"
  type        = bool
  default     = true
}

variable "enable_defender" {
  description = "Enable Microsoft Defender for Containers"
  type        = bool
  default     = true
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID"
  type        = string
}

variable "acr_id" {
  description = "Azure Container Registry ID for pull access"
  type        = string
  default     = ""
}

variable "automatic_channel_upgrade" {
  description = "Automatic upgrade channel (patch, stable, rapid, node-image)"
  type        = string
  default     = "stable"
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}
