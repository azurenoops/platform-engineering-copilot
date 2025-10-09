terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

resource "random_string" "storage_suffix" {
  length  = 4
  special = false
  upper   = false
}

resource "azurerm_storage_account" "main" {
  name                     = "${var.project_name}st${var.environment}${random_string.storage_suffix.result}"
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = var.account_tier
  account_replication_type = var.replication_type
  account_kind             = var.account_kind
  
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = var.shared_access_key_enabled

  blob_properties {
    delete_retention_policy {
      days = var.blob_delete_retention_days
    }
    container_delete_retention_policy {
      days = var.container_delete_retention_days
    }
    versioning_enabled  = var.versioning_enabled
    change_feed_enabled = var.change_feed_enabled
  }

  network_rules {
    default_action             = var.network_default_action
    ip_rules                   = var.allowed_ip_addresses
    virtual_network_subnet_ids = var.allowed_subnet_ids
    bypass                     = ["AzureServices"]
  }

  tags = var.tags
}

# Storage containers
resource "azurerm_storage_container" "logs" {
  count                 = var.create_logs_container ? 1 : 0
  name                  = "logs"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "backups" {
  count                 = var.create_backups_container ? 1 : 0
  name                  = "backups"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "documents" {
  count                 = var.create_documents_container ? 1 : 0
  name                  = "documents"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

# Storage tables for structured data
resource "azurerm_storage_table" "audit_logs" {
  count                = var.create_audit_table ? 1 : 0
  name                 = "auditlogs"
  storage_account_name = azurerm_storage_account.main.name
}

resource "azurerm_storage_table" "metrics" {
  count                = var.create_metrics_table ? 1 : 0
  name                 = "metrics"
  storage_account_name = azurerm_storage_account.main.name
}

# Storage queues for message processing
resource "azurerm_storage_queue" "notifications" {
  count                = var.create_notifications_queue ? 1 : 0
  name                 = "notifications"
  storage_account_name = azurerm_storage_account.main.name
}

resource "azurerm_storage_queue" "processing" {
  count                = var.create_processing_queue ? 1 : 0
  name                 = "processing"
  storage_account_name = azurerm_storage_account.main.name
}

# Diagnostic settings
resource "azurerm_monitor_diagnostic_setting" "storage_account" {
  count                          = var.log_analytics_workspace_id != null ? 1 : 0
  name                           = "storage-account-diagnostics"
  target_resource_id             = azurerm_storage_account.main.id
  log_analytics_workspace_id     = var.log_analytics_workspace_id

  enabled_log {
    category = "StorageWrite"
  }

  enabled_log {
    category = "StorageRead"
  }

  enabled_log {
    category = "StorageDelete"
  }

  metric {
    category = "Transaction"
    enabled  = true
  }
}