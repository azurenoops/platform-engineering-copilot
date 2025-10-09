# Variables for Network Module

variable "vnet_name" {
  description = "Name of the Virtual Network"
  type        = string
}

variable "location" {
  description = "Azure region for resources"
  type        = string
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
}

variable "vnet_address_prefix" {
  description = "Virtual Network address prefix"
  type        = string
  default     = "10.0.0.0/16"
}

variable "app_service_subnet_prefix" {
  description = "App Service subnet address prefix"
  type        = string
  default     = "10.0.1.0/24"
}

variable "private_endpoint_subnet_prefix" {
  description = "Private endpoint subnet address prefix"
  type        = string
  default     = "10.0.2.0/24"
}

variable "management_subnet_prefix" {
  description = "Management subnet address prefix"
  type        = string
  default     = "10.0.3.0/24"
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}