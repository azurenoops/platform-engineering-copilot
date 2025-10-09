terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

resource "azurerm_mssql_server" "main" {
  name                         = "${var.project_name}-sql-${var.environment}"
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.admin_login
  administrator_login_password = var.admin_password

  azuread_administrator {
    login_username = var.azure_ad_admin_login
    object_id      = var.azure_ad_admin_object_id
  }

  identity {
    type = "SystemAssigned"
  }

  tags = var.tags
}

resource "azurerm_mssql_database" "main" {
  name           = "${var.project_name}-db-${var.environment}"
  server_id      = azurerm_mssql_server.main.id
  collation      = "SQL_Latin1_General_CP1_CI_AS"
  license_type   = "LicenseIncluded"
  max_size_gb    = var.max_size_gb
  sku_name       = var.sku_name
  zone_redundant = var.zone_redundant

  threat_detection_policy {
    state                      = "Enabled"
    email_account_admins       = "Enabled"
    email_addresses            = var.threat_detection_emails
    retention_days             = 30
    storage_account_access_key = var.storage_account_access_key
    storage_endpoint           = var.storage_endpoint
  }

  tags = var.tags
}

resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_mssql_firewall_rule" "app_service" {
  count            = length(var.app_service_outbound_ip_addresses)
  name             = "AllowAppService${count.index}"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = var.app_service_outbound_ip_addresses[count.index]
  end_ip_address   = var.app_service_outbound_ip_addresses[count.index]
}

resource "azurerm_mssql_virtual_network_rule" "main" {
  count     = var.subnet_id != null ? 1 : 0
  name      = "sql-vnet-rule"
  server_id = azurerm_mssql_server.main.id
  subnet_id = var.subnet_id
}