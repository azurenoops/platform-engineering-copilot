using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;

/// <summary>
/// Adapter for ECSModuleGenerator to work with the unified orchestrator
/// </summary>
public class TerraformECSModuleAdapter : ModuleGeneratorBase
{
    private readonly ECSModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public override ComputePlatform Platform => ComputePlatform.ECS;
    public override CloudProvider Provider => CloudProvider.AWS;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateModule(request);
    }
}

/// <summary>
/// Adapter for LambdaModuleGenerator to work with the unified orchestrator
/// </summary>
public class TerraformLambdaModuleAdapter : ModuleGeneratorBase
{
    private readonly LambdaModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public override ComputePlatform Platform => ComputePlatform.Lambda;
    public override CloudProvider Provider => CloudProvider.AWS;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateModule(request);
    }
}

/// <summary>
/// Adapter for CloudRunModuleGenerator to work with the unified orchestrator
/// </summary>
public class TerraformCloudRunModuleAdapter : ModuleGeneratorBase
{
    private readonly CloudRunModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public override ComputePlatform Platform => ComputePlatform.CloudRun;
    public override CloudProvider Provider => CloudProvider.GCP;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateModule(request);
    }
}

/// <summary>
/// Adapter for EKSModuleGenerator to work with the unified orchestrator
/// </summary>
public class TerraformEKSModuleAdapter : ModuleGeneratorBase
{
    private readonly EKSModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public override ComputePlatform Platform => ComputePlatform.EKS;
    public override CloudProvider Provider => CloudProvider.AWS;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateEKSModule(request);
    }
    
    public override bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        return infrastructure.Format == InfrastructureFormat.Terraform &&
               infrastructure.Provider == CloudProvider.AWS &&
               (infrastructure.ComputePlatform == ComputePlatform.EKS ||
                infrastructure.ComputePlatform == ComputePlatform.Kubernetes);
    }
}

/// <summary>
/// Adapter for GKEModuleGenerator to work with the unified orchestrator
/// </summary>
public class TerraformGKEModuleAdapter : ModuleGeneratorBase
{
    private readonly GKEModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public override ComputePlatform Platform => ComputePlatform.GKE;
    public override CloudProvider Provider => CloudProvider.GCP;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateGKEModule(request);
    }
    
    public override bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        return infrastructure.Format == InfrastructureFormat.Terraform &&
               infrastructure.Provider == CloudProvider.GCP &&
               (infrastructure.ComputePlatform == ComputePlatform.GKE ||
                infrastructure.ComputePlatform == ComputePlatform.Kubernetes);
    }
}

/// <summary>
/// Adapter for AKSModuleGenerator to work with the unified orchestrator
/// </summary>
public class TerraformAKSModuleAdapter : ModuleGeneratorBase
{
    private readonly AKSModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public override ComputePlatform Platform => ComputePlatform.AKS;
    public override CloudProvider Provider => CloudProvider.Azure;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateAKSModule(request);
    }
    
    public override bool CanGenerate(TemplateGenerationRequest request)
    {
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        
        return infrastructure.Format == InfrastructureFormat.Terraform &&
               infrastructure.Provider == CloudProvider.Azure &&
               (infrastructure.ComputePlatform == ComputePlatform.AKS ||
                infrastructure.ComputePlatform == ComputePlatform.Kubernetes);
    }
}

/// <summary>
/// Adapter for AppServiceModuleGenerator to work with the unified orchestrator
/// </summary>
public class TerraformAppServiceModuleAdapter : ModuleGeneratorBase
{
    private readonly AppServiceModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public override ComputePlatform Platform => ComputePlatform.AppService;
    public override CloudProvider Provider => CloudProvider.Azure;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateModule(request);
    }
}

/// <summary>
/// Adapter for ContainerInstancesModuleGenerator to work with the unified orchestrator
/// </summary>
public class TerraformContainerInstancesModuleAdapter : ModuleGeneratorBase
{
    private readonly ContainerInstancesModuleGenerator _generator = new();
    
    public override InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public override ComputePlatform Platform => ComputePlatform.ContainerApps;
    public override CloudProvider Provider => CloudProvider.Azure;
    
    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateModule(request);
    }
}
