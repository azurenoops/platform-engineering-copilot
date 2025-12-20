using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.KeyVault;

/// <summary>
/// Terraform module generator for Azure Key Vault infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (PE, diagnostics, RBAC) are handled by reusable generators
/// </summary>
public class TerraformKeyVaultResourceModuleGenerator : IResourceModuleGenerator
{    
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public ComputePlatform Platform => ComputePlatform.Security;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "keyvault", "key-vault", "vault", "secrets" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by Key Vault
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.PrivateEndpoint,
        CrossCuttingType.DiagnosticSettings,
        CrossCuttingType.RBACAssignment
    };
    
    /// <summary>
    /// Azure resource type for Key Vault
    /// </summary>
    public string AzureResourceType => "Microsoft.KeyVault/vaults";

    /// <summary>
    /// Generate ONLY the core Key Vault resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "keyvault";

        // Generate only core KeyVault module - no PE, diagnostics, or RBAC
        files["modules/keyvault/main.tf"] = GenerateCoreMainTf(request);
        files["modules/keyvault/variables.tf"] = GenerateCoreVariablesTf(request);
        files["modules/keyvault/outputs.tf"] = GenerateCoreOutputsTf(request);
        files["modules/keyvault/README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "azurerm_key_vault.keyvault", // Terraform resource reference
            ResourceType = "Microsoft.KeyVault/vaults",
            OutputNames = new List<string>
            {
                "key_vault_id",
                "key_vault_name",
                "key_vault_uri",
                "key_vault_tenant_id"
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
        // For full Key Vault modules, delegate to core resource generation
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
               infrastructure.ComputePlatform == ComputePlatform.Security;
    }

    /// <summary>
    /// Core main.tf - only Key Vault resource, no cross-cutting
    /// </summary>
    private string GenerateCoreMainTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "keyvault";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();

        sb.AppendLine("# Azure Key Vault Infrastructure Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: SC-12 (Cryptographic Key Management), SC-28 (Encryption at Rest), AU-2 (Audit Events), AC-3 (Access Control)");
        sb.AppendLine($"# Service: {serviceName}");
        sb.AppendLine($"# Region: {infrastructure.Region}");
        sb.AppendLine("# NOTE: Cross-cutting concerns (PE, diagnostics, RBAC) are composed via separate modules");
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
        sb.AppendLine("  enable_rbac_authorization       = var.enable_rbac  # FedRAMP AC-3 - Access control");
        sb.AppendLine("  soft_delete_retention_days      = var.soft_delete_retention_days  # FedRAMP AU-11");
        sb.AppendLine("  purge_protection_enabled        = true  # FedRAMP CP-9 - Prevent permanent deletion");
        sb.AppendLine("  public_network_access_enabled   = false # FedRAMP SC-7 - Network isolation");
        sb.AppendLine();

        sb.AppendLine("  network_acls {");
        sb.AppendLine("    bypass                     = \"AzureServices\"");
        sb.AppendLine("    default_action             = \"Deny\"");
        sb.AppendLine("    virtual_network_subnet_ids = var.allowed_subnet_ids");
        sb.AppendLine("  }");
        sb.AppendLine();

        sb.AppendLine("  tags = var.tags");
        sb.AppendLine("}");
        sb.AppendLine();

        // Access Policy (if not using RBAC)
        sb.AppendLine("# Key Vault Access Policy (when not using RBAC)");
        sb.AppendLine("resource \"azurerm_key_vault_access_policy\" \"deployer\" {");
        sb.AppendLine("  count        = var.enable_rbac ? 0 : 1");
        sb.AppendLine("  key_vault_id = azurerm_key_vault.keyvault.id");
        sb.AppendLine("  tenant_id    = data.azurerm_client_config.current.tenant_id");
        sb.AppendLine("  object_id    = data.azurerm_client_config.current.object_id");
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

        return sb.ToString();
    }

    /// <summary>
    /// Core variables.tf - only variables for core resource
    /// </summary>
    private string GenerateCoreVariablesTf(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Key Vault Variables - FedRAMP Compliant");
        sb.AppendLine("# Cross-cutting variables are defined in their respective modules");
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
        sb.AppendLine("  description = \"Azure region for resources\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"sku_name\" {");
        sb.AppendLine("  description = \"Key Vault SKU (standard or premium)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"premium\"  # FedRAMP requires premium for HSM-backed keys");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"enable_rbac\" {");
        sb.AppendLine("  description = \"Enable RBAC authorization for Key Vault\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = true  # FedRAMP AC-3 - Recommended");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"soft_delete_retention_days\" {");
        sb.AppendLine("  description = \"Number of days to retain soft-deleted items\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 90  # FedRAMP AU-11");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"allowed_subnet_ids\" {");
        sb.AppendLine("  description = \"List of subnet IDs allowed to access Key Vault\"");
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

        sb.AppendLine("# Key Vault Outputs");
        sb.AppendLine("# Used by cross-cutting modules for composition");
        sb.AppendLine();
        sb.AppendLine("output \"key_vault_id\" {");
        sb.AppendLine("  description = \"The ID of the Key Vault\"");
        sb.AppendLine("  value       = azurerm_key_vault.keyvault.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"key_vault_name\" {");
        sb.AppendLine("  description = \"The name of the Key Vault\"");
        sb.AppendLine("  value       = azurerm_key_vault.keyvault.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"key_vault_uri\" {");
        sb.AppendLine("  description = \"The URI of the Key Vault\"");
        sb.AppendLine("  value       = azurerm_key_vault.keyvault.vault_uri");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"key_vault_tenant_id\" {");
        sb.AppendLine("  description = \"The tenant ID of the Key Vault\"");
        sb.AppendLine("  value       = azurerm_key_vault.keyvault.tenant_id");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate README documentation
    /// </summary>
    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "keyvault";

        sb.AppendLine($"# Azure Key Vault Module - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("This Terraform module creates an Azure Key Vault with FedRAMP-compliant security settings.");
        sb.AppendLine("Cross-cutting concerns (Private Endpoints, Diagnostic Settings, RBAC) are composed via separate modules.");
        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Description | Implementation |");
        sb.AppendLine("|---------|-------------|----------------|");
        sb.AppendLine("| SC-12 | Cryptographic Key Management | Key Vault with Premium SKU |");
        sb.AppendLine("| SC-28 | Encryption at Rest | Enabled for disk encryption |");
        sb.AppendLine("| SC-7 | Boundary Protection | Network isolation, no public access |");
        sb.AppendLine("| AC-3 | Access Control | RBAC authorization |");
        sb.AppendLine("| CP-9 | Information Backup | Purge protection enabled |");
        sb.AppendLine("| AU-11 | Audit Record Retention | 90-day soft delete |");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"keyvault\" {");
        sb.AppendLine("  source = \"./modules/keyvault\"");
        sb.AppendLine();
        sb.AppendLine("  key_vault_name      = \"kv-myapp\"");
        sb.AppendLine("  resource_group_name = azurerm_resource_group.main.name");
        sb.AppendLine("  location            = azurerm_resource_group.main.location");
        sb.AppendLine("  allowed_subnet_ids  = [module.network.data_subnet_id]");
        sb.AppendLine("  tags                = local.common_tags");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Composition");
        sb.AppendLine();
        sb.AppendLine("To add cross-cutting concerns, use the appropriate modules:");
        sb.AppendLine();
        sb.AppendLine("```hcl");
        sb.AppendLine("module \"keyvault_pe\" {");
        sb.AppendLine("  source = \"./modules/cross-cutting/private-endpoint\"");
        sb.AppendLine("  ");
        sb.AppendLine("  resource_id   = module.keyvault.key_vault_id");
        sb.AppendLine("  resource_name = module.keyvault.key_vault_name");
        sb.AppendLine("  subresource   = \"vault\"");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }
}
