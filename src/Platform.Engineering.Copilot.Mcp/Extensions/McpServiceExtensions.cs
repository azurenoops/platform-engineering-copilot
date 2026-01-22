using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Mcp.Server;
using Platform.Engineering.Copilot.Mcp.Tools;

namespace Platform.Engineering.Copilot.Mcp.Extensions;

/// <summary>
/// Extension methods for registering MCP services
/// </summary>
public static class McpServiceExtensions
{
    /// <summary>
    /// Adds MCP server and domain-specific tools to the service collection.
    /// Must be called after AddAgentFramework to ensure Agent Framework tools are registered.
    /// </summary>
    public static IServiceCollection AddMcpServer(this IServiceCollection services)
    {
        // Register domain-specific MCP tools (wrappers around Agent Framework tools)
        services.AddScoped<ConfigurationMcpTools>();
        services.AddScoped<ComplianceMcpTools>();
        services.AddScoped<DiscoveryMcpTools>();
        services.AddScoped<InfrastructureMcpTools>();
        services.AddScoped<CostManagementMcpTools>();
        services.AddScoped<KnowledgeBaseMcpTools>();

        // Register the MCP server with domain-specific tools (scoped for per-request lifetime)
        // McpServer now uses PlatformAgentGroupChat directly from Agents project
        services.AddScoped<McpServer>();

        // Register HTTP bridge for REST API access (singleton since it just maps routes)
        services.AddSingleton<McpHttpBridge>();

        return services;
    }

    /// <summary>
    /// Adds MCP stdio service for running as a background service
    /// </summary>
    public static IServiceCollection AddMcpStdioService(this IServiceCollection services)
    {
        services.AddHostedService<McpStdioService>();
        return services;
    }
}
