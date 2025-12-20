using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;
using Platform.Engineering.Copilot.Core.Services.Chat;
using Platform.Engineering.Copilot.Core.Services.TokenManagement;
using Platform.Engineering.Copilot.Core.Configuration;

namespace Platform.Engineering.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for registering token management services
/// </summary>
public static class TokenManagementServiceCollectionExtensions
{
    /// <summary>
    /// Add token management services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddTokenManagementServices(this IServiceCollection services)
    {
        // Register configuration options
        services.AddOptions<TokenManagementOptions>()
            .BindConfiguration(TokenManagementOptions.SectionName);
        
        // Register TokenCounter as singleton (thread-safe, caches encoders)
        services.AddSingleton<ITokenCounter, TokenCounter>();
        
        // Register PromptOptimizer as singleton (stateless service)
        services.AddSingleton<IPromptOptimizer, PromptOptimizer>();
        
        // Register RAGContextOptimizer as singleton (stateless service)
        services.AddSingleton<IRagContextOptimizer, RagContextOptimizer>();
        
        // Register ChatBuilder as scoped (uses ITokenCounter)
        services.AddScoped<ChatBuilder>();
        
        // Register SemanticKernelRAGService as scoped (uses ITokenCounter, ChatBuilder, ISemanticKernelService)
        services.AddScoped<ISemanticKernelRAGService, SemanticKernelRAGService>();
        
        // Register TokenManagementHelper as scoped (uses configuration)
        services.AddScoped<TokenManagementHelper>();
        
        // Register CostTrackingService as singleton (Phase 4: Cost tracking and metrics)
        services.AddSingleton<ICostTrackingService, CostTrackingService>();
        
        // Register ConversationHistoryOptimizer as singleton (Phase 5: Conversation history optimization)
        services.AddSingleton<IConversationHistoryOptimizer, ConversationHistoryOptimizer>();
        
        return services;
    }
}