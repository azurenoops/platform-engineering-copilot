# Resource Group
resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location

  tags = local.common_tags
}

# Network module
module "network" {
  source = "./modules/network"

  vnet_name                      = "${var.project_name}-vnet-${var.environment}"
  environment                    = var.environment
  resource_group_name            = azurerm_resource_group.main.name
  location                       = azurerm_resource_group.main.location
  vnet_address_prefix            = var.vnet_address_prefix
  app_service_subnet_prefix      = var.app_service_subnet_prefix
  private_endpoint_subnet_prefix = var.private_endpoint_subnet_prefix
  management_subnet_prefix       = var.management_subnet_prefix

  tags = local.common_tags
}

# Monitoring module
module "monitoring" {
  source = "./modules/monitoring"

  project_name        = var.project_name
  environment         = var.environment
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  
  alert_email_addresses = var.alert_email_addresses

  tags = local.common_tags
}

# Storage module
module "storage" {
  source = "./modules/storage"

  project_name        = var.project_name
  environment         = var.environment
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id

  tags = local.common_tags
}

# SQL Database module
module "sql" {
  source = "./modules/sql"

  project_name               = var.project_name
  environment                = var.environment
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  
  admin_login                = var.sql_admin_login
  admin_password             = var.sql_admin_password
  azure_ad_admin_login       = var.azure_ad_admin_login
  azure_ad_admin_object_id   = var.azure_ad_admin_object_id
  
  subnet_id                  = module.network.private_endpoint_subnet_id
  storage_account_access_key = module.storage.storage_account_primary_access_key
  storage_endpoint           = module.storage.storage_account_primary_blob_endpoint

  tags = local.common_tags

  depends_on = [module.storage]
}

# App Service module
module "appservice" {
  source = "./modules/appservice"

  project_name                          = var.project_name
  environment                           = var.environment
  resource_group_name                   = azurerm_resource_group.main.name
  location                              = azurerm_resource_group.main.location
  
  application_insights_connection_string = module.monitoring.application_insights_connection_string
  database_connection_string             = module.sql.connection_string
  
  subnet_id                             = module.network.app_service_subnet_id
  log_analytics_workspace_id            = module.monitoring.log_analytics_workspace_id

  tags = local.common_tags

  depends_on = [module.monitoring, module.sql]
}

# Update SQL firewall rules after App Service is created
resource "azurerm_mssql_firewall_rule" "app_service_api" {
  count            = length(module.appservice.api_app_service_outbound_ip_addresses)
  name             = "AllowAppServiceAPI${count.index}"
  server_id        = module.sql.sql_server_id
  start_ip_address = module.appservice.api_app_service_outbound_ip_addresses[count.index]
  end_ip_address   = module.appservice.api_app_service_outbound_ip_addresses[count.index]

  depends_on = [module.appservice]
}

resource "azurerm_mssql_firewall_rule" "app_service_mcp" {
  count            = length(module.appservice.mcp_app_service_outbound_ip_addresses)
  name             = "AllowAppServiceMCP${count.index}"
  server_id        = module.sql.sql_server_id
  start_ip_address = module.appservice.mcp_app_service_outbound_ip_addresses[count.index]
  end_ip_address   = module.appservice.mcp_app_service_outbound_ip_addresses[count.index]

  depends_on = [module.appservice]
}

# Key Vault module
module "keyvault" {
  source = "./modules/keyvault"

  project_name        = var.project_name
  environment         = var.environment
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  
  admin_object_id                           = var.key_vault_admin_object_id
  app_service_principal_ids                 = [
    module.appservice.api_app_service_identity_principal_id,
    module.appservice.mcp_app_service_identity_principal_id
  ]
  
  database_connection_string                = module.sql.connection_string
  application_insights_connection_string    = module.monitoring.application_insights_connection_string
  application_insights_instrumentation_key  = module.monitoring.application_insights_instrumentation_key
  
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id

  tags = local.common_tags

  depends_on = [module.appservice, module.sql, module.monitoring]
}
