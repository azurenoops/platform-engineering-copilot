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

variable "account_tier" {
  description = "The tier to use for this storage account"
  type        = string
  default     = "Standard"
}

variable "replication_type" {
  description = "The type of replication to use for this storage account"
  type        = string
  default     = "LRS"
}

variable "account_kind" {
  description = "The kind of storage account"
  type        = string
  default     = "StorageV2"
}

variable "shared_access_key_enabled" {
  description = "Indicates whether the storage account permits requests to be authorized with the account access key via Shared Key"
  type        = bool
  default     = true
}

variable "blob_delete_retention_days" {
  description = "Number of days to retain deleted blobs"
  type        = number
  default     = 7
}

variable "container_delete_retention_days" {
  description = "Number of days to retain deleted containers"
  type        = number
  default     = 7
}

variable "versioning_enabled" {
  description = "Is versioning enabled for blobs"
  type        = bool
  default     = false
}

variable "change_feed_enabled" {
  description = "Is the blob service properties for change feed events enabled"
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
  description = "List of IP addresses allowed to access the storage account"
  type        = list(string)
  default     = []
}

variable "allowed_subnet_ids" {
  description = "List of subnet IDs allowed to access the storage account"
  type        = list(string)
  default     = []
}

variable "create_logs_container" {
  description = "Create a container for logs"
  type        = bool
  default     = true
}

variable "create_backups_container" {
  description = "Create a container for backups"
  type        = bool
  default     = true
}

variable "create_documents_container" {
  description = "Create a container for documents"
  type        = bool
  default     = false
}

variable "create_audit_table" {
  description = "Create a table for audit logs"
  type        = bool
  default     = true
}

variable "create_metrics_table" {
  description = "Create a table for metrics"
  type        = bool
  default     = false
}

variable "create_notifications_queue" {
  description = "Create a queue for notifications"
  type        = bool
  default     = false
}

variable "create_processing_queue" {
  description = "Create a queue for processing"
  type        = bool
  default     = false
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