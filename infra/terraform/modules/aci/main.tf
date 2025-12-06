# ==============================================================================
# Azure Container Instances (ACI) - IL5/IL6 Compliant
# ==============================================================================
# Creates container instances with:
# - VNet integration for private networking
# - Managed identity for ACR access
# - Log Analytics integration
# - Health probes (liveness and readiness)
# ==============================================================================

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.80"
    }
  }
}

# Container Group
resource "azurerm_container_group" "aci" {
  name                = var.container_group_name
  location            = var.location
  resource_group_name = var.resource_group_name
  os_type             = var.os_type
  restart_policy      = var.restart_policy

  # Managed identity for ACR pull
  identity {
    type = var.use_managed_identity ? "SystemAssigned" : null
  }

  # Image registry credentials (if not using managed identity)
  dynamic "image_registry_credential" {
    for_each = !var.use_managed_identity && var.acr_login_server != "" ? [1] : []
    content {
      server   = var.acr_login_server
      username = var.acr_username
      password = var.acr_password
    }
  }

  # Main container
  container {
    name   = var.container_name
    image  = var.container_image
    cpu    = var.cpu_cores
    memory = var.memory_in_gb

    ports {
      port     = var.port
      protocol = "TCP"
    }

    # Environment variables
    dynamic "environment_variables" {
      for_each = { for env in var.environment_variables : env.name => env.value }
      content {
        name  = environment_variables.key
        value = environment_variables.value
      }
    }

    # Secure environment variables
    dynamic "secure_environment_variables" {
      for_each = { for env in var.secure_environment_variables : env.name => env.value }
      content {
        name  = secure_environment_variables.key
        value = secure_environment_variables.value
      }
    }

    # Liveness probe
    dynamic "liveness_probe" {
      for_each = var.enable_liveness_probe ? [1] : []
      content {
        http_get {
          path   = var.liveness_probe_path
          port   = var.port
          scheme = "Http"
        }
        initial_delay_seconds = 30
        period_seconds        = 10
        failure_threshold     = 3
        timeout_seconds       = 5
      }
    }

    # Readiness probe
    dynamic "readiness_probe" {
      for_each = var.enable_readiness_probe ? [1] : []
      content {
        http_get {
          path   = var.readiness_probe_path
          port   = var.port
          scheme = "Http"
        }
        initial_delay_seconds = 10
        period_seconds        = 5
        failure_threshold     = 3
        timeout_seconds       = 3
      }
    }

    # Volume mounts
    dynamic "volume" {
      for_each = var.volumes
      content {
        name       = volume.value.name
        mount_path = volume.value.mount_path
        read_only  = volume.value.read_only
        share_name = volume.value.share_name
        storage_account_name = volume.value.storage_account_name
        storage_account_key  = volume.value.storage_account_key
      }
    }
  }

  # IP address configuration
  ip_address_type = var.enable_vnet_integration ? "Private" : "Public"
  
  dynamic "subnet_ids" {
    for_each = var.enable_vnet_integration && var.subnet_id != "" ? [var.subnet_id] : []
    content {
      value = subnet_ids.value
    }
  }

  dns_name_label = !var.enable_vnet_integration && var.dns_name_label != "" ? var.dns_name_label : null

  # Log Analytics integration
  diagnostics {
    log_analytics {
      workspace_id  = var.log_analytics_workspace_id
      workspace_key = var.log_analytics_workspace_key
    }
  }

  tags = merge(
    var.tags,
    {
      ManagedBy = "Terraform"
      Service   = "Container Instance"
    }
  )
}

# Role Assignment: ACI to ACR (if using managed identity)
resource "azurerm_role_assignment" "aci_acr_pull" {
  count = var.use_managed_identity && var.acr_id != "" ? 1 : 0

  scope                = var.acr_id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_container_group.aci.identity[0].principal_id
}
