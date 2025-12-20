using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Generators.AppService;
using Platform.Engineering.Copilot.Core.Services.Generators.AWS;
using Platform.Engineering.Copilot.Core.Services.Generators.Base;
using Platform.Engineering.Copilot.Core.Services.Generators.Containers;
using Platform.Engineering.Copilot.Core.Services.Generators.Google;
using Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Generators.Kubernetes;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Adapters;

/// <summary>
/// Adapter for TerraformECSModuleGenerator to work with the unified orchestrator
/// </summary>
public class TerraformECSModuleAdapter : ModuleGeneratorBase
{
    private readonly TerraformECSModuleGenerator _generator = new();
    
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
