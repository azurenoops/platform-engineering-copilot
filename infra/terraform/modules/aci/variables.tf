variable "container_group_name" {
  description = "Name of the container group"
  type        = string
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
}

variable "container_image" {
  description = "Container image (from ACR or public registry)"
  type        = string
}

variable "container_name" {
  description = "Name of the container"
  type        = string
}

variable "cpu_cores" {
  description = "Number of CPU cores"
  type        = number
  default     = 2
}

variable "memory_in_gb" {
  description = "Memory in GB"
  type        = number
  default     = 4
}

variable "port" {
  description = "Container port"
  type        = number
  default     = 5100
}

variable "os_type" {
  description = "OS type (Linux or Windows)"
  type        = string
  default     = "Linux"
}

variable "restart_policy" {
  description = "Restart policy (Always, OnFailure, Never)"
  type        = string
  default     = "Always"
}

variable "use_managed_identity" {
  description = "Use managed identity for ACR authentication"
  type        = bool
  default     = true
}

variable "acr_login_server" {
  description = "ACR login server"
  type        = string
  default     = ""
}

variable "acr_username" {
  description = "ACR username (if not using managed identity)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "acr_password" {
  description = "ACR password (if not using managed identity)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "acr_id" {
  description = "ACR resource ID for role assignment"
  type        = string
  default     = ""
}

variable "environment_variables" {
  description = "List of environment variables"
  type = list(object({
    name  = string
    value = string
  }))
  default = []
}

variable "secure_environment_variables" {
  description = "List of secure environment variables"
  type = list(object({
    name  = string
    value = string
  }))
  default   = []
  sensitive = true
}

variable "enable_vnet_integration" {
  description = "Enable VNet integration"
  type        = bool
  default     = false
}

variable "subnet_id" {
  description = "Subnet ID for VNet integration"
  type        = string
  default     = ""
}

variable "dns_name_label" {
  description = "DNS name label for public IP"
  type        = string
  default     = ""
}

variable "enable_liveness_probe" {
  description = "Enable liveness probe"
  type        = bool
  default     = true
}

variable "liveness_probe_path" {
  description = "Path for liveness probe"
  type        = string
  default     = "/health"
}

variable "enable_readiness_probe" {
  description = "Enable readiness probe"
  type        = bool
  default     = true
}

variable "readiness_probe_path" {
  description = "Path for readiness probe"
  type        = string
  default     = "/ready"
}

variable "volumes" {
  description = "List of volumes to mount"
  type = list(object({
    name                 = string
    mount_path           = string
    read_only            = bool
    share_name           = string
    storage_account_name = string
    storage_account_key  = string
  }))
  default = []
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID"
  type        = string
}

variable "log_analytics_workspace_key" {
  description = "Log Analytics workspace key"
  type        = string
  sensitive   = true
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}
