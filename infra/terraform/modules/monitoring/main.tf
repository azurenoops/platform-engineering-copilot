terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "${var.project_name}-logs-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.log_analytics_sku
  retention_in_days   = var.log_retention_days

  tags = var.tags
}

resource "azurerm_application_insights" "main" {
  name                = "${var.project_name}-ai-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = var.application_type

  tags = var.tags
}

# Action Group for alerts
resource "azurerm_monitor_action_group" "main" {
  name                = "${var.project_name}-alerts-${var.environment}"
  resource_group_name = var.resource_group_name
  short_name          = "alerts"

  dynamic "email_receiver" {
    for_each = var.alert_email_addresses
    content {
      name          = replace(email_receiver.value, "@", "-at-")
      email_address = email_receiver.value
    }
  }

  tags = var.tags
}

# Metric alerts
resource "azurerm_monitor_metric_alert" "app_service_cpu" {
  count               = var.app_service_id != null ? 1 : 0
  name                = "${var.project_name}-cpu-alert-${var.environment}"
  resource_group_name = var.resource_group_name
  scopes              = [var.app_service_id]
  description         = "Alert when CPU usage is high"
  severity            = 2
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "CpuPercentage"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = var.cpu_threshold
  }

  action {
    action_group_id = azurerm_monitor_action_group.main.id
  }

  tags = var.tags
}

resource "azurerm_monitor_metric_alert" "app_service_memory" {
  count               = var.app_service_id != null ? 1 : 0
  name                = "${var.project_name}-memory-alert-${var.environment}"
  resource_group_name = var.resource_group_name
  scopes              = [var.app_service_id]
  description         = "Alert when memory usage is high"
  severity            = 2
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "MemoryPercentage"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = var.memory_threshold
  }

  action {
    action_group_id = azurerm_monitor_action_group.main.id
  }

  tags = var.tags
}

resource "azurerm_monitor_metric_alert" "app_service_response_time" {
  count               = var.app_service_id != null ? 1 : 0
  name                = "${var.project_name}-response-time-alert-${var.environment}"
  resource_group_name = var.resource_group_name
  scopes              = [var.app_service_id]
  description         = "Alert when response time is high"
  severity            = 2
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "AverageResponseTime"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = var.response_time_threshold
  }

  action {
    action_group_id = azurerm_monitor_action_group.main.id
  }

  tags = var.tags
}

resource "azurerm_monitor_metric_alert" "sql_database_dtu" {
  count               = var.sql_database_id != null ? 1 : 0
  name                = "${var.project_name}-sql-dtu-alert-${var.environment}"
  resource_group_name = var.resource_group_name
  scopes              = [var.sql_database_id]
  description         = "Alert when SQL Database DTU usage is high"
  severity            = 2
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.Sql/servers/databases"
    metric_name      = "dtu_consumption_percent"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = var.sql_dtu_threshold
  }

  action {
    action_group_id = azurerm_monitor_action_group.main.id
  }

  tags = var.tags
}

# Application availability test
resource "azurerm_application_insights_web_test" "api_availability" {
  count                   = var.api_url != null ? 1 : 0
  name                    = "${var.project_name}-api-availability-${var.environment}"
  location                = var.location
  resource_group_name     = var.resource_group_name
  application_insights_id = azurerm_application_insights.main.id
  kind                    = "ping"
  frequency               = 300
  timeout                 = 30
  enabled                 = true
  geo_locations           = ["us-va-ash-azr", "us-ca-sjc-azr", "us-tx-sn1-azr"]

  configuration = <<XML
<WebTest Name="${var.project_name}-api-availability-${var.environment}"
         Id="ABD48585-0831-40CB-9069-682EA6BB3583"
         Enabled="True" CssProjectStructure="" CssIteration=""
         Timeout="30" WorkItemIds="" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
         Description="" CredentialUserName="" CredentialPassword=""
         PreAuthenticate="True" Proxy="default" StopOnError="False"
         RecordedResultFile="" ResultsLocale="">
  <Items>
    <Request Method="GET" Guid="a5f10126-e4cd-570d-961c-cea43999a200"
             Version="1.1" Url="${var.api_url}/health" ThinkTime="0"
             Timeout="30" ParseDependentRequests="True"
             FollowRedirects="True" RecordResult="True" Cache="False"
             ResponseTimeGoal="0" Encoding="utf-8" ExpectedHttpStatusCode="200"
             ExpectedResponseUrl="" ReportingName="" IgnoreHttpStatusCode="False" />
  </Items>
</WebTest>
XML

  tags = var.tags
}

# Smart detection rules (automatic)
resource "azurerm_application_insights_smart_detection_rule" "failure_anomalies" {
  name                    = "Failure Anomalies - ${var.project_name}-${var.environment}"
  application_insights_id = azurerm_application_insights.main.id
  enabled                 = true
  send_emails_to_subscription_owners = false
  additional_email_recipients = var.alert_email_addresses
}