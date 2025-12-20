using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Azure.Identity;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Audits;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Interfaces.Notifications;
using Platform.Engineering.Copilot.Core.Interfaces.GitHub;
using Platform.Engineering.Copilot.Core.Interfaces.Jobs;
using Platform.Engineering.Copilot.Core.Interfaces.Cache;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Cache;
using Platform.Engineering.Copilot.Core.Services.Jobs;
using Platform.Engineering.Copilot.Core.Services.Chat;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services.Azure.ResourceHealth;
using Platform.Engineering.Copilot.Core.Services.Azure.Security;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services.Audits;
using Platform.Engineering.Copilot.Core.Services.Notifications;
using Platform.Engineering.Copilot.Core.Services.Validation;
using Platform.Engineering.Copilot.Core.Services.Validation.Validators;
using Platform.Engineering.Copilot.Core.Services.ServiceCreation;
using Platform.Engineering.Copilot.Core.Services.Generators.Adapters;
using Platform.Engineering.Copilot.Core.Services.Generators.CrossCutting;
using Platform.Engineering.Copilot.Core.Services.Generators.KeyVault;
using Platform.Engineering.Copilot.Core.Services.Generators.ContainerRegistry;
using Platform.Engineering.Copilot.Core.Services.Generators.LogAnalytics;
using Platform.Engineering.Copilot.Core.Services.Generators.ManagedIdentity;
using Platform.Engineering.Copilot.Core.Services.Generators.Storage;
using Platform.Engineering.Copilot.Core.Services.Generators.Database;
using Platform.Engineering.Copilot.Core.Services.Generators.Kubernetes;
using Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Generators.AppService;
using Platform.Engineering.Copilot.Core.Services.Generators.Containers;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Services.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for registering Platform.Engineering.Copilot.Core services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all Platform.Engineering.Copilot.Core services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddPlatformEngineeringCopilotCore(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Azure Gateway configuration options
        services.AddOptions<AzureGatewayOptions>()
            .BindConfiguration(AzureGatewayOptions.SectionName);
        
        // Register caching services
        services.AddMemoryCache(); // Required for IMemoryCache
        services.AddSingleton<IIntelligentChatCacheService, IntelligentChatCacheService>();
        
        // Register configuration service for persistent subscription storage
        services.AddSingleton<ConfigService>();
        
        // Register shared configuration plugin (available to all agents)
        services.AddTransient<Plugins.ConfigurationPlugin>();
        
        // Register token management services (Phase 1)
        services.AddTokenManagementServices();
        
        // Register Semantic Text Memory for Service Wizard state management
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
        services.AddSingleton<Microsoft.SemanticKernel.Memory.ISemanticTextMemory>(sp =>
        {
            var memoryBuilder = new Microsoft.SemanticKernel.Memory.MemoryBuilder();
            return memoryBuilder.Build();
        });
#pragma warning restore SKEXP0001
        
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
        
        // Register Azure resource service - Singleton (no DbContext dependency)
        services.AddSingleton<IAzureResourceService, AzureResourceService>();
        
        // Register Azure resource health service - Singleton (no DbContext dependency)
        services.AddSingleton<IAzureResourceHealthService, AzureResourceHealthService>();
                
        // Register audit logging service - Singleton (no DbContext dependency, uses in-memory store)
        services.AddSingleton<IAuditLoggingService, AuditLoggingService>();
        
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
        
        services.AddScoped<IAzureSecurityConfigurationService, AzureSecurityConfigurationService>();
        
        // Register Repository services (required by Storage services)
        services.AddScoped<IEnvironmentTemplateRepository, EnvironmentTemplateRepository>();
        services.AddScoped<IEnvironmentDeploymentRepository, EnvironmentDeploymentRepository>();
        services.AddScoped<IComplianceAssessmentRepository, ComplianceAssessmentRepository>();
        
        // Register Template Storage Service (required by domain services)
        services.AddScoped<ITemplateStorageService, Data.Services.TemplateStorageService>();
        
        // Register GitHub Services - Singleton (no DbContext dependency)
        services.AddSingleton<IGitHubServices, GitHubGatewayService>();
        
        // Register Notification Services - Singleton (no DbContext dependency)
        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<ISlackService, SlackService>();
        services.AddSingleton<ITeamsNotificationService, TeamsNotificationService>();

        // ========================================
        // MULTI-AGENT SYSTEM REGISTRATION
        // ========================================
        
        // Note: Agents and plugins are now registered in their respective domain projects:
                
        // Register SharedMemory as singleton (shared across all agents for context)
        services.AddSingleton<SharedMemory>();

        // Register execution plan validator
        services.AddSingleton<ExecutionPlanValidator>();

        // OPTIMIZATION: Register execution plan cache
        services.AddSingleton<ExecutionPlanCache>();

        // Register OrchestratorAgent (coordinates all specialized agents) - Scoped to match agents
        services.AddScoped<OrchestratorAgent>();

        // Register SemanticKernelService (creates kernels for agents) - Scoped to match agents
        services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        
        // Register Background Job Service for long-running operations
        services.AddSingleton<IBackgroundJobService, BackgroundJobService>();
        
        // Register Job Cleanup Background Service
        services.AddHostedService<JobCleanupBackgroundService>();
        
        // Register Template Cleanup Background Service (expires templates after 30 minutes)
        services.AddHostedService<TemplateCleanupBackgroundService>();
        
        // Register Cross-Cutting Module Generators (Phase 1 of Template Generation Refactor)
        // These provide reusable components for Private Endpoints, Diagnostics, RBAC, NSG
        // Bicep cross-cutting generators
        services.AddSingleton<ICrossCuttingModuleGenerator, BicepPrivateEndpointGenerator>();
        services.AddSingleton<ICrossCuttingModuleGenerator, BicepDiagnosticSettingsGenerator>();
        services.AddSingleton<ICrossCuttingModuleGenerator, BicepRBACGenerator>();
        services.AddSingleton<ICrossCuttingModuleGenerator, BicepNSGGenerator>();
        // Terraform cross-cutting generators
        services.AddSingleton<ICrossCuttingModuleGenerator, TerraformPrivateEndpointGenerator>();
        services.AddSingleton<ICrossCuttingModuleGenerator, TerraformDiagnosticSettingsGenerator>();
        services.AddSingleton<ICrossCuttingModuleGenerator, TerraformRBACGenerator>();
        
        // Register IResourceModuleGenerator implementations (Phase 2 - Composition Pattern)
        // These generate core resources and support cross-cutting module composition
        // Since IResourceModuleGenerator extends IModuleGenerator, we register as both interfaces
        
        // Bicep Azure resource generators - registered as both IResourceModuleGenerator and IModuleGenerator
        services.AddSingleton<BicepKeyVaultModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepKeyVaultModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepKeyVaultModuleGenerator>());
        
        services.AddSingleton<BicepContainerRegistryModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepContainerRegistryModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepContainerRegistryModuleGenerator>());
        
        services.AddSingleton<BicepLogAnalyticsModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepLogAnalyticsModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepLogAnalyticsModuleGenerator>());
        
        services.AddSingleton<BicepManagedIdentityModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepManagedIdentityModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepManagedIdentityModuleGenerator>());
        
        services.AddSingleton<BicepStorageAccountModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepStorageAccountModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepStorageAccountModuleGenerator>());
        
        services.AddSingleton<BicepSQLModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepSQLModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepSQLModuleGenerator>());
        
        services.AddSingleton<BicepAKSResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepAKSResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepAKSResourceModuleGenerator>());
        
        services.AddSingleton<BicepNetworkResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepNetworkResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepNetworkResourceModuleGenerator>());
        
        // Bicep App Service and Container Apps - registered as both IResourceModuleGenerator and IModuleGenerator
        services.AddSingleton<BicepAppServiceResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepAppServiceResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepAppServiceResourceModuleGenerator>());
        
        services.AddSingleton<BicepContainerAppsResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<BicepContainerAppsResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<BicepContainerAppsResourceModuleGenerator>());
        
        // Terraform Azure resource generators - registered as both IResourceModuleGenerator and IModuleGenerator
        services.AddSingleton<TerraformStorageResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<TerraformStorageResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<TerraformStorageResourceModuleGenerator>());
        
        services.AddSingleton<TerraformSQLResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<TerraformSQLResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<TerraformSQLResourceModuleGenerator>());
        
        services.AddSingleton<TerraformAKSResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<TerraformAKSResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<TerraformAKSResourceModuleGenerator>());
        
        services.AddSingleton<TerraformNetworkResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<TerraformNetworkResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<TerraformNetworkResourceModuleGenerator>());
        
        services.AddSingleton<TerraformKeyVaultResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<TerraformKeyVaultResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<TerraformKeyVaultResourceModuleGenerator>());
        
        // Terraform App Service and Container Instances - registered as both IResourceModuleGenerator and IModuleGenerator
        services.AddSingleton<TerraformAppServiceResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<TerraformAppServiceResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<TerraformAppServiceResourceModuleGenerator>());
        
        services.AddSingleton<TerraformContainerInstancesResourceModuleGenerator>();
        services.AddSingleton<IResourceModuleGenerator>(sp => sp.GetRequiredService<TerraformContainerInstancesResourceModuleGenerator>());
        services.AddSingleton<IModuleGenerator>(sp => sp.GetRequiredService<TerraformContainerInstancesResourceModuleGenerator>());
        
        // Legacy adapters - kept ONLY for non-Azure cloud providers (AWS/GCP)
        services.AddSingleton<IModuleGenerator, TerraformECSModuleAdapter>();
        services.AddSingleton<IModuleGenerator, TerraformLambdaModuleAdapter>();
        services.AddSingleton<IModuleGenerator, TerraformCloudRunModuleAdapter>();
        services.AddSingleton<IModuleGenerator, TerraformEKSModuleAdapter>();
        services.AddSingleton<IModuleGenerator, TerraformGKEModuleAdapter>();

        // Register Azure MCP Client (Microsoft's official Azure MCP Server integration)
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var gatewayOptions = new GatewayOptions();
            config.GetSection(GatewayOptions.SectionName).Bind(gatewayOptions);

            return new AzureMcpConfiguration
            {
                ReadOnly = config.GetValue("AzureMcp:ReadOnly", false),
                Debug = config.GetValue("AzureMcp:Debug", false),
                DisableUserConfirmation = config.GetValue("AzureMcp:DisableUserConfirmation", false),
                Namespaces = config.GetSection("AzureMcp:Namespaces").Get<string[]>(),

                // Set subscription and tenant from Gateway configuration or environment variables
                SubscriptionId = gatewayOptions.Azure.SubscriptionId ?? Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID"),
                TenantId = gatewayOptions.Azure.TenantId ?? Environment.GetEnvironmentVariable("AZURE_TENANT_ID"),
                AuthenticationMethod = "credential" // Use Azure Identity SDK (Service Principal, Managed Identity, or Azure CLI)
            };
        });
        services.AddSingleton<AzureMcpClient>();

        // Register JIT (Just-In-Time) privilege elevation services
        services.AddJitServices(configuration);

        return services;
    }

    /// <summary>
    /// Add semantic processing services with custom configuration
    /// </summary>
    public static IServiceCollection AddSemanticProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddPlatformEngineeringCopilotCore(configuration);
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
