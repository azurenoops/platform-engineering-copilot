using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Generators.Bicep;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;

/// <summary>
/// Adapter for Bicep Network Module Generator
/// Handles pure network infrastructure (VNet, Subnets, NSG, DDoS, Peering) without compute
/// </summary>
public class BicepNetworkModuleAdapter : ModuleGeneratorBase
{
    private readonly BicepNetworkModuleGenerator _generator = new();

    public override InfrastructureFormat Format => InfrastructureFormat.Bicep;
    public override ComputePlatform Platform => ComputePlatform.Network;
    public override CloudProvider Provider => CloudProvider.Azure;

    public override Dictionary<string, string> GenerateModule(TemplateGenerationRequest request)
    {
        return _generator.GenerateModule(request);
    }
}
