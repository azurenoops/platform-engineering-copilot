using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Azure.Identity;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Cache;
using Platform.Engineering.Copilot.Core.Services.Cache;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Compliance.Agent.Services.PullRequest;
using Platform.Engineering.Copilot.Core.Services.Compliance;
using Platform.Engineering.Copilot.Core.Services.Jobs;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins;
using Platform.Engineering.Copilot.Core.Services.Chat;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Notifications;
using Platform.Engineering.Copilot.Core.Services.Notifications;
using Platform.Engineering.Copilot.Core.Interfaces.Jobs;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Compliance.Agent.Extensions;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance.Remediation;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance.Remediation; // For AddEnhancedAtoCompliance
using Platform.Engineering.Copilot.Compliance.Agent.Plugins.ATO;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins.Code;

namespace Platform.Engineering.Copilot.Compliance.Core.Extensions;

/// <summary>
/// Extension methods for registering Compliance domain services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Compliance domain services (ComplianceAgent, CompliancePlugin) to the dependency injection container
    /// </summary>
    public static IServiceCollection AddComplianceAgent(this IServiceCollection services, IConfiguration configuration)
    {
        // Note: Configuration is registered in Program.cs from AgentConfiguration:ComplianceAgent section
        
        // Register caching services
        services.AddMemoryCache(); // Required for IMemoryCache
        services.AddSingleton<IIntelligentChatCacheService, IntelligentChatCacheService>();
        
        // Register Knowledge Base Services (RMF, STIG, DoD Instructions, etc.) - required by AtoComplianceEngine
        // This must be called before registering AtoComplianceEngine
        ComplianceAgentCollectionExtensions.AddEnhancedAtoCompliance(services, configuration);
        
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
        
        // Register Azure security services - removed as it's infrastructure-focused
        
        // Register Azure Policy Service (required for compliance analysis)
        services.AddScoped<IAzurePolicyService, AzurePolicyEngine>();
        
        // Register Policy Enforcement Service - Scoped (validates templates against IL2/IL4/IL5/IL6 policies)
        services.AddScoped<IPolicyEnforcementService, PolicyEnforcementService>();
        
        // Register GitHub Services - Singleton (no DbContext dependency)
        //services.AddSingleton<IGitHubServices, GitHubGatewayService>();
        
        // Register NIST Controls Service - Singleton (no DbContext dependency)
        services.AddSingleton<INistControlsService, NistControlsService>();
        
        // Register Compliance Metrics Service - Singleton (no DbContext dependency)
        services.AddSingleton<ComplianceMetricsService>();
        
        // Register Governance Engine - Scoped (policy enforcement and approval workflows)
        services.AddScoped<IGovernanceEngine, Agent.Services.Governance.GovernanceEngine>();
        
        // Register STIG Validation Service - Scoped (refactored from AtoComplianceEngine)
        services.AddScoped<IStigValidationService, StigValidationService>();
        
        // Register ATO Compliance Engine - Scoped (requires DbContext)
        services.AddScoped<IAtoComplianceEngine, AtoComplianceEngine>();
        
        // Register Script Sanitization Service - Scoped (validates and sanitizes remediation scripts)
        services.AddScoped<IScriptSanitizationService, ScriptSanitizationService>();
        
        // Register Remediation Support Services - Scoped (refactored from AtoRemediationEngine)
        services.AddScoped<INistRemediationStepsService, NistRemediationStepsService>();
        services.AddScoped<IAzureArmRemediationService, AzureArmRemediationService>();
        services.AddScoped<IRemediationScriptExecutor, RemediationScriptExecutor>();
        services.AddScoped<IAiRemediationPlanGenerator, AiRemediationPlanGenerator>();
        
        // Register ATO Remediation Engine - Scoped (AI-enhanced with optional GPT-4 integration)
        services.AddScoped<IRemediationEngine, AtoRemediationEngine>();

        // Register Evidence Storage Service - Scoped (stores compliance evidence to Azure Blob Storage)
        services.AddScoped<IEvidenceStorageService, EvidenceStorageService>();

        // Register Code Scanning Engine - Scoped (orchestrates security analysis tools)
        services.AddScoped<ICodeScanningEngine, CodeScanningEngine>();
        
        // Register Document Generation Service - Scoped (generates ATO compliance documents: SSP, SAR, POA&M)
        services.AddScoped<IDocumentGenerationService, Agent.Services.Documents.DocumentGenerationService>();
        
        // Register Document Versioning Service - Scoped (manages document versions and revisions)
        services.AddScoped<IDocumentVersioningService, Agent.Services.Documents.DocumentVersioningService>();
        
        // Register Collaborative Editing Service - Scoped (manages real-time collaborative editing sessions)
        services.AddScoped<ICollaborativeEditingService, Agent.Services.Documents.CollaborativeEditingService>();
        
        // Register Pull Request Review Services - Scoped (GitHub API integration for IaC compliance)
        services.AddHttpClient("GitHub", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Platform-Engineering-Copilot");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });
        services.AddScoped<GitHubPullRequestService>();
        services.AddScoped<PullRequestReviewService>();
        
        // Configure GitHub settings (for PR reviews)
        services.AddOptions<GitHubConfiguration>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("Gateway:GitHub").Bind(settings);
            });
        
        // Register Compliance Remediation Service - Scoped (requires HttpClient and Azure services)
        services.AddScoped<IComplianceRemediationService, ComplianceRemediationService>();
        
        // Register Compliance-Aware Template Enhancer - Scoped
        services.AddScoped<Copilot.Core.Services.IComplianceAwareTemplateEnhancer, Copilot.Core.Services.TemplateGeneration.ComplianceAwareTemplateEnhancer>();
        
        // Register Notification Services - Singleton (no DbContext dependency)
        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<ISlackService, SlackService>();
        services.AddSingleton<ITeamsNotificationService, TeamsNotificationService>();


        // ========================================
        // MULTI-AGENT SYSTEM REGISTRATION
        // ========================================
        
        // Register plugins (required by specialized agents) - Scoped to allow DbContext injection
        services.AddScoped<CompliancePlugin>();
        services.AddScoped<CodeScanningPlugin>();
        services.AddScoped<AtoPreparationPlugin>();
        services.AddScoped<DocumentGenerationPlugin>();
        services.AddScoped<PullRequestReviewPlugin>();
        
        // Register SharedMemory as singleton (shared across all agents for context)
        services.AddSingleton<SharedMemory>();

        // Register ONLY ComplianceAgent as ISpecializedAgent (top-level domain agent)
        // Sub-agents (CodeScanning, AtoPreparation, Document) all share AgentType.Compliance
        // and should be registered by specific type, not as ISpecializedAgent (to avoid duplicate keys)
        services.AddScoped<ISpecializedAgent, ComplianceAgent>();
        services.AddScoped<CodeScanningAgent>();
        services.AddScoped<AtoPreparationAgent>();
        services.AddScoped<DocumentAgent>();

        // NOTE: ExecutionPlanValidator and ExecutionPlanCache are registered in Core project
        // They are ONLY used by OrchestratorAgent (which creates execution plans)
        // Individual agents are executors, not planners

        // Register SemanticKernelService (creates kernels for agents) - Scoped to match agents
        services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        
        // Register Background Job Service for long-running operations
        services.AddSingleton<IBackgroundJobService, BackgroundJobService>();
        
        // Register Job Cleanup Background Service
        services.AddHostedService<JobCleanupBackgroundService>();

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
        return services.AddComplianceAgent(configuration);
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