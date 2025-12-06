# Resource Group
resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location

  tags = local.common_tags
}

# ==============================================================================
# Data Sources for Existing Resources
# ==============================================================================

# Existing Virtual Network
data "azurerm_virtual_network" "existing" {
  count               = var.use_existing_network ? 1 : 0
  name                = var.existing_vnet_name
  resource_group_name = var.existing_vnet_resource_group != "" ? var.existing_vnet_resource_group : azurerm_resource_group.main.name
}

data "azurerm_subnet" "existing_app_service" {
  count                = var.use_existing_network ? 1 : 0
  name                 = var.existing_app_service_subnet_name
  virtual_network_name = var.existing_vnet_name
  resource_group_name  = var.existing_vnet_resource_group != "" ? var.existing_vnet_resource_group : azurerm_resource_group.main.name
}

data "azurerm_subnet" "existing_private_endpoint" {
  count                = var.use_existing_network ? 1 : 0
  name                 = var.existing_private_endpoint_subnet_name
  virtual_network_name = var.existing_vnet_name
  resource_group_name  = var.existing_vnet_resource_group != "" ? var.existing_vnet_resource_group : azurerm_resource_group.main.name
}

# Existing Log Analytics Workspace
data "azurerm_log_analytics_workspace" "existing" {
  count               = var.use_existing_log_analytics ? 1 : 0
  name                = var.existing_log_analytics_workspace_name
  resource_group_name = var.existing_log_analytics_resource_group != "" ? var.existing_log_analytics_resource_group : azurerm_resource_group.main.name
}

# Existing Key Vault
data "azurerm_key_vault" "existing" {
  count               = var.use_existing_keyvault ? 1 : 0
  name                = var.existing_keyvault_name
  resource_group_name = var.existing_keyvault_resource_group != "" ? var.existing_keyvault_resource_group : azurerm_resource_group.main.name
}

# ==============================================================================
# Network Module (conditional - only create if not using existing)
# ==============================================================================

module "network" {
  count  = var.use_existing_network ? 0 : 1
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

# Local values for network resources (existing or new)
locals {
  vnet_id                      = var.use_existing_network ? data.azurerm_virtual_network.existing[0].id : module.network[0].vnet_id
  vnet_name                    = var.use_existing_network ? data.azurerm_virtual_network.existing[0].name : module.network[0].vnet_name
  app_service_subnet_id        = var.use_existing_network ? data.azurerm_subnet.existing_app_service[0].id : module.network[0].app_service_subnet_id
  private_endpoint_subnet_id   = var.use_existing_network ? data.azurerm_subnet.existing_private_endpoint[0].id : module.network[0].private_endpoint_subnet_id
}

# ==============================================================================
# Monitoring Module (conditional - only create if not using existing)
# ==============================================================================

module "monitoring" {
  count  = var.use_existing_log_analytics ? 0 : 1
  source = "./modules/monitoring"

  project_name        = var.project_name
  environment         = var.environment
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  
  alert_email_addresses = var.alert_email_addresses

  tags = local.common_tags
}

# Local values for monitoring resources (existing or new)
locals {
  log_analytics_workspace_id              = var.use_existing_log_analytics ? data.azurerm_log_analytics_workspace.existing[0].id : module.monitoring[0].log_analytics_workspace_id
  log_analytics_workspace_name            = var.use_existing_log_analytics ? data.azurerm_log_analytics_workspace.existing[0].name : module.monitoring[0].log_analytics_workspace_name
  application_insights_connection_string  = var.use_existing_log_analytics ? "" : module.monitoring[0].application_insights_connection_string
  application_insights_instrumentation_key = var.use_existing_log_analytics ? "" : module.monitoring[0].application_insights_instrumentation_key
}

# Storage module
module "storage" {
  source = "./modules/storage"

  project_name        = var.project_name
  environment         = var.environment
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  
  log_analytics_workspace_id = local.log_analytics_workspace_id

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
  
  subnet_id                  = local.private_endpoint_subnet_id
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
  
  # Deployment flags
  deploy_admin_api                      = var.deploy_admin_api
  deploy_chat                           = var.deploy_chat
  
  application_insights_connection_string = local.application_insights_connection_string
  database_connection_string             = module.sql.connection_string
  
  subnet_id                             = local.app_service_subnet_id
  log_analytics_workspace_id            = local.log_analytics_workspace_id

  tags = local.common_tags

  depends_on = [module.monitoring, module.sql]
}

# Update SQL firewall rules after App Service is created
resource "azurerm_mssql_firewall_rule" "app_service_api" {
  count            = var.deploy_admin_api ? length(module.appservice.api_app_service_outbound_ip_addresses) : 0
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

resource "azurerm_mssql_firewall_rule" "app_service_chat" {
  count            = var.deploy_chat ? length(module.appservice.chat_app_service_outbound_ip_addresses) : 0
  name             = "AllowAppServiceChat${count.index}"
  server_id        = module.sql.sql_server_id
  start_ip_address = module.appservice.chat_app_service_outbound_ip_addresses[count.index]
  end_ip_address   = module.appservice.chat_app_service_outbound_ip_addresses[count.index]

  depends_on = [module.appservice]
}

# ==============================================================================
# Key Vault Module (conditional - only create if not using existing)
# ==============================================================================

module "keyvault" {
  count  = var.use_existing_keyvault ? 0 : 1
  source = "./modules/keyvault"

  project_name        = var.project_name
  environment         = var.environment
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  
  admin_object_id                           = var.key_vault_admin_object_id
  app_service_principal_ids                 = compact([
    var.deploy_admin_api ? module.appservice.api_app_service_identity_principal_id : null,
    module.appservice.mcp_app_service_identity_principal_id,
    var.deploy_chat ? module.appservice.chat_app_service_identity_principal_id : null
  ])
  
  database_connection_string                = module.sql.connection_string
  application_insights_connection_string    = local.application_insights_connection_string
  application_insights_instrumentation_key  = local.application_insights_instrumentation_key
  
  log_analytics_workspace_id = local.log_analytics_workspace_id

  tags = local.common_tags

  depends_on = [module.appservice, module.sql]
}

# Local values for Key Vault (existing or new)
locals {
  keyvault_id   = var.use_existing_keyvault ? data.azurerm_key_vault.existing[0].id : module.keyvault[0].key_vault_id
  keyvault_name = var.use_existing_keyvault ? data.azurerm_key_vault.existing[0].name : module.keyvault[0].key_vault_name
  keyvault_uri  = var.use_existing_keyvault ? data.azurerm_key_vault.existing[0].vault_uri : module.keyvault[0].key_vault_uri
}

# ==============================================================================
# Container Infrastructure - ACR, AKS, ACI
# ==============================================================================

# Azure Container Registry (ACR)
module "acr" {
  source = "./modules/acr"
  count  = var.enable_container_infrastructure ? 1 : 0

  registry_name       = "${var.project_name}acr${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  
  sku                              = var.acr_sku
  admin_enabled                    = var.acr_admin_enabled
  public_network_access_enabled    = var.acr_public_network_access_enabled
  zone_redundancy_enabled          = var.acr_zone_redundancy_enabled
  
  # Network configuration
  enable_private_endpoint          = var.acr_enable_private_endpoint
  private_endpoint_subnet_id       = local.private_endpoint_subnet_id
  network_rule_set_enabled         = var.acr_network_rule_set_enabled
  network_rule_default_action      = var.acr_network_rule_default_action
  allowed_ip_ranges                = var.acr_allowed_ip_ranges
  allowed_subnet_ids               = var.acr_allowed_subnet_ids
  
  # Security features
  content_trust_enabled            = var.acr_content_trust_enabled
  quarantine_enabled               = var.acr_quarantine_enabled
  retention_policy_enabled         = var.acr_retention_policy_enabled
  retention_days                   = var.acr_retention_days
  
  # Geo-replication (Premium SKU only)
  georeplications                  = var.acr_georeplications
  
  # Encryption (optional)
  encryption_enabled               = var.acr_encryption_enabled
  encryption_key_vault_key_id      = var.acr_encryption_key_vault_key_id
  encryption_identity_client_id    = var.acr_encryption_identity_client_id
  
  # Monitoring
  log_analytics_workspace_id       = local.log_analytics_workspace_id

  tags = local.common_tags

  depends_on = var.use_existing_network ? [] : [module.network[0]]
}

# Azure Kubernetes Service (AKS)
module "aks" {
  source = "./modules/aks"
  count  = var.enable_aks ? 1 : 0

  cluster_name        = "${var.project_name}-aks-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  dns_prefix          = "${var.project_name}-${var.environment}"
  
  kubernetes_version         = var.aks_kubernetes_version
  enable_private_cluster     = var.aks_enable_private_cluster
  
  # System node pool
  system_node_vm_size        = var.aks_system_node_vm_size
  system_node_count          = var.aks_system_node_count
  subnet_id                  = local.app_service_subnet_id
  availability_zones         = var.aks_availability_zones
  
  # User node pool - uses the module's native variables
  user_node_vm_size          = var.aks_user_node_vm_size
  user_node_count            = var.aks_user_node_min_count
  enable_auto_scaling        = var.aks_enable_user_node_pool
  min_node_count             = var.aks_user_node_min_count
  max_node_count             = var.aks_user_node_max_count
  
  # Azure AD integration
  enable_azure_rbac          = var.aks_enable_azure_rbac
  aad_admin_group_object_ids = var.aks_aad_admin_group_object_ids
  
  # Network configuration
  network_plugin             = var.aks_network_plugin
  network_policy             = var.aks_network_policy
  service_cidr               = var.aks_service_cidr
  dns_service_ip             = var.aks_dns_service_ip
  pod_cidr                   = var.aks_pod_cidr
  outbound_type              = var.aks_outbound_type
  
  # Security features
  enable_azure_policy        = var.aks_enable_azure_policy
  enable_defender            = var.aks_enable_defender
  
  # Monitoring
  log_analytics_workspace_id = local.log_analytics_workspace_id

  tags = local.common_tags

  depends_on = [module.acr]
}

# AKS - ACR Integration (Role Assignment)
resource "azurerm_role_assignment" "aks_acr_pull" {
  count                = var.enable_container_infrastructure && var.enable_aks ? 1 : 0
  
  principal_id         = module.aks[0].aks_kubelet_identity_object_id
  role_definition_name = "AcrPull"
  scope                = module.acr[0].acr_id
  
  depends_on = [module.aks, module.acr]
}

# Azure Container Instances (ACI) - MCP Server Example
module "aci_mcp_server" {
  source = "./modules/aci"
  count  = var.enable_aci ? 1 : 0

  container_group_name = "${var.project_name}-mcp-aci-${var.environment}"
  resource_group_name  = azurerm_resource_group.main.name
  location             = azurerm_resource_group.main.location
  
  container_name       = "mcp-server"
  container_image      = var.aci_mcp_container_image
  cpu_cores            = var.aci_mcp_cpu_cores
  memory_in_gb         = var.aci_mcp_memory_in_gb
  port                 = var.aci_mcp_port
  
  os_type              = "Linux"
  restart_policy       = var.aci_restart_policy
  
  # Use managed identity for ACR pull
  use_managed_identity = var.enable_container_infrastructure
  acr_login_server     = var.enable_container_infrastructure ? module.acr[0].acr_login_server : ""
  
  # Network configuration
  enable_vnet_integration = var.aci_enable_vnet_integration
  subnet_id               = var.aci_enable_vnet_integration ? local.private_endpoint_subnet_id : null
  
  # Environment variables
  environment_variables = var.aci_mcp_environment_variables
  secure_environment_variables = var.aci_mcp_secure_environment_variables
  
  # Health probes
  enable_liveness_probe  = var.aci_enable_liveness_probe
  liveness_probe_path    = var.aci_liveness_probe_path
  enable_readiness_probe = var.aci_enable_readiness_probe
  readiness_probe_path   = var.aci_readiness_probe_path
  
  # Monitoring
  log_analytics_workspace_id       = local.log_analytics_workspace_id
  log_analytics_workspace_key      = var.use_existing_log_analytics ? "" : module.monitoring[0].log_analytics_workspace_key

  tags = local.common_tags

  depends_on = [module.acr]
}

# ACI - ACR Integration (Role Assignment)
resource "azurerm_role_assignment" "aci_acr_pull" {
  count                = var.enable_container_infrastructure && var.enable_aci ? 1 : 0
  
  principal_id         = module.aci_mcp_server[0].container_group_principal_id
  role_definition_name = "AcrPull"
  scope                = module.acr[0].acr_id
  
  depends_on = [module.aci_mcp_server, module.acr]
}
