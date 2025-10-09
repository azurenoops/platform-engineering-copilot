output "sql_server_id" {
  description = "The ID of the SQL server"
  value       = azurerm_mssql_server.main.id
}

output "sql_server_name" {
  description = "The name of the SQL server"
  value       = azurerm_mssql_server.main.name
}

output "sql_server_fqdn" {
  description = "The fully qualified domain name of the SQL server"
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "database_id" {
  description = "The ID of the database"
  value       = azurerm_mssql_database.main.id
}

output "database_name" {
  description = "The name of the database"
  value       = azurerm_mssql_database.main.name
}

output "connection_string" {
  description = "Connection string for the database"
  value       = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Persist Security Info=False;User ID=${var.admin_login};Password=${var.admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  sensitive   = true
}

output "sql_server_identity_principal_id" {
  description = "The principal ID of the SQL server managed identity"
  value       = azurerm_mssql_server.main.identity[0].principal_id
}