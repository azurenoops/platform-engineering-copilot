output "service_plan_id" {
  description = "The ID of the App Service Plan"
  value       = azurerm_service_plan.main.id
}

output "service_plan_name" {
  description = "The name of the App Service Plan"
  value       = azurerm_service_plan.main.name
}

output "api_app_service_id" {
  description = "The ID of the Admin API App Service"
  value       = var.deploy_admin_api ? azurerm_linux_web_app.api[0].id : null
}

output "api_app_service_name" {
  description = "The name of the Admin API App Service"
  value       = var.deploy_admin_api ? azurerm_linux_web_app.api[0].name : null
}

output "api_app_service_hostname" {
  description = "The default hostname of the Admin API App Service"
  value       = var.deploy_admin_api ? azurerm_linux_web_app.api[0].default_hostname : null
}

output "api_app_service_identity_principal_id" {
  description = "The principal ID of the Admin API App Service managed identity"
  value       = var.deploy_admin_api ? azurerm_linux_web_app.api[0].identity[0].principal_id : null
}

output "api_app_service_outbound_ip_addresses" {
  description = "The outbound IP addresses of the Admin API App Service"
  value       = var.deploy_admin_api ? split(",", azurerm_linux_web_app.api[0].outbound_ip_addresses) : []
}

output "api_url" {
  description = "The URL of the Admin API App Service"
  value       = var.deploy_admin_api ? "https://${azurerm_linux_web_app.api[0].default_hostname}" : null
}

output "mcp_app_service_id" {
  description = "The ID of the MCP App Service"
  value       = azurerm_linux_web_app.mcp.id
}

output "mcp_app_service_name" {
  description = "The name of the MCP App Service"
  value       = azurerm_linux_web_app.mcp.name
}

output "mcp_app_service_hostname" {
  description = "The default hostname of the MCP App Service"
  value       = azurerm_linux_web_app.mcp.default_hostname
}

output "mcp_app_service_identity_principal_id" {
  description = "The principal ID of the MCP App Service managed identity"
  value       = azurerm_linux_web_app.mcp.identity[0].principal_id
}

output "mcp_app_service_outbound_ip_addresses" {
  description = "The outbound IP addresses of the MCP App Service"
  value       = split(",", azurerm_linux_web_app.mcp.outbound_ip_addresses)
}

output "mcp_url" {
  description = "The URL of the MCP App Service"
  value       = "https://${azurerm_linux_web_app.mcp.default_hostname}"
}

# Chat App Service Outputs
output "chat_app_service_id" {
  description = "The ID of the Chat App Service"
  value       = var.deploy_chat ? azurerm_linux_web_app.chat[0].id : null
}

output "chat_app_service_name" {
  description = "The name of the Chat App Service"
  value       = var.deploy_chat ? azurerm_linux_web_app.chat[0].name : null
}

output "chat_app_service_hostname" {
  description = "The default hostname of the Chat App Service"
  value       = var.deploy_chat ? azurerm_linux_web_app.chat[0].default_hostname : null
}

output "chat_app_service_identity_principal_id" {
  description = "The principal ID of the Chat App Service managed identity"
  value       = var.deploy_chat ? azurerm_linux_web_app.chat[0].identity[0].principal_id : null
}

output "chat_app_service_outbound_ip_addresses" {
  description = "The outbound IP addresses of the Chat App Service"
  value       = var.deploy_chat ? split(",", azurerm_linux_web_app.chat[0].outbound_ip_addresses) : []
}

output "chat_url" {
  description = "The URL of the Chat App Service"
  value       = var.deploy_chat ? "https://${azurerm_linux_web_app.chat[0].default_hostname}" : null
}