using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Database;

/// <summary>
/// Terraform module generator for Azure SQL Database infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (PE, diagnostics, RBAC) are handled by reusable generators
/// </summary>
public class TerraformSQLResourceModuleGenerator : IResourceModuleGenerator
{    
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public ComputePlatform Platform => ComputePlatform.Database;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "sql-database", "sql", "azure-sql", "sqldb" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by SQL Database
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.PrivateEndpoint,
        CrossCuttingType.DiagnosticSettings,
        CrossCuttingType.RBACAssignment
    };
    
    /// <summary>
    /// Azure resource type for SQL Server
    /// </summary>
    public string AzureResourceType => "Microsoft.Sql/servers";

    /// <summary>
    /// Generate ONLY the core SQL Server/Database resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "sqldb";

        // Generate only core SQL module - no PE, diagnostics, or RBAC
        files["modules/database/main.tf"] = GenerateCoreMainTf(request);
        files["modules/database/variables.tf"] = GenerateCoreVariablesTf(request);
        files["modules/database/outputs.tf"] = GenerateCoreOutputsTf(request);
        files["modules/database/README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "azurerm_mssql_server.sql", // Terraform resource reference
            ResourceType = "Microsoft.Sql/servers",
            OutputNames = new List<string>
            {
                "sql_server_id",
                "sql_server_name",
                "sql_server_fqdn",
                "sql_database_id",
                "sql_database_name"
            },
            SupportedCrossCutting = new List<CrossCuttingType>
            {
                CrossCuttingType.PrivateEndpoint,
                CrossCuttingType.DiagnosticSettings,
                CrossCuttingType.RBACAssignment
            }
        };
    }

    /// <summary>
    /// Legacy GenerateModule - delegates to legacy generator for full module
    /// </summary>
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        // For full SQL modules, delegate to core resource generation
        var result = GenerateCoreResource(request);
        return result.Files;
    }
    
    /// <summary>
    /// Check if this generator can handle the request
    /// </summary>
    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        return infrastructure.Format == InfrastructureFormat.Terraform &&
               infrastructure.Provider == CloudProvider.Azure &&
               infrastructure.ComputePlatform == ComputePlatform.Database;
    }

    /// <summary>
    /// Core main.tf - only SQL Server/Database resources, no cross-cutting
    /// </summary>
    private string GenerateCoreMainTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "sqldb";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();

        sb.AppendLine("# Azure SQL Database Infrastructure Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: SC-28 (Encryption at Rest), AU-2/AU-3 (Auditing), SI-4 (Security Monitoring), RA-5 (Vulnerability Scanning)");
        sb.AppendLine($"# Service: {serviceName}");
        sb.AppendLine($"# Region: {infrastructure.Region}");
        sb.AppendLine("# NOTE: Cross-cutting concerns (PE, diagnostics, RBAC) are composed via separate modules");
        sb.AppendLine();

        // SQL Server
        sb.AppendLine("resource \"azurerm_mssql_server\" \"sql\" {");
        sb.AppendLine("  name                         = var.sql_server_name");
        sb.AppendLine("  resource_group_name          = var.resource_group_name");
        sb.AppendLine("  location                     = var.location");
        sb.AppendLine("  version                      = \"12.0\"");
        sb.AppendLine("  administrator_login          = var.administrator_login");
        sb.AppendLine("  administrator_login_password = var.administrator_password");
        sb.AppendLine("  minimum_tls_version          = \"1.2\"  # FedRAMP SC-8");
        sb.AppendLine("  public_network_access_enabled = false  # FedRAMP SC-7");
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

        return sb.ToString();
    }

    /// <summary>
    /// Core variables.tf - only variables for core resource
    /// </summary>
    private string GenerateCoreVariablesTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# SQL Database Variables - FedRAMP Compliant");
        sb.AppendLine("# Cross-cutting variables are defined in their respective modules");
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
        sb.AppendLine("  description = \"Azure region for resources\"");
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
        sb.AppendLine("  description = \"SQL Database SKU name\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"S1\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"max_size_gb\" {");
        sb.AppendLine("  description = \"Maximum database size in GB\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 250");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"backup_retention_days\" {");
        sb.AppendLine("  description = \"Number of days to retain backups\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 35  # FedRAMP CP-9");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"audit_storage_endpoint\" {");
        sb.AppendLine("  description = \"Storage account endpoint for audit logs\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"audit_storage_access_key\" {");
        sb.AppendLine("  description = \"Storage account access key for audit logs\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"audit_retention_days\" {");
        sb.AppendLine("  description = \"Number of days to retain audit logs\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 90  # FedRAMP AU-11");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"security_alert_emails\" {");
        sb.AppendLine("  description = \"Email addresses for security alerts\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"threat_detection_retention_days\" {");
        sb.AppendLine("  description = \"Number of days to retain threat detection logs\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 90  # FedRAMP AU-11");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Tags to apply to resources\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Core outputs.tf - resource outputs for cross-cutting composition
    /// </summary>
    private string GenerateCoreOutputsTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# SQL Database Outputs");
        sb.AppendLine("# Used by cross-cutting modules for composition");
        sb.AppendLine();
        sb.AppendLine("output \"sql_server_id\" {");
        sb.AppendLine("  description = \"The ID of the SQL Server\"");
        sb.AppendLine("  value       = azurerm_mssql_server.sql.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"sql_server_name\" {");
        sb.AppendLine("  description = \"The name of the SQL Server\"");
        sb.AppendLine("  value       = azurerm_mssql_server.sql.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"sql_server_fqdn\" {");
        sb.AppendLine("  description = \"The fully qualified domain name of the SQL Server\"");
        sb.AppendLine("  value       = azurerm_mssql_server.sql.fully_qualified_domain_name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"sql_database_id\" {");
        sb.AppendLine("  description = \"The ID of the SQL Database\"");
        sb.AppendLine("  value       = azurerm_mssql_database.db.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"sql_database_name\" {");
        sb.AppendLine("  description = \"The name of the SQL Database\"");
        sb.AppendLine("  value       = azurerm_mssql_database.db.name");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate README documentation
    /// </summary>
    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "sqldb";

        sb.AppendLine($"# Azure SQL Database Module - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("This Terraform module creates an Azure SQL Server and Database with FedRAMP-compliant security settings.");
        sb.AppendLine("Cross-cutting concerns (Private Endpoints, Diagnostic Settings, RBAC) are composed via separate modules.");
        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Description | Implementation |");
        sb.AppendLine("|---------|-------------|----------------|");
        sb.AppendLine("| SC-28 | Encryption at Rest | TDE enabled |");
        sb.AppendLine("| SC-8 | Transmission Confidentiality | TLS 1.2+ required |");
        sb.AppendLine("| AU-2/AU-3 | Audit Events | Extended auditing policy |");
        sb.AppendLine("| SI-4 | Security Monitoring | Advanced Threat Protection |");
        sb.AppendLine("| RA-5 | Vulnerability Scanning | Vulnerability Assessment enabled |");
        sb.AppendLine("| CP-9/CP-10 | Backup/Recovery | Zone redundancy, geo-backup, LTR |");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"sql\" {");
        sb.AppendLine("  source = \"./modules/database\"");
        sb.AppendLine();
        sb.AppendLine("  sql_server_name         = \"my-sql-server\"");
        sb.AppendLine("  sql_database_name       = \"mydb\"");
        sb.AppendLine("  resource_group_name     = azurerm_resource_group.main.name");
        sb.AppendLine("  location                = azurerm_resource_group.main.location");
        sb.AppendLine("  administrator_login     = \"sqladmin\"");
        sb.AppendLine("  administrator_password  = var.sql_admin_password");
        sb.AppendLine("  audit_storage_endpoint  = module.storage.primary_blob_endpoint");
        sb.AppendLine("  audit_storage_access_key = module.storage.primary_access_key");
        sb.AppendLine("  tags                    = local.common_tags");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Composition");
        sb.AppendLine();
        sb.AppendLine("To add cross-cutting concerns, use the appropriate modules:");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"sql_pe\" {");
        sb.AppendLine("  source = \"./modules/cross-cutting/private-endpoint\"");
        sb.AppendLine("  ");
        sb.AppendLine("  resource_id   = module.sql.sql_server_id");
        sb.AppendLine("  resource_name = module.sql.sql_server_name");
        sb.AppendLine("  subresource   = \"sqlServer\"");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }
}
