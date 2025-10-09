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