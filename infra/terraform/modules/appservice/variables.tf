variable "project_name" {
  description = "The name of the project"
  type        = string
}

variable "environment" {
  description = "The environment (dev, staging, prod)"
  type        = string
}

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

variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "location" {
  description = "The Azure region where resources will be created"
  type        = string
}

variable "sku_name" {
  description = "The SKU name for the App Service Plan"
  type        = string
  default     = "B1"
}

variable "always_on" {
  description = "Should the App Service always be on"
  type        = bool
  default     = false
}

variable "cors_allowed_origins" {
  description = "List of allowed origins for CORS"
  type        = list(string)
  default     = ["*"]
}

variable "application_insights_connection_string" {
  description = "Application Insights connection string"
  type        = string
  sensitive   = true
  default     = null
}

variable "database_connection_string" {
  description = "Database connection string"
  type        = string
  sensitive   = true
}

variable "api_app_settings" {
  description = "Additional app settings for the API app service"
  type        = map(string)
  default     = {}
}

variable "mcp_app_settings" {
  description = "Additional app settings for the MCP app service"
  type        = map(string)
  default     = {}
}

variable "chat_app_settings" {
  description = "Additional app settings for the Chat app service"
  type        = map(string)
  default     = {}
}

variable "log_retention_days" {
  description = "Number of days to retain logs"
  type        = number
  default     = 30
}

variable "subnet_id" {
  description = "The subnet ID for VNet integration"
  type        = string
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