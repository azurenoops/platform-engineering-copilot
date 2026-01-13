using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Audits;
using Platform.Engineering.Copilot.Core.Interfaces.Notifications;
using Platform.Engineering.Copilot.Core.Interfaces.GitHub;
using Platform.Engineering.Copilot.Core.Interfaces.Jobs;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Jobs;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services.Azure.ResourceHealth;
using Platform.Engineering.Copilot.Core.Services.Azure.Security;
using Platform.Engineering.Copilot.Core.Services.Audits;
using Platform.Engineering.Copilot.Core.Services.Notifications;
using Platform.Engineering.Copilot.Core.Services.Validation;
using Platform.Engineering.Copilot.Core.Services.Validation.Validators;
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
        
        // Register Azure Client Factory (required for all Azure operations)
        services.AddAzureClientFactory();
        
        // Register caching services
        services.AddMemoryCache(); // Required for IMemoryCache
        
        // Register configuration service for persistent subscription storage
        services.AddSingleton<ConfigService>();
        
        // Note: ConfigurationPlugin has been moved to Infrastructure Agent
        // services.AddTransient<Plugins.ConfigurationPlugin>();
        
        // Note: AI chat capabilities are now provided via IChatClient from Microsoft.Extensions.AI,
        // registered in AddAzureOpenAIChatClient() in the Agents project.
        
        // Register Azure resource service - Singleton (no DbContext dependency)
        services.AddSingleton<IAzureResourceService, AzureResourceService>();
        services.AddSingleton<IAzureResourceService, AzureResourceService>();
        
        // Register Azure Resource Graph service - Singleton (no DbContext dependency)
        services.AddSingleton<Services.Azure.Graph.AzureResourceGraphService>();
        
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
        services.AddScoped<IInfrastructureTemplateRepository, InfrastructureTemplateRepository>();
        services.AddScoped<IInfrastructureDeploymentRepository, InfrastructureDeploymentRepository>();
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
        
        // Note: Agents and orchestration are now registered in Platform.Engineering.Copilot.Agents project
        // via PlatformAgentGroupChat, BaseAgent, and domain-specific agents.
        // AI chat capabilities are provided via IChatClient from Microsoft.Extensions.AI,
        // registered in AddAzureOpenAIChatClient() in the Agents project.
        
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

        return services;
    }

    /// <summary>
    /// Add semantic processing services with custom configuration
    /// </summary>
    public static IServiceCollection AddSemanticProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddPlatformEngineeringCopilotCore(configuration);
    }
}
