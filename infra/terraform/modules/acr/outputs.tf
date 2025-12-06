# ==============================================================================
# Outputs for Azure Container Registry Module
# ==============================================================================

output "acr_id" {
  description = "ID of the Azure Container Registry"
  value       = azurerm_container_registry.acr.id
}

output "acr_name" {
  description = "Name of the Azure Container Registry"
  value       = azurerm_container_registry.acr.name
}

output "acr_login_server" {
  description = "Login server URL for the ACR"
  value       = azurerm_container_registry.acr.login_server
}

output "acr_admin_username" {
  description = "Admin username for the ACR (if admin enabled)"
  value       = var.admin_enabled ? azurerm_container_registry.acr.admin_username : null
  sensitive   = true
}

output "acr_admin_password" {
  description = "Admin password for the ACR (if admin enabled)"
  value       = var.admin_enabled ? azurerm_container_registry.acr.admin_password : null
  sensitive   = true
}

output "acr_identity_principal_id" {
  description = "Principal ID of the ACR managed identity"
  value       = azurerm_container_registry.acr.identity[0].principal_id
}

output "acr_identity_tenant_id" {
  description = "Tenant ID of the ACR managed identity"
  value       = azurerm_container_registry.acr.identity[0].tenant_id
}

output "private_endpoint_id" {
  description = "ID of the private endpoint"
  value       = var.enable_private_endpoint ? azurerm_private_endpoint.acr_pe[0].id : null
}

output "private_endpoint_ip" {
  description = "Private IP address of the ACR"
  value       = var.enable_private_endpoint ? azurerm_private_endpoint.acr_pe[0].private_service_connection[0].private_ip_address : null
}
