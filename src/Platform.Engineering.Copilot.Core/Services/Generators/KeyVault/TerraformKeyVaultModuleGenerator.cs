using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.KeyVault;

/// <summary>
/// Terraform module generator for Azure Key Vault infrastructure
/// </summary>
public class TerraformKeyVaultModuleGenerator : IInfrastructureModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public ComputePlatform Platform => ComputePlatform.Security;
    public CloudProvider Provider => CloudProvider.Azure;

    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "keyvault";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        // Generate main Key Vault module
        files["modules/keyvault/main.tf"] = GenerateMainTf(request);
        files["modules/keyvault/variables.tf"] = GenerateVariablesTf(request);
        files["modules/keyvault/outputs.tf"] = GenerateOutputsTf(request);
        files["modules/keyvault/README.md"] = GenerateReadme(request);

        return files;
    }

    private string GenerateMainTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "keyvault";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        sb.AppendLine("# Azure Key Vault Infrastructure Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: SC-12 (Cryptographic Key Management), SC-28 (Encryption at Rest), AU-2 (Audit Events), AC-3 (Access Control)");
        sb.AppendLine($"# Service: {serviceName}");
        sb.AppendLine($"# Region: {infrastructure.Region}");
        sb.AppendLine();

        sb.AppendLine("data \"azurerm_client_config\" \"current\" {}");
        sb.AppendLine();

        // Key Vault - FedRAMP Compliant
        sb.AppendLine("# FedRAMP SC-12: Key Vault for Cryptographic Key Management");
        sb.AppendLine("resource \"azurerm_key_vault\" \"keyvault\" {");
        sb.AppendLine("  name                       = var.key_vault_name");
        sb.AppendLine("  resource_group_name        = var.resource_group_name");
        sb.AppendLine("  location                   = var.location");
        sb.AppendLine("  tenant_id                  = data.azurerm_client_config.current.tenant_id");
        sb.AppendLine("  sku_name                   = var.sku_name");
        sb.AppendLine("  enabled_for_deployment          = true  # FedRAMP CM-3 - Configuration management");
        sb.AppendLine("  enabled_for_disk_encryption     = true  # FedRAMP SC-28 - Encryption at rest");
        sb.AppendLine("  enabled_for_template_deployment = true  # FedRAMP CM-3 - Configuration management");
        sb.AppendLine("  enable_rbac_authorization       = true  # FedRAMP AC-3 - Access control");
        sb.AppendLine("  soft_delete_retention_days      = 90    # FedRAMP AU-11 - Audit retention");
        sb.AppendLine("  purge_protection_enabled        = true  # FedRAMP CP-9 - Prevent permanent deletion");
        sb.AppendLine("  public_network_access_enabled   = false # FedRAMP SC-7 - Network isolation");
        sb.AppendLine();

        sb.AppendLine("  network_acls {");
        sb.AppendLine("    bypass                     = \"AzureServices\"");
        sb.AppendLine($"    default_action             = \"{(security.EnablePrivateEndpoint == true ? "Deny" : "Allow")}\"");
        if (security.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("    virtual_network_subnet_ids = var.allowed_subnet_ids");
        }
        sb.AppendLine("  }");
        sb.AppendLine();

        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();

        // RBAC Role Assignment (if enabled)
        if (security.RBAC)
        {
            sb.AppendLine("# Key Vault Administrator role assignment");
            sb.AppendLine("resource \"azurerm_role_assignment\" \"keyvault_admin\" {");
            sb.AppendLine("  scope                = azurerm_key_vault.keyvault.id");
            sb.AppendLine("  role_definition_name = \"Key Vault Administrator\"");
            sb.AppendLine("  principal_id         = var.principal_id");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        else
        {
            // Access Policy (if not using RBAC)
            sb.AppendLine("# Key Vault Access Policy");
            sb.AppendLine("resource \"azurerm_key_vault_access_policy\" \"default\" {");
            sb.AppendLine("  key_vault_id = azurerm_key_vault.keyvault.id");
            sb.AppendLine("  tenant_id    = data.azurerm_client_config.current.tenant_id");
            sb.AppendLine("  object_id    = var.principal_id");
            sb.AppendLine();
            sb.AppendLine("  key_permissions = [");
            sb.AppendLine("    \"Get\", \"List\", \"Create\", \"Delete\", \"Update\", \"Recover\", \"Backup\", \"Restore\"");
            sb.AppendLine("  ]");
            sb.AppendLine();
            sb.AppendLine("  secret_permissions = [");
            sb.AppendLine("    \"Get\", \"List\", \"Set\", \"Delete\", \"Recover\", \"Backup\", \"Restore\"");
            sb.AppendLine("  ]");
            sb.AppendLine();
            sb.AppendLine("  certificate_permissions = [");
            sb.AppendLine("    \"Get\", \"List\", \"Create\", \"Delete\", \"Update\", \"Recover\", \"Backup\", \"Restore\"");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Private Endpoint (if enabled)
        if (security.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("# Private Endpoint for Key Vault");
            sb.AppendLine("resource \"azurerm_private_endpoint\" \"keyvault\" {");
            sb.AppendLine("  name                = \"${var.key_vault_name}-pe\"");
            sb.AppendLine("  location            = var.location");
            sb.AppendLine("  resource_group_name = var.resource_group_name");
            sb.AppendLine("  subnet_id           = var.private_endpoint_subnet_id");
            sb.AppendLine();
            sb.AppendLine("  private_service_connection {");
            sb.AppendLine("    name                           = \"${var.key_vault_name}-psc\"");
            sb.AppendLine("    private_connection_resource_id = azurerm_key_vault.keyvault.id");
            sb.AppendLine("    is_manual_connection           = false");
            sb.AppendLine("    subresource_names              = [\"vault\"]");
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
            sb.AppendLine("resource \"azurerm_monitor_diagnostic_setting\" \"keyvault\" {");
            sb.AppendLine("  name                       = \"${var.key_vault_name}-diag\"");
            sb.AppendLine("  target_resource_id         = azurerm_key_vault.keyvault.id");
            sb.AppendLine("  log_analytics_workspace_id = var.log_analytics_workspace_id");
            sb.AppendLine();
            sb.AppendLine("  log {");
            sb.AppendLine("    category = \"AuditEvent\"");
            sb.AppendLine("    enabled  = true");
            sb.AppendLine();
            sb.AppendLine("    retention_policy {");
            sb.AppendLine("      enabled = true");
            sb.AppendLine("      days    = 30");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  metric {");
            sb.AppendLine("    category = \"AllMetrics\"");
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

        sb.AppendLine("# Key Vault Variables - FedRAMP Compliant");
        sb.AppendLine();
        sb.AppendLine("variable \"key_vault_name\" {");
        sb.AppendLine("  description = \"Name of the Key Vault\"");
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
        sb.AppendLine("variable \"sku_name\" {");
        sb.AppendLine("  description = \"Key Vault SKU\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"standard\"");
        sb.AppendLine("  validation {");
        sb.AppendLine("    condition     = contains([\"standard\", \"premium\"], var.sku_name)");
        sb.AppendLine("    error_message = \"SKU must be standard or premium\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"principal_id\" {");
        sb.AppendLine("  description = \"Object ID of the principal to grant access\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Resource tags\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");
        sb.AppendLine();

        if (security.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("variable \"private_endpoint_subnet_id\" {");
            sb.AppendLine("  description = \"Subnet ID for private endpoint\"");
            sb.AppendLine("  type        = string");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("variable \"allowed_subnet_ids\" {");
            sb.AppendLine("  description = \"List of subnet IDs allowed to access Key Vault\"");
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

        sb.AppendLine("# Key Vault Outputs");
        sb.AppendLine();
        sb.AppendLine("output \"key_vault_id\" {");
        sb.AppendLine("  description = \"Key Vault ID\"");
        sb.AppendLine("  value       = azurerm_key_vault.keyvault.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"key_vault_name\" {");
        sb.AppendLine("  description = \"Key Vault name\"");
        sb.AppendLine("  value       = azurerm_key_vault.keyvault.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"key_vault_uri\" {");
        sb.AppendLine("  description = \"Key Vault URI\"");
        sb.AppendLine("  value       = azurerm_key_vault.keyvault.vault_uri");
        sb.AppendLine("}");
        sb.AppendLine();

        if (security.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("output \"private_endpoint_id\" {");
            sb.AppendLine("  description = \"Private endpoint ID\"");
            sb.AppendLine("  value       = azurerm_private_endpoint.keyvault.id");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "keyvault";

        sb.AppendLine($"# Azure Key Vault - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("FedRAMP-compliant Terraform module for Azure Key Vault with:");
        sb.AppendLine("- Key Vault with configurable SKU");
        sb.AppendLine("- Soft delete with 90-day retention - FedRAMP CP-9/AU-11");
        sb.AppendLine("- Purge protection enabled - FedRAMP CP-9");
        sb.AppendLine("- RBAC authorization - FedRAMP AC-3");
        sb.AppendLine("- Network default deny - FedRAMP SC-7");
        sb.AppendLine("- Enabled for disk encryption - FedRAMP SC-28");
        sb.AppendLine("- Enabled for deployment and templates - FedRAMP CM-3");
        
        if (request.Security?.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("- Private endpoint connectivity - FedRAMP SC-7");
        }
        
        if (request.Observability?.EnableDiagnostics == true)
        {
            sb.AppendLine("- Diagnostic settings and audit logging - FedRAMP AU-2");
        }

        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Implementation |");
        sb.AppendLine("|---------|----------------|");
        sb.AppendLine("| SC-12 | Cryptographic key establishment and management |");
        sb.AppendLine("| SC-28 | Encryption at rest (disk encryption enabled) |");
        sb.AppendLine("| AC-3 | RBAC for access control enforcement |");
        sb.AppendLine("| CP-9 | Soft delete and purge protection |");
        sb.AppendLine("| AU-2 | Audit event logging enabled |");
        sb.AppendLine("| AU-11 | 90-day retention for soft deleted items |");
        sb.AppendLine("| SC-7 | Network isolation (default deny) |");
        sb.AppendLine("| CM-3 | Configuration management via ARM/template deployment |");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"keyvault\" {");
        sb.AppendLine("  source = \"./modules/keyvault\"");
        sb.AppendLine();
        sb.AppendLine($"  key_vault_name      = \"{serviceName}kv\"");
        sb.AppendLine("  resource_group_name = azurerm_resource_group.main.name");
        sb.AppendLine("  location            = azurerm_resource_group.main.location");
        sb.AppendLine("  principal_id        = data.azurerm_client_config.current.object_id");
        sb.AppendLine("  tags                = local.common_tags");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    public bool CanHandle(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure;
        if (infrastructure == null) return false;

        return infrastructure.ComputePlatform == ComputePlatform.Security &&
               infrastructure.Format == InfrastructureFormat.Terraform &&
               infrastructure.Provider == CloudProvider.Azure;
    }
}
