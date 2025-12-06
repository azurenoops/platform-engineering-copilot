using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Storage;

/// <summary>
/// Terraform module generator for Azure Storage Account infrastructure
/// </summary>
public class TerraformStorageModuleGenerator : IInfrastructureModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public ComputePlatform Platform => ComputePlatform.Storage;
    public CloudProvider Provider => CloudProvider.Azure;

    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "storage";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        // Generate main storage module
        files["modules/storage/main.tf"] = GenerateMainTf(request);
        files["modules/storage/variables.tf"] = GenerateVariablesTf(request);
        files["modules/storage/outputs.tf"] = GenerateOutputsTf(request);
        files["modules/storage/README.md"] = GenerateReadme(request);

        return files;
    }

    private string GenerateMainTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "storage";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        sb.AppendLine("# Azure Storage Account Infrastructure Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: SC-28 (Encryption at Rest), CP-9/CP-10 (Backup/Recovery), AU-11 (Audit Retention), AC-3 (Access Control)");
        sb.AppendLine($"# Service: {serviceName}");
        sb.AppendLine($"# Region: {infrastructure.Region}");
        sb.AppendLine();

        // Storage Account - FedRAMP Compliant
        sb.AppendLine("resource \"azurerm_storage_account\" \"storage\" {");
        sb.AppendLine("  name                          = var.storage_account_name");
        sb.AppendLine("  resource_group_name           = var.resource_group_name");
        sb.AppendLine("  location                      = var.location");
        sb.AppendLine("  account_tier                  = var.account_tier");
        sb.AppendLine("  account_replication_type      = var.replication_type");
        sb.AppendLine("  account_kind                  = var.account_kind");
        sb.AppendLine("  enable_https_traffic_only     = true   # FedRAMP SC-8");
        sb.AppendLine("  min_tls_version               = \"TLS1_2\"  # FedRAMP SC-8");
        sb.AppendLine("  allow_nested_items_to_be_public = false   # FedRAMP AC-3");
        sb.AppendLine("  shared_access_key_enabled     = false  # FedRAMP AC-3 - Require AAD auth");
        sb.AppendLine("  infrastructure_encryption_enabled = true  # FedRAMP SC-28 - Double encryption");
        sb.AppendLine();

        sb.AppendLine("  network_rules {");
        sb.AppendLine("    default_action             = \"Deny\"  # FedRAMP SC-7");
        sb.AppendLine("    bypass                     = [\"AzureServices\"]");
        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("    virtual_network_subnet_ids = var.allowed_subnet_ids");
        }
        sb.AppendLine("  }");
        sb.AppendLine();

        sb.AppendLine("  # FedRAMP CP-9/CP-10: Blob soft delete and versioning");
        sb.AppendLine("  blob_properties {");
        sb.AppendLine("    versioning_enabled       = true  # FedRAMP AU-11");
        sb.AppendLine("    change_feed_enabled      = true  # FedRAMP AU-3");
        sb.AppendLine("    change_feed_retention_in_days = 90");
        sb.AppendLine("    ");
        sb.AppendLine("    delete_retention_policy {");
        sb.AppendLine("      days = var.soft_delete_retention_days  # FedRAMP CP-9");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    container_delete_retention_policy {");
        sb.AppendLine("      days = var.soft_delete_retention_days  # FedRAMP CP-9");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();

        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();

        // Private Endpoint (if enabled)
        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("# Private Endpoint for Storage Account");
            sb.AppendLine("resource \"azurerm_private_endpoint\" \"storage\" {");
            sb.AppendLine("  name                = \"${var.storage_account_name}-pe\"");
            sb.AppendLine("  location            = var.location");
            sb.AppendLine("  resource_group_name = var.resource_group_name");
            sb.AppendLine("  subnet_id           = var.private_endpoint_subnet_id");
            sb.AppendLine();
            sb.AppendLine("  private_service_connection {");
            sb.AppendLine("    name                           = \"${var.storage_account_name}-psc\"");
            sb.AppendLine("    private_connection_resource_id = azurerm_storage_account.storage.id");
            sb.AppendLine("    is_manual_connection           = false");
            sb.AppendLine("    subresource_names              = [\"blob\"]");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  tags = var.tags");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Diagnostics (if enabled)
        if (observability.EnableDiagnostics == true)
        {
            sb.AppendLine("# Diagnostic Settings");
            sb.AppendLine("resource \"azurerm_monitor_diagnostic_setting\" \"storage\" {");
            sb.AppendLine("  name                       = \"${var.storage_account_name}-diag\"");
            sb.AppendLine("  target_resource_id         = azurerm_storage_account.storage.id");
            sb.AppendLine("  log_analytics_workspace_id = var.log_analytics_workspace_id");
            sb.AppendLine();
            sb.AppendLine("  metric {");
            sb.AppendLine("    category = \"Transaction\"");
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

        sb.AppendLine("# Storage Account Variables - FedRAMP Compliant");
        sb.AppendLine();
        sb.AppendLine("variable \"storage_account_name\" {");
        sb.AppendLine("  description = \"Name of the storage account\"");
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
        sb.AppendLine("variable \"account_tier\" {");
        sb.AppendLine("  description = \"Storage account tier\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"Standard\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"replication_type\" {");
        sb.AppendLine("  description = \"Storage account replication type (use GRS or RAGRS for FedRAMP CP-9)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"GRS\"  # FedRAMP CP-9 - Geo-redundant storage");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"account_kind\" {");
        sb.AppendLine("  description = \"Storage account kind\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"StorageV2\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"soft_delete_retention_days\" {");
        sb.AppendLine("  description = \"Number of days to retain soft deleted items (FedRAMP CP-9)\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 14");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Resource tags\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");
        sb.AppendLine();

        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("variable \"private_endpoint_subnet_id\" {");
            sb.AppendLine("  description = \"Subnet ID for private endpoint\"");
            sb.AppendLine("  type        = string");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("variable \"allowed_subnet_ids\" {");
            sb.AppendLine("  description = \"List of subnet IDs allowed to access storage\"");
            sb.AppendLine("  type        = list(string)");
            sb.AppendLine("  default     = []");
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

        sb.AppendLine("# Storage Account Outputs");
        sb.AppendLine();
        sb.AppendLine("output \"storage_account_id\" {");
        sb.AppendLine("  description = \"Storage account ID\"");
        sb.AppendLine("  value       = azurerm_storage_account.storage.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"storage_account_name\" {");
        sb.AppendLine("  description = \"Storage account name\"");
        sb.AppendLine("  value       = azurerm_storage_account.storage.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"primary_blob_endpoint\" {");
        sb.AppendLine("  description = \"Primary blob endpoint\"");
        sb.AppendLine("  value       = azurerm_storage_account.storage.primary_blob_endpoint");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"primary_access_key\" {");
        sb.AppendLine("  description = \"Primary access key\"");
        sb.AppendLine("  value       = azurerm_storage_account.storage.primary_access_key");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();

        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("output \"private_endpoint_id\" {");
            sb.AppendLine("  description = \"Private endpoint ID\"");
            sb.AppendLine("  value       = azurerm_private_endpoint.storage.id");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "storage";

        sb.AppendLine($"# Azure Storage Account - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("FedRAMP-compliant Terraform module for Azure Storage Account with:");
        sb.AppendLine("- Storage account with geo-redundant replication - FedRAMP CP-9");
        sb.AppendLine("- Infrastructure encryption (double encryption) - FedRAMP SC-28");
        sb.AppendLine("- HTTPS-only access with TLS 1.2 - FedRAMP SC-8");
        sb.AppendLine("- Blob soft delete (14 days) - FedRAMP CP-9");
        sb.AppendLine("- Container soft delete (14 days) - FedRAMP CP-9");
        sb.AppendLine("- Blob versioning - FedRAMP AU-11");
        sb.AppendLine("- Change feed enabled - FedRAMP AU-3");
        sb.AppendLine("- Shared key access disabled (AAD only) - FedRAMP AC-3");
        sb.AppendLine("- Network default deny - FedRAMP SC-7");
        
        if (request.Security?.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("- Private endpoint connectivity - FedRAMP SC-7");
        }
        
        if (request.Observability?.EnableDiagnostics == true)
        {
            sb.AppendLine("- Diagnostic settings and logging - FedRAMP AU-2");
        }

        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Implementation |");
        sb.AppendLine("|---------|----------------|");
        sb.AppendLine("| SC-28 | Infrastructure encryption (double encryption) |");
        sb.AppendLine("| SC-8 | TLS 1.2 encryption in transit |");
        sb.AppendLine("| CP-9/CP-10 | Geo-redundant storage and soft delete |");
        sb.AppendLine("| AU-11 | Blob versioning for audit retention |");
        sb.AppendLine("| AU-3 | Change feed for audit tracking |");
        sb.AppendLine("| AC-3 | AAD authentication required (shared key disabled) |");
        sb.AppendLine("| SC-7 | Network default deny and private endpoint |");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"storage\" {");
        sb.AppendLine("  source = \"./modules/storage\"");
        sb.AppendLine();
        sb.AppendLine($"  storage_account_name       = \"{serviceName}sa\"");
        sb.AppendLine("  resource_group_name        = azurerm_resource_group.main.name");
        sb.AppendLine("  location                   = azurerm_resource_group.main.location");
        sb.AppendLine("  replication_type           = \"GRS\"  # Geo-redundant for FedRAMP");
        sb.AppendLine("  soft_delete_retention_days = 14");
        sb.AppendLine("  tags                       = local.common_tags");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    public bool CanHandle(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure;
        if (infrastructure == null) return false;

        return infrastructure.ComputePlatform == ComputePlatform.Storage &&
               infrastructure.Format == InfrastructureFormat.Terraform &&
               infrastructure.Provider == CloudProvider.Azure;
    }
}
