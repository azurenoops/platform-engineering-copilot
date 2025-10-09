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

variable "admin_login" {
  description = "The administrator login for the SQL server"
  type        = string
  sensitive   = true
}

variable "admin_password" {
  description = "The administrator password for the SQL server"
  type        = string
  sensitive   = true
}

variable "azure_ad_admin_login" {
  description = "The Azure AD administrator login"
  type        = string
}

variable "azure_ad_admin_object_id" {
  description = "The Azure AD administrator object ID"
  type        = string
}

variable "max_size_gb" {
  description = "The maximum size of the database in GB"
  type        = number
  default     = 2
}

variable "sku_name" {
  description = "The SKU name for the database"
  type        = string
  default     = "S0"
}

variable "zone_redundant" {
  description = "Whether the database should be zone redundant"
  type        = bool
  default     = false
}

variable "threat_detection_emails" {
  description = "List of email addresses for threat detection alerts"
  type        = list(string)
  default     = []
}

variable "storage_account_access_key" {
  description = "Storage account access key for threat detection"
  type        = string
  sensitive   = true
  default     = null
}

variable "storage_endpoint" {
  description = "Storage endpoint for threat detection"
  type        = string
  default     = null
}

variable "app_service_outbound_ip_addresses" {
  description = "List of App Service outbound IP addresses"
  type        = list(string)
  default     = []
}

variable "subnet_id" {
  description = "The subnet ID for VNet integration"
  type        = string
  default     = null
}

variable "tags" {
  description = "A mapping of tags to assign to the resources"
  type        = map(string)
  default     = {}
}