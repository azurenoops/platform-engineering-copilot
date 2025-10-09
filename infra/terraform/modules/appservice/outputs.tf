output "service_plan_id" {
  description = "The ID of the App Service Plan"
  value       = azurerm_service_plan.main.id
}

output "service_plan_name" {
  description = "The name of the App Service Plan"
  value       = azurerm_service_plan.main.name
}

output "api_app_service_id" {
  description = "The ID of the API App Service"
  value       = azurerm_linux_web_app.api.id
}

output "api_app_service_name" {
  description = "The name of the API App Service"
  value       = azurerm_linux_web_app.api.name
}

output "api_app_service_hostname" {
  description = "The default hostname of the API App Service"
  value       = azurerm_linux_web_app.api.default_hostname
}

output "api_app_service_identity_principal_id" {
  description = "The principal ID of the API App Service managed identity"
  value       = azurerm_linux_web_app.api.identity[0].principal_id
}

output "api_app_service_outbound_ip_addresses" {
  description = "The outbound IP addresses of the API App Service"
  value       = split(",", azurerm_linux_web_app.api.outbound_ip_addresses)
}

output "api_url" {
  description = "The URL of the API App Service"
  value       = "https://${azurerm_linux_web_app.api.default_hostname}"
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