using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Admin.API.Extensions;

/// <summary>
/// Registers Admin API services: DbContext, domain services, deployer, and background services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdminServices(this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core — InMemory or SqlServer toggle via DatabaseProvider config
        var provider = configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
        if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
                options.UseInMemoryDatabase("PlatformCopilotAdmin"));
        }
        else
        {
            services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        }

        // Domain services
        services.AddScoped<IServiceTemplateCatalogService, ServiceTemplateCatalogService>();
        services.AddScoped<IProvisionedEnvironmentService, ProvisionedEnvironmentService>();
        services.AddScoped<INaturalLanguageTemplateMatchingService, NaturalLanguageTemplateMatchingService>();
        services.AddScoped<IGitTemplateSyncService, GitTemplateSyncService>();
        services.AddScoped<IAzureResourceService, AzureResourceService>();
        services.AddScoped<EnvironmentActivityService>();
        services.AddScoped<BicepParameterParser>();

        // Deployer
        services.AddSingleton<IDeployerFactory, DeployerFactory>();

        // Background services
        services.AddHostedService<Platform.Engineering.Copilot.Core.BackgroundServices.GitTemplateSyncBackgroundService>();
        services.AddHostedService<Platform.Engineering.Copilot.Core.BackgroundServices.DeploymentStatusPollingBackgroundService>();
        services.AddHostedService<Platform.Engineering.Copilot.Core.BackgroundServices.SoftDeletePurgeBackgroundService>();

        return services;
    }
}
