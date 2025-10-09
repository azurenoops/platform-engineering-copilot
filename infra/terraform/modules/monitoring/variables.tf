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

variable "log_analytics_sku" {
  description = "The SKU for the Log Analytics workspace"
  type        = string
  default     = "PerGB2018"
}

variable "log_retention_days" {
  description = "The number of days to retain logs"
  type        = number
  default     = 30
}

variable "application_type" {
  description = "The type of application for Application Insights"
  type        = string
  default     = "web"
}

variable "alert_email_addresses" {
  description = "List of email addresses to send alerts to"
  type        = list(string)
  default     = []
}

variable "app_service_id" {
  description = "The ID of the App Service to monitor"
  type        = string
  default     = null
}

variable "sql_database_id" {
  description = "The ID of the SQL Database to monitor"
  type        = string
  default     = null
}

variable "api_url" {
  description = "The URL of the API for availability testing"
  type        = string
  default     = null
}

variable "cpu_threshold" {
  description = "CPU percentage threshold for alerts"
  type        = number
  default     = 80
}

variable "memory_threshold" {
  description = "Memory percentage threshold for alerts"
  type        = number
  default     = 80
}

variable "response_time_threshold" {
  description = "Response time threshold in seconds for alerts"
  type        = number
  default     = 5
}

variable "sql_dtu_threshold" {
  description = "SQL Database DTU percentage threshold for alerts"
  type        = number
  default     = 80
}

variable "tags" {
  description = "A mapping of tags to assign to the resources"
  type        = map(string)
  default     = {}
}