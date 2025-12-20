using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.CrossCutting;

/// <summary>
/// Terraform generator for Diagnostic Settings cross-cutting concern
/// Generates reusable Diagnostic Settings configuration for any Azure resource
/// </summary>
public class TerraformDiagnosticSettingsGenerator : ICrossCuttingModuleGenerator
{
    public CrossCuttingType Type => CrossCuttingType.DiagnosticSettings;
    public InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public CloudProvider Provider => CloudProvider.Azure;

    /// <summary>
    /// Mapping of Azure resource types to their supported log categories
    /// </summary>
    private static readonly Dictionary<string, string[]> ResourceTypeToLogCategories = new()
    {
        { "Microsoft.KeyVault/vaults", new[] { "AuditEvent", "AzurePolicyEvaluationDetails" } },
        { "Microsoft.Storage/storageAccounts", new[] { "StorageRead", "StorageWrite", "StorageDelete" } },
        { "Microsoft.ContainerRegistry/registries", new[] { "ContainerRegistryRepositoryEvents", "ContainerRegistryLoginEvents" } },
        { "Microsoft.Sql/servers/databases", new[] { "SQLInsights", "AutomaticTuning", "QueryStoreRuntimeStatistics" } },
        { "Microsoft.Web/sites", new[] { "AppServiceHTTPLogs", "AppServiceConsoleLogs", "AppServiceAppLogs" } },
        { "Microsoft.ContainerService/managedClusters", new[] { "kube-apiserver", "kube-audit", "kube-controller-manager", "kube-scheduler", "cluster-autoscaler" } },
        { "Microsoft.Network/applicationGateways", new[] { "ApplicationGatewayAccessLog", "ApplicationGatewayPerformanceLog", "ApplicationGatewayFirewallLog" } }
    };

    public Dictionary<string, string> GenerateModule(CrossCuttingRequest request)
    {
        var files = new Dictionary<string, string>();
        var resourceType = request.ResourceType ?? "Microsoft.KeyVault/vaults";

        files["diagnostic-settings.tf"] = GenerateDiagnosticSettingsTerraform(request, resourceType);
        files["variables.tf"] = GenerateVariablesTerraform(request);
        files["outputs.tf"] = GenerateOutputsTerraform(request);

        return files;
    }

    public bool CanGenerate(string resourceType)
    {
        // Diagnostic settings are supported for most Azure resources
        return !string.IsNullOrEmpty(resourceType);
    }

    public string GenerateModuleInvocation(CrossCuttingRequest request, string dependsOn)
    {
        var sb = new StringBuilder();
        var resourceName = request.ResourceReference ?? "resource";
        var config = request.DiagnosticSettings ?? new DiagnosticSettingsConfig();

        sb.AppendLine($"# Diagnostic Settings for {resourceName}");
        sb.AppendLine($"module \"diagnostics_{resourceName}\" {{");
        sb.AppendLine($"  source = \"./modules/diagnostic-settings\"");
        sb.AppendLine();
        sb.AppendLine($"  resource_id            = module.{dependsOn}.resource_id");
        sb.AppendLine($"  resource_name          = module.{dependsOn}.resource_name");
        sb.AppendLine($"  resource_type          = \"{request.ResourceType}\"");
        sb.AppendLine($"  log_analytics_workspace_id = \"{config.WorkspaceId}\"");
        if (!string.IsNullOrEmpty(config.StorageAccountId))
        {
            sb.AppendLine($"  storage_account_id    = \"{config.StorageAccountId}\"");
        }
        sb.AppendLine($"  retention_days         = {config.RetentionDays}");
        sb.AppendLine();
        sb.AppendLine($"  depends_on = [module.{dependsOn}]");
        sb.AppendLine($"}}");

        return sb.ToString();
    }

    private string GenerateDiagnosticSettingsTerraform(CrossCuttingRequest request, string resourceType)
    {
        var sb = new StringBuilder();
        var config = request.DiagnosticSettings ?? new DiagnosticSettingsConfig();
        var logCategories = GetLogCategoriesForResourceType(resourceType);

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# Diagnostic Settings Module - FedRAMP Compliant");
        sb.AppendLine("# Implements: AU-2 (Audit Events), AU-3 (Content of Audit Records), AU-6 (Audit Review)");
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

        // Local variables for log categories
        sb.AppendLine("# Default log categories by resource type");
        sb.AppendLine("locals {");
        sb.AppendLine("  log_categories = coalesce(var.log_categories, [");
        foreach (var category in logCategories)
        {
            sb.AppendLine($"    \"{category}\",");
        }
        sb.AppendLine("  ])");
        sb.AppendLine("}");
        sb.AppendLine();

        // Diagnostic Settings resource
        sb.AppendLine("resource \"azurerm_monitor_diagnostic_setting\" \"main\" {");
        sb.AppendLine("  name                       = \"${var.resource_name}-diag\"");
        sb.AppendLine("  target_resource_id         = var.resource_id");
        sb.AppendLine("  log_analytics_workspace_id = var.log_analytics_workspace_id");
        sb.AppendLine("  storage_account_id         = var.storage_account_id");
        sb.AppendLine();
        
        // Dynamic log categories
        sb.AppendLine("  dynamic \"enabled_log\" {");
        sb.AppendLine("    for_each = local.log_categories");
        sb.AppendLine("    content {");
        sb.AppendLine("      category = enabled_log.value");
        sb.AppendLine("      retention_policy {");
        sb.AppendLine("        enabled = true");
        sb.AppendLine("        days    = var.retention_days");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Metrics
        sb.AppendLine("  metric {");
        sb.AppendLine("    category = \"AllMetrics\"");
        sb.AppendLine("    enabled  = true");
        sb.AppendLine("    retention_policy {");
        sb.AppendLine("      enabled = true");
        sb.AppendLine("      days    = var.retention_days");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateVariablesTerraform(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# Diagnostic Settings Variables");
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_id\" {");
        sb.AppendLine("  description = \"The resource ID of the target resource\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_name\" {");
        sb.AppendLine("  description = \"The name of the target resource\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"resource_type\" {");
        sb.AppendLine("  description = \"The Azure resource type (e.g., Microsoft.KeyVault/vaults)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"log_analytics_workspace_id\" {");
        sb.AppendLine("  description = \"Log Analytics Workspace ID for diagnostics\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"storage_account_id\" {");
        sb.AppendLine("  description = \"Storage Account ID for log archival (optional)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"log_categories\" {");
        sb.AppendLine("  description = \"Log categories to enable (null for defaults based on resource type)\"");
        sb.AppendLine("  type        = list(string)");
        sb.AppendLine("  default     = null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"retention_days\" {");
        sb.AppendLine("  description = \"Log retention in days (FedRAMP minimum: 90)\"");
        sb.AppendLine("  type        = number");
        sb.AppendLine("  default     = 90");
        sb.AppendLine();
        sb.AppendLine("  validation {");
        sb.AppendLine("    condition     = var.retention_days >= 90");
        sb.AppendLine("    error_message = \"FedRAMP requires minimum 90 days retention for audit logs.\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateOutputsTerraform(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# Diagnostic Settings Outputs");
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();
        sb.AppendLine("output \"diagnostic_setting_id\" {");
        sb.AppendLine("  description = \"The ID of the diagnostic setting\"");
        sb.AppendLine("  value       = azurerm_monitor_diagnostic_setting.main.id");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("output \"diagnostic_setting_name\" {");
        sb.AppendLine("  description = \"The name of the diagnostic setting\"");
        sb.AppendLine("  value       = azurerm_monitor_diagnostic_setting.main.name");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string[] GetLogCategoriesForResourceType(string resourceType)
    {
        return ResourceTypeToLogCategories.TryGetValue(resourceType, out var categories) 
            ? categories 
            : new[] { "AuditEvent" }; // Default
    }
}
