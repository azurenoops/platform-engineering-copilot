terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

resource "azurerm_service_plan" "main" {
  name                = "${var.project_name}-asp-${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = var.sku_name

  tags = var.tags
}

# Admin API App Service (Conditional)
resource "azurerm_linux_web_app" "api" {
  count               = var.deploy_admin_api ? 1 : 0
  name                = "${var.project_name}-admin-api-${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.main.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on                         = var.always_on
    application_stack {
      dotnet_version = "8.0"
    }
    health_check_path                 = "/health"
    health_check_eviction_time_in_min = 2
    http2_enabled                     = true
    minimum_tls_version               = "1.2"
    scm_minimum_tls_version           = "1.2"
    ftps_state                        = "Disabled"
    
    cors {
      allowed_origins     = var.cors_allowed_origins
      support_credentials = false
    }

    ip_restriction {
      action      = "Allow"
      priority    = 100
      name        = "AllowAll"
      ip_address  = "0.0.0.0/0"
    }
  }

  app_settings = merge({
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "false"
    "ASPNETCORE_ENVIRONMENT"              = var.environment == "prod" ? "Production" : title(var.environment)
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = var.application_insights_connection_string
  }, var.api_app_settings)

  connection_string {
    name  = "DefaultConnection"
    type  = "SQLServer"
    value = var.database_connection_string
  }

  logs {
    detailed_error_messages = true
    failed_request_tracing  = true
    
    http_logs {
      file_system {
        retention_in_days = var.log_retention_days
        retention_in_mb   = 35
      }
    }

    application_logs {
      file_system_level = "Information"
    }
  }

  tags = var.tags
}

# MCP Server App Service
resource "azurerm_linux_web_app" "mcp" {
  name                = "${var.project_name}-mcp-${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.main.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on                         = var.always_on
    application_stack {
      dotnet_version = "8.0"
    }
    health_check_path                 = "/health"
    health_check_eviction_time_in_min = 2
    http2_enabled                     = true
    minimum_tls_version               = "1.2"
    scm_minimum_tls_version           = "1.2"
    ftps_state                        = "Disabled"
    
    cors {
      allowed_origins     = var.cors_allowed_origins
      support_credentials = false
    }

    ip_restriction {
      action      = "Allow"
      priority    = 100
      name        = "AllowAll"
      ip_address  = "0.0.0.0/0"
    }
  }

  app_settings = merge({
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "false"
    "ASPNETCORE_ENVIRONMENT"              = var.environment == "prod" ? "Production" : title(var.environment)
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = var.application_insights_connection_string
  }, var.mcp_app_settings)

  connection_string {
    name  = "DefaultConnection"
    type  = "SQLServer"
    value = var.database_connection_string
  }

  logs {
    detailed_error_messages = true
    failed_request_tracing  = true
    
    http_logs {
      file_system {
        retention_in_days = var.log_retention_days
        retention_in_mb   = 35
      }
    }

    application_logs {
      file_system_level = "Information"
    }
  }

  tags = var.tags
}

# Chat App Service (Conditional)
resource "azurerm_linux_web_app" "chat" {
  count               = var.deploy_chat ? 1 : 0
  name                = "${var.project_name}-chat-${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.main.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on                         = var.always_on
    application_stack {
      dotnet_version = "8.0"
    }
    health_check_path                 = "/health"
    health_check_eviction_time_in_min = 2
    http2_enabled                     = true
    minimum_tls_version               = "1.2"
    scm_minimum_tls_version           = "1.2"
    ftps_state                        = "Disabled"
    
    cors {
      allowed_origins     = var.cors_allowed_origins
      support_credentials = false
    }

    ip_restriction {
      action      = "Allow"
      priority    = 100
      name        = "AllowAll"
      ip_address  = "0.0.0.0/0"
    }
  }

  app_settings = merge({
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "false"
    "ASPNETCORE_ENVIRONMENT"              = var.environment == "prod" ? "Production" : title(var.environment)
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = var.application_insights_connection_string
  }, var.chat_app_settings)

  connection_string {
    name  = "DefaultConnection"
    type  = "SQLServer"
    value = var.database_connection_string
  }

  logs {
    detailed_error_messages = true
    failed_request_tracing  = true
    
    http_logs {
      file_system {
        retention_in_days = var.log_retention_days
        retention_in_mb   = 35
      }
    }

    application_logs {
      file_system_level = "Information"
    }
  }

  tags = var.tags
}

# VNet integration (if subnet provided)
resource "azurerm_app_service_virtual_network_swift_connection" "api" {
  count          = var.deploy_admin_api && var.subnet_id != null ? 1 : 0
  app_service_id = azurerm_linux_web_app.api[0].id
  subnet_id      = var.subnet_id
}

resource "azurerm_app_service_virtual_network_swift_connection" "mcp" {
  count          = var.subnet_id != null ? 1 : 0
  app_service_id = azurerm_linux_web_app.mcp.id
  subnet_id      = var.subnet_id
}

resource "azurerm_app_service_virtual_network_swift_connection" "chat" {
  count          = var.deploy_chat && var.subnet_id != null ? 1 : 0
  app_service_id = azurerm_linux_web_app.chat[0].id
  subnet_id      = var.subnet_id
}

# Diagnostic settings
resource "azurerm_monitor_diagnostic_setting" "api" {
  count                      = var.deploy_admin_api && var.log_analytics_workspace_id != null ? 1 : 0
  name                       = "api-app-service-diagnostics"
  target_resource_id         = azurerm_linux_web_app.api[0].id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "AppServiceHTTPLogs"
  }

  enabled_log {
    category = "AppServiceConsoleLogs"
  }

  enabled_log {
    category = "AppServiceAppLogs"
  }

  enabled_log {
    category = "AppServiceAuditLogs"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}

resource "azurerm_monitor_diagnostic_setting" "mcp" {
  count                      = var.log_analytics_workspace_id != null ? 1 : 0
  name                       = "mcp-app-service-diagnostics"
  target_resource_id         = azurerm_linux_web_app.mcp.id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "AppServiceHTTPLogs"
  }

  enabled_log {
    category = "AppServiceConsoleLogs"
  }

  enabled_log {
    category = "AppServiceAppLogs"
  }

  enabled_log {
    category = "AppServiceAuditLogs"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}

resource "azurerm_monitor_diagnostic_setting" "chat" {
  count                      = var.deploy_chat && var.log_analytics_workspace_id != null ? 1 : 0
  name                       = "chat-app-service-diagnostics"
  target_resource_id         = azurerm_linux_web_app.chat[0].id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "AppServiceHTTPLogs"
  }

  enabled_log {
    category = "AppServiceConsoleLogs"
  }

  enabled_log {
    category = "AppServiceAppLogs"
  }

  enabled_log {
    category = "AppServiceAuditLogs"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}