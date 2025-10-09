# Outputs for Network Module

output "vnet_id" {
  description = "Virtual Network ID"
  value       = azurerm_virtual_network.main.id
}

output "vnet_name" {
  description = "Virtual Network Name"
  value       = azurerm_virtual_network.main.name
}

output "app_service_subnet_id" {
  description = "App Service Subnet ID"
  value       = azurerm_subnet.app_service_subnet.id
}

output "private_endpoint_subnet_id" {
  description = "Private Endpoint Subnet ID"
  value       = azurerm_subnet.private_endpoint_subnet.id
}

output "management_subnet_id" {
  description = "Management Subnet ID"
  value       = azurerm_subnet.management_subnet.id
}

output "app_service_nsg_id" {
  description = "App Service Network Security Group ID"
  value       = azurerm_network_security_group.app_service_nsg.id
}

output "private_endpoint_nsg_id" {
  description = "Private Endpoint Network Security Group ID"
  value       = azurerm_network_security_group.private_endpoint_nsg.id
}