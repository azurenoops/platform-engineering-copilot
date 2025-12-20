using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Database;

/// <summary>
/// Bicep module generator for Azure SQL Database infrastructure
/// Implements IResourceModuleGenerator for composition-based generation
/// Cross-cutting concerns (PE, diagnostics, RBAC) are handled by reusable generators
/// </summary>
public class BicepSQLModuleGenerator : IResourceModuleGenerator
{
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public ComputePlatform Platform => ComputePlatform.Database;
    public CloudProvider Provider => CloudProvider.Azure;
    
    /// <summary>
    /// Resource types this generator handles
    /// </summary>
    public string[] SupportedResourceTypes => new[] { "sql-database", "sql", "azure-sql", "sqldb" };
    
    /// <summary>
    /// Cross-cutting capabilities supported by Azure SQL
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
    /// Generate ONLY the core SQL resources - cross-cutting modules are composed by orchestrator
    /// </summary>
    public ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var serviceName = request.ServiceName ?? "sql";

        // Generate only core SQL modules - no PE, diagnostics, or RBAC
        files["sql-server.bicep"] = GenerateSQLServerBicep(request);
        files["sql-database.bicep"] = GenerateSQLDatabaseBicep(request);
        files["main.bicep"] = GenerateCoreMainBicep(request);
        files["README.md"] = GenerateReadme(request);

        return new ResourceModuleResult
        {
            Files = files,
            ResourceReference = "sqlServer", // Module name for cross-cutting references
            ResourceType = "Microsoft.Sql/servers",
            OutputNames = new List<string>
            {
                "sqlServerId",
                "sqlServerName",
                "sqlDatabaseId",
                "sqlDatabaseName",
                "fullyQualifiedDomainName",
                "connectionString",
                "resourceId",
                "resourceName"
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
    /// Legacy GenerateModule - delegates to GenerateCoreResource for composition pattern
    /// </summary>
    public Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        var result = GenerateCoreResource(request);
        return result.Files;
    }
    
    /// <summary>
    /// Check if this generator can handle the request
    /// </summary>
    public bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        return infrastructure.Format == InfrastructureFormat.Bicep &&
               infrastructure.Provider == CloudProvider.Azure &&
               infrastructure.ComputePlatform == ComputePlatform.Database;
    }

    /// <summary>
    /// Core main.bicep - only SQL Server and Database, no cross-cutting modules
    /// Cross-cutting modules are composed by the orchestrator
    /// </summary>
    private string GenerateCoreMainBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "sql";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        sb.AppendLine("// Azure SQL Database Core Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-28 (Encryption), AC-3 (Access Control), AU-2 (Audit Events)");
        sb.AppendLine("// Cross-cutting concerns (PE, diagnostics, RBAC) are composed separately");
        sb.AppendLine($"// Service: {serviceName}");
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
        sb.AppendLine("@description('Enable Azure AD authentication')");
        sb.AppendLine("param enableAzureADAuth bool = true");
        sb.AppendLine();
        sb.AppendLine("@description('Azure AD admin object ID')");
        sb.AppendLine("param azureADAdminObjectId string = ''");
        sb.AppendLine();
        sb.AppendLine("@description('Azure AD admin login name')");
        sb.AppendLine("param azureADAdminLogin string = ''");
        sb.AppendLine();
        sb.AppendLine("@description('SQL Database SKU name')");
        sb.AppendLine("@allowed(['Basic', 'S0', 'S1', 'S2', 'S3', 'S4', 'P1', 'P2', 'P4', 'GP_S_Gen5_1', 'GP_Gen5_2'])");
        sb.AppendLine("param skuName string = 'S0'");
        sb.AppendLine();
        sb.AppendLine("@description('Enable public network access')");
        sb.AppendLine("param enablePublicNetworkAccess bool = false");
        sb.AppendLine();

        // SQL Server Module
        sb.AppendLine("// SQL Server Core Resource");
        sb.AppendLine("module sqlServer './sql-server.bicep' = {");
        sb.AppendLine("  name: '${sqlServerName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    sqlServerName: sqlServerName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    administratorLogin: administratorLogin");
        sb.AppendLine("    administratorLoginPassword: administratorLoginPassword");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("    minimalTlsVersion: '1.2'  // FedRAMP SC-8");
        sb.AppendLine("    publicNetworkAccess: enablePublicNetworkAccess ? 'Enabled' : 'Disabled'");
        sb.AppendLine("    enableAzureADAuth: enableAzureADAuth");
        sb.AppendLine("    azureADAdminObjectId: azureADAdminObjectId");
        sb.AppendLine("    azureADAdminLogin: azureADAdminLogin");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // SQL Database Module
        sb.AppendLine("// SQL Database");
        sb.AppendLine("module sqlDatabase './sql-database.bicep' = {");
        sb.AppendLine("  name: '${sqlDatabaseName}-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine("    sqlServerName: sqlServer.outputs.sqlServerName");
        sb.AppendLine("    sqlDatabaseName: sqlDatabaseName");
        sb.AppendLine("    location: location");
        sb.AppendLine("    skuName: skuName");
        sb.AppendLine("    tags: tags");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// Outputs for cross-cutting module composition");
        sb.AppendLine("output sqlServerId string = sqlServer.outputs.sqlServerId");
        sb.AppendLine("output sqlServerName string = sqlServer.outputs.sqlServerName");
        sb.AppendLine("output sqlDatabaseId string = sqlDatabase.outputs.sqlDatabaseId");
        sb.AppendLine("output sqlDatabaseName string = sqlDatabase.outputs.sqlDatabaseName");
        sb.AppendLine("output fullyQualifiedDomainName string = sqlServer.outputs.fullyQualifiedDomainName");
        sb.AppendLine("output connectionString string = 'Server=tcp:${sqlServer.outputs.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};'");
        sb.AppendLine("output resourceId string = sqlServer.outputs.sqlServerId");
        sb.AppendLine("output resourceName string = sqlServer.outputs.sqlServerName");

        return sb.ToString();
    }

    private string GenerateSQLServerBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// SQL Server Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-28 (Encryption), AC-3 (Access Control), SC-8 (Transmission Confidentiality)");
        sb.AppendLine();
        sb.AppendLine("param sqlServerName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param tags object");
        sb.AppendLine("@secure()");
        sb.AppendLine("param administratorLogin string");
        sb.AppendLine("@secure()");
        sb.AppendLine("param administratorLoginPassword string");
        sb.AppendLine("param minimalTlsVersion string");
        sb.AppendLine("param publicNetworkAccess string");
        sb.AppendLine("param enableAzureADAuth bool");
        sb.AppendLine("param azureADAdminObjectId string");
        sb.AppendLine("param azureADAdminLogin string");
        sb.AppendLine();

        sb.AppendLine("resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {");
        sb.AppendLine("  name: sqlServerName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    administratorLogin: administratorLogin");
        sb.AppendLine("    administratorLoginPassword: administratorLoginPassword");
        sb.AppendLine("    minimalTlsVersion: minimalTlsVersion");
        sb.AppendLine("    publicNetworkAccess: publicNetworkAccess");
        sb.AppendLine("    version: '12.0'");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Azure AD Admin
        sb.AppendLine("// Azure AD Administrator - FedRAMP AC-3");
        sb.AppendLine("resource sqlServerAzureADAdmin 'Microsoft.Sql/servers/administrators@2023-05-01-preview' = if (enableAzureADAuth && !empty(azureADAdminObjectId)) {");
        sb.AppendLine("  parent: sqlServer");
        sb.AppendLine("  name: 'ActiveDirectory'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    administratorType: 'ActiveDirectory'");
        sb.AppendLine("    login: azureADAdminLogin");
        sb.AppendLine("    sid: azureADAdminObjectId");
        sb.AppendLine("    tenantId: subscription().tenantId");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Transparent Data Encryption
        sb.AppendLine("// Transparent Data Encryption - FedRAMP SC-28");
        sb.AppendLine("resource transparentDataEncryption 'Microsoft.Sql/servers/encryptionProtector@2023-05-01-preview' = {");
        sb.AppendLine("  parent: sqlServer");
        sb.AppendLine("  name: 'current'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    serverKeyType: 'ServiceManaged'");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("output sqlServerId string = sqlServer.id");
        sb.AppendLine("output sqlServerName string = sqlServer.name");
        sb.AppendLine("output fullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName");

        return sb.ToString();
    }

    private string GenerateSQLDatabaseBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// SQL Database Resource - FedRAMP Compliant");
        sb.AppendLine("// Implements: SC-28 (Encryption at Rest)");
        sb.AppendLine();
        sb.AppendLine("param sqlServerName string");
        sb.AppendLine("param sqlDatabaseName string");
        sb.AppendLine("param location string");
        sb.AppendLine("param skuName string");
        sb.AppendLine("param tags object");
        sb.AppendLine();

        sb.AppendLine("resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' existing = {");
        sb.AppendLine("  name: sqlServerName");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {");
        sb.AppendLine("  parent: sqlServer");
        sb.AppendLine("  name: sqlDatabaseName");
        sb.AppendLine("  location: location");
        sb.AppendLine("  tags: tags");
        sb.AppendLine("  sku: {");
        sb.AppendLine("    name: skuName");
        sb.AppendLine("  }");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    collation: 'SQL_Latin1_General_CP1_CI_AS'");
        sb.AppendLine("    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'");
        sb.AppendLine("    zoneRedundant: false");
        sb.AppendLine("    readScale: 'Disabled'");
        sb.AppendLine("    requestedBackupStorageRedundancy: 'Geo'  // FedRAMP CP-9");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Short-term backup retention
        sb.AppendLine("// Short-term backup retention - FedRAMP CP-9");
        sb.AppendLine("resource backupPolicy 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-05-01-preview' = {");
        sb.AppendLine("  parent: sqlDatabase");
        sb.AppendLine("  name: 'default'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    retentionDays: 14");
        sb.AppendLine("    diffBackupIntervalInHours: 12");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("output sqlDatabaseId string = sqlDatabase.id");
        sb.AppendLine("output sqlDatabaseName string = sqlDatabase.name");

        return sb.ToString();
    }

    private string GenerateReadme(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "sql";

        sb.AppendLine($"# Azure SQL Database Module - {serviceName}");
        sb.AppendLine();
        sb.AppendLine("## FedRAMP Compliance");
        sb.AppendLine();
        sb.AppendLine("This module implements the following NIST 800-53 controls:");
        sb.AppendLine();
        sb.AppendLine("| Control | Description | Implementation |");
        sb.AppendLine("|---------|-------------|----------------|");
        sb.AppendLine("| SC-28 | Protection of Information at Rest | Transparent Data Encryption (TDE) enabled |");
        sb.AppendLine("| SC-8 | Transmission Confidentiality | TLS 1.2 minimum |");
        sb.AppendLine("| CP-9 | Information System Backup | Geo-redundant backup with 14-day retention |");
        sb.AppendLine("| AC-3 | Access Enforcement | Azure AD authentication, public access disabled |");
        sb.AppendLine("| AU-2 | Audit Events | Supports diagnostic settings for audit logging |");
        sb.AppendLine();
        sb.AppendLine("## Architecture");
        sb.AppendLine();
        sb.AppendLine("This is a **core resource module** that generates SQL Server and Database resources.");
        sb.AppendLine("Cross-cutting concerns are composed separately by the infrastructure orchestrator:");
        sb.AppendLine();
        sb.AppendLine("- **Private Endpoint**: Network isolation via Azure Private Link");
        sb.AppendLine("- **Diagnostic Settings**: Audit logging to Log Analytics");
        sb.AppendLine("- **RBAC**: Role-based access control assignments");
        sb.AppendLine();
        sb.AppendLine("## Parameters");
        sb.AppendLine();
        sb.AppendLine("| Parameter | Type | Default | Description |");
        sb.AppendLine("|-----------|------|---------|-------------|");
        sb.AppendLine("| sqlServerName | string | required | Name of the SQL server |");
        sb.AppendLine("| sqlDatabaseName | string | required | Name of the database |");
        sb.AppendLine("| administratorLogin | string | required | SQL admin login |");
        sb.AppendLine("| administratorLoginPassword | string | required | SQL admin password |");
        sb.AppendLine("| skuName | string | S0 | Database SKU |");
        sb.AppendLine();
        sb.AppendLine("## Outputs");
        sb.AppendLine();
        sb.AppendLine("| Output | Description |");
        sb.AppendLine("|--------|-------------|");
        sb.AppendLine("| sqlServerId | Resource ID of the SQL server |");
        sb.AppendLine("| sqlServerName | Name of the SQL server |");
        sb.AppendLine("| fullyQualifiedDomainName | FQDN for connections |");
        sb.AppendLine("| connectionString | Connection string template |");

        return sb.ToString();
    }
}
