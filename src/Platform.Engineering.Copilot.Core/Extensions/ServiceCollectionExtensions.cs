using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Azure.Identity;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Cache;
using Platform.Engineering.Copilot.Core.Services.Compliance;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Onboarding;

namespace Platform.Engineering.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for registering Platform.Engineering.Copilot.Core services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all Platform.Engineering.Copilot.Core services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddSupervisorCore(this IServiceCollection services)
    {
        // Register caching services
        services.AddMemoryCache(); // Required for IMemoryCache
        services.AddSingleton<IIntelligentChatCacheService, IntelligentChatCacheService>();
        
        // Register Semantic Kernel with Plugins (required by IntelligentChatService)
        // CHANGED TO TRANSIENT to avoid circular dependency deadlock
        // Each resolution gets a fresh Kernel instance
        // CRITICAL FIX: Register Kernel WITHOUT plugins to avoid circular dependency
        // Plugins will be registered by IntelligentChatService using its own serviceProvider
        services.AddTransient<Kernel>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var logger = serviceProvider.GetRequiredService<ILogger<Kernel>>();
            var builder = Kernel.CreateBuilder();
            
            // Configure Azure OpenAI
            var azureOpenAIEndpoint = configuration.GetValue<string>("Gateway:AzureOpenAI:Endpoint");
            var azureOpenAIApiKey = configuration.GetValue<string>("Gateway:AzureOpenAI:ApiKey");
            var azureOpenAIDeployment = configuration.GetValue<string>("Gateway:AzureOpenAI:DeploymentName") ?? "gpt-4o";
            var useManagedIdentity = configuration.GetValue<bool>("Gateway:AzureOpenAI:UseManagedIdentity");

            if (!string.IsNullOrEmpty(azureOpenAIEndpoint) && 
                !string.IsNullOrEmpty(azureOpenAIDeployment) &&
                (!string.IsNullOrEmpty(azureOpenAIApiKey) || useManagedIdentity))
            {
                if (useManagedIdentity)
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: azureOpenAIDeployment,
                        endpoint: azureOpenAIEndpoint,
                        credentials: new DefaultAzureCredential()
                    );
                }
                else
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: azureOpenAIDeployment,
                        endpoint: azureOpenAIEndpoint,
                        apiKey: azureOpenAIApiKey!
                    );
                }
            }
            
            logger.LogInformation("🔨 Building Kernel WITHOUT plugins (plugins added later by service)...");
            var kernel = builder.Build();
            logger.LogInformation("✅ Kernel built successfully (plugins will be added by IntelligentChatService)");
            
            return kernel;
        });
        
        // NOTE: Plugins are NOT registered in Kernel factory to avoid circular dependencies.
        // Instead, IntelligentChatService will register plugins when needed using its own serviceProvider.
        
        // NOTE: The following legacy services are marked as [Obsolete] and not registered:
        // - IIntentClassifier / IntentClassifier (replaced by SK auto-calling in IntelligentChatService)
        // - IParameterExtractor / ParameterExtractor (replaced by SK auto-calling)
        // - ISemanticQueryProcessor / SemanticQueryProcessor (replaced by IntelligentChatService)
        // - IToolSchemaRegistry / ToolSchemaRegistry (not actively used, removed)
        // These services are kept in the codebase for reference but should not be used.
        
        services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        
        // Register IntelligentChatService (uses SK auto-calling instead of manual routing)
        services.AddScoped<IIntelligentChatService, IntelligentChatService>();
        
        // Register Azure resource service (stub implementation for DI resolution)
        services.AddScoped<IAzureResourceService, Platform.Engineering.Copilot.Core.Services.AzureServices.AzureResourceService>();
        
        // Register Azure resource health service (stub implementation for DI resolution)
        services.AddScoped<IAzureResourceHealthService, Platform.Engineering.Copilot.Core.Services.AzureServices.AzureResourceHealthService>();
        
        // Register cost management services
        services.AddHttpClient<AzureCostManagementService>();
        services.AddScoped<AzureCostManagementService>();
        services.AddScoped<IAzureCostManagementService>(sp => sp.GetRequiredService<AzureCostManagementService>());

        // Register cost optimization engine
        services.AddScoped<ICostOptimizationEngine, CostOptimizationEngine>();
        
        // Register environment management engine
        services.AddScoped<IEnvironmentManagementEngine, EnvironmentManagementEngine>();
        
        // Register environment storage service (required by EnvironmentManagementPlugin)
        services.AddScoped<EnvironmentStorageService>();
        
        // Register onboarding services
        services.AddScoped<IOnboardingService, FlankspeedOnboardingService>();
        
        // Register deployment orchestration service (required by EnvironmentManagementEngine)
        services.AddScoped<IDeploymentOrchestrationService, DeploymentOrchestrationService>();
        
        // Register Azure metrics service (required by CostOptimizationEngine)
        services.AddScoped<IAzureMetricsService, AzureMetricsService>();

        // Register dynamic template generator
        services.AddScoped<IDynamicTemplateGenerator, DynamicTemplateGeneratorService>();
        
        // Register template generation enhancements
        services.AddScoped<Platform.Engineering.Copilot.Core.Services.TemplateGeneration.IComplianceAwareTemplateEnhancer, 
            Platform.Engineering.Copilot.Core.Services.TemplateGeneration.ComplianceAwareTemplateEnhancer>();
        services.AddScoped<Platform.Engineering.Copilot.Core.Services.Infrastructure.INetworkTopologyDesignService, 
            Platform.Engineering.Copilot.Core.Services.Infrastructure.NetworkTopologyDesignService>();
        services.AddScoped<Platform.Engineering.Copilot.Core.Services.Security.IAzureSecurityConfigurationService, 
            Platform.Engineering.Copilot.Core.Services.Security.AzureSecurityConfigurationService>();
        
        // Register infrastructure provisioning service (AI-powered, requires Kernel)
        services.AddScoped<IInfrastructureProvisioningService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<InfrastructureProvisioningService>>();
            var azureResourceService = serviceProvider.GetRequiredService<IAzureResourceService>();
            var kernel = serviceProvider.GetRequiredService<Kernel>();
            
            return new InfrastructureProvisioningService(logger, azureResourceService, kernel);
        });

        // Register compliance service (AI-powered, requires Kernel)
        services.AddScoped<ComplianceService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ComplianceService>>();
            var kernel = serviceProvider.GetRequiredService<Kernel>();
            
            return new ComplianceService(logger, kernel);
        });

        return services;
    }

    /// <summary>
    /// Add semantic processing services with custom configuration
    /// </summary>
    public static IServiceCollection AddSemanticProcessing(this IServiceCollection services)
    {
        return services.AddSupervisorCore();
    }

    /// <summary>
    /// Add semantic kernel services with OpenAI configuration
    /// </summary>
    public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
    {
        services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        return services;
    }
}