using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;

/// <summary>
/// Interface for cross-cutting infrastructure module generators
/// These generate reusable components that attach to primary resources
/// (e.g., Private Endpoints, Diagnostic Settings, RBAC, NSG)
/// </summary>
public interface ICrossCuttingModuleGenerator
{
    /// <summary>
    /// The type of cross-cutting component this generator produces
    /// </summary>
    CrossCuttingType Type { get; }
    
    /// <summary>
    /// The infrastructure format this generator supports (Bicep, Terraform)
    /// </summary>
    InfrastructureFormat Format { get; }
    
    /// <summary>
    /// The cloud provider this generator supports (Azure, AWS, GCP)
    /// </summary>
    CloudProvider Provider { get; }
    
    /// <summary>
    /// Generate cross-cutting module files for attachment to a resource
    /// </summary>
    /// <param name="request">Request containing resource reference and configuration</param>
    /// <returns>Dictionary of file paths to file contents</returns>
    Dictionary<string, string> GenerateModule(CrossCuttingRequest request);
    
    /// <summary>
    /// Check if this generator can handle the specified resource type
    /// </summary>
    /// <param name="resourceType">Azure resource type (e.g., Microsoft.KeyVault/vaults)</param>
    /// <returns>True if this generator supports the resource type</returns>
    bool CanGenerate(string resourceType);
    
    /// <summary>
    /// Generate the Bicep/Terraform module invocation for the main orchestrator
    /// </summary>
    /// <param name="request">Cross-cutting request</param>
    /// <param name="dependsOn">Module name to depend on</param>
    /// <returns>Module invocation code snippet</returns>
    string GenerateModuleInvocation(CrossCuttingRequest request, string dependsOn);
}
