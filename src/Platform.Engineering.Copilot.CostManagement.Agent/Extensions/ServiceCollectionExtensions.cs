using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Cost;
using Platform.Engineering.Copilot.CostManagement.Agent.Services.Agents;
using Platform.Engineering.Copilot.CostManagement.Agent.Plugins;
using Platform.Engineering.Copilot.Core.Services.Azure.Cost;
using Platform.Engineering.Copilot.CostManagement.Core.Configuration;

namespace Platform.Engineering.Copilot.CostManagement.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCostManagementAgent(this IServiceCollection services)
    {
        // Note: Configuration is registered in Program.cs from AgentConfiguration:CostManagementAgent section

        // Required for IMemoryCache subscription caching
        services.AddMemoryCache();
        
        // Register Cost Management Agent and Plugin
        services.AddScoped<CostManagementAgent>();
        services.AddScoped<ISpecializedAgent, CostManagementAgent>(sp => sp.GetRequiredService<CostManagementAgent>());
        services.AddScoped<CostManagementPlugin>();
        
        // Register Cost Optimization Services
        services.AddScoped<ICostOptimizationEngine, CostOptimizationEngine>();
        
        // Register Azure Metrics Service
        services.AddScoped<AzureMetricsService>();
        services.AddScoped<IAzureMetricsService, AzureMetricsService>();
        
        // Register Azure Cost Management Service
        services.AddScoped<AzureCostManagementService>();
        services.AddScoped<IAzureCostManagementService, AzureCostManagementService>();
        
        return services;
    }
}
