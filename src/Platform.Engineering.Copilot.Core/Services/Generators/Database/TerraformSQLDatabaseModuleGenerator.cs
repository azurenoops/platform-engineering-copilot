using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Database;

/// <summary>
/// Terraform module generator for Azure SQL Database infrastructure
/// </summary>
public class TerraformSQLDatabaseModuleGenerator : IInfrastructureModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public ComputePlatform Platform => ComputePlatform.Database;
    public CloudProvider Provider => CloudProvider.Azure;

    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "sqldb";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        // Generate main SQL module
        files["modules/database/main.tf"] = GenerateMainTf(request);
        files["modules/database/variables.tf"] = GenerateVariablesTf(request);
        files["modules/database/outputs.tf"] = GenerateOutputsTf(request);
        files["modules/database/README.md"] = GenerateReadme(request);

        return files;
    }

    private string GenerateMainTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "sqldb";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        sb.AppendLine("# Azure SQL Database Infrastructure Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: SC-28 (Encryption at Rest), AU-2/AU-3 (Auditing), SI-4 (Security Monitoring), RA-5 (Vulnerability Scanning)");
        sb.AppendLine($"# Service: {serviceName}");
        sb.AppendLine($"# Region: {infrastructure.Region}");
        sb.AppendLine();

        // SQL Server
        sb.AppendLine("resource \"azurerm_mssql_server\" \"sql\" {");
        sb.AppendLine("  name                         = var.sql_server_name");
        sb.AppendLine("  resource_group_name          = var.resource_group_name");
        sb.AppendLine("  location                     = var.location");
        sb.AppendLine("  version                      = \"12.0\"");
        sb.AppendLine("  administrator_login          = var.administrator_login");
        sb.AppendLine("  administrator_login_password = var.administrator_password");
        sb.AppendLine("  minimum_tls_version          = \"1.2\"");
        sb.AppendLine("  public_network_access_enabled = false");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // FedRAMP: Transparent Data Encryption (SC-28)
        sb.AppendLine("# FedRAMP SC-28: Transparent Data Encryption (Encryption at Rest)");
        sb.AppendLine("resource \"azurerm_mssql_server_transparent_data_encryption\" \"tde\" {");
        sb.AppendLine("  server_id = azurerm_mssql_server.sql.id");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // FedRAMP: Auditing (AU-2, AU-3, AU-6)
        sb.AppendLine("# FedRAMP AU-2/AU-3: Server Auditing (Audit Events/Content)");
        sb.AppendLine("resource \"azurerm_mssql_server_extended_auditing_policy\" \"audit\" {");
        sb.AppendLine("  server_id                               = azurerm_mssql_server.sql.id");
        sb.AppendLine("  storage_endpoint                        = var.audit_storage_endpoint");
        sb.AppendLine("  storage_account_access_key              = var.audit_storage_access_key");
        sb.AppendLine("  storage_account_access_key_is_secondary = false");
        sb.AppendLine("  retention_in_days                       = var.audit_retention_days");
        sb.AppendLine("  log_monitoring_enabled                  = true");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // FedRAMP: Threat Detection (SI-4, IR-4)
        sb.AppendLine("# FedRAMP SI-4: Advanced Threat Protection (Security Monitoring)");
        sb.AppendLine("resource \"azurerm_mssql_server_security_alert_policy\" \"threat\" {");
        sb.AppendLine("  resource_group_name  = var.resource_group_name");
        sb.AppendLine("  server_name          = azurerm_mssql_server.sql.name");
        sb.AppendLine("  state                = \"Enabled\"");
        sb.AppendLine("  email_addresses      = var.security_alert_emails");
        sb.AppendLine("  retention_days       = var.threat_detection_retention_days");
        sb.AppendLine("  disabled_alerts      = []");
        sb.AppendLine("  email_account_admins = true");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // FedRAMP: Vulnerability Assessment (RA-5)
        sb.AppendLine("# FedRAMP RA-5: Vulnerability Assessment (Vulnerability Scanning)");
        sb.AppendLine("resource \"azurerm_mssql_server_vulnerability_assessment\" \"va\" {");
        sb.AppendLine("  server_security_alert_policy_id = azurerm_mssql_server_security_alert_policy.threat.id");
        sb.AppendLine("  storage_container_path          = \"${var.audit_storage_endpoint}vulnerability-assessment/\"");
        sb.AppendLine("  storage_account_access_key      = var.audit_storage_access_key");
        sb.AppendLine();
        sb.AppendLine("  recurring_scans {");
        sb.AppendLine("    enabled                   = true");
        sb.AppendLine("    email_subscription_admins = true");
        sb.AppendLine("    emails                    = var.security_alert_emails");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // SQL Database - FedRAMP Compliant
        sb.AppendLine("# FedRAMP CP-9/CP-10: SQL Database with Zone Redundancy and Geo-Backup");
        sb.AppendLine("resource \"azurerm_mssql_database\" \"db\" {");
        sb.AppendLine("  name           = var.sql_database_name");
        sb.AppendLine("  server_id      = azurerm_mssql_server.sql.id");
        sb.AppendLine("  collation      = \"SQL_Latin1_General_CP1_CI_AS\"");
        sb.AppendLine("  max_size_gb    = var.max_size_gb");
        sb.AppendLine("  sku_name       = var.sku_name");
        sb.AppendLine("  zone_redundant = true  # FedRAMP CP-9 - Availability Zone Redundancy");
        sb.AppendLine();
        sb.AppendLine("  # FedRAMP CP-9/CP-10: Backup and Recovery");
        sb.AppendLine("  geo_backup_enabled                    = true");
        sb.AppendLine("  storage_account_type                  = \"Geo\"  # Geo-redundant backup storage");
        sb.AppendLine("  transparent_data_encryption_enabled   = true   # FedRAMP SC-28");
        sb.AppendLine();
        sb.AppendLine("  short_term_retention_policy {");
        sb.AppendLine("    retention_days           = var.backup_retention_days");
        sb.AppendLine("    backup_interval_in_hours = 12  # Point-in-time restore interval");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  long_term_retention_policy {");
        sb.AppendLine("    weekly_retention  = \"P4W\"   # 4 weeks");
        sb.AppendLine("    monthly_retention = \"P12M\"  # 12 months");
        sb.AppendLine("    yearly_retention  = \"P5Y\"   # 5 years");
        sb.AppendLine("    week_of_year      = 1");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();

        // Firewall Rules (if enabled and no private endpoint)
        if (security.EnableFirewall && security.EnablePrivateEndpoint != true)
        {
            sb.AppendLine("# Allow Azure services to access SQL Server");
            sb.AppendLine("resource \"azurerm_mssql_firewall_rule\" \"azure_services\" {");
            sb.AppendLine("  name             = \"AllowAzureServices\"");
            sb.AppendLine("  server_id        = azurerm_mssql_server.sql.id");
            sb.AppendLine("  start_ip_address = \"0.0.0.0\"");
            sb.AppendLine("  end_ip_address   = \"0.0.0.0\"");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Private Endpoint (if enabled)
        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("# Private Endpoint for SQL Server");
            sb.AppendLine("resource \"azurerm_private_endpoint\" \"sql\" {");
            sb.AppendLine("  name                = \"${var.sql_server_name}-pe\"");
            sb.AppendLine("  location            = var.location");
            sb.AppendLine("  resource_group_name = var.resource_group_name");
            sb.AppendLine("  subnet_id           = var.private_endpoint_subnet_id");
            sb.AppendLine();
            sb.AppendLine("  private_service_connection {");
            sb.AppendLine("    name                           = \"${var.sql_server_name}-psc\"");
            sb.AppendLine("    private_connection_resource_id = azurerm_mssql_server.sql.id");
            sb.AppendLine("    is_manual_connection           = false");
            sb.AppendLine("    subresource_names              = [\"sqlServer\"]");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  tags = var.tags");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Diagnostics (if enabled)
        if (observability.EnableDiagnostics == true)
        {
            sb.AppendLine("# Diagnostic Settings for SQL Database");
            sb.AppendLine("resource \"azurerm_monitor_diagnostic_setting\" \"db\" {");
            sb.AppendLine("  name                       = \"${var.sql_database_name}-diag\"");
            sb.AppendLine("  target_resource_id         = azurerm_mssql_database.db.id");
            sb.AppendLine("  log_analytics_workspace_id = var.log_analytics_workspace_id");
            sb.AppendLine();
            sb.AppendLine("  log {");
            sb.AppendLine("    category = \"SQLInsights\"");
            sb.AppendLine("    enabled  = true");
            sb.AppendLine();
            sb.AppendLine("    retention_policy {");
            sb.AppendLine("      enabled = true");
            sb.AppendLine("      days    = 30");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  log {");
            sb.AppendLine("    category = \"QueryStoreRuntimeStatistics\"");
            sb.AppendLine("    enabled  = true");
            sb.AppendLine();
            sb.AppendLine("    retention_policy {");
            sb.AppendLine("      enabled = true");
            sb.AppendLine("      days    = 30");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  metric {");
            sb.AppendLine("    category = \"Basic\"");
            sb.AppendLine("    enabled  = true");
            sb.AppendLine();
            sb.AppendLine("    retention_policy {");
            sb.AppendLine("      enabled = true");
            sb.AppendLine("      days    = 30");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GenerateVariablesTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        sb.AppendLine("# SQL Database Variables - FedRAMP Compliant");
        sb.AppendLine();
        sb.AppendLine("variable \"sql_server_name\" {");
        sb.AppendLine("  description = \"Name of the SQL Server\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"sql_database_name\" {");
        sb.AppendLine("  description = \"Name of the SQL Database\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_group_name\" {");
        sb.AppendLine("  description = \"Name of the resource group\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"location\" {");
        sb.AppendLine("  description = \"Azure region\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"administrator_login\" {");
        sb.AppendLine("  description = \"SQL Server administrator login\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"administrator_password\" {");
        sb.AppendLine("  description = \"SQL Server administrator password\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"sku_name\" {");
        sb.AppendLine("  description = \"SQL Database SKU\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"S0\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"max_size_gb\" {");
        sb.AppendLine("  description = \"Maximum size in GB\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 2");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Resource tags\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // FedRAMP Required Variables
        sb.AppendLine("# FedRAMP Compliance Variables - AU-2/AU-3 (Auditing)");
        sb.AppendLine("variable \"audit_storage_endpoint\" {");
        sb.AppendLine("  description = \"Storage account endpoint for SQL audit logs (FedRAMP AU-2/AU-3)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"audit_storage_access_key\" {");
        sb.AppendLine("  description = \"Storage account access key for SQL audit logs\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"audit_retention_days\" {");
        sb.AppendLine("  description = \"Number of days to retain audit logs (FedRAMP AU-11)\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 90");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# FedRAMP Compliance Variables - SI-4 (Security Monitoring)");
        sb.AppendLine("variable \"security_alert_emails\" {");
        sb.AppendLine("  description = \"Email addresses for security alerts (FedRAMP SI-4/IR-4)\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"threat_detection_retention_days\" {");
        sb.AppendLine("  description = \"Number of days to retain threat detection logs\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 90");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# FedRAMP Compliance Variables - CP-9/CP-10 (Backup and Recovery)");
        sb.AppendLine("variable \"backup_retention_days\" {");
        sb.AppendLine("  description = \"Number of days for short-term backup retention (FedRAMP CP-9)\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 35");
        sb.AppendLine("}");
        sb.AppendLine();

        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("variable \"private_endpoint_subnet_id\" {");
            sb.AppendLine("  description = \"Subnet ID for private endpoint\"");
            sb.AppendLine("  type        = string");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        if (observability.EnableDiagnostics == true)
        {
            sb.AppendLine("variable \"log_analytics_workspace_id\" {");
            sb.AppendLine("  description = \"Log Analytics Workspace ID\"");
            sb.AppendLine("  type        = string");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GenerateOutputsTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var security = request.Security ?? new SecuritySpec();

        sb.AppendLine("# SQL Database Outputs");
        sb.AppendLine();
        sb.AppendLine("output \"sql_server_id\" {");
        sb.AppendLine("  description = \"SQL Server ID\"");
        sb.AppendLine("  value       = azurerm_mssql_server.sql.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"sql_server_name\" {");
        sb.AppendLine("  description = \"SQL Server name\"");
        sb.AppendLine("  value       = azurerm_mssql_server.sql.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"sql_server_fqdn\" {");
        sb.AppendLine("  description = \"SQL Server FQDN\"");
        sb.AppendLine("  value       = azurerm_mssql_server.sql.fully_qualified_domain_name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"sql_database_id\" {");
        sb.AppendLine("  description = \"SQL Database ID\"");
        sb.AppendLine("  value       = azurerm_mssql_database.db.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"sql_database_name\" {");
        sb.AppendLine("  description = \"SQL Database name\"");
        sb.AppendLine("  value       = azurerm_mssql_database.db.name");
        sb.AppendLine("}");
        sb.AppendLine();

        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("output \"private_endpoint_id\" {");
            sb.AppendLine("  description = \"Private endpoint ID\"");
            sb.AppendLine("  value       = azurerm_private_endpoint.sql.id");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "sqldb";

        sb.AppendLine($"# Azure SQL Database - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("FedRAMP-compliant Terraform module for Azure SQL Database with:");
        sb.AppendLine("- Azure SQL Server with TLS 1.2 minimum");
        sb.AppendLine("- Azure SQL Database with configurable SKU");
        sb.AppendLine("- Transparent Data Encryption (TDE) - FedRAMP SC-28");
        sb.AppendLine("- Server Auditing with 90-day retention - FedRAMP AU-2/AU-3");
        sb.AppendLine("- Advanced Threat Protection - FedRAMP SI-4");
        sb.AppendLine("- Vulnerability Assessment - FedRAMP RA-5");
        sb.AppendLine("- Zone Redundancy - FedRAMP CP-9");
        sb.AppendLine("- Geo-redundant Backup - FedRAMP CP-9/CP-10");
        sb.AppendLine("- Long-term Retention (5 years) - FedRAMP AU-11");
        
        if (request.Security?.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("- Private endpoint connectivity - FedRAMP SC-7");
        }
        
        if (request.Security?.EnableFirewall == true && request.Security?.EnablePrivateEndpoint != true)
        {
            sb.AppendLine("- Firewall rules for Azure services");
        }
        
        if (request.Observability?.EnableDiagnostics == true)
        {
            sb.AppendLine("- Diagnostic settings and logging");
        }

        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Implementation |");
        sb.AppendLine("|---------|----------------|");
        sb.AppendLine("| SC-28 | Transparent Data Encryption (TDE) |");
        sb.AppendLine("| AU-2/AU-3 | Server Auditing with 90-day retention |");
        sb.AppendLine("| AU-11 | Long-term retention (5 years) |");
        sb.AppendLine("| SI-4 | Advanced Threat Protection |");
        sb.AppendLine("| RA-5 | Vulnerability Assessment with recurring scans |");
        sb.AppendLine("| CP-9/CP-10 | Zone redundancy and geo-redundant backup |");
        sb.AppendLine("| SC-7 | Private endpoint (network isolation) |");
        sb.AppendLine("| SC-8 | TLS 1.2 encryption in transit |");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"database\" {");
        sb.AppendLine("  source = \"./modules/database\"");
        sb.AppendLine();
        sb.AppendLine($"  sql_server_name          = \"{serviceName}-server\"");
        sb.AppendLine($"  sql_database_name        = \"{serviceName}-db\"");
        sb.AppendLine("  resource_group_name      = azurerm_resource_group.main.name");
        sb.AppendLine("  location                 = azurerm_resource_group.main.location");
        sb.AppendLine("  administrator_login      = var.sql_admin_username");
        sb.AppendLine("  administrator_password   = var.sql_admin_password");
        sb.AppendLine();
        sb.AppendLine("  # FedRAMP Required - Auditing");
        sb.AppendLine("  audit_storage_endpoint   = azurerm_storage_account.audit.primary_blob_endpoint");
        sb.AppendLine("  audit_storage_access_key = azurerm_storage_account.audit.primary_access_key");
        sb.AppendLine("  audit_retention_days     = 90");
        sb.AppendLine();
        sb.AppendLine("  # FedRAMP Required - Security Alerts");
        sb.AppendLine("  security_alert_emails    = [\"security@example.com\"]");
        sb.AppendLine();
        sb.AppendLine("  tags = local.common_tags");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    public bool CanHandle(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure;
        if (infrastructure == null) return false;

        return infrastructure.ComputePlatform == ComputePlatform.Database &&
               infrastructure.Format == InfrastructureFormat.Terraform &&
               infrastructure.Provider == CloudProvider.Azure;
    }
}
