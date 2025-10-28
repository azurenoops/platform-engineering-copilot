using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Azure.Identity;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Deployment;
using Platform.Engineering.Copilot.Core.Services.Cache;
using Platform.Engineering.Copilot.Core.Services.Compliance;
using Platform.Engineering.Copilot.Core.Services.Jobs;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Onboarding;
using Platform.Engineering.Copilot.Core.Services.Chat;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services.Azure.Cost;
using Platform.Engineering.Copilot.Core.Services.Azure.ResourceHealth;
using Platform.Engineering.Copilot.Core.Services.Azure.Security;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Services.Validation;
using Platform.Engineering.Copilot.Core.Services.Validation.Validators;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;

namespace Platform.Engineering.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for registering Platform.Engineering.Copilot.Core services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all Platform.Engineering.Copilot.Core services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddPlatformEngineeringCopilotCore(this IServiceCollection services)
    {
        // Register caching services
        services.AddMemoryCache(); // Required for IMemoryCache
        services.AddSingleton<IIntelligentChatCacheService, IntelligentChatCacheService>();
        
        // Register Semantic Kernel with Plugins (required by IntelligentChatService)
        // CHANGED TO TRANSIENT to avoid circular dependency deadlock
        // Each resolution gets a fresh Kernel instance
        // CRITICAL FIX: Register Kernel WITHOUT plugins to avoid circular dependency
        // Plugins will be registered by IntelligentChatService using its own serviceProvider
        services.AddTransient(serviceProvider =>
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
                // Create HttpClient with extended timeout for complex queries
                var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(5) // Increase from default 100 seconds to 5 minutes
                };
                
                if (useManagedIdentity)
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: azureOpenAIDeployment,
                        endpoint: azureOpenAIEndpoint,
                        credentials: new DefaultAzureCredential(),
                        httpClient: httpClient
                    );
                }
                else
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: azureOpenAIDeployment,
                        endpoint: azureOpenAIEndpoint,
                        apiKey: azureOpenAIApiKey!,
                        httpClient: httpClient
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
        // NOTE: IntelligentChatService now delegates to OrchestratorAgent only

        // Register IntelligentChatService (pure multi-agent - delegates to OrchestratorAgent)
        services.AddScoped<IIntelligentChatService, IntelligentChatService>();
        
        // Register Azure resource service (stub implementation for DI resolution)
        services.AddScoped<IAzureResourceService, AzureResourceService>();
        
        // Register Azure resource health service (stub implementation for DI resolution)
        services.AddScoped<IAzureResourceHealthService, AzureResourceHealthService>();
        
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
        
        // Register configuration validation service and validators
        services.AddScoped<ConfigurationValidationService>();
        services.AddScoped<IConfigurationValidator, AKSConfigValidator>();
        services.AddScoped<IConfigurationValidator, EKSConfigValidator>();
        services.AddScoped<IConfigurationValidator, GKEConfigValidator>();
        services.AddScoped<IConfigurationValidator, ECSConfigValidator>();
        services.AddScoped<IConfigurationValidator, ContainerAppsConfigValidator>();
        services.AddScoped<IConfigurationValidator, AppServiceConfigValidator>();
        services.AddScoped<IConfigurationValidator, LambdaConfigValidator>();
        services.AddScoped<IConfigurationValidator, CloudRunConfigValidator>();
        services.AddScoped<IConfigurationValidator, VMConfigValidator>();
        
        // Register template generation enhancements
        services.AddScoped<Services.TemplateGeneration.IComplianceAwareTemplateEnhancer,
            Services.TemplateGeneration.ComplianceAwareTemplateEnhancer>();
        services.AddScoped<INetworkTopologyDesignService,
            NetworkTopologyDesignService>();
        services.AddScoped<IPredictiveScalingEngine,
            PredictiveScalingEngine>();
        services.AddScoped<IAzureSecurityConfigurationService, AzureSecurityConfigurationService>();
        
        // Register Azure Policy Service (required by ComplianceAwareTemplateEnhancer)
        services.AddScoped<IAzurePolicyService, AzurePolicyEngine>();
        
        // Register infrastructure provisioning service (AI-powered, requires Kernel)
        services.AddScoped<IInfrastructureProvisioningService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<InfrastructureProvisioningService>>();
            var azureResourceService = serviceProvider.GetRequiredService<IAzureResourceService>();
            
            return new InfrastructureProvisioningService(logger, azureResourceService);
        });

        // Register compliance service (AI-powered, requires Kernel)
        services.AddScoped(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ComplianceService>>();
            var kernel = serviceProvider.GetRequiredService<Kernel>();
            
            return new ComplianceService(logger, kernel);
        });

        // ========================================
        // MULTI-AGENT SYSTEM REGISTRATION
        // ========================================
        
        // Register plugins (required by specialized agents)
        services.AddScoped<CompliancePlugin>();
        services.AddScoped<CostManagementPlugin>();
        services.AddScoped<EnvironmentManagementPlugin>();
        services.AddScoped<ResourceDiscoveryPlugin>();
        services.AddScoped<OnboardingPlugin>();
        // Note: InfrastructurePlugin and DeploymentPlugin are already registered by IntelligentChatService
        
        // Register SharedMemory as singleton (shared across all agents for context)
        services.AddSingleton<SharedMemory>();

        // Register all specialized agents as ISpecializedAgent
        // Each agent is registered as a singleton since they are stateless (state is in SharedMemory)
        services.AddSingleton<ISpecializedAgent, InfrastructureAgent>();
        services.AddSingleton<ISpecializedAgent, ComplianceAgent>();
        services.AddSingleton<ISpecializedAgent, CostManagementAgent>();
        services.AddSingleton<ISpecializedAgent, EnvironmentAgent>();
        services.AddSingleton<ISpecializedAgent, DiscoveryAgent>();
        services.AddSingleton<ISpecializedAgent, OnboardingAgent>();

        // Register execution plan validator
        services.AddSingleton<ExecutionPlanValidator>();

        // OPTIMIZATION: Register execution plan cache
        services.AddSingleton<ExecutionPlanCache>();

        // Register OrchestratorAgent (coordinates all specialized agents)
        services.AddSingleton<OrchestratorAgent>();

        // Register SemanticKernelService (creates kernels for agents)
        services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        
        // Register Background Job Service for long-running operations
        services.AddSingleton<IBackgroundJobService, BackgroundJobService>();
        
        // Register Job Cleanup Background Service
        services.AddHostedService<JobCleanupBackgroundService>();

        // Register Azure MCP Client (Microsoft's official Azure MCP Server integration)
        services.AddSingleton(sp => 
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new AzureMcpConfiguration
            {
                ReadOnly = config.GetValue("AzureMcp:ReadOnly", false),
                Debug = config.GetValue("AzureMcp:Debug", false),
                DisableUserConfirmation = config.GetValue("AzureMcp:DisableUserConfirmation", false),
                Namespaces = config.GetSection("AzureMcp:Namespaces").Get<string[]>()
            };
        });
        services.AddSingleton<AzureMcpClient>();

        return services;
    }

    /// <summary>
    /// Add semantic processing services with custom configuration
    /// </summary>
    public static IServiceCollection AddSemanticProcessing(this IServiceCollection services)
    {
        return services.AddPlatformEngineeringCopilotCore();
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