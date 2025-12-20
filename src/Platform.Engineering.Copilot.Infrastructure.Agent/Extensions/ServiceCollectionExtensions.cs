using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;
using Platform.Engineering.Copilot.Infrastructure.Core.Services;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.ServiceCreation;
using Platform.Engineering.Copilot.Core.Services.Generators.Documentation;
using Platform.Engineering.Copilot.Core.Services.Generators.Composite;
using Platform.Engineering.Copilot.Infrastructure.Agent.Configuration;
using Platform.Engineering.Copilot.Infrastructure.Agent.Plugins;
using Platform.Engineering.Copilot.Infrastructure.Agent.Services.Deployment;

namespace Platform.Engineering.Copilot.Infrastructure.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureAgent(this IServiceCollection services)
    {
        // Note: Configuration is registered in Program.cs from AgentConfiguration:InfrastructureAgent section
        
        // Required for IMemoryCache subscription caching
        services.AddMemoryCache();
        
        // Register Infrastructure Agent and Plugin
        services.AddScoped<InfrastructureAgent>();
        services.AddScoped<ISpecializedAgent, InfrastructureAgent>(sp => sp.GetRequiredService<InfrastructureAgent>());
        services.AddScoped<InfrastructurePlugin>();
        
        // Register Deployment Orchestration Service
        services.AddSingleton<IDeploymentOrchestrationService, DeploymentOrchestrationService>();
        
        // Register Infrastructure Services
        services.AddScoped<NetworkTopologyDesignService>();
        services.AddScoped<INetworkTopologyDesignService, NetworkTopologyDesignService>();
        services.AddScoped<PredictiveScalingEngine>();
        services.AddScoped<InfrastructureProvisioningService>();
        services.AddScoped<IInfrastructureProvisioningService, InfrastructureProvisioningService>();

        // Register Azure Services
        services.AddScoped<IPredictiveScalingEngine, PredictiveScalingEngine>();
        
        // Register Template Generation Services
        services.AddScoped<DynamicTemplateGeneratorService>();
        services.AddScoped<IDynamicTemplateGenerator, DynamicTemplateGeneratorService>();
        
        // Register Composite Infrastructure Generator for multi-resource orchestration
        services.AddScoped<ICompositeInfrastructureGenerator, CompositeInfrastructureGenerator>();
        
        // Register Service Wizard Services
        services.AddScoped<ServiceWizardStateManager>();
        services.AddScoped<WizardPromptEngine>();
        services.AddScoped<DoDMetadataValidator>();
        services.AddScoped<DoDDocumentationGenerator>();
        services.AddScoped<ServiceWizardPlugin>();
        
        return services;
    }
}
