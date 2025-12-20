using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.CrossCutting;

/// <summary>
/// Reusable Bicep RBAC Role Assignment Generator
/// Creates role assignments for any Azure resource
/// Implements FedRAMP AC-3 (Access Enforcement), AC-6 (Least Privilege)
/// </summary>
public class BicepRBACGenerator : ICrossCuttingModuleGenerator
{
    public CrossCuttingType Type => CrossCuttingType.RBACAssignment;
    public InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public CloudProvider Provider => CloudProvider.Azure;

    public Dictionary<string, string> GenerateModule(CrossCuttingRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var modulePath = string.IsNullOrEmpty(request.ModulePath) 
            ? "modules/rbac" 
            : request.ModulePath;

        files[$"{modulePath}/rbac.bicep"] = GenerateRBACBicep(request);

        return files;
    }

    public bool CanGenerate(string resourceType)
    {
        return CrossCuttingCapabilityMap.SupportsCapability(resourceType, CrossCuttingType.RBACAssignment);
    }

    public string GenerateModuleInvocation(CrossCuttingRequest request, string dependsOn)
    {
        var sb = new StringBuilder();
        var moduleName = $"{request.ResourceName.Replace("-", "_")}_rbac";
        var modulePath = string.IsNullOrEmpty(request.ModulePath) 
            ? "modules/rbac" 
            : request.ModulePath;
        var config = GetConfig(request);

        sb.AppendLine($"// RBAC Role Assignment for {request.ResourceName} - FedRAMP AC-3, AC-6");
        sb.AppendLine($"module {moduleName} './{modulePath}/rbac.bicep' = {{");
        sb.AppendLine($"  name: '{request.ResourceName}-rbac-deployment'");
        sb.AppendLine("  params: {");
        sb.AppendLine($"    resourceId: {request.ResourceReference}");
        sb.AppendLine($"    principalId: {(string.IsNullOrEmpty(config.PrincipalId) ? "principalId" : $"'{config.PrincipalId}'")}");
        sb.AppendLine($"    roleDefinitionId: '{CrossCuttingCapabilityMap.GetRoleDefinitionId(config.RoleDefinitionIdOrName)}'");
        sb.AppendLine($"    principalType: '{config.PrincipalType}'");
        sb.AppendLine("  }");
        
        if (!string.IsNullOrEmpty(dependsOn))
        {
            sb.AppendLine($"  dependsOn: [{dependsOn}]");
        }
        
        sb.AppendLine("}");

        return sb.ToString();
    }

    private RBACAssignmentConfig GetConfig(CrossCuttingRequest request)
    {
        if (request.Config.TryGetValue("rbac", out var configObj) && configObj is RBACAssignmentConfig rbacConfig)
        {
            return rbacConfig;
        }

        // Build config from individual properties
        return new RBACAssignmentConfig
        {
            PrincipalId = request.Config.TryGetValue("principalId", out var pid) ? pid?.ToString() ?? "" : "",
            RoleDefinitionIdOrName = request.Config.TryGetValue("roleDefinitionId", out var rid) ? rid?.ToString() ?? "Reader" : "Reader",
            PrincipalType = request.Config.TryGetValue("principalType", out var pt) ? pt?.ToString() ?? "ServicePrincipal" : "ServicePrincipal",
            Description = request.Config.TryGetValue("description", out var desc) ? desc?.ToString() : null
        };
    }

    private string GenerateRBACBicep(CrossCuttingRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// =============================================================================");
        sb.AppendLine("// RBAC Role Assignment Module - FedRAMP Compliant");
        sb.AppendLine("// Implements: AC-3 (Access Enforcement), AC-6 (Least Privilege)");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine();
        
        // Parameters
        sb.AppendLine("@description('Resource ID to scope the role assignment to')");
        sb.AppendLine("param resourceId string");
        sb.AppendLine();
        sb.AppendLine("@description('Principal ID to assign the role to')");
        sb.AppendLine("param principalId string");
        sb.AppendLine();
        sb.AppendLine("@description('Role Definition ID (GUID)')");
        sb.AppendLine("param roleDefinitionId string");
        sb.AppendLine();
        sb.AppendLine("@description('Principal type')");
        sb.AppendLine("@allowed(['User', 'Group', 'ServicePrincipal', 'ForeignGroup', 'Device'])");
        sb.AppendLine("param principalType string = 'ServicePrincipal'");
        sb.AppendLine();
        sb.AppendLine("@description('Description for the role assignment')");
        sb.AppendLine("param description string = 'Managed by Bicep - FedRAMP AC-3, AC-6'");
        sb.AppendLine();

        // Role Assignment resource
        sb.AppendLine("// Unique name for role assignment using resource scope, principal, and role");
        sb.AppendLine("var roleAssignmentName = guid(resourceId, principalId, roleDefinitionId)");
        sb.AppendLine();
        
        sb.AppendLine("// Role Assignment Resource");
        sb.AppendLine("// Note: This module should be deployed at the resource scope");
        sb.AppendLine("resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {");
        sb.AppendLine("  name: roleAssignmentName");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)");
        sb.AppendLine("    principalId: principalId");
        sb.AppendLine("    principalType: principalType");
        sb.AppendLine("    description: description");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Outputs
        sb.AppendLine("// ===== OUTPUTS =====");
        sb.AppendLine("output roleAssignmentId string = roleAssignment.id");
        sb.AppendLine("output roleAssignmentName string = roleAssignment.name");
        sb.AppendLine("output roleDefinitionId string = roleDefinitionId");

        return sb.ToString();
    }

    /// <summary>
    /// Generate an inline role assignment (not as a module) for simpler scenarios
    /// </summary>
    public string GenerateInlineRoleAssignment(
        string resourceName,
        string resourceSymbol,
        string principalIdParam,
        string roleDefinitionId,
        string principalType = "ServicePrincipal")
    {
        var sb = new StringBuilder();
        var roleId = CrossCuttingCapabilityMap.GetRoleDefinitionId(roleDefinitionId);

        sb.AppendLine($"// RBAC Role Assignment for {resourceName} - FedRAMP AC-3, AC-6");
        sb.AppendLine($"resource {resourceName.Replace("-", "_")}_rbac 'Microsoft.Authorization/roleAssignments@2022-04-01' = {{");
        sb.AppendLine($"  name: guid({resourceSymbol}.id, {principalIdParam}, '{roleId}')");
        sb.AppendLine($"  scope: {resourceSymbol}");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '{roleId}')");
        sb.AppendLine($"    principalId: {principalIdParam}");
        sb.AppendLine($"    principalType: '{principalType}'");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
