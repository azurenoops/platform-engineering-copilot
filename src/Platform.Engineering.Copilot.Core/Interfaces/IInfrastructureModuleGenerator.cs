using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Interface for platform-specific infrastructure module generators
/// Supports both Terraform and Bicep implementations
/// </summary>
public interface IInfrastructureModuleGenerator
{
    /// <summary>
    /// The infrastructure format this generator produces (Terraform, Bicep, etc.)
    /// </summary>
    InfrastructureFormat Format { get; }
    
    /// <summary>
    /// The compute platform this generator targets (ECS, EKS, AKS, etc.)
    /// </summary>
    ComputePlatform Platform { get; }
    
    /// <summary>
    /// The cloud provider this generator supports (AWS, Azure, GCP)
    /// </summary>
    CloudProvider Provider { get; }
    
    /// <summary>
    /// Generates infrastructure module files for the specific platform
    /// </summary>
    /// <param name="request">Template generation request with all specifications</param>
    /// <returns>Dictionary of file paths to file contents</returns>
    Dictionary<string, string> GenerateModule(TemplateGenerationRequest request);
    
    /// <summary>
    /// Validates if this generator can handle the given request
    /// </summary>
    /// <param name="request">Template generation request to validate</param>
    /// <returns>True if this generator can handle the request</returns>
    bool CanHandle(TemplateGenerationRequest request);
}
