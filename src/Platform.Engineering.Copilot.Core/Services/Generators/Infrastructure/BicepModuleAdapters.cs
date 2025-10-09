using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Generators.Bicep;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;

/// <summary>
/// Adapter for BicepAKSModuleGenerator to work with the unified orchestrator
/// </summary>
public class BicepAKSModuleAdapter : ModuleGeneratorBase
{
    private readonly BicepAKSModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public override ComputePlatform Platform => ComputePlatform.AKS;
    public override CloudProvider Provider => CloudProvider.Azure;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateAKSModule(request);
    }
    
    public override bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        return infrastructure.Format == InfrastructureFormat.Bicep &&
               infrastructure.Provider == CloudProvider.Azure &&
               (infrastructure.ComputePlatform == ComputePlatform.AKS ||
                infrastructure.ComputePlatform == ComputePlatform.Kubernetes);
    }
}

/// <summary>
/// Adapter for BicepAppServiceModuleGenerator to work with the unified orchestrator
/// </summary>
public class BicepAppServiceModuleAdapter : ModuleGeneratorBase
{
    private readonly BicepAppServiceModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public override ComputePlatform Platform => ComputePlatform.AppService;
    public override CloudProvider Provider => CloudProvider.Azure;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateAppServiceModule(request);
    }
}

/// <summary>
/// Adapter for BicepContainerAppsModuleGenerator to work with the unified orchestrator
/// </summary>
public class BicepContainerAppsModuleAdapter : ModuleGeneratorBase
{
    private readonly BicepContainerAppsModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public override ComputePlatform Platform => ComputePlatform.ContainerApps;
    public override CloudProvider Provider => CloudProvider.Azure;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateContainerAppsModule(request);
    }
}
