using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Discovery;
using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Azure.Graph;
using Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Discovery.Agent.Plugins;
using Platform.Engineering.Copilot.Discovery.Agent.Services;
using Platform.Engineering.Copilot.Discovery.Core.Configuration;

namespace Platform.Engineering.Copilot.Discovery.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscoveryAgent(this IServiceCollection services)
    {
        // Note: Configuration is registered in Program.cs from AgentConfiguration:DiscoveryAgent section
        
        // Register Azure Resource Graph Service
        services.AddSingleton<AzureResourceGraphService>();
        
        // Register Resource Type Handlers
        services.AddSingleton<IResourceTypeHandler, AppServiceHandler>();
        services.AddSingleton<IResourceTypeHandler, AksHandler>();
        services.AddSingleton<IResourceTypeHandler, StorageAccountHandler>();
        services.AddSingleton<IResourceTypeHandler, SqlDatabaseHandler>();
        services.AddSingleton<IResourceTypeHandler, KeyVaultHandler>();
        
        // Register Discovery Service
        services.AddScoped<IAzureResourceDiscoveryService, AzureResourceDiscoveryService>();
        
        // Register Discovery Agent and Plugin
        services.AddScoped<DiscoveryAgent>();
        services.AddScoped<ISpecializedAgent, DiscoveryAgent>(sp => sp.GetRequiredService<DiscoveryAgent>());
        services.AddScoped<AzureResourceDiscoveryPlugin>();
        
        return services;
    }
}
