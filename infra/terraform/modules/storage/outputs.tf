output "storage_account_id" {
  description = "The ID of the storage account"
  value       = azurerm_storage_account.main.id
}

output "storage_account_name" {
  description = "The name of the storage account"
  value       = azurerm_storage_account.main.name
}

output "storage_account_primary_access_key" {
  description = "The primary access key for the storage account"
  value       = azurerm_storage_account.main.primary_access_key
  sensitive   = true
}

output "storage_account_secondary_access_key" {
  description = "The secondary access key for the storage account"
  value       = azurerm_storage_account.main.secondary_access_key
  sensitive   = true
}

output "storage_account_primary_connection_string" {
  description = "The primary connection string for the storage account"
  value       = azurerm_storage_account.main.primary_connection_string
  sensitive   = true
}

output "storage_account_secondary_connection_string" {
  description = "The secondary connection string for the storage account"
  value       = azurerm_storage_account.main.secondary_connection_string
  sensitive   = true
}

output "storage_account_primary_blob_endpoint" {
  description = "The endpoint URL for blob storage in the primary location"
  value       = azurerm_storage_account.main.primary_blob_endpoint
}

output "storage_account_primary_queue_endpoint" {
  description = "The endpoint URL for queue storage in the primary location"
  value       = azurerm_storage_account.main.primary_queue_endpoint
}

output "storage_account_primary_table_endpoint" {
  description = "The endpoint URL for table storage in the primary location"
  value       = azurerm_storage_account.main.primary_table_endpoint
}

output "logs_container_name" {
  description = "The name of the logs container"
  value       = var.create_logs_container ? azurerm_storage_container.logs[0].name : null
}

output "backups_container_name" {
  description = "The name of the backups container"
  value       = var.create_backups_container ? azurerm_storage_container.backups[0].name : null
}

output "documents_container_name" {
  description = "The name of the documents container"
  value       = var.create_documents_container ? azurerm_storage_container.documents[0].name : null
}