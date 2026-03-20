using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Configuration;
using Platform.Engineering.Copilot.Agents.Configuration.Tools;
using Platform.Engineering.Copilot.Agents.CostManagement;
using Platform.Engineering.Copilot.Agents.CostManagement.Tools;
using Platform.Engineering.Copilot.Agents.Discovery;
using Platform.Engineering.Copilot.Agents.Discovery.Tools;
using Platform.Engineering.Copilot.Agents.Environment;
using Platform.Engineering.Copilot.Agents.Environment.Tools;
using Platform.Engineering.Copilot.Agents.Infrastructure;
using Platform.Engineering.Copilot.Agents.Infrastructure.Tools;
using Platform.Engineering.Copilot.Agents.KnowledgeBase;
using Platform.Engineering.Copilot.Agents.Security;
using Platform.Engineering.Copilot.Agents.Security.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Services;
using Platform.Engineering.Copilot.Core.Observability;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Core.Extensions;

/// <summary>
/// Shared DI registration for platform copilot services.
/// Called by both MCP and Chat hosts to ensure identical agent capabilities (FR-004, FR-007).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all platform copilot services: agents, tools, orchestrator,
    /// Azure OpenAI config binding, and IChatClient factory.
    /// </summary>
    public static IServiceCollection AddPlatformCopilotServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Configuration ───
        services.AddOptions();
        services.Configure<AzureOpenAIOptions>(
            configuration.GetSection(AzureOpenAIOptions.SectionName));

        // ─── Core Services ───
        services.AddMemoryCache();
        services.AddSingleton<HealthCheckService>();
        services.AddSingleton<MetricsService>();
        services.AddSingleton<AuditLogService>();
        services.AddSingleton<AzureErrorHandler>();
        services.AddSingleton<IAgentStateManager, InMemoryAgentStateManager>();
        services.AddSingleton<RemediationBoardService>();
        services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();
        services.AddScoped<CorrelationIdAccessor>();

        // ─── Azure OpenAI Chat Client Factory ───
        services.AddSingleton<AzureOpenAIChatClientFactory>();
        services.AddSingleton<IChatClient?>(sp =>
        {
            var factory = sp.GetRequiredService<AzureOpenAIChatClientFactory>();
            return factory.CreateChatClient();
        });

        // ─── Configuration Agent (1 tool) ───
        services.AddSingleton<ConfigurationManageTool>();
        services.AddSingleton<ConfigurationAgent>(sp =>
        {
            return new ConfigurationAgent(
                sp.GetRequiredService<ILogger<ConfigurationAgent>>(),
                new BaseTool[] { sp.GetRequiredService<ConfigurationManageTool>() },
                sp.GetService<IChatClient>(),
                sp.GetRequiredService<IOptions<AzureOpenAIOptions>>());
        });

        // ─── Knowledge Base Agent (shell — no tools) ───
        services.AddSingleton<KnowledgeBaseAgent>(sp =>
        {
            return new KnowledgeBaseAgent(
                sp.GetRequiredService<ILogger<KnowledgeBaseAgent>>(),
                Array.Empty<BaseTool>(),
                sp.GetService<IChatClient>(),
                sp.GetRequiredService<IOptions<AzureOpenAIOptions>>());
        });

        // ─── Infrastructure Agent (6 tools) ───
        services.AddSingleton<GenerateInfrastructureTemplateTool>();
        services.AddSingleton<ProvisionInfrastructureTool>();
        services.AddSingleton<ValidateTemplateTool>();
        services.AddSingleton<ListDeploymentsTool>();
        services.AddSingleton<GetDeploymentStatusTool>();
        services.AddSingleton<RollbackDeploymentTool>();
        services.AddSingleton<InfrastructureAgent>(sp =>
        {
            return new InfrastructureAgent(
                sp.GetRequiredService<ILogger<InfrastructureAgent>>(),
                new BaseTool[]
                {
                    sp.GetRequiredService<GenerateInfrastructureTemplateTool>(),
                    sp.GetRequiredService<ProvisionInfrastructureTool>(),
                    sp.GetRequiredService<ValidateTemplateTool>(),
                    sp.GetRequiredService<ListDeploymentsTool>(),
                    sp.GetRequiredService<GetDeploymentStatusTool>(),
                    sp.GetRequiredService<RollbackDeploymentTool>()
                },
                sp.GetService<IChatClient>(),
                sp.GetRequiredService<IOptions<AzureOpenAIOptions>>());
        });

        // ─── Cost Management Agent (6 tools) ───
        services.AddSingleton<GetCostAnalysisTool>();
        services.AddSingleton<GetCostForecastTool>();
        services.AddSingleton<GetOptimizationSuggestionsTool>();
        services.AddSingleton<GetCachedCostReportTool>();
        services.AddSingleton<GetBudgetStatusTool>();
        services.AddSingleton<GetCostAnomaliesTool>();
        services.AddSingleton<CostManagementAgent>(sp =>
        {
            return new CostManagementAgent(
                sp.GetRequiredService<ILogger<CostManagementAgent>>(),
                new BaseTool[]
                {
                    sp.GetRequiredService<GetCostAnalysisTool>(),
                    sp.GetRequiredService<GetCostForecastTool>(),
                    sp.GetRequiredService<GetOptimizationSuggestionsTool>(),
                    sp.GetRequiredService<GetCachedCostReportTool>(),
                    sp.GetRequiredService<GetBudgetStatusTool>(),
                    sp.GetRequiredService<GetCostAnomaliesTool>()
                },
                sp.GetService<IChatClient>(),
                sp.GetRequiredService<IOptions<AzureOpenAIOptions>>());
        });

        // ─── Discovery Agent (9 tools) ───
        services.AddSingleton<DiscoverResourcesTool>();
        services.AddSingleton<GetResourceDependenciesTool>();
        services.AddSingleton<CrossSubscriptionQueryTool>();
        services.AddSingleton<GetResourceHealthTool>();
        services.AddSingleton<GetNetworkTopologyTool>();
        services.AddSingleton<AnalyzeTagsTool>();
        services.AddSingleton<GetResourceChangesTool>();
        services.AddSingleton<GetOrphanedResourcesTool>();
        services.AddSingleton<GetResourceMetricsTool>();
        services.AddSingleton<DiscoveryAgent>(sp =>
        {
            return new DiscoveryAgent(
                sp.GetRequiredService<ILogger<DiscoveryAgent>>(),
                new BaseTool[]
                {
                    sp.GetRequiredService<DiscoverResourcesTool>(),
                    sp.GetRequiredService<GetResourceDependenciesTool>(),
                    sp.GetRequiredService<CrossSubscriptionQueryTool>(),
                    sp.GetRequiredService<GetResourceHealthTool>(),
                    sp.GetRequiredService<GetNetworkTopologyTool>(),
                    sp.GetRequiredService<AnalyzeTagsTool>(),
                    sp.GetRequiredService<GetResourceChangesTool>(),
                    sp.GetRequiredService<GetOrphanedResourcesTool>(),
                    sp.GetRequiredService<GetResourceMetricsTool>()
                },
                sp.GetService<IChatClient>(),
                sp.GetRequiredService<IOptions<AzureOpenAIOptions>>());
        });

        // ─── Environment Agent (10 tools) ───
        services.AddSingleton<CloneEnvironmentTool>();
        services.AddSingleton<DetectDriftTool>();
        services.AddSingleton<CompareEnvironmentsTool>();
        services.AddSingleton<PromoteEnvironmentTool>();
        services.AddSingleton<ListEnvironmentsTool>();
        services.AddSingleton<GetEnvironmentStatusTool>();
        services.AddSingleton<CreateEnvironmentTool>();
        services.AddSingleton<DeleteEnvironmentTool>();
        services.AddSingleton<GetEnvironmentHistoryTool>();
        services.AddSingleton<ValidateEnvironmentTool>();
        services.AddSingleton<EnvironmentAgent>(sp =>
        {
            return new EnvironmentAgent(
                sp.GetRequiredService<ILogger<EnvironmentAgent>>(),
                new BaseTool[]
                {
                    sp.GetRequiredService<CloneEnvironmentTool>(),
                    sp.GetRequiredService<DetectDriftTool>(),
                    sp.GetRequiredService<CompareEnvironmentsTool>(),
                    sp.GetRequiredService<PromoteEnvironmentTool>(),
                    sp.GetRequiredService<ListEnvironmentsTool>(),
                    sp.GetRequiredService<GetEnvironmentStatusTool>(),
                    sp.GetRequiredService<CreateEnvironmentTool>(),
                    sp.GetRequiredService<DeleteEnvironmentTool>(),
                    sp.GetRequiredService<GetEnvironmentHistoryTool>(),
                    sp.GetRequiredService<ValidateEnvironmentTool>()
                },
                sp.GetService<IChatClient>(),
                sp.GetRequiredService<IOptions<AzureOpenAIOptions>>());
        });

        // ─── Security Agent (3 tools) ───
        services.AddSingleton<GetSecureScoreTool>();
        services.AddSingleton<GetSecurityRecommendationsTool>();
        services.AddSingleton<ManageSecurityPolicyTool>();
        services.AddSingleton<SecurityAgent>(sp =>
        {
            return new SecurityAgent(
                sp.GetRequiredService<ILogger<SecurityAgent>>(),
                new BaseTool[]
                {
                    sp.GetRequiredService<GetSecureScoreTool>(),
                    sp.GetRequiredService<GetSecurityRecommendationsTool>(),
                    sp.GetRequiredService<ManageSecurityPolicyTool>()
                },
                sp.GetService<IChatClient>(),
                sp.GetRequiredService<IOptions<AzureOpenAIOptions>>());
        });

        // ─── Orchestrator ───
        services.AddSingleton<PlatformOrchestrator>(sp =>
        {
            var chatClient = sp.GetService<IChatClient>();
            var orchestrator = new PlatformOrchestrator(
                sp.GetRequiredService<ILogger<PlatformOrchestrator>>(),
                chatClient);

            // Register all agents with the orchestrator
            orchestrator.RegisterAgent(sp.GetRequiredService<ConfigurationAgent>());
            orchestrator.RegisterAgent(sp.GetRequiredService<KnowledgeBaseAgent>());
            orchestrator.RegisterAgent(sp.GetRequiredService<InfrastructureAgent>());
            orchestrator.RegisterAgent(sp.GetRequiredService<CostManagementAgent>());
            orchestrator.RegisterAgent(sp.GetRequiredService<DiscoveryAgent>());
            orchestrator.RegisterAgent(sp.GetRequiredService<EnvironmentAgent>());
            orchestrator.RegisterAgent(sp.GetRequiredService<SecurityAgent>());

            return orchestrator;
        });

        return services;
    }
}
