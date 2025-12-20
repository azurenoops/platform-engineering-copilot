using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.CrossCutting;

/// <summary>
/// Reusable Bicep Diagnostic Settings Generator
/// Creates diagnostic settings for any supported Azure resource type
/// Implements FedRAMP AU-2 (Audit Events), AU-6 (Audit Review), AU-12 (Audit Generation)
/// </summary>
public class BicepDiagnosticSettingsGenerator : ICrossCuttingModuleGenerator
{
    public CrossCuttingType Type => CrossCuttingType.DiagnosticSettings;
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public CloudProvider Provider => CloudProvider.Azure;

    public Dictionary<string, string> GenerateModule(CrossCuttingRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var modulePath = string.IsNullOrEmpty(request.ModulePath) 
            ? "modules/diagnostics" 
            : request.ModulePath;

        files[$"{modulePath}/diagnostics.bicep"] = GenerateDiagnosticsBicep(request);

        return files;
    }

    public bool CanGenerate(string resourceType)
    {
        // Almost all Azure resources support diagnostic settings
        return CrossCuttingCapabilityMap.SupportsCapability(resourceType, CrossCuttingType.DiagnosticSettings);
    }

    public string GenerateModuleInvocation(CrossCuttingRequest request, string dependsOn)
    {
        var sb = new StringBuilder();
        var moduleName = $"{request.ResourceName.Replace("-", "_")}_diag";
        var modulePath = string.IsNullOrEmpty(request.ModulePath) 
            ? "modules/diagnostics" 
            : request.ModulePath;
        var config = GetConfig(request);

        sb.AppendLine($"// Diagnostic Settings for {request.ResourceName} - FedRAMP AU-2, AU-6");
        sb.AppendLine($"module {moduleName} './{modulePath}/diagnostics.bicep' = {{");
        sb.AppendLine($"  name: '{request.ResourceName}-diag-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine($"    resourceName: '{request.ResourceName}'");
        sb.AppendLine($"    resourceId: {request.ResourceReference}");
        sb.AppendLine($"    workspaceId: {(string.IsNullOrEmpty(config.WorkspaceId) ? "logAnalyticsWorkspaceId" : $"'{config.WorkspaceId}'")}");
        
        if (!string.IsNullOrEmpty(config.StorageAccountId))
        {
            sb.AppendLine($"    storageAccountId: '{config.StorageAccountId}'");
        }
        
        sb.AppendLine("  }");
        
        if (!string.IsNullOrEmpty(dependsOn))
        {
            sb.AppendLine($"  dependsOn: [{dependsOn}]");
        }
        
        sb.AppendLine("}");

        return sb.ToString();
    }

    private DiagnosticSettingsConfig GetConfig(CrossCuttingRequest request)
    {
        if (request.Config.TryGetValue("diagnostics", out var configObj) && configObj is DiagnosticSettingsConfig diagConfig)
        {
            return diagConfig;
        }

        // Build config from individual properties
        return new DiagnosticSettingsConfig
        {
            WorkspaceId = request.Config.TryGetValue("workspaceId", out var ws) ? ws?.ToString() ?? "" : "",
            StorageAccountId = request.Config.TryGetValue("storageAccountId", out var sa) ? sa?.ToString() : null,
            RetentionDays = request.Config.TryGetValue("retentionDays", out var rd) && rd is int days ? days : 90,
            LogCategories = request.Config.TryGetValue("logCategories", out var lc) && lc is List<string> cats ? cats : null
        };
    }

    private string GenerateDiagnosticsBicep(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();
        var logCategories = CrossCuttingCapabilityMap.GetDefaultLogCategories(request.ResourceType);
        var config = GetConfig(request);

        // Use custom categories if provided
        if (config.LogCategories != null && config.LogCategories.Any())
        {
            logCategories = config.LogCategories;
        }

        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// Diagnostic Settings Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: AU-2 (Audit Events), AU-6 (Audit Review), AU-12 (Audit Generation)");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine();
        
        // Parameters
        sb.AppendLine("@description('Name of the resource for diagnostic settings naming')");
        sb.AppendLine("param resourceName string");
        sb.AppendLine();
        sb.AppendLine("@description('Resource ID to attach diagnostic settings to')");
        sb.AppendLine("param resourceId string");
        sb.AppendLine();
        sb.AppendLine("@description('Log Analytics Workspace ID')");
        sb.AppendLine("param workspaceId string");
        sb.AppendLine();
        sb.AppendLine("@description('Storage Account ID for long-term archival (optional)')");
        sb.AppendLine("param storageAccountId string = ''");
        sb.AppendLine();
        sb.AppendLine("@description('Event Hub Authorization Rule ID (optional)')");
        sb.AppendLine("param eventHubAuthorizationRuleId string = ''");
        sb.AppendLine();
        sb.AppendLine("@description('Event Hub Name (optional)')");
        sb.AppendLine("param eventHubName string = ''");
        sb.AppendLine();
        sb.AppendLine("@description('Retention days (0 = indefinite, FedRAMP minimum: 90)')");
        sb.AppendLine("@minValue(0)");
        sb.AppendLine("@maxValue(365)");
        sb.AppendLine($"param retentionDays int = {config.RetentionDays}");
        sb.AppendLine();

        // Diagnostic Settings resource
        sb.AppendLine("// Diagnostic Settings Resource");
        sb.AppendLine("resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {");
        sb.AppendLine("  name: '${resourceName}-diagnostics'");
        sb.AppendLine("  scope: resource");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    workspaceId: workspaceId");
        sb.AppendLine("    storageAccountId: !empty(storageAccountId) ? storageAccountId : null");
        sb.AppendLine("    eventHubAuthorizationRuleId: !empty(eventHubAuthorizationRuleId) ? eventHubAuthorizationRuleId : null");
        sb.AppendLine("    eventHubName: !empty(eventHubName) ? eventHubName : null");
        
        // Log categories
        sb.AppendLine("    logs: [");
        foreach (var category in logCategories)
        {
            sb.AppendLine("      {");
            if (category == "allLogs")
            {
                sb.AppendLine($"        categoryGroup: '{category}'");
            }
            else
            {
                sb.AppendLine($"        category: '{category}'");
            }
            sb.AppendLine("        enabled: true");
            if (config.RetentionDays > 0)
            {
                sb.AppendLine("        retentionPolicy: {");
                sb.AppendLine("          enabled: true");
                sb.AppendLine("          days: retentionDays");
                sb.AppendLine("        }");
            }
            sb.AppendLine("      }");
        }
        sb.AppendLine("    ]");
        
        // Metrics
        sb.AppendLine("    metrics: [");
        sb.AppendLine("      {");
        sb.AppendLine("        category: 'AllMetrics'");
        sb.AppendLine("        enabled: true");
        if (config.RetentionDays > 0)
        {
            sb.AppendLine("        retentionPolicy: {");
            sb.AppendLine("          enabled: true");
            sb.AppendLine("          days: retentionDays");
            sb.AppendLine("        }");
        }
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Note about existing resource reference
        sb.AppendLine("// Reference to the existing resource (passed via scope)");
        sb.AppendLine("// The resource reference is determined by the resource type at deployment time");
        sb.AppendLine("resource resource 'Microsoft.Resources/deployments@2021-04-01' existing = {");
        sb.AppendLine("  name: resourceId");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// ===== OUTPUTS =====");
        sb.AppendLine("output diagnosticSettingsId string = diagnosticSettings.id");
        sb.AppendLine("output diagnosticSettingsName string = diagnosticSettings.name");

        return sb.ToString();
    }
}
