using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Azure.Identity;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Cache;
using Platform.Engineering.Copilot.Core.Services.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Compliance;
using Platform.Engineering.Copilot.Core.Plugins;

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
        services.AddScoped<Kernel>(serviceProvider =>
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
            
            var kernel = builder.Build();
            
            // Register Semantic Kernel Plugins for automatic function calling
            // Note: Plugins need to be registered after kernel is built because they need tool handlers from DI
            try
            {
                // Get tool handlers from Extensions assembly
                var infrastructureHandler = GetToolHandlerByName(serviceProvider, "InfrastructureProvisioningTool");
                var complianceHandler = GetToolHandlerByName(serviceProvider, "AtoComplianceTool");
                var costHandler = GetToolHandlerByName(serviceProvider, "CostManagementTool");
                var documentHandler = GetToolHandlerByName(serviceProvider, "DocumentUploadAnalyzeTool");
                var onboardingHandler = GetToolHandlerByName(serviceProvider, "FlankspeedOnboardingTool");
                
                // Register InfrastructurePlugin with new AI-powered service
                try
                {
                    var infrastructureService = serviceProvider.GetService<IInfrastructureProvisioningService>();
                    if (infrastructureService != null)
                    {
                        var infrastructurePlugin = new InfrastructurePlugin(
                            serviceProvider.GetRequiredService<ILogger<InfrastructurePlugin>>(),
                            kernel,
                            infrastructureService);
                        kernel.Plugins.AddFromObject(infrastructurePlugin, "Infrastructure");
                        logger.LogInformation("Registered InfrastructurePlugin with Semantic Kernel");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to register InfrastructurePlugin");
                }
                
                // Register CompliancePlugin with ComplianceService
                try
                {
                    var complianceService = serviceProvider.GetRequiredService<ComplianceService>();
                    var compliancePlugin = new CompliancePlugin(
                        serviceProvider.GetRequiredService<ILogger<CompliancePlugin>>(),
                        kernel,
                        complianceService);
                    kernel.Plugins.AddFromObject(compliancePlugin, "Compliance");
                    logger.LogInformation("Registered CompliancePlugin with Semantic Kernel");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to register CompliancePlugin");
                }
                
                if (costHandler != null)
                {
                    var costPlugin = new CostManagementPlugin(
                        costHandler,
                        serviceProvider.GetRequiredService<ILogger<CostManagementPlugin>>(),
                        kernel);
                    kernel.Plugins.AddFromObject(costPlugin, "CostManagement");
                    logger.LogInformation("Registered CostManagementPlugin with Semantic Kernel");
                }
                
                if (documentHandler != null)
                {
                    var documentPlugin = new DocumentPlugin(
                        documentHandler,
                        serviceProvider.GetRequiredService<ILogger<DocumentPlugin>>(),
                        kernel);
                    kernel.Plugins.AddFromObject(documentPlugin, "Document");
                    logger.LogInformation("Registered DocumentPlugin with Semantic Kernel");
                }
                
                if (onboardingHandler != null)
                {
                    var onboardingPlugin = new OnboardingPlugin(
                        onboardingHandler,
                        serviceProvider.GetRequiredService<ILogger<OnboardingPlugin>>(),
                        kernel);
                    kernel.Plugins.AddFromObject(onboardingPlugin, "Onboarding");
                    logger.LogInformation("Registered OnboardingPlugin with Semantic Kernel");
                }
                
                // ResourceDiscoveryPlugin and SecurityPlugin use infrastructure handler
                if (infrastructureHandler != null)
                {
                    var resourcePlugin = new ResourceDiscoveryPlugin(
                        infrastructureHandler,
                        serviceProvider.GetRequiredService<ILogger<ResourceDiscoveryPlugin>>(),
                        kernel);
                    kernel.Plugins.AddFromObject(resourcePlugin, "ResourceDiscovery");
                    
                    var securityPlugin = new SecurityPlugin(
                        infrastructureHandler,
                        serviceProvider.GetRequiredService<ILogger<SecurityPlugin>>(),
                        kernel);
                    kernel.Plugins.AddFromObject(securityPlugin, "Security");
                    
                    logger.LogInformation("Registered ResourceDiscoveryPlugin and SecurityPlugin with Semantic Kernel");
                }
                
                logger.LogInformation("Successfully registered {PluginCount} Semantic Kernel plugins", kernel.Plugins.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to register some Semantic Kernel plugins. This is expected during initial DI setup.");
            }
            
            return kernel;
        });
        
        /// <summary>
        /// Helper method to get tool handler by name from DI container
        /// </summary>
        static Platform.Engineering.Copilot.Core.Contracts.IMcpToolHandler? GetToolHandlerByName(IServiceProvider serviceProvider, string toolName)
        {
            try
            {
                var handlers = serviceProvider.GetServices<Platform.Engineering.Copilot.Core.Contracts.IMcpToolHandler>();
                return handlers.FirstOrDefault(h => h.GetType().Name == toolName);
            }
            catch
            {
                return null;
            }
        }
        
        // Register semantic processing services
        services.AddSingleton<IToolSchemaRegistry, ToolSchemaRegistry>();
        
        // NOTE: The following legacy services are marked as [Obsolete] and not registered:
        // - IIntentClassifier / IntentClassifier (replaced by SK auto-calling in IntelligentChatService_v2)
        // - IParameterExtractor / ParameterExtractor (replaced by SK auto-calling)
        // - ISemanticQueryProcessor / SemanticQueryProcessor (replaced by IntelligentChatService_v2)
        // These services are kept in the codebase for reference but should not be used.
        
        services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        
        // Register IntelligentChatService V2 (uses SK auto-calling instead of manual routing)
        services.AddScoped<IIntelligentChatService, IntelligentChatService_v2>();
        
        // Register cost optimization engine
        services.AddScoped<ICostOptimizationEngine, CostOptimizationEngine>();
        
        // Register environment management engine
        services.AddScoped<IEnvironmentManagementEngine, EnvironmentManagementEngine>();
        
        // Register dynamic template generator
        services.AddScoped<IDynamicTemplateGenerator, DynamicTemplateGeneratorService>();
        
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
    /// Add only the tool registry service (for lightweight scenarios)
    /// </summary>
    public static IServiceCollection AddToolRegistry(this IServiceCollection services)
    {
        services.AddSingleton<IToolSchemaRegistry, ToolSchemaRegistry>();
        return services;
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