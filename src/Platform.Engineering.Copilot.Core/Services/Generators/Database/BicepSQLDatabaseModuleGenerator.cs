using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Database;

/// <summary>
/// Bicep module generator for Azure SQL Database infrastructure
/// </summary>
public class BicepSQLDatabaseModuleGenerator : IInfrastructureModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
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
        files["infra/modules/database/main.bicep"] = GenerateMainBicep(request);
        files["infra/modules/database/sql-server.bicep"] = GenerateSQLServerBicep(request);
        files["infra/modules/database/sql-database.bicep"] = GenerateSQLDatabaseBicep(request);
        
        if (security.EnablePrivateEndpoint)
        {
            files["infra/modules/database/private-endpoint.bicep"] = GeneratePrivateEndpointBicep(request);
        }

        if (observability.EnableDiagnostics == true)
        {
            files["infra/modules/database/diagnostics.bicep"] = GenerateDiagnosticsBicep(request);
        }

        if (security.EnableFirewall)
        {
            files["infra/modules/database/firewall-rules.bicep"] = GenerateFirewallRulesBicep(request);
        }

        // Generate README
        files["infra/modules/database/README.md"] = GenerateReadme(request);

        return files;
    }

    private string GenerateMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "sqldb";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var security = request.Security ?? new SecuritySpec();
        var observability = request.Observability ?? new ObservabilitySpec();

        sb.AppendLine("// Azure SQL Database Infrastructure Module");
        sb.AppendLine($"// Service: {serviceName}");
        sb.AppendLine($"// Region: {infrastructure.Region}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("@description('Name of the SQL Server')");
        sb.AppendLine("param sqlServerName string");
        sb.AppendLine();
        sb.AppendLine("@description('Name of the SQL Database')");
        sb.AppendLine("param sqlDatabaseName string");
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
        sb.AppendLine("@description('SQL Server administrator login')");
        sb.AppendLine("@secure()");
        sb.AppendLine("param administratorLogin string");
        sb.AppendLine();
        sb.AppendLine("@description('SQL Server administrator password')");
        sb.AppendLine("@secure()");
        sb.AppendLine("param administratorLoginPassword string");
        sb.AppendLine();
        sb.AppendLine("@description('SQL Database SKU')");
        sb.AppendLine("param skuName string = 'S0'");
        sb.AppendLine();
        sb.AppendLine("@description('SQL Database tier')");
        sb.AppendLine("param skuTier string = 'Standard'");
        sb.AppendLine();

        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("@description('Subnet ID for private endpoint')");
            sb.AppendLine("param subnetId string");
            sb.AppendLine();
        }

        if (observability.EnableDiagnostics == true)
        {
            sb.AppendLine("@description('Log Analytics Workspace ID')");
            sb.AppendLine("param logAnalyticsWorkspaceId string");
            sb.AppendLine();
        }

        // SQL Server Module
        sb.AppendLine("// SQL Server");
        sb.AppendLine("module sqlServer 'sql-server.bicep' = {");
        sb.AppendLine("  name: '${sqlServerName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    sqlServerName: sqlServerName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    administratorLogin: administratorLogin");
        sb.AppendLine("    administratorLoginPassword: administratorLoginPassword");
        sb.AppendLine("    tags: tags");
        sb.AppendLine($"    minimalTlsVersion: '{security.TLSVersion ?? "1.2"}'");
        sb.AppendLine($"    publicNetworkAccess: '{(security.EnablePrivateEndpoint == true ? "Disabled" : "Enabled")}'");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // SQL Database Module
        sb.AppendLine("// SQL Database");
        sb.AppendLine("module sqlDatabase 'sql-database.bicep' = {");
        sb.AppendLine("  name: '${sqlDatabaseName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    sqlServerName: sqlServer.outputs.sqlServerName");
        sb.AppendLine("    sqlDatabaseName: sqlDatabaseName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    skuName: skuName");
        sb.AppendLine("    skuTier: skuTier");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("  }");
        sb.AppendLine("  dependsOn: [");
        sb.AppendLine("    sqlServer");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();

        // Private Endpoint Module (if enabled)
        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("// Private Endpoint for SQL Server");
            sb.AppendLine("module privateEndpoint 'private-endpoint.bicep' = {");
            sb.AppendLine("  name: '${sqlServerName}-pe-deployment'");
            sb.AppendLine("  params: {");
            sb.AppendLine("    privateEndpointName: '${sqlServerName}-pe'");
            sb.AppendLine("    location: location");
            sb.AppendLine("    sqlServerId: sqlServer.outputs.sqlServerId");
            sb.AppendLine("    subnetId: subnetId");
            sb.AppendLine("    tags: tags");
            sb.AppendLine("  }");
            sb.AppendLine("  dependsOn: [");
            sb.AppendLine("    sqlServer");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Firewall Rules (if enabled and no private endpoint)
        if (security.EnableFirewall && security.EnablePrivateEndpoint != true)
        {
            sb.AppendLine("// Firewall Rules");
            sb.AppendLine("module firewallRules 'firewall-rules.bicep' = {");
            sb.AppendLine("  name: '${sqlServerName}-fw-deployment'");
            sb.AppendLine("  params: {");
            sb.AppendLine("    sqlServerName: sqlServer.outputs.sqlServerName");
            sb.AppendLine("  }");
            sb.AppendLine("  dependsOn: [");
            sb.AppendLine("    sqlServer");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Diagnostics Module (if enabled)
        if (observability.EnableDiagnostics == true)
        {
            sb.AppendLine("// Diagnostic Settings");
            sb.AppendLine("module diagnostics 'diagnostics.bicep' = {");
            sb.AppendLine("  name: '${sqlDatabaseName}-diag-deployment'");
            sb.AppendLine("  params: {");
            sb.AppendLine("    sqlServerName: sqlServer.outputs.sqlServerName");
            sb.AppendLine("    sqlDatabaseName: sqlDatabase.outputs.sqlDatabaseName");
            sb.AppendLine("    logAnalyticsWorkspaceId: logAnalyticsWorkspaceId");
            sb.AppendLine("  }");
            sb.AppendLine("  dependsOn: [");
            sb.AppendLine("    sqlDatabase");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Outputs
        sb.AppendLine("// Outputs");
        sb.AppendLine("output sqlServerId string = sqlServer.outputs.sqlServerId");
        sb.AppendLine("output sqlServerName string = sqlServer.outputs.sqlServerName");
        sb.AppendLine("output sqlServerFqdn string = sqlServer.outputs.sqlServerFqdn");
        sb.AppendLine("output sqlDatabaseId string = sqlDatabase.outputs.sqlDatabaseId");
        sb.AppendLine("output sqlDatabaseName string = sqlDatabase.outputs.sqlDatabaseName");
        
        if (security.EnablePrivateEndpoint)
        {
            sb.AppendLine("output privateEndpointId string = privateEndpoint.outputs.privateEndpointId");
        }

        return sb.ToString();
    }

    private string GenerateSQLServerBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// SQL Server Resource");
        sb.AppendLine();
        sb.AppendLine("param sqlServerName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param administratorLogin string");
        sb.AppendLine("@secure()");
        sb.AppendLine("param administratorLoginPassword string");
        sb.AppendLine("param tags object");
        sb.AppendLine("param minimalTlsVersion string");
        sb.AppendLine("param publicNetworkAccess string");
        sb.AppendLine();

        sb.AppendLine("resource sqlServer 'Microsoft.Sql/servers@2023-02-01-preview' = {");
        sb.AppendLine("  name: sqlServerName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    administratorLogin: administratorLogin");
        sb.AppendLine("    administratorLoginPassword: administratorLoginPassword");
        sb.AppendLine("    version: '12.0'");
        sb.AppendLine("    minimalTlsVersion: minimalTlsVersion");
        sb.AppendLine("    publicNetworkAccess: publicNetworkAccess");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output sqlServerId string = sqlServer.id");
        sb.AppendLine("output sqlServerName string = sqlServer.name");
        sb.AppendLine("output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName");

        return sb.ToString();
    }

    private string GenerateSQLDatabaseBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var security = request.Security ?? new SecuritySpec();
        var isFedRAMPCompliant = security.ComplianceStandards?.Any(s => 
            s.Contains("FedRAMP", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("DoD", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("NIST", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("IL5", StringComparison.OrdinalIgnoreCase)) ?? false;

        sb.AppendLine("// SQL Database Resource");
        sb.AppendLine("// FedRAMP/NIST Compliance: SC-28 (Encryption at Rest), AU-2 (Auditing), SC-7 (Network Protection)");
        sb.AppendLine();
        sb.AppendLine("param sqlServerName string");
        sb.AppendLine("param sqlDatabaseName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param skuName string");
        sb.AppendLine("param skuTier string");
        sb.AppendLine("param tags object");
        if (isFedRAMPCompliant)
        {
            sb.AppendLine();
            sb.AppendLine("@description('Enable zone redundancy for high availability (FedRAMP requirement)')");
            sb.AppendLine("param zoneRedundant bool = true");
            sb.AppendLine();
            sb.AppendLine("@description('Log Analytics Workspace ID for auditing (AU-2)')");
            sb.AppendLine("param logAnalyticsWorkspaceId string = ''");
        }
        sb.AppendLine();

        sb.AppendLine("resource sqlServer 'Microsoft.Sql/servers@2023-02-01-preview' existing = {");
        sb.AppendLine("  name: sqlServerName");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-02-01-preview' = {");
        sb.AppendLine("  parent: sqlServer");
        sb.AppendLine("  name: sqlDatabaseName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  sku: {");
        sb.AppendLine("    name: skuName");
        sb.AppendLine("    tier: skuTier");
        sb.AppendLine("  }");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    collation: 'SQL_Latin1_General_CP1_CI_AS'");
        sb.AppendLine("    maxSizeBytes: 2147483648"); // 2GB
        sb.AppendLine("    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'");
        sb.AppendLine($"    zoneRedundant: {(isFedRAMPCompliant ? "zoneRedundant" : "false")}");
        if (isFedRAMPCompliant)
        {
            sb.AppendLine("    // SC-28: Encryption at rest is enabled by default (TDE)");
            sb.AppendLine("    // Transparent Data Encryption (TDE) is automatically enabled for Azure SQL");
            sb.AppendLine("    requestedBackupStorageRedundancy: 'Geo' // GRS for disaster recovery");
        }
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Add TDE configuration for FedRAMP compliance
        if (isFedRAMPCompliant)
        {
            sb.AppendLine("// SC-28: Transparent Data Encryption (enabled by default, but explicit for compliance)");
            sb.AppendLine("resource tde 'Microsoft.Sql/servers/databases/transparentDataEncryption@2023-02-01-preview' = {");
            sb.AppendLine("  parent: sqlDatabase");
            sb.AppendLine("  name: 'current'");
            sb.AppendLine("  properties: {");
            sb.AppendLine("    state: 'Enabled'");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Add auditing for AU-2/AU-3 compliance
            sb.AppendLine("// AU-2, AU-3: Database Auditing");
            sb.AppendLine("resource sqlAudit 'Microsoft.Sql/servers/auditingSettings@2023-02-01-preview' = {");
            sb.AppendLine("  parent: sqlServer");
            sb.AppendLine("  name: 'default'");
            sb.AppendLine("  properties: {");
            sb.AppendLine("    state: 'Enabled'");
            sb.AppendLine("    isAzureMonitorTargetEnabled: true");
            sb.AppendLine("    retentionDays: 90 // FedRAMP requires minimum 90 days retention");
            sb.AppendLine("    auditActionsAndGroups: [");
            sb.AppendLine("      'SUCCESSFUL_DATABASE_AUTHENTICATION_GROUP'");
            sb.AppendLine("      'FAILED_DATABASE_AUTHENTICATION_GROUP'");
            sb.AppendLine("      'BATCH_COMPLETED_GROUP'");
            sb.AppendLine("      'DATABASE_PERMISSION_CHANGE_GROUP'");
            sb.AppendLine("      'DATABASE_PRINCIPAL_CHANGE_GROUP'");
            sb.AppendLine("      'DATABASE_ROLE_MEMBER_CHANGE_GROUP'");
            sb.AppendLine("      'SCHEMA_OBJECT_ACCESS_GROUP'");
            sb.AppendLine("      'SCHEMA_OBJECT_CHANGE_GROUP'");
            sb.AppendLine("      'SCHEMA_OBJECT_PERMISSION_CHANGE_GROUP'");
            sb.AppendLine("    ]");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Add threat detection for SI-4 compliance
            sb.AppendLine("// SI-4: Advanced Threat Protection");
            sb.AppendLine("resource threatDetection 'Microsoft.Sql/servers/securityAlertPolicies@2023-02-01-preview' = {");
            sb.AppendLine("  parent: sqlServer");
            sb.AppendLine("  name: 'Default'");
            sb.AppendLine("  properties: {");
            sb.AppendLine("    state: 'Enabled'");
            sb.AppendLine("    emailAccountAdmins: true");
            sb.AppendLine("    retentionDays: 90");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Add vulnerability assessment for RA-5 compliance
            sb.AppendLine("// RA-5: Vulnerability Assessment");
            sb.AppendLine("resource vulnerabilityAssessment 'Microsoft.Sql/servers/vulnerabilityAssessments@2023-02-01-preview' = {");
            sb.AppendLine("  parent: sqlServer");
            sb.AppendLine("  name: 'default'");
            sb.AppendLine("  properties: {");
            sb.AppendLine("    recurringScans: {");
            sb.AppendLine("      isEnabled: true");
            sb.AppendLine("      emailSubscriptionAdmins: true");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("  dependsOn: [");
            sb.AppendLine("    threatDetection");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();

            // Add Azure AD-only authentication for IA-2 compliance
            sb.AppendLine("// IA-2: Azure AD-only Authentication (recommended for FedRAMP)");
            sb.AppendLine("// Note: Uncomment below to enforce Azure AD-only auth (disables SQL auth)");
            sb.AppendLine("// resource azureAdOnlyAuth 'Microsoft.Sql/servers/azureADOnlyAuthentications@2023-02-01-preview' = {");
            sb.AppendLine("//   parent: sqlServer");
            sb.AppendLine("//   name: 'Default'");
            sb.AppendLine("//   properties: {");
            sb.AppendLine("//     azureADOnlyAuthentication: true");
            sb.AppendLine("//   }");
            sb.AppendLine("// }");
            sb.AppendLine();
        }

        sb.AppendLine("output sqlDatabaseId string = sqlDatabase.id");
        sb.AppendLine("output sqlDatabaseName string = sqlDatabase.name");

        return sb.ToString();
    }

    private string GeneratePrivateEndpointBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Private Endpoint for SQL Server");
        sb.AppendLine();
        sb.AppendLine("param privateEndpointName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param sqlServerId string");
        sb.AppendLine("param subnetId string");
        sb.AppendLine("param tags object");
        sb.AppendLine();

        sb.AppendLine("resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-04-01' = {");
        sb.AppendLine("  name: privateEndpointName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    subnet: {");
        sb.AppendLine("      id: subnetId");
        sb.AppendLine("    }");
        sb.AppendLine("    privateLinkServiceConnections: [");
        sb.AppendLine("      {");
        sb.AppendLine("        name: '${privateEndpointName}-connection'");
        sb.AppendLine("        properties: {");
        sb.AppendLine("          privateLinkServiceId: sqlServerId");
        sb.AppendLine("          groupIds: ['sqlServer']");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("output privateEndpointId string = privateEndpoint.id");

        return sb.ToString();
    }

    private string GenerateDiagnosticsBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Diagnostic Settings for SQL Database");
        sb.AppendLine();
        sb.AppendLine("param sqlServerName string");
        sb.AppendLine("param sqlDatabaseName string");
        sb.AppendLine("param logAnalyticsWorkspaceId string");
        sb.AppendLine();

        sb.AppendLine("resource sqlServer 'Microsoft.Sql/servers@2023-02-01-preview' existing = {");
        sb.AppendLine("  name: sqlServerName");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-02-01-preview' existing = {");
        sb.AppendLine("  parent: sqlServer");
        sb.AppendLine("  name: sqlDatabaseName");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {");
        sb.AppendLine("  name: '${sqlDatabaseName}-diagnostics'");
        sb.AppendLine("  scope: sqlDatabase");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    workspaceId: logAnalyticsWorkspaceId");
        sb.AppendLine("    logs: [");
        sb.AppendLine("      {");
        sb.AppendLine("        category: 'SQLInsights'");
        sb.AppendLine("        enabled: true");
        sb.AppendLine("      }");
        sb.AppendLine("      {");
        sb.AppendLine("        category: 'QueryStoreRuntimeStatistics'");
        sb.AppendLine("        enabled: true");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("    metrics: [");
        sb.AppendLine("      {");
        sb.AppendLine("        category: 'Basic'");
        sb.AppendLine("        enabled: true");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateFirewallRulesBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Firewall Rules for SQL Server");
        sb.AppendLine();
        sb.AppendLine("param sqlServerName string");
        sb.AppendLine();

        sb.AppendLine("resource sqlServer 'Microsoft.Sql/servers@2023-02-01-preview' existing = {");
        sb.AppendLine("  name: sqlServerName");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("// Allow Azure services");
        sb.AppendLine("resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-02-01-preview' = {");
        sb.AppendLine("  parent: sqlServer");
        sb.AppendLine("  name: 'AllowAllWindowsAzureIps'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    startIpAddress: '0.0.0.0'");
        sb.AppendLine("    endIpAddress: '0.0.0.0'");
        sb.AppendLine("  }");
        sb.AppendLine("}");

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
        sb.AppendLine("Bicep infrastructure for Azure SQL Database with:");
        sb.AppendLine("- Azure SQL Server");
        sb.AppendLine("- Azure SQL Database with configurable SKU");
        sb.AppendLine("- TLS encryption");
        
        if (request.Security?.EnablePrivateEndpoint == true)
        {
            sb.AppendLine("- Private endpoint connectivity");
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
        sb.AppendLine("## Deployment");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Deploy the SQL Database infrastructure");
        sb.AppendLine("az deployment group create \\");
        sb.AppendLine("  --resource-group <resource-group> \\");
        sb.AppendLine("  --template-file infra/modules/database/main.bicep \\");
        sb.AppendLine($"  --parameters sqlServerName={serviceName}-server sqlDatabaseName={serviceName}-db administratorLogin=sqladmin");
        sb.AppendLine("```");

        return sb.ToString();
    }

    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure;
        if (infrastructure == null) return false;

        return infrastructure.ComputePlatform == ComputePlatform.Database &&
               infrastructure.Format == InfrastructureFormat.Bicep &&
               infrastructure.Provider == CloudProvider.Azure;
    }

    public bool CanHandle(TemplateGenerationRequest request)
    {
        return CanGenerate(request);
    }
}
