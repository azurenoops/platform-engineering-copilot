terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "main" {
  name                       = "${var.project_name}-kv-${var.environment}-${random_string.key_vault_suffix.result}"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = var.sku_name
  soft_delete_retention_days = var.soft_delete_retention_days
  purge_protection_enabled   = var.purge_protection_enabled

  network_acls {
    default_action             = var.network_default_action
    bypass                     = "AzureServices"
    ip_rules                   = var.allowed_ip_addresses
    virtual_network_subnet_ids = var.allowed_subnet_ids
  }

  tags = var.tags
}

resource "random_string" "key_vault_suffix" {
  length  = 4
  special = false
  upper   = false
}

# Access policy for the admin user
resource "azurerm_key_vault_access_policy" "admin" {
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = var.admin_object_id

  key_permissions = [
    "Backup",
    "Create",
    "Decrypt",
    "Delete",
    "Encrypt",
    "Get",
    "Import",
    "List",
    "Purge",
    "Recover",
    "Restore",
    "Sign",
    "UnwrapKey",
    "Update",
    "Verify",
    "WrapKey"
  ]

  secret_permissions = [
    "Backup",
    "Delete",
    "Get",
    "List",
    "Purge",
    "Recover",
    "Restore",
    "Set"
  ]

  certificate_permissions = [
    "Backup",
    "Create",
    "Delete",
    "DeleteIssuers",
    "Get",
    "GetIssuers",
    "Import",
    "List",
    "ListIssuers",
    "ManageContacts",
    "ManageIssuers",
    "Purge",
    "Recover",
    "Restore",
    "SetIssuers",
    "Update"
  ]
}

# Access policy for App Services (if principal IDs provided)
resource "azurerm_key_vault_access_policy" "app_services" {
  count        = length(var.app_service_principal_ids)
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = var.app_service_principal_ids[count.index]

  secret_permissions = [
    "Get",
    "List"
  ]
}

# Store connection strings and secrets
resource "azurerm_key_vault_secret" "database_connection_string" {
  count        = var.database_connection_string != null ? 1 : 0
  name         = "DatabaseConnectionString"
  value        = var.database_connection_string
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_key_vault_access_policy.admin]

  tags = var.tags
}

resource "azurerm_key_vault_secret" "application_insights_connection_string" {
  count        = var.application_insights_connection_string != null ? 1 : 0
  name         = "ApplicationInsightsConnectionString"
  value        = var.application_insights_connection_string
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_key_vault_access_policy.admin]

  tags = var.tags
}

resource "azurerm_key_vault_secret" "application_insights_instrumentation_key" {
  count        = var.application_insights_instrumentation_key != null ? 1 : 0
  name         = "ApplicationInsightsInstrumentationKey"
  value        = var.application_insights_instrumentation_key
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_key_vault_access_policy.admin]

  tags = var.tags
}

# Diagnostic settings
resource "azurerm_monitor_diagnostic_setting" "key_vault" {
  count                      = var.log_analytics_workspace_id != null ? 1 : 0
  name                       = "key-vault-diagnostics"
  target_resource_id         = azurerm_key_vault.main.id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "AuditEvent"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}