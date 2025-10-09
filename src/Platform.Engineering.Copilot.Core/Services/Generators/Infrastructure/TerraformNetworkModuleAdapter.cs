using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Generators.Terraform;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;

/// <summary>
/// Adapter for Terraform Network Module Generator
/// Handles pure network infrastructure (VNet, Subnets, NSG, DDoS, Peering) without compute
/// </summary>
public class TerraformNetworkModuleAdapter : ModuleGeneratorBase
{
    private readonly TerraformNetworkModuleGenerator _generator = new();

    public override InfrastructureFormat Format => InfrastructureFormat.Terraform;
    public override ComputePlatform Platform => ComputePlatform.Network;
    public override CloudProvider Provider => CloudProvider.Azure;

    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateModule(request);
    }
}
