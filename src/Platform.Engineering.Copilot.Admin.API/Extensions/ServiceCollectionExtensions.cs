using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Data.Extensions;
using Platform.Engineering.Copilot.Core.Data.Services;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;
using Platform.Engineering.Copilot.Core.Models.TemplateMatching;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Agents.Environments.Services;
using Platform.Engineering.Copilot.Agents.Infrastructure.Deployment;

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
        
        // Add database initialization service for migrations and seeding
        services.AddDatabaseInitialization();
        
        // Register deployment services (Bicep, Terraform deployers) for real Azure deployments
        services.AddScoped<ITemplateDeployer, BicepDeployer>();
        services.AddScoped<ITemplateDeployer, TerraformDeployer>();
        services.AddScoped<IDeployerFactory, DeployerFactory>();
        services.Configure<DeployerOptions>(configuration.GetSection("Deployment"));
        
        // Register Azure services for querying and managing Azure resources
        services.Configure<Core.Configuration.GatewayOptions>(configuration.GetSection("Gateway"));
        services.AddAzureClientFactory();
        services.AddSingleton<IAzureResourceService, AzureResourceService>();
        
        // Register template and environment services (Scoped to work with EF Core)
        services.AddScoped<IServiceTemplateCatalogService, ServiceTemplateCatalogService>();
        services.AddScoped<IProvisionedEnvironmentService, ProvisionedEnvironmentService>();
        services.AddScoped<Core.Interfaces.Environments.IEnvironmentActivityService, 
            Platform.Engineering.Copilot.Agents.Environments.Services.EnvironmentActivityService>();

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
            return new GitTemplateSyncService(logger, repository, options, httpClientFactory);
        });

        // Register Git sync background service for automatic periodic syncing
        services.AddHostedService<GitTemplateSyncBackgroundService>();

        // Register deployment status polling service for automatic status updates
        services.Configure<DeploymentPollingOptions>(configuration.GetSection("DeploymentPolling"));
        services.AddHostedService<DeploymentStatusPollingBackgroundService>();

        return services;
    }
}
