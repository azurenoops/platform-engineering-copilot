using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Interface for platform-specific module generators (Terraform, Bicep, etc.)
/// </summary>
public interface IModuleGenerator
{
    /// <summary>
    /// Gets the infrastructure format this generator supports (Terraform, Bicep, etc.)
    /// </summary>
    InfrastructureFormat Format { get; }
    
    /// <summary>
    /// Gets the compute platform this generator supports (EKS, AKS, GKE, ECS, etc.)
    /// </summary>
    ComputePlatform Platform { get; }
    
    /// <summary>
    /// Gets the cloud provider this generator supports (AWS, Azure, GCP)
    /// </summary>
    CloudProvider Provider { get; }
    
    /// <summary>
    /// Generates module files for the specified request
    /// </summary>
    /// <param name="request">Template generation request</param>
    /// <returns>Dictionary of file paths to file contents</returns>
    Dictionary<string, string> GenerateModule(TemplateGenerationRequest request);
    
    /// <summary>
    /// Checks if this generator can handle the specified request
    /// </summary>
    /// <param name="request">Template generation request</param>
    /// <returns>True if this generator can handle the request</returns>
    bool CanGenerate(TemplateGenerationRequest request);
}
