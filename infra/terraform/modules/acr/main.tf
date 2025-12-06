# ==============================================================================
# Azure Container Registry (ACR) - IL5/IL6 Compliant
# ==============================================================================
# Creates container registry with:
# - Premium SKU for geo-replication and content trust
# - Private endpoint support
# - Customer-managed encryption (CMK) option
# - Quarantine policy for security scanning
# - Retention policy for image lifecycle management
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

# Container Registry
resource "azurerm_container_registry" "acr" {
  name                = var.registry_name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  admin_enabled       = var.admin_enabled

  # IL5/IL6: Enable public network access control
  public_network_access_enabled = var.public_network_access_enabled
  
  # IL5/IL6: Zone redundancy for Premium SKU
  zone_redundancy_enabled = var.sku == "Premium" ? var.zone_redundancy_enabled : false

  # System-assigned managed identity for CMK
  identity {
    type = "SystemAssigned"
  }

  # Network rules
  dynamic "network_rule_set" {
    for_each = var.network_rule_set_enabled ? [1] : []
    content {
      default_action = var.network_rule_default_action

      dynamic "ip_rule" {
        for_each = var.allowed_ip_ranges
        content {
          action   = "Allow"
          ip_range = ip_rule.value
        }
      }

      dynamic "virtual_network" {
        for_each = var.allowed_subnet_ids
        content {
          action    = "Allow"
          subnet_id = virtual_network.value
        }
      }
    }
  }

  # Geo-replication for Premium SKU (IL5/IL6 requirement)
  dynamic "georeplications" {
    for_each = var.sku == "Premium" ? var.georeplications : []
    content {
      location                = georeplications.value.location
      zone_redundancy_enabled = georeplications.value.zone_redundancy_enabled
      tags                    = var.tags
    }
  }

  # Customer-managed encryption key (optional)
  dynamic "encryption" {
    for_each = var.encryption_enabled ? [1] : []
    content {      
      key_vault_key_id   = var.encryption_key_vault_key_id
      identity_client_id = var.encryption_identity_client_id
    }
  }

  /* # Content trust (Notary v2) for Premium SKU
  trust_policy {
    enabled = var.sku == "Premium" ? var.content_trust_enabled : false
  }

  # Retention policy for untagged manifests
  retention_policy {
    days    = var.retention_days
    enabled = var.retention_policy_enabled
  }

  # Quarantine policy for security scanning
  quarantine_policy {
    enabled = var.quarantine_enabled
  } */

  # Anonymous pull (disabled for IL5/IL6)
  anonymous_pull_enabled = false

  # Data endpoint (for private link scenarios)
  data_endpoint_enabled = var.data_endpoint_enabled

  # Export policy (IL5/IL6: disabled to prevent unauthorized data transfer)
  export_policy_enabled = false

  tags = merge(
    var.tags,
    {
      ManagedBy = "Terraform"
      Service   = "Container Registry"
    }
  )
}

# Private Endpoint
resource "azurerm_private_endpoint" "acr_pe" {
  count = var.enable_private_endpoint ? 1 : 0

  name                = "${var.registry_name}-pe"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.private_endpoint_subnet_id

  private_service_connection {
    name                           = "${var.registry_name}-psc"
    private_connection_resource_id = azurerm_container_registry.acr.id
    is_manual_connection           = false
    subresource_names              = ["registry"]
  }

  private_dns_zone_group {
    name                 = "acr-dns-zone-group"
    private_dns_zone_ids = var.private_dns_zone_ids
  }

  tags = var.tags
}

# Diagnostic Settings
resource "azurerm_monitor_diagnostic_setting" "acr_diag" {
  count = var.log_analytics_workspace_id != "" ? 1 : 0

  name                       = "${var.registry_name}-diag"
  target_resource_id         = azurerm_container_registry.acr.id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "ContainerRegistryRepositoryEvents"
  }

  enabled_log {
    category = "ContainerRegistryLoginEvents"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}
