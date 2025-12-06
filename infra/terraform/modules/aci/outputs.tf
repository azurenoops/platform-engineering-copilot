output "container_group_id" {
  description = "ID of the container group"
  value       = azurerm_container_group.aci.id
}

output "container_group_name" {
  description = "Name of the container group"
  value       = azurerm_container_group.aci.name
}

output "container_group_ip" {
  description = "IP address of the container group"
  value       = azurerm_container_group.aci.ip_address
}

output "container_group_fqdn" {
  description = "FQDN of the container group"
  value       = azurerm_container_group.aci.fqdn
}

output "container_group_principal_id" {
  description = "Principal ID of the managed identity"
  value       = var.use_managed_identity ? azurerm_container_group.aci.identity[0].principal_id : null
}
