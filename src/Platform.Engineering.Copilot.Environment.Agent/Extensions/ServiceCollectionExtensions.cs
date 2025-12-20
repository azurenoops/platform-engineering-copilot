using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Environment.Agent.Plugins;
using Platform.Engineering.Copilot.Environment.Agent.Services.Agents;
using Platform.Engineering.Copilot.Infrastructure.Core.Services;

namespace Platform.Engineering.Copilot.Environment.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEnvironmentAgent(this IServiceCollection services)
    {
        // Required for IMemoryCache subscription caching
        services.AddMemoryCache();
        
        // Register Environment Agent and Plugin
        services.AddScoped<EnvironmentAgent>();
        services.AddScoped<ISpecializedAgent, EnvironmentAgent>(sp => sp.GetRequiredService<EnvironmentAgent>());
        services.AddScoped<EnvironmentManagementPlugin>();

        // Register Environment Management Services
        services.AddScoped<IEnvironmentManagementEngine, EnvironmentManagementEngine>();
        
        // Register Environment Storage Service - Scoped (requires DbContext)
        services.AddScoped<EnvironmentStorageService>();
        
        return services;
    }
}
