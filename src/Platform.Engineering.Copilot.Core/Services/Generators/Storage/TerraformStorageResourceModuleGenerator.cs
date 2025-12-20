using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Storage;

/// <summary>
/// Terraform module generator for Azure Storage Account infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (PE, diagnostics, RBAC) are handled by reusable generators
/// </summary>
public class TerraformStorageResourceModuleGenerator : IResourceModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public ComputePlatform Platform => ComputePlatform.Storage;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "storage-account", "storage", "blob", "datalake" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by Storage Account
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.PrivateEndpoint,
        CrossCuttingType.DiagnosticSettings,
        CrossCuttingType.RBACAssignment
    };
    
    /// <summary>
    /// Azure resource type for Storage Account
    /// </summary>
    public string AzureResourceType => "Microsoft.Storage/storageAccounts";

    /// <summary>
    /// Generate ONLY the core Storage Account resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "storage";

        // Generate only core Storage module - no PE, diagnostics, or RBAC
        files["modules/storage/main.tf"] = GenerateCoreMainTf(request);
        files["modules/storage/variables.tf"] = GenerateCoreVariablesTf(request);
        files["modules/storage/outputs.tf"] = GenerateCoreOutputsTf(request);
        files["modules/storage/README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "azurerm_storage_account.storage", // Terraform resource reference
            ResourceType = "Microsoft.Storage/storageAccounts",
            OutputNames = new List<string>
            {
                "storage_account_id",
                "storage_account_name",
                "primary_blob_endpoint",
                "primary_access_key",
                "primary_connection_string"
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
        // For full Storage modules, delegate to core resource generation
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
               (infrastructure.ComputePlatform == ComputePlatform.Storage ||
                infrastructure.IncludeStorage == true);
    }

    /// <summary>
    /// Core main.tf - only Storage Account resource, no cross-cutting
    /// </summary>
    private string GenerateCoreMainTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "storage";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();

        sb.AppendLine("# Azure Storage Account Infrastructure Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: SC-28 (Encryption at Rest), CP-9/CP-10 (Backup/Recovery), AU-11 (Audit Retention), AC-3 (Access Control)");
        sb.AppendLine($"# Service: {serviceName}");
        sb.AppendLine($"# Region: {infrastructure.Region}");
        sb.AppendLine("# NOTE: Cross-cutting concerns (PE, diagnostics, RBAC) are composed via separate modules");
        sb.AppendLine();

        // Storage Account - FedRAMP Compliant (core resource only)
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
        sb.AppendLine("    virtual_network_subnet_ids = var.allowed_subnet_ids");
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

        return sb.ToString();
    }

    /// <summary>
    /// Core variables.tf - only variables for core resource
    /// </summary>
    private string GenerateCoreVariablesTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Storage Account Variables - FedRAMP Compliant");
        sb.AppendLine("# Cross-cutting variables are defined in their respective modules");
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
        sb.AppendLine("  description = \"Azure region for resources\"");
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
        sb.AppendLine("  description = \"Storage replication type (LRS, GRS, RAGRS, ZRS, GZRS, RAGZRS)\"");
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
        sb.AppendLine("  description = \"Number of days to retain soft-deleted blobs\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 90  # FedRAMP CP-9");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"allowed_subnet_ids\" {");
        sb.AppendLine("  description = \"List of subnet IDs allowed to access storage\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = []");
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

        sb.AppendLine("# Storage Account Outputs");
        sb.AppendLine("# Used by cross-cutting modules for composition");
        sb.AppendLine();
        sb.AppendLine("output \"storage_account_id\" {");
        sb.AppendLine("  description = \"The ID of the storage account\"");
        sb.AppendLine("  value       = azurerm_storage_account.storage.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"storage_account_name\" {");
        sb.AppendLine("  description = \"The name of the storage account\"");
        sb.AppendLine("  value       = azurerm_storage_account.storage.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"primary_blob_endpoint\" {");
        sb.AppendLine("  description = \"The primary blob endpoint URL\"");
        sb.AppendLine("  value       = azurerm_storage_account.storage.primary_blob_endpoint");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"primary_access_key\" {");
        sb.AppendLine("  description = \"The primary access key for the storage account\"");
        sb.AppendLine("  value       = azurerm_storage_account.storage.primary_access_key");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"primary_connection_string\" {");
        sb.AppendLine("  description = \"The primary connection string for the storage account\"");
        sb.AppendLine("  value       = azurerm_storage_account.storage.primary_connection_string");
        sb.AppendLine("  sensitive   = true");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate README documentation
    /// </summary>
    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "storage";

        sb.AppendLine($"# Azure Storage Account Module - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("This Terraform module creates an Azure Storage Account with FedRAMP-compliant security settings.");
        sb.AppendLine("Cross-cutting concerns (Private Endpoints, Diagnostic Settings, RBAC) are composed via separate modules.");
        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Description | Implementation |");
        sb.AppendLine("|---------|-------------|----------------|");
        sb.AppendLine("| SC-28 | Encryption at Rest | Double encryption enabled |");
        sb.AppendLine("| SC-8 | Transmission Confidentiality | HTTPS only, TLS 1.2+ |");
        sb.AppendLine("| CP-9 | Information System Backup | Soft delete, versioning, GRS |");
        sb.AppendLine("| AC-3 | Access Enforcement | Network rules, AAD auth only |");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"storage\" {");
        sb.AppendLine("  source = \"./modules/storage\"");
        sb.AppendLine();
        sb.AppendLine("  storage_account_name = \"mystorageaccount\"");
        sb.AppendLine("  resource_group_name  = azurerm_resource_group.main.name");
        sb.AppendLine("  location             = azurerm_resource_group.main.location");
        sb.AppendLine("  allowed_subnet_ids   = [azurerm_subnet.private.id]");
        sb.AppendLine("  tags                 = local.common_tags");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Composition");
        sb.AppendLine();
        sb.AppendLine("To add cross-cutting concerns, use the appropriate modules:");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"storage_pe\" {");
        sb.AppendLine("  source = \"./modules/cross-cutting/private-endpoint\"");
        sb.AppendLine("  ");
        sb.AppendLine("  resource_id   = module.storage.storage_account_id");
        sb.AppendLine("  resource_name = module.storage.storage_account_name");
        sb.AppendLine("  subresource   = \"blob\"");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }
}
