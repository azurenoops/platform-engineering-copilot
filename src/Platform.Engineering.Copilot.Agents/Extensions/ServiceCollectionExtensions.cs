using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Compliance.Agents;
using Platform.Engineering.Copilot.Agents.Compliance.Configuration;
using Platform.Engineering.Copilot.Agents.Compliance.Services.Compliance;
using Platform.Engineering.Copilot.Agents.Compliance.Services.Data;
using Platform.Engineering.Copilot.Agents.Compliance.State;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Agents.Configuration.Agents;
using Platform.Engineering.Copilot.Agents.Configuration.Configuration;
using Platform.Engineering.Copilot.Agents.Configuration.State;
using Platform.Engineering.Copilot.Agents.Configuration.Tools;
using Platform.Engineering.Copilot.Agents.CostManagement.Agents;
using Platform.Engineering.Copilot.Agents.CostManagement.Configuration;
using Platform.Engineering.Copilot.Agents.CostManagement.State;
using Platform.Engineering.Copilot.Agents.CostManagement.Tools;
using Platform.Engineering.Copilot.Agents.Discovery.Agents;
using Platform.Engineering.Copilot.Agents.Discovery.Configuration;
using Platform.Engineering.Copilot.Agents.Discovery.State;
using Platform.Engineering.Copilot.Agents.Discovery.Tools;
using Platform.Engineering.Copilot.Agents.Environments.Agents;
using Platform.Engineering.Copilot.Agents.Environments.Configuration;
using Platform.Engineering.Copilot.Agents.Environments.Services;
using Platform.Engineering.Copilot.Agents.Infrastructure.Deployment;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;
using Platform.Engineering.Copilot.Agents.Environments.State;
using Platform.Engineering.Copilot.Agents.Environments.Tools;
using Platform.Engineering.Copilot.Agents.Infrastructure.Agents;
using Platform.Engineering.Copilot.Agents.Infrastructure.Configuration;
using Platform.Engineering.Copilot.Agents.Infrastructure.Services;
using Platform.Engineering.Copilot.Agents.Infrastructure.State;
using Platform.Engineering.Copilot.Agents.Infrastructure.Tools;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Agents;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Configuration;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Services;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.State;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;
using Platform.Engineering.Copilot.Agents.Orchestration;
using Platform.Engineering.Copilot.Channels.Extensions;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance.Remediation;
using Platform.Engineering.Copilot.Core.Interfaces.Cost;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Agents.Compliance.Services.Compliance.Remediation;
using Platform.Engineering.Copilot.State.Extensions;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Platform.Engineering.Copilot.Agents.Extensions;

/// <summary>
/// Extension methods for registering Agent Framework services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Agent Framework services including orchestration, agents, tools, and state.
    /// Also registers IChatClient using Azure OpenAI configuration from Gateway:AzureOpenAI section.
    /// </summary>
    public static IServiceCollection AddAgentFramework(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add memory cache for state management
        services.AddMemoryCache();

        // Register Azure OpenAI as IChatClient from configuration
        services.AddAzureOpenAIChatClient(configuration);

        // Add state management services (conversation, agent, shared memory)
        services.AddStateManagement(configuration);

        // Add channel services (messaging, streaming)
        services.AddChannels(configuration);

        // Add common tools available to all agents
        services.AddCommonTools();

        // Add orchestration services
        services.AddOrchestration(configuration);

        // Add configuration agent (handles subscription settings)
        services.AddConfigurationAgent(configuration);

        // Add discovery agent
        services.AddDiscoveryAgent(configuration);

        // Add environment agent
        services.AddEnvironmentAgent(configuration);

        // Add cost management agent
        services.AddCostManagementAgent(configuration);

        // Add infrastructure agent
        services.AddInfrastructureAgent(configuration);

        // Add compliance agent
        services.AddComplianceAgent(configuration);

        // Add knowledge base agent
        services.AddKnowledgeBaseAgent(configuration);

        // Add tool registry
        services.AddToolRegistry();

        return services;
    }
    public static IServiceCollection AddAzureOpenAIChatClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IChatClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AzureOpenAIClient>>();
            
            var endpoint = configuration.GetValue<string>("Gateway:AzureOpenAI:Endpoint");
            var apiKey = configuration.GetValue<string>("Gateway:AzureOpenAI:ApiKey");
            var deploymentName = configuration.GetValue<string>("Gateway:AzureOpenAI:ChatDeploymentName") 
                ?? configuration.GetValue<string>("Gateway:AzureOpenAI:DeploymentName") 
                ?? "gpt-4o";
            var useManagedIdentity = configuration.GetValue<bool>("Gateway:AzureOpenAI:UseManagedIdentity");

            if (string.IsNullOrEmpty(endpoint))
            {
                logger.LogWarning("⚠️ Azure OpenAI endpoint not configured (Gateway:AzureOpenAI:Endpoint). Using mock chat client.");
                return new MockChatClient();
            }

            logger.LogInformation("🤖 Configuring Azure OpenAI Chat Client: Endpoint={Endpoint}, Deployment={Deployment}, UseManagedIdentity={UseManagedIdentity}",
                endpoint, deploymentName, useManagedIdentity);

            AzureOpenAIClient azureClient;
            if (useManagedIdentity)
            {
                azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
            }
            else if (!string.IsNullOrEmpty(apiKey))
            {
                azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            }
            else
            {
                logger.LogWarning("⚠️ Azure OpenAI API key not configured and UseManagedIdentity is false. Using mock chat client.");
                return new MockChatClient();
            }

            // Get the ChatClient from Azure OpenAI and convert to IChatClient
            var chatClient = azureClient.GetChatClient(deploymentName);
            return chatClient.AsIChatClient();
        });

        return services;
    }

    /// <summary>
    /// Adds all Agent Framework services with in-memory state and channels (for development/testing).
    /// </summary>
    public static IServiceCollection AddAgentFrameworkInMemory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add memory cache for state management
        services.AddMemoryCache();

        // Register Azure OpenAI as IChatClient from configuration
        services.AddAzureOpenAIChatClient(configuration);

        // Add in-memory state management
        services.AddInMemoryStateManagement();

        // Add in-memory channels
        services.AddInMemoryChannels();

        // Add orchestration services
        services.AddOrchestration(configuration);

        // Add configuration agent (handles subscription settings)
        services.AddConfigurationAgent(configuration);

        // Add discovery agent
        services.AddDiscoveryAgent(configuration);

        // Add environment agent
        services.AddEnvironmentAgent(configuration);

        // Add cost management agent
        services.AddCostManagementAgent(configuration);

        // Add infrastructure agent
        services.AddInfrastructureAgent(configuration);

        // Add compliance agent
        services.AddComplianceAgent(configuration);

        // Add knowledge base agent
        services.AddKnowledgeBaseAgent(configuration);

        // Add tool registry
        services.AddToolRegistry();

        return services;
    }

    /// <summary>
    /// Adds orchestration services (PlatformAgentGroupChat, strategies).
    /// </summary>
    public static IServiceCollection AddOrchestration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<PlatformSelectionStrategy>();
        services.AddScoped<PlatformTerminationStrategy>();
        services.AddScoped<PlatformAgentGroupChat>();

        return services;
    }

    /// <summary>
    /// Adds the Configuration Agent for managing subscription and environment settings.
    /// </summary>
    public static IServiceCollection AddConfigurationAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<ConfigurationAgentOptions>(
            configuration.GetSection(ConfigurationAgentOptions.SectionName));

        // Check if agent is enabled
        var options = configuration.GetSection(ConfigurationAgentOptions.SectionName)
            .Get<ConfigurationAgentOptions>() ?? new ConfigurationAgentOptions();

        // Add state accessors (always needed for potential runtime enable)
        services.AddScoped<ConfigurationStateAccessors>();

        // Add tools
        services.AddScoped<ConfigurationTool>();

        // Register agent
        services.AddScoped<ConfigurationAgent>();
        if (options.Enabled)
        {
            services.AddScoped<BaseAgent>(sp => sp.GetRequiredService<ConfigurationAgent>());
        }

        return services;
    }

    /// <summary>
    /// Adds the Discovery Agent for resource discovery, inventory, and health monitoring.
    /// </summary>
    public static IServiceCollection AddDiscoveryAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<DiscoveryAgentOptions>(
            configuration.GetSection(DiscoveryAgentOptions.SectionName));

        // Check if agent is enabled
        var options = configuration.GetSection(DiscoveryAgentOptions.SectionName)
            .Get<DiscoveryAgentOptions>() ?? new DiscoveryAgentOptions();

        // Add state accessors (always needed for potential runtime enable)
        services.AddScoped<DiscoveryStateAccessors>();

        // Add tools (always available even if agent is disabled)
        services.AddScoped<ResourceDiscoveryTool>();
        services.AddScoped<SubscriptionListTool>();
        services.AddScoped<ResourceDetailsTool>();
        services.AddScoped<DependencyMappingTool>();
        services.AddScoped<ResourceHealthTool>();
        services.AddScoped<ResourceTagSearchTool>();
        services.AddScoped<ResourceGroupListTool>();
        services.AddScoped<ResourceGroupSummaryTool>();
        services.AddScoped<SubscriptionInventoryTool>();

        // Only register agent if enabled
        services.AddScoped<DiscoveryAgent>();
        if (options.Enabled)
        {
            services.AddScoped<BaseAgent>(sp => sp.GetRequiredService<DiscoveryAgent>());
        }

        return services;
    }

    /// <summary>
    /// Adds the Cost Management Agent for cost analysis, optimization, budgeting, and forecasting.
    /// </summary>
    public static IServiceCollection AddCostManagementAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<CostManagementAgentOptions>(
            configuration.GetSection(CostManagementAgentOptions.SectionName));
        // Governance options needed by AzureCostManagementService (use Core.Configuration to avoid ambiguity with Compliance.Configuration)
        services.Configure<Core.Configuration.GovernanceOptions>(
            configuration.GetSection(Core.Configuration.GovernanceOptions.SectionName));

        // Check if agent is enabled
        var options = configuration.GetSection(CostManagementAgentOptions.SectionName)
            .Get<CostManagementAgentOptions>() ?? new CostManagementAgentOptions();

        // Add state accessors (always needed for potential runtime enable)
        services.AddScoped<CostManagementStateAccessors>();

        // Add tools (always available even if agent is disabled)
        services.AddScoped<CostAnalysisTool>();
        services.AddScoped<CostOptimizationTool>();
        services.AddScoped<BudgetManagementTool>();
        services.AddScoped<CostForecastTool>();
        services.AddScoped<CostScenarioTool>();
        services.AddScoped<CostAnomalyTool>();

        // Only register agent if enabled
        services.AddScoped<CostManagementAgent>();
        if (options.Enabled)
        {
            services.AddScoped<BaseAgent>(sp => sp.GetRequiredService<CostManagementAgent>());
        }

        return services;
    }

    /// <summary>
    /// Adds the Infrastructure Agent for template generation, provisioning, scaling, and Azure Arc.
    /// </summary>
    public static IServiceCollection AddInfrastructureAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<InfrastructureAgentOptions>(
            configuration.GetSection(InfrastructureAgentOptions.SectionName));

        // Check if agent is enabled
        var options = configuration.GetSection(InfrastructureAgentOptions.SectionName)
            .Get<InfrastructureAgentOptions>() ?? new InfrastructureAgentOptions();

        // Add state accessors (always needed for potential runtime enable)
        services.AddScoped<InfrastructureStateAccessors>();

        // Add template generation services
        services.AddScoped<IDynamicTemplateGenerator, DynamicTemplateGeneratorService>();

        // Add infrastructure services
        services.AddScoped<IInfrastructureProvisioningService, InfrastructureProvisioningService>();
        
        // Add predictive scaling engine for ScalingAnalysisTool (optional - depends on Azure services)
        // Will be null if dependencies (IAzureMetricsService, etc.) are not registered
        services.AddScoped<IPredictiveScalingEngine>(sp =>
        {
            try
            {
                var logger = sp.GetRequiredService<ILogger<PredictiveScalingEngine>>();
                var metricsService = sp.GetService<IAzureMetricsService>();
                var resourceService = sp.GetService<IAzureResourceService>();
                var costOptimizationEngine = sp.GetService<ICostOptimizationEngine>();
                
                if (metricsService == null || resourceService == null || costOptimizationEngine == null)
                {
                    logger.LogWarning("PredictiveScalingEngine dependencies not available - scaling analysis will use simulated data");
                    return null!;
                }
                
                return new PredictiveScalingEngine(logger, metricsService, resourceService, costOptimizationEngine);
            }
            catch
            {
                return null!;
            }
        });

        // Add tools (always available even if agent is disabled)
        services.AddScoped<TemplateGenerationTool>();
        services.AddScoped<TemplateRetrievalTool>();
        services.AddScoped<ResourceProvisioningTool>();
        services.AddScoped<ScalingAnalysisTool>();
        services.AddScoped<AzureArcTool>();
        services.AddScoped<ResourceDeletionTool>();

        // Only register agent if enabled
        services.AddScoped<InfrastructureAgent>();
        if (options.Enabled)
        {
            services.AddScoped<BaseAgent>(sp => sp.GetRequiredService<InfrastructureAgent>());
        }

        return services;
    }

    /// <summary>
    /// Adds the Compliance Agent for NIST 800-53 compliance assessment, remediation, and documentation.
    /// </summary>
    public static IServiceCollection AddComplianceAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<ComplianceAgentOptions>(
            configuration.GetSection(ComplianceAgentOptions.SectionName));
        // NistControls is nested under ComplianceAgent in appsettings.json
        // Use Compliance.Configuration.NistControlsOptions to avoid ambiguity with Core.Configuration
        services.Configure<Compliance.Configuration.NistControlsOptions>(
            configuration.GetSection($"{ComplianceAgentOptions.SectionName}:NistControls"));

        // Check if agent is enabled
        var options = configuration.GetSection(ComplianceAgentOptions.SectionName)
            .Get<ComplianceAgentOptions>() ?? new ComplianceAgentOptions();

        // Add state accessors (always needed for potential runtime enable)
        services.AddScoped<ComplianceStateAccessors>();

        // Add compliance core services (required by tools and agents)
        services.AddSingleton<ComplianceMetricsService>();
        services.AddHttpClient<NistControlsService>();
        services.AddScoped<INistControlsService, NistControlsService>();

        // Add TokenCredential for Azure services (uses DefaultAzureCredential which works with
        // Azure CLI, Managed Identity, Environment Variables, etc.)
        services.AddScoped<Azure.Core.TokenCredential>(sp =>
        {
            // Use DefaultAzureCredential which automatically handles:
            // - Environment variables (AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID)
            // - Managed Identity (when running in Azure)
            // - Azure CLI credentials (when running locally)
            // - Visual Studio / VS Code credentials
            return new DefaultAzureCredential();
        });

        // Add compliance engine dependencies
        services.AddScoped<IAssessmentService, AssessmentService>();
        
        // Conditional Defender for Cloud service registration
        // Uses real service when enabled, null service when disabled for graceful degradation
        if (options.DefenderForCloud.Enabled)
        {
            services.AddScoped<IDefenderForCloudService, DefenderForCloudService>();
        }
        else
        {
            services.AddScoped<IDefenderForCloudService, NullDefenderForCloudService>();
        }
        
        services.AddScoped<IEvidenceStorageService, EvidenceStorageService>();
        services.AddScoped<IStigValidationService, StigValidationService>();

        // Add the ATO Compliance Engine (core scanning engine)
        services.AddScoped<IAtoComplianceEngine, AtoComplianceEngine>();

        // Add remediation engine dependencies
        services.AddScoped<IComplianceRemediationService, ComplianceRemediationService>();
        services.AddScoped<INistRemediationStepsService, NistRemediationStepsService>();
        services.AddScoped<IAzureArmRemediationService, AzureArmRemediationService>();
        services.AddScoped<IRemediationScriptExecutor, RemediationScriptExecutor>();
        services.AddScoped<IAiRemediationPlanGenerator, AiRemediationPlanGenerator>();
        
        // Add the ATO Remediation Engine (core remediation engine)
        services.AddScoped<IRemediationEngine, AtoRemediationEngine>();

        // Add tools (always available even if agent is disabled)
        services.AddScoped<ComplianceAssessmentTool>();
        services.AddScoped<RemediationExecuteTool>();
        services.AddScoped<BatchRemediationTool>();
        services.AddScoped<DefenderForCloudTool>();
        services.AddScoped<ControlFamilyTool>();
        services.AddScoped<EvidenceCollectionTool>();
        services.AddScoped<DocumentGenerationTool>();
        services.AddScoped<ValidateRemediationTool>();
        services.AddScoped<RemediationPlanTool>();
        services.AddScoped<ComplianceHistoryTool>();
        services.AddScoped<ComplianceStatusTool>();
        services.AddScoped<AssessmentAuditLogTool>();

        // Only register agent if enabled
        services.AddScoped<ComplianceAgent>();
        if (options.Enabled)
        {
            services.AddScoped<BaseAgent>(sp => sp.GetRequiredService<ComplianceAgent>());
        }

        return services;
    }

    /// <summary>
    /// Adds the Knowledge Base Agent for RMF/STIG/DoD compliance knowledge queries.
    /// </summary>
    public static IServiceCollection AddKnowledgeBaseAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<KnowledgeBaseAgentOptions>(
            configuration.GetSection(KnowledgeBaseAgentOptions.SectionName));

        // Check if agent is enabled
        var options = configuration.GetSection(KnowledgeBaseAgentOptions.SectionName)
            .Get<KnowledgeBaseAgentOptions>() ?? new KnowledgeBaseAgentOptions();

        // Add state accessors (always needed for potential runtime enable)
        services.AddScoped<KnowledgeBaseStateAccessors>();

        // Add knowledge base services (required by tools)
        services.AddScoped<IDoDInstructionService, DoDInstructionService>();
        services.AddScoped<IDoDWorkflowService, DoDWorkflowService>();
        services.AddScoped<IStigKnowledgeService, StigKnowledgeService>();
        services.AddScoped<IRmfKnowledgeService, RmfKnowledgeService>();
        services.AddScoped<IImpactLevelService, ImpactLevelService>();
        services.AddScoped<IFedRampTemplateService, FedRampTemplateService>();

        // Add tools (always available even if agent is disabled)
        services.AddScoped<NistControlExplainerTool>();
        services.AddScoped<NistControlSearchTool>();
        services.AddScoped<StigExplainerTool>();
        services.AddScoped<StigSearchTool>();
        services.AddScoped<RmfExplainerTool>();
        services.AddScoped<ImpactLevelTool>();
        services.AddScoped<FedRampTemplateTool>();

        // Only register agent if enabled
        services.AddScoped<KnowledgeBaseAgent>();
        if (options.Enabled)
        {
            services.AddScoped<BaseAgent>(sp => sp.GetRequiredService<KnowledgeBaseAgent>());
        }

        return services;
    }

    /// <summary>
    /// Adds the Environment Agent for environment lifecycle management using Platform Engineering service templates.
    /// </summary>
    public static IServiceCollection AddEnvironmentAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<EnvironmentAgentOptions>(
            configuration.GetSection(EnvironmentAgentOptions.SectionName));

        // Check if agent is enabled
        var options = configuration.GetSection(EnvironmentAgentOptions.SectionName)
            .Get<EnvironmentAgentOptions>() ?? new EnvironmentAgentOptions();

        // Add state accessors (always needed for potential runtime enable)
        services.AddScoped<EnvironmentStateAccessors>();

        // Add deployment services (Bicep, Terraform deployers)
        services.AddScoped<ITemplateDeployer, BicepDeployer>();
        services.AddScoped<ITemplateDeployer, TerraformDeployer>();
        services.AddScoped<IDeployerFactory, DeployerFactory>();
        services.Configure<DeployerOptions>(configuration.GetSection("Deployment"));

        // Add Platform Engineering template services
        services.AddScoped<IServiceTemplateCatalogService, ServiceTemplateCatalogService>();
        services.AddScoped<IProvisionedEnvironmentService, ProvisionedEnvironmentService>();
        services.AddScoped<Core.Interfaces.Environments.IEnvironmentActivityService, EnvironmentActivityService>();

        // Add governance validation service for runtime policy enforcement
        services.AddScoped<Core.Services.Governance.IGovernanceValidationService, Core.Services.Governance.GovernanceValidationService>();

        // Add template-based tools (Platform Engineering approach)
        services.AddScoped<ServiceTemplateListTool>();
        services.AddScoped<ServiceTemplateDetailsTool>();
        services.AddScoped<ServiceTemplateMatchTool>();
        services.AddScoped<CreateEnvironmentFromTemplateTool>();
        services.AddScoped<ProvisionedEnvironmentListTool>();
        services.AddScoped<EnvironmentScaleFromTemplateTool>();
        services.AddScoped<EnvironmentCloneFromTemplateTool>();
        services.AddScoped<EnvironmentDeleteTool>();
        services.AddScoped<EnvironmentDriftDetectionTool>();
        services.AddScoped<EnvironmentDriftRemediationTool>();

        // Only register agent if enabled
        services.AddScoped<EnvironmentAgent>();
        if (options.Enabled)
        {
            services.AddScoped<BaseAgent>(sp => sp.GetRequiredService<EnvironmentAgent>());
        }

        return services;
    }

    /// <summary>
    /// Adds common tools available to all agents (configuration, etc.).
    /// </summary>
    public static IServiceCollection AddCommonTools(this IServiceCollection services)
    {
        // ConfigurationTool - manages subscription and config settings
        services.AddScoped<ConfigurationTool>();

        return services;
    }

    /// <summary>
    /// Adds the tool registry with all available tools.
    /// </summary>
    public static IServiceCollection AddToolRegistry(this IServiceCollection services)
    {
        services.AddSingleton<ToolRegistry>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ToolRegistry>>();
            var registry = new ToolRegistry(logger);

            // Tools will be registered when first resolved from scoped services
            return registry;
        });

        return services;
    }

    /// <summary>
    /// Adds a chat client implementation. Call this with your preferred LLM provider.
    /// Note: AddAgentFramework already calls AddAzureOpenAIChatClient automatically.
    /// </summary>
    /// <remarks>
    /// Example using Azure OpenAI:
    /// <code>
    /// services.AddChatClient(sp => 
    ///     new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    ///         .GetChatClient(deploymentName)
    ///         .AsChatClient());
    /// </code>
    /// </remarks>
    public static IServiceCollection AddChatClient(
        this IServiceCollection services,
        Func<IServiceProvider, IChatClient> factory)
    {
        services.AddScoped(factory);
        return services;
    }

    /// <summary>
    /// Adds a mock chat client for testing purposes.
    /// </summary>
    public static IServiceCollection AddMockChatClient(this IServiceCollection services)
    {
        services.AddScoped<IChatClient, MockChatClient>();
        return services;
    }
}

/// <summary>
/// Mock chat client for testing without a real LLM.
/// </summary>
internal class MockChatClient : IChatClient
{
    public ChatClientMetadata Metadata => new("MockChatClient");

    public object? GetService(Type serviceType, object? key = null) => null;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        var lastUserMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User);
        var content = lastUserMessage?.Text ?? "test";

        return new ChatResponse(new ChatMessage(ChatRole.Assistant,
            $"[Mock Response] Processed: {content[..Math.Min(50, content.Length)]}..."));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        var update = new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new Microsoft.Extensions.AI.TextContent("[Mock Streaming Response]")]
        };
        yield return update;
    }

    public void Dispose() { }
}
