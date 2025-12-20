using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.CrossCutting;

/// <summary>
/// Terraform generator for RBAC Assignment cross-cutting concern
/// Generates reusable RBAC role assignment configuration for any Azure resource
/// </summary>
public class TerraformRBACGenerator : ICrossCuttingModuleGenerator
{
    public CrossCuttingType Type => CrossCuttingType.RBACAssignment;
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public CloudProvider Provider => CloudProvider.Azure;

    /// <summary>
    /// Common Azure built-in role IDs
    /// </summary>
    private static readonly Dictionary<string, string> BuiltInRoles = new()
    {
        { "Owner", "8e3af657-a8ff-443c-a75c-2fe8c4bcb635" },
        { "Contributor", "b24988ac-6180-42a0-ab88-20f7382dd24c" },
        { "Reader", "acdd72a7-3385-48ef-bd42-f606fba81ae7" },
        { "Key Vault Administrator", "00482a5a-887f-4fb3-b363-3b7fe8e74483" },
        { "Key Vault Secrets User", "4633458b-17de-408a-b874-0445c86b69e6" },
        { "Key Vault Crypto User", "12338af0-0e69-4776-bea7-57ae8d297424" },
        { "Storage Blob Data Contributor", "ba92f5b4-2d11-453d-a403-e96b0029c9fe" },
        { "Storage Blob Data Reader", "2a2b9908-6ea1-4ae2-8e65-a410df84e7d1" },
        { "AcrPull", "7f951dda-4ed3-4680-a7ca-43fe172d538d" },
        { "AcrPush", "8311e382-0749-4cb8-b61a-304f252e45ec" },
        { "SQL DB Contributor", "9b7fa17d-e63e-47b0-bb0a-15c516ac86ec" },
        { "Monitoring Contributor", "749f88d5-cbae-40b8-bcfc-e573ddc772fa" },
        { "Monitoring Reader", "43d0d8ad-25c7-4714-9337-8ba259a9fe05" },
        { "Network Contributor", "4d97b98b-1d4f-4787-a291-c67834d212e7" },
        { "Virtual Machine Contributor", "9980e02c-c2be-4d73-94e8-173b1dc7cf3c" }
    };

    /// <summary>
    /// Mapping of Azure resource types to their recommended roles
    /// </summary>
    private static readonly Dictionary<string, string> ResourceTypeToDefaultRole = new()
    {
        { "Microsoft.KeyVault/vaults", "Key Vault Secrets User" },
        { "Microsoft.Storage/storageAccounts", "Storage Blob Data Contributor" },
        { "Microsoft.ContainerRegistry/registries", "AcrPull" },
        { "Microsoft.Sql/servers/databases", "SQL DB Contributor" },
        { "Microsoft.OperationalInsights/workspaces", "Monitoring Reader" }
    };

    public Dictionary<string, string> GenerateModule(CrossCuttingRequest request)
    {
        var files = new Dictionary<string, string>();

        files["rbac.tf"] = GenerateRBACTerraform(request);
        files["variables.tf"] = GenerateVariablesTerraform(request);
        files["outputs.tf"] = GenerateOutputsTerraform(request);

        return files;
    }

    public bool CanGenerate(string resourceType)
    {
        // RBAC is supported for all Azure resources
        return !string.IsNullOrEmpty(resourceType);
    }

    public string GenerateModuleInvocation(CrossCuttingRequest request, string dependsOn)
    {
        var sb = new StringBuilder();
        var resourceName = request.ResourceReference ?? "resource";
        var config = request.RBAC ?? new RBACAssignmentConfig();

        sb.AppendLine($"# RBAC Assignment for {resourceName}");
        sb.AppendLine($"module \"rbac_{resourceName}\" {{");
        sb.AppendLine($"  source = \"./modules/rbac\"");
        sb.AppendLine();
        sb.AppendLine($"  scope         = module.{dependsOn}.resource_id");
        sb.AppendLine($"  principal_id  = \"{config.PrincipalId}\"");
        
        if (!string.IsNullOrEmpty(config.RoleDefinitionIdOrName))
        {
            // Try to determine if it's a GUID (role ID) or a name
            if (Guid.TryParse(config.RoleDefinitionIdOrName, out _))
            {
                sb.AppendLine($"  role_definition_id = \"{config.RoleDefinitionIdOrName}\"");
            }
            else
            {
                sb.AppendLine($"  role_name     = \"{config.RoleDefinitionIdOrName}\"");
            }
        }
        
        sb.AppendLine($"  principal_type = \"{config.PrincipalType}\"");
        sb.AppendLine();
        sb.AppendLine($"  depends_on = [module.{dependsOn}]");
        sb.AppendLine($"}}");

        return sb.ToString();
    }

    private string GenerateRBACTerraform(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();
        var config = request.RBAC ?? new RBACAssignmentConfig();

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# RBAC Role Assignment Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: AC-2 (Account Management), AC-3 (Access Enforcement), AC-6 (Least Privilege)");
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();
        sb.AppendLine("terraform {");
        sb.AppendLine("  required_providers {");
        sb.AppendLine("    azurerm = {");
        sb.AppendLine("      source  = \"hashicorp/azurerm\"");
        sb.AppendLine("      version = \"~> 3.0\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Local variables for role mapping
        sb.AppendLine("# Built-in role IDs");
        sb.AppendLine("locals {");
        sb.AppendLine("  builtin_roles = {");
        foreach (var role in BuiltInRoles)
        {
            sb.AppendLine($"    \"{role.Key}\" = \"{role.Value}\"");
        }
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  # Resolve role definition ID from name or use provided ID");
        sb.AppendLine("  role_definition_id = var.role_definition_id != null ? var.role_definition_id : (");
        sb.AppendLine("    var.role_name != null ? lookup(local.builtin_roles, var.role_name, null) : null");
        sb.AppendLine("  )");
        sb.AppendLine("}");
        sb.AppendLine();

        // Role Assignment resource
        sb.AppendLine("resource \"azurerm_role_assignment\" \"main\" {");
        sb.AppendLine("  scope                = var.scope");
        sb.AppendLine("  role_definition_id   = \"/subscriptions/${data.azurerm_subscription.current.subscription_id}/providers/Microsoft.Authorization/roleDefinitions/${local.role_definition_id}\"");
        sb.AppendLine("  principal_id         = var.principal_id");
        sb.AppendLine("  principal_type       = var.principal_type");
        sb.AppendLine("  skip_service_principal_aad_check = var.skip_service_principal_aad_check");
        sb.AppendLine("  description          = var.description");
        sb.AppendLine("}");
        sb.AppendLine();

        // Data source for subscription
        sb.AppendLine("data \"azurerm_subscription\" \"current\" {}");

        return sb.ToString();
    }

    private string GenerateVariablesTerraform(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# RBAC Variables");
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();
        sb.AppendLine("variable \"scope\" {");
        sb.AppendLine("  description = \"The scope at which the role assignment applies (resource ID)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"principal_id\" {");
        sb.AppendLine("  description = \"The principal ID to assign the role to\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"role_definition_id\" {");
        sb.AppendLine("  description = \"The role definition ID (GUID) - use this OR role_name\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"role_name\" {");
        sb.AppendLine("  description = \"The built-in role name (e.g., 'Key Vault Secrets User') - use this OR role_definition_id\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"principal_type\" {");
        sb.AppendLine("  description = \"The type of principal (User, Group, ServicePrincipal, etc.)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"ServicePrincipal\"");
        sb.AppendLine();
        sb.AppendLine("  validation {");
        sb.AppendLine("    condition     = contains([\"User\", \"Group\", \"ServicePrincipal\", \"ForeignGroup\", \"Device\"], var.principal_type)");
        sb.AppendLine("    error_message = \"principal_type must be User, Group, ServicePrincipal, ForeignGroup, or Device.\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"skip_service_principal_aad_check\" {");
        sb.AppendLine("  description = \"Skip AAD check for service principal existence\"");
        sb.AppendLine("  type        = bool");
        sb.AppendLine("  default     = false");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"description\" {");
        sb.AppendLine("  description = \"Description of the role assignment\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"Managed by Terraform - FedRAMP AC-2/AC-3/AC-6\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateOutputsTerraform(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# RBAC Outputs");
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();
        sb.AppendLine("output \"role_assignment_id\" {");
        sb.AppendLine("  description = \"The ID of the role assignment\"");
        sb.AppendLine("  value       = azurerm_role_assignment.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"role_assignment_name\" {");
        sb.AppendLine("  description = \"The name of the role assignment\"");
        sb.AppendLine("  value       = azurerm_role_assignment.main.name");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"principal_type\" {");
        sb.AppendLine("  description = \"The type of principal\"");
        sb.AppendLine("  value       = azurerm_role_assignment.main.principal_type");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
