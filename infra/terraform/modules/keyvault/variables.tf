variable "project_name" {
  description = "The name of the project"
  type        = string
}

variable "environment" {
  description = "The environment (dev, staging, prod)"
  type        = string
}

variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "location" {
  description = "The Azure region where resources will be created"
  type        = string
}

variable "sku_name" {
  description = "The SKU name for the Key Vault"
  type        = string
  default     = "standard"
}

variable "soft_delete_retention_days" {
  description = "The number of days that items should be retained for once soft-deleted"
  type        = number
  default     = 7
}

variable "purge_protection_enabled" {
  description = "Whether purge protection is enabled for this Key Vault"
  type        = bool
  default     = false
}

variable "network_default_action" {
  description = "The default action to use when no rules match from ip_rules / virtual_network_subnet_ids"
  type        = string
  default     = "Allow"
  validation {
    condition     = contains(["Allow", "Deny"], var.network_default_action)
    error_message = "Network default action must be either 'Allow' or 'Deny'."
  }
}

variable "allowed_ip_addresses" {
  description = "List of IP addresses allowed to access the Key Vault"
  type        = list(string)
  default     = []
}

variable "allowed_subnet_ids" {
  description = "List of subnet IDs allowed to access the Key Vault"
  type        = list(string)
  default     = []
}

variable "admin_object_id" {
  description = "The object ID of the admin user/service principal"
  type        = string
}

variable "app_service_principal_ids" {
  description = "List of App Service managed identity principal IDs"
  type        = list(string)
  default     = []
}

variable "database_connection_string" {
  description = "Database connection string to store in Key Vault"
  type        = string
  sensitive   = true
  default     = null
}

variable "application_insights_connection_string" {
  description = "Application Insights connection string to store in Key Vault"
  type        = string
  sensitive   = true
  default     = null
}

variable "application_insights_instrumentation_key" {
  description = "Application Insights instrumentation key to store in Key Vault"
  type        = string
  sensitive   = true
  default     = null
}

variable "log_analytics_workspace_id" {
  description = "The ID of the Log Analytics workspace to send diagnostics to"
  type        = string
  default     = null
}

variable "tags" {
  description = "A mapping of tags to assign to the resources"
  type        = map(string)
  default     = {}
}