using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;

/// <summary>
/// Interface for resource module generators that produce core resources only
/// Cross-cutting concerns (private endpoints, diagnostics, RBAC) are handled separately
/// </summary>
public interface IResourceModuleGenerator : IModuleGenerator
{
    /// <summary>
    /// Resource types this generator can produce (normalized names)
    /// Example: ["keyvault", "key-vault", "vault"]
    /// </summary>
    string[] SupportedResourceTypes { get; }
    
    /// <summary>
    /// Cross-cutting capabilities this resource supports
    /// Used by CompositeInfrastructureGenerator to determine which cross-cutting modules to attach
    /// </summary>
    CrossCuttingType[] SupportedCrossCutting { get; }
    
    /// <summary>
    /// Azure resource type produced by this generator
    /// Example: "Microsoft.KeyVault/vaults"
    /// </summary>
    string AzureResourceType { get; }
    
    /// <summary>
    /// Generate ONLY the core resource module (no cross-cutting components)
    /// </summary>
    /// <param name="request">Template generation request</param>
    /// <returns>Result containing files, resource reference, and output names</returns>
    ResourceModuleResult GenerateCoreResource(TemplateGenerationRequest request);
}
