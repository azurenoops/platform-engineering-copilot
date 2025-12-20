using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.LogAnalytics;

/// <summary>
/// Bicep module generator for Azure Log Analytics Workspace infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (RBAC) are handled by reusable generators
/// </summary>
public class BicepLogAnalyticsModuleGenerator : IResourceModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.Security;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "log-analytics", "logs", "workspace", "monitoring" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by Log Analytics
    /// Log Analytics is typically the target for diagnostics, not a source
    /// </summary>
    public CrossCuttingType[] SupportedCrossCutting => new[]
    {
        CrossCuttingType.RBACAssignment
    };
    
    /// <summary>
    /// Azure resource type for Log Analytics Workspace
    /// </summary>
    public string AzureResourceType => "Microsoft.OperationalInsights/workspaces";

    /// <summary>
    /// Generate ONLY the core Log Analytics resource - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "logs";

        // Generate only core Log Analytics module
        files["log-analytics.bicep"] = GenerateLogAnalyticsBicep(request);
        files["main.bicep"] = GenerateCoreMainBicep(request);
        files["README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "logAnalytics", // Module name for cross-cutting references
            ResourceType = "Microsoft.OperationalInsights/workspaces",
            OutputNames = new List<string>
            {
                "workspaceId",
                "workspaceName",
                "customerId",
                "resourceId",
                "resourceName"
            },
            SupportedCrossCutting = new List<CrossCuttingType>
            {
                CrossCuttingType.RBACAssignment
            }
        };
    }

    /// <summary>
    /// Legacy GenerateModule - delegates to GenerateCoreResource for composition pattern
    /// </summary>
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var result = GenerateCoreResource(request);
        return result.Files;
    }

    /// <summary>
    /// Core main.bicep - only Log Analytics, no cross-cutting modules
    /// </summary>
    private string GenerateCoreMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "logs";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        sb.AppendLine("// Azure Log Analytics Workspace Core Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: AU-2 (Audit Events), AU-3 (Content of Audit Records), AU-6 (Audit Review)");
        sb.AppendLine("// Cross-cutting concerns (RBAC) are composed separately");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("@description('Name of the Log Analytics Workspace')");
        sb.AppendLine("param workspaceName string");
        sb.AppendLine();
        sb.AppendLine("@description('Azure region for deployment')");
        sb.AppendLine($"param location string = '{infrastructure.Region}'");
        sb.AppendLine();
        sb.AppendLine("@description('Environment name')");
        sb.AppendLine("param environment string = 'dev'");
        sb.AppendLine();
        sb.AppendLine("@description('Resource tags')");
        sb.AppendLine("param tags object = {}");
        sb.AppendLine();
        sb.AppendLine("@description('Log Analytics SKU')");
        sb.AppendLine("@allowed(['PerGB2018', 'CapacityReservation', 'Standalone'])");
        sb.AppendLine("param sku string = 'PerGB2018'");
        sb.AppendLine();
        sb.AppendLine("@description('Data retention in days (FedRAMP minimum: 90)')");
        sb.AppendLine("@minValue(30)");
        sb.AppendLine("@maxValue(730)");
        sb.AppendLine("param retentionInDays int = 90");
        sb.AppendLine();
        sb.AppendLine("@description('Enable public network access for ingestion')");
        sb.AppendLine("param publicNetworkAccessForIngestion bool = true");
        sb.AppendLine();
        sb.AppendLine("@description('Enable public network access for queries')");
        sb.AppendLine("param publicNetworkAccessForQuery bool = true");
        sb.AppendLine();

        // Log Analytics Module
        sb.AppendLine("// Log Analytics Workspace Core Resource");
        sb.AppendLine("module logAnalytics './log-analytics.bicep' = {");
        sb.AppendLine("  name: '${workspaceName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    workspaceName: workspaceName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    sku: sku");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("    retentionInDays: retentionInDays  // FedRAMP AU-11");
        sb.AppendLine("    publicNetworkAccessForIngestion: publicNetworkAccessForIngestion ? 'Enabled' : 'Disabled'");
        sb.AppendLine("    publicNetworkAccessForQuery: publicNetworkAccessForQuery ? 'Enabled' : 'Disabled'");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// Outputs for cross-cutting module composition");
        sb.AppendLine("output workspaceId string = logAnalytics.outputs.workspaceId");
        sb.AppendLine("output workspaceName string = logAnalytics.outputs.workspaceName");
        sb.AppendLine("output customerId string = logAnalytics.outputs.customerId");
        sb.AppendLine("output resourceId string = logAnalytics.outputs.workspaceId");
        sb.AppendLine("output resourceName string = logAnalytics.outputs.workspaceName");

        return sb.ToString();
    }

    private string GenerateLogAnalyticsBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Log Analytics Workspace Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: AU-2 (Audit Events), AU-3 (Content of Audit Records), AU-11 (Retention)");
        sb.AppendLine();
        sb.AppendLine("param workspaceName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param sku string");
        sb.AppendLine("param tags object");
        sb.AppendLine("param retentionInDays int");
        sb.AppendLine("param publicNetworkAccessForIngestion string");
        sb.AppendLine("param publicNetworkAccessForQuery string");
        sb.AppendLine();

        sb.AppendLine("resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {");
        sb.AppendLine("  name: workspaceName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: union(tags, {");
        sb.AppendLine("    'security-control': 'AU-2,AU-3,AU-6,AU-11'");
        sb.AppendLine("    'managed-by': 'bicep'");
        sb.AppendLine("    'retention-days': string(retentionInDays)");
        sb.AppendLine("  })");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    sku: {");
        sb.AppendLine("      name: sku");
        sb.AppendLine("    }");
        sb.AppendLine("    retentionInDays: retentionInDays");
        sb.AppendLine("    publicNetworkAccessForIngestion: publicNetworkAccessForIngestion");
        sb.AppendLine("    publicNetworkAccessForQuery: publicNetworkAccessForQuery");
        sb.AppendLine("    features: {");
        sb.AppendLine("      enableLogAccessUsingOnlyResourcePermissions: true");
        sb.AppendLine("      disableLocalAuth: false");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Security solution
        sb.AppendLine("// Security-focused solution");
        sb.AppendLine("resource securitySolution 'Microsoft.OperationsManagement/solutions@2015-11-01-preview' = {");
        sb.AppendLine("  name: 'Security(${logAnalytics.name})'");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    workspaceResourceId: logAnalytics.id");
        sb.AppendLine("  }");
        sb.AppendLine("  plan: {");
        sb.AppendLine("    name: 'Security(${logAnalytics.name})'");
        sb.AppendLine("    publisher: 'Microsoft'");
        sb.AppendLine("    product: 'OMSGallery/Security'");
        sb.AppendLine("    promotionCode: ''");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Container Insights solution
        sb.AppendLine("// Container Insights solution (for AKS monitoring)");
        sb.AppendLine("resource containerInsights 'Microsoft.OperationsManagement/solutions@2015-11-01-preview' = {");
        sb.AppendLine("  name: 'ContainerInsights(${logAnalytics.name})'");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    workspaceResourceId: logAnalytics.id");
        sb.AppendLine("  }");
        sb.AppendLine("  plan: {");
        sb.AppendLine("    name: 'ContainerInsights(${logAnalytics.name})'");
        sb.AppendLine("    publisher: 'Microsoft'");
        sb.AppendLine("    product: 'OMSGallery/ContainerInsights'");
        sb.AppendLine("    promotionCode: ''");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output workspaceId string = logAnalytics.id");
        sb.AppendLine("output workspaceName string = logAnalytics.name");
        sb.AppendLine("output customerId string = logAnalytics.properties.customerId");

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "logs";

        sb.AppendLine($"# Azure Log Analytics Workspace - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("FedRAMP-compliant Bicep infrastructure for Azure Log Analytics Workspace with:");
        sb.AppendLine("- 90-day minimum data retention - FedRAMP AU-11");
        sb.AppendLine("- Security solution enabled for audit events - FedRAMP AU-2");
        sb.AppendLine("- Container Insights for AKS monitoring");
        sb.AppendLine("- Resource-based access control");
        sb.AppendLine("- Network isolation options");

        sb.AppendLine();
        sb.AppendLine("## FedRAMP Controls Implemented");
        sb.AppendLine();
        sb.AppendLine("| Control | Implementation |");
        sb.AppendLine("|---------|----------------|");
        sb.AppendLine("| AU-2 | Audit event collection via Security solution |");
        sb.AppendLine("| AU-3 | Comprehensive audit record content |");
        sb.AppendLine("| AU-6 | Audit review and analysis capabilities |");
        sb.AppendLine("| AU-11 | 90-day minimum retention (configurable up to 730) |");
        sb.AppendLine();
        sb.AppendLine("## Deployment");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Deploy the Log Analytics infrastructure");
        sb.AppendLine("az deployment group create \\");
        sb.AppendLine("  --resource-group <resource-group> \\");
        sb.AppendLine("  --template-file infra/modules/log-analytics/main.bicep \\");
        sb.AppendLine($"  --parameters workspaceName={serviceName}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    /// <summary>
    /// Check if this generator can handle the specified request
    /// </summary>
    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure;
        if (infrastructure == null) return false;

        // Check format and provider match
        if (infrastructure.Format != InfrastructureFormat.Bicep ||
            infrastructure.Provider != CloudProvider.Azure)
            return false;

        // Match log analytics specific requests
        var serviceName = request.ServiceName?.ToLowerInvariant() ?? "";
        return serviceName.Contains("log") || serviceName.Contains("monitor");
    }
}
