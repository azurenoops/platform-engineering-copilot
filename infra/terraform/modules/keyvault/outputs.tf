output "key_vault_id" {
  description = "The ID of the Key Vault"
  value       = azurerm_key_vault.main.id
}

output "key_vault_name" {
  description = "The name of the Key Vault"
  value       = azurerm_key_vault.main.name
}

output "key_vault_uri" {
  description = "The URI of the Key Vault"
  value       = azurerm_key_vault.main.vault_uri
}

output "key_vault_tenant_id" {
  description = "The tenant ID for the Key Vault"
  value       = azurerm_key_vault.main.tenant_id
}

output "database_connection_string_secret_id" {
  description = "The ID of the database connection string secret"
  value       = var.database_connection_string != null ? azurerm_key_vault_secret.database_connection_string[0].id : null
}

output "application_insights_connection_string_secret_id" {
  description = "The ID of the Application Insights connection string secret"
  value       = var.application_insights_connection_string != null ? azurerm_key_vault_secret.application_insights_connection_string[0].id : null
}

output "application_insights_instrumentation_key_secret_id" {
  description = "The ID of the Application Insights instrumentation key secret"
  value       = var.application_insights_instrumentation_key != null ? azurerm_key_vault_secret.application_insights_instrumentation_key[0].id : null
}