using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Data.Extensions;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.TemplateMatching;
using Platform.Engineering.Copilot.Agents.Environments.Services;

namespace Platform.Engineering.Copilot.Admin.API.Extensions;

/// <summary>
/// Service collection extensions for Admin API
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all Admin API services
    /// </summary>
    public static IServiceCollection AddAdminServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register database context and repositories from Core data layer
        // This registers PlatformEngineeringCopilotContext, all repositories, and related services
        var useInMemoryDb = configuration.GetValue<bool>("UseInMemoryDatabase");
        
        if (useInMemoryDb)
        {
            services.AddEnvironmentManagementDataInMemory("AdminApiTestDb");
        }
        else
        {
            services.AddEnvironmentManagementData(configuration);
        }
        
        // Register template and environment services (Scoped to work with EF Core)
        services.AddScoped<IServiceTemplateCatalogService, ServiceTemplateCatalogService>();
        services.AddScoped<IProvisionedEnvironmentService, ProvisionedEnvironmentService>();

        // Register NL template matching service (optional - works without LLM)
        services.AddScoped<INaturalLanguageTemplateMatchingService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<NaturalLanguageTemplateMatchingService>>();
            var catalogService = sp.GetRequiredService<IServiceTemplateCatalogService>();
            var kernel = sp.GetService<Kernel>(); // Optional - uses keyword matching if not available
            return new NaturalLanguageTemplateMatchingService(logger, catalogService, kernel);
        });

        // Register Git sync service
        services.Configure<GitSyncOptions>(configuration.GetSection("GitSync"));
        services.AddHttpClient<IGitTemplateSyncService, GitTemplateSyncService>();
        services.AddScoped<IGitTemplateSyncService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<GitTemplateSyncService>>();
            var repository = sp.GetRequiredService<Core.Data.Repositories.IServiceTemplateRepository>();
            var options = sp.GetRequiredService<IOptions<GitSyncOptions>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new GitTemplateSyncService(logger, repository, options, httpClientFactory.CreateClient());
        });

        // Register Git sync background service for automatic periodic syncing
        services.AddHostedService<GitTemplateSyncBackgroundService>();

        return services;
    }
}
