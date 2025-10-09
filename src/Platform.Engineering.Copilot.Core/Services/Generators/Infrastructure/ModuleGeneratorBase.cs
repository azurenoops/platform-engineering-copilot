using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;

/// <summary>
/// Base class for all module generators (Terraform, Bicep, etc.)
/// Provides common functionality and enforces consistent patterns
/// </summary>
public abstract class ModuleGeneratorBase : IModuleGenerator
{
    public abstract InfrastructureFormat Format { get; }
    public abstract ComputePlatform Platform { get; }
    public abstract CloudProvider Provider { get; }
    
    public abstract Dictionary<string, string> GenerateModule(TemplateGenerationRequest request);
    
    public virtual bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        // Check if format matches
        if (infrastructure.Format != Format)
            return false;
        
        // Check if compute platform matches (with Kubernetes wildcard support)
        if (Platform == ComputePlatform.Kubernetes)
        {
            // Kubernetes generators can handle EKS, GKE, AKS, or generic Kubernetes
            if (infrastructure.ComputePlatform != ComputePlatform.Kubernetes &&
                infrastructure.ComputePlatform != ComputePlatform.EKS &&
                infrastructure.ComputePlatform != ComputePlatform.GKE &&
                infrastructure.ComputePlatform != ComputePlatform.AKS)
            {
                return false;
            }
        }
        else if (infrastructure.ComputePlatform != Platform)
        {
            return false;
        }
        
        // Check if provider matches (skip check if provider is OnPremises for cloud providers)
        // This allows multi-cloud generators to work with any provider
        if (Provider != CloudProvider.OnPremises && 
            infrastructure.Provider != CloudProvider.OnPremises && 
            infrastructure.Provider != Provider)
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Helper to safely access deployment spec with defaults
    /// </summary>
    protected DeploymentSpec GetDeploymentSpec(TemplateGenerationRequest request)
    {
        return request.Deployment ?? new DeploymentSpec();
    }
    
    /// <summary>
    /// Helper to safely access infrastructure spec with defaults
    /// </summary>
    protected InfrastructureSpec GetInfrastructureSpec(TemplateGenerationRequest request)
    {
        return request.Infrastructure ?? new InfrastructureSpec();
    }
    
    /// <summary>
    /// Helper to safely access application spec with defaults
    /// </summary>
    protected ApplicationSpec GetApplicationSpec(TemplateGenerationRequest request)
    {
        return request.Application ?? new ApplicationSpec();
    }
    
    /// <summary>
    /// Helper to safely access security spec with defaults
    /// </summary>
    protected SecuritySpec GetSecuritySpec(TemplateGenerationRequest request)
    {
        return request.Security ?? new SecuritySpec();
    }
    
    /// <summary>
    /// Helper to safely access observability spec with defaults
    /// </summary>
    protected ObservabilitySpec GetObservabilitySpec(TemplateGenerationRequest request)
    {
        return request.Observability ?? new ObservabilitySpec();
    }
}
