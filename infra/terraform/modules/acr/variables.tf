# ==============================================================================
# Variables for Azure Container Registry Module
# ==============================================================================

variable "registry_name" {
  description = "Name of the Azure Container Registry"
  type        = string
  validation {
    condition     = can(regex("^[a-zA-Z0-9]+$", var.registry_name))
    error_message = "ACR name must contain only alphanumeric characters."
  }
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "location" {
  description = "Azure region for the registry"
  type        = string
}

variable "sku" {
  description = "SKU for the container registry (Basic, Standard, Premium)"
  type        = string
  default     = "Premium"
  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.sku)
    error_message = "SKU must be Basic, Standard, or Premium."
  }
}

variable "admin_enabled" {
  description = "Enable admin user (not recommended for production)"
  type        = bool
  default     = false
}

variable "public_network_access_enabled" {
  description = "Enable public network access"
  type        = bool
  default     = false
}

variable "zone_redundancy_enabled" {
  description = "Enable zone redundancy (Premium SKU only)"
  type        = bool
  default     = true
}

variable "network_rule_set_enabled" {
  description = "Enable network rule set"
  type        = bool
  default     = true
}

variable "network_rule_default_action" {
  description = "Default action for network rules (Allow or Deny)"
  type        = string
  default     = "Deny"
}

variable "allowed_ip_ranges" {
  description = "List of allowed IP ranges"
  type        = list(string)
  default     = []
}

variable "allowed_subnet_ids" {
  description = "List of allowed subnet IDs"
  type        = list(string)
  default     = []
}

variable "georeplications" {
  description = "List of geo-replication locations"
  type = list(object({
    location                = string
    zone_redundancy_enabled = bool
  }))
  default = []
}

variable "encryption_enabled" {
  description = "Enable customer-managed key encryption"
  type        = bool
  default     = false
}

variable "encryption_key_vault_key_id" {
  description = "Key Vault key ID for encryption"
  type        = string
  default     = ""
}

variable "encryption_identity_client_id" {
  description = "Client ID of managed identity for encryption"
  type        = string
  default     = ""
}

variable "content_trust_enabled" {
  description = "Enable content trust (Notary v2)"
  type        = bool
  default     = true
}

variable "retention_days" {
  description = "Number of days to retain untagged manifests"
  type        = number
  default     = 7
}

variable "retention_policy_enabled" {
  description = "Enable retention policy"
  type        = bool
  default     = true
}

variable "quarantine_enabled" {
  description = "Enable quarantine policy for security scanning"
  type        = bool
  default     = true
}

variable "data_endpoint_enabled" {
  description = "Enable data endpoint for private link"
  type        = bool
  default     = false
}

variable "enable_private_endpoint" {
  description = "Enable private endpoint"
  type        = bool
  default     = true
}

variable "private_endpoint_subnet_id" {
  description = "Subnet ID for private endpoint"
  type        = string
  default     = ""
}

variable "private_dns_zone_ids" {
  description = "List of private DNS zone IDs"
  type        = list(string)
  default     = []
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID for diagnostics"
  type        = string
  default     = ""
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}
