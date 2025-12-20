using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Configuration;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Plugins;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.Agents;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.KnowledgeBase;

namespace Platform.Engineering.Copilot.KnowledgeBase.Agent.Extensions;

/// <summary>
/// Service collection extensions for Knowledge Base Agent
/// </summary>
public static class KnowledgeBaseAgentCollectionExtensions
{
    /// <summary>
    /// Add Knowledge Base Agent with RAG capabilities
    /// </summary>
    public static IServiceCollection AddKnowledgeBaseAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<KnowledgeBaseAgentOptions>(
            configuration.GetSection(KnowledgeBaseAgentOptions.SectionName));

        // Validation of configuration
        services.AddOptionsWithValidateOnStart<KnowledgeBaseAgentOptions>()
            .Bind(configuration.GetSection(KnowledgeBaseAgentOptions.SectionName))
            .Validate(options =>
            {
                if (options.EnableRag && string.IsNullOrWhiteSpace(options.KnowledgeBaseIndexName))
                {
                    throw new InvalidOperationException(
                        "KnowledgeBaseIndexName must be configured when RAG is enabled");
                }

                if (options.MinimumRelevanceScore < 0 || options.MinimumRelevanceScore > 1)
                {
                    throw new InvalidOperationException(
                        "MinimumRelevanceScore must be between 0.0 and 1.0");
                }

                if (options.MaxRagResults < 1 || options.MaxRagResults > 20)
                {
                    throw new InvalidOperationException(
                        "MaxRagResults must be between 1 and 20");
                }

                if (options.Temperature < 0 || options.Temperature > 2)
                {
                    throw new InvalidOperationException(
                        "Temperature must be between 0.0 and 2.0");
                }

                return true;
            }, "Knowledge Base Agent configuration is invalid");

        // Knowledge Base Services (RMF, STIG, DoD Instructions, Workflows, Impact Levels, FedRAMP)
        services.AddSingleton<IRmfKnowledgeService, RmfKnowledgeService>();
        services.AddSingleton<IStigKnowledgeService, StigKnowledgeService>();
        services.AddSingleton<IDoDInstructionService, DoDInstructionService>();
        services.AddSingleton<IDoDWorkflowService, DoDWorkflowService>();
        services.AddSingleton<IImpactLevelService, ImpactLevelService>();
        services.AddSingleton<IFedRampTemplateService, FedRampTemplateService>();

        // Register plugin
        services.AddScoped<KnowledgeBasePlugin>();

        // Register Agent as both ISpecializedAgent and concrete type
        services.AddScoped<KnowledgeBaseAgent>();
        services.AddScoped<ISpecializedAgent>(sp =>
            sp.GetRequiredService<KnowledgeBaseAgent>());

        // Memory caching for knowledge base results
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Add Knowledge Base Agent with custom options
    /// </summary>
    public static IServiceCollection AddKnowledgeBaseAgent(
        this IServiceCollection services,
        Action<KnowledgeBaseAgentOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Validate options
        services.AddOptionsWithValidateOnStart<KnowledgeBaseAgentOptions>()
            .Validate(options =>
            {
                if (options.EnableRag && string.IsNullOrWhiteSpace(options.KnowledgeBaseIndexName))
                {
                    throw new InvalidOperationException(
                        "KnowledgeBaseIndexName must be configured when RAG is enabled");
                }

                if (options.MinimumRelevanceScore < 0 || options.MinimumRelevanceScore > 1)
                {
                    throw new InvalidOperationException(
                        "MinimumRelevanceScore must be between 0.0 and 1.0");
                }

                return true;
            }, "Knowledge Base Agent configuration is invalid");

        // NOTE: Knowledge Base Services (RMF, STIG, DoD Instructions, Workflows)
        // are registered in Compliance.Agent.Extensions.AddEnhancedAtoCompliance()
        // They must be registered before calling AddKnowledgeBaseAgent()

        // Register Plugin
        services.AddSingleton<KnowledgeBasePlugin>();

        // Register Agent
        services.AddSingleton<KnowledgeBaseAgent>();
        services.AddSingleton<ISpecializedAgent>(sp =>
            sp.GetRequiredService<KnowledgeBaseAgent>());

        // Memory caching
        services.AddMemoryCache();

        return services;
    }
}
