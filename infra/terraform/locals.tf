# Local values for resource naming
locals {
  resource_prefix = "${var.project_name}-${var.environment}"
  unique_suffix   = random_string.unique_suffix.result
  
  # Resource names
  vnet_name                      = "${local.resource_prefix}-vnet"
  sql_server_name               = "${local.resource_prefix}-sql-${local.unique_suffix}"
  sql_database_name             = "${local.resource_prefix}-db"
  key_vault_name                = "${local.resource_prefix}-kv-${local.unique_suffix}"
  storage_account_name          = replace("${local.resource_prefix}st${local.unique_suffix}", "-", "")
  app_service_plan_name         = "${local.resource_prefix}-asp"
  api_app_service_name          = "${local.resource_prefix}-api"
  mcp_app_service_name          = "${local.resource_prefix}-mcp"
  application_insights_name     = "${local.resource_prefix}-ai"
  log_analytics_workspace_name = "${local.resource_prefix}-law"
  
  # Common tags
  common_tags = {
    Project     = "PlatformEngineeringCopilot"
    Environment = var.environment
    CreatedBy   = "Terraform"
    CreatedDate = formatdate("YYYY-MM-DD", timestamp())
  }
}