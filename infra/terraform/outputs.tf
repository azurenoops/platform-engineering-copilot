# Outputs for Platform Engineering Copilot Infrastructure

output "admin_api_url" {
  description = "Admin API App Service URL"
  value       = var.deploy_admin_api && module.appservice.api_app_service_hostname != null ? "https://${module.appservice.api_app_service_hostname}" : null
}

output "chat_url" {
  description = "Chat App Service URL"
  value       = var.deploy_chat && module.appservice.chat_app_service_hostname != null ? "https://${module.appservice.chat_app_service_hostname}" : null
}

output "mcp_url" {
  description = "MCP Server URL"
  value       = "https://${module.appservice.mcp_app_service_hostname}"
}

output "sql_server_fqdn" {
  description = "SQL Server FQDN"
  value       = module.sql.sql_server_fqdn
}

output "key_vault_uri" {
  description = "Key Vault URI"
  value       = local.keyvault_uri
}

output "application_insights_instrumentation_key" {
  description = "Application Insights Instrumentation Key"
  value       = local.application_insights_instrumentation_key
  sensitive   = true
}

output "storage_account_name" {
  description = "Storage Account Name"
  value       = module.storage.storage_account_name
}

output "resource_group_name" {
  description = "Resource Group Name"
  value       = var.resource_group_name
}

output "virtual_network_name" {
  description = "Virtual Network Name"
  value       = local.vnet_name
}

output "deployment_summary" {
  description = "Deployment Summary"
  value = {
    project_name                 = var.project_name
    environment                  = var.environment
    location                     = var.location
    
    # Deployment options
    deploy_admin_api             = var.deploy_admin_api
    deploy_chat                  = var.deploy_chat
    use_existing_network         = var.use_existing_network
    use_existing_log_analytics   = var.use_existing_log_analytics
    use_existing_keyvault        = var.use_existing_keyvault
    
    # App Services
    admin_api_app_service        = module.appservice.api_app_service_name
    chat_app_service             = module.appservice.chat_app_service_name
    mcp_app_service              = module.appservice.mcp_app_service_name
    
    # Core Infrastructure
    sql_server                   = module.sql.sql_server_name
    sql_database                 = module.sql.sql_database_name
    key_vault                    = local.keyvault_name
    storage_account              = module.storage.storage_account_name
    application_insights         = var.use_existing_log_analytics ? "Using Existing" : module.monitoring[0].application_insights_name
    virtual_network              = local.vnet_name
    log_analytics_workspace      = local.log_analytics_workspace_name
    
    deployed_at                  = timestamp()
  }
}

# ==============================================================================
# Container Infrastructure Outputs
# ==============================================================================

# Azure Container Registry Outputs
output "acr_login_server" {
  description = "ACR login server URL"
  value       = var.enable_container_infrastructure ? module.acr[0].acr_login_server : null
}

output "acr_name" {
  description = "ACR name"
  value       = var.enable_container_infrastructure ? module.acr[0].acr_name : null
}

output "acr_id" {
  description = "ACR resource ID"
  value       = var.enable_container_infrastructure ? module.acr[0].acr_id : null
}

output "acr_admin_username" {
  description = "ACR admin username (if enabled)"
  value       = var.enable_container_infrastructure && var.acr_admin_enabled ? module.acr[0].acr_admin_username : null
  sensitive   = true
}

output "acr_admin_password" {
  description = "ACR admin password (if enabled)"
  value       = var.enable_container_infrastructure && var.acr_admin_enabled ? module.acr[0].acr_admin_password : null
  sensitive   = true
}

# Azure Kubernetes Service Outputs
output "aks_cluster_name" {
  description = "AKS cluster name"
  value       = var.enable_aks ? module.aks[0].aks_name : null
}

output "aks_cluster_id" {
  description = "AKS cluster ID"
  value       = var.enable_aks ? module.aks[0].aks_id : null
}

output "aks_fqdn" {
  description = "AKS cluster FQDN"
  value       = var.enable_aks ? module.aks[0].aks_fqdn : null
}

output "aks_private_fqdn" {
  description = "AKS cluster private FQDN"
  value       = var.enable_aks ? module.aks[0].aks_private_fqdn : null
}

output "aks_node_resource_group" {
  description = "AKS node resource group"
  value       = var.enable_aks ? module.aks[0].aks_node_resource_group : null
}

output "aks_oidc_issuer_url" {
  description = "AKS OIDC issuer URL for workload identity"
  value       = var.enable_aks ? module.aks[0].aks_oidc_issuer_url : null
}

output "kube_config" {
  description = "Kubernetes configuration for kubectl"
  value       = var.enable_aks ? module.aks[0].kube_config : null
  sensitive   = true
}

output "kube_config_command" {
  description = "Command to get AKS credentials"
  value       = var.enable_aks ? "az aks get-credentials --resource-group ${azurerm_resource_group.main.name} --name ${module.aks[0].aks_name}" : null
}

# Azure Container Instances Outputs
output "aci_mcp_server_ip" {
  description = "ACI MCP server IP address"
  value       = var.enable_aci ? module.aci_mcp_server[0].container_group_ip : null
}

output "aci_mcp_server_fqdn" {
  description = "ACI MCP server FQDN"
  value       = var.enable_aci ? module.aci_mcp_server[0].container_group_fqdn : null
}

output "aci_mcp_server_url" {
  description = "ACI MCP server URL"
  value       = var.enable_aci ? "http://${module.aci_mcp_server[0].container_group_ip}:${var.aci_mcp_port}" : null
}

# Container Infrastructure Summary
output "container_infrastructure_summary" {
  description = "Container Infrastructure Summary"
  value = var.enable_container_infrastructure || var.enable_aks || var.enable_aci ? {
    acr_enabled     = var.enable_container_infrastructure
    acr_name        = var.enable_container_infrastructure ? module.acr[0].acr_name : null
    acr_login_server = var.enable_container_infrastructure ? module.acr[0].acr_login_server : null
    aks_enabled     = var.enable_aks
    aks_cluster_name = var.enable_aks ? module.aks[0].aks_name : null
    aks_fqdn        = var.enable_aks ? module.aks[0].aks_private_fqdn : null
    aci_enabled     = var.enable_aci
    aci_mcp_ip      = var.enable_aci ? module.aci_mcp_server[0].container_group_ip : null
  } : null
}