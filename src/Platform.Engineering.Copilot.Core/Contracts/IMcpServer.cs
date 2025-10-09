using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Contracts;

/// <summary>
/// Contract for MCP tool handlers
/// </summary>
public interface IMcpToolHandler
{
    /// <summary>
    /// Get the tools supported by this handler
    /// </summary>
    Task<IEnumerable<McpTool>> GetToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a tool call
    /// </summary>
    Task<McpToolResult> ExecuteToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if this handler supports the given tool
    /// </summary>
    bool SupportsTools(string toolName);
}

/// <summary>
/// Contract for MCP resource handlers
/// </summary>
public interface IMcpResourceHandler
{
    /// <summary>
    /// Get the resources supported by this handler
    /// </summary>
    Task<IEnumerable<McpResource>> GetResourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the content of a resource
    /// </summary>
    Task<McpResourceContent> GetResourceContentAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if this handler supports the given resource URI
    /// </summary>
    bool SupportsResource(string uri);
}

/// <summary>
/// Contract for the main MCP server
/// </summary>
public interface IMcpServer
{
    /// <summary>
    /// Get server information
    /// </summary>
    Task<McpServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available tools
    /// </summary>
    Task<IEnumerable<McpTool>> GetToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a tool call
    /// </summary>
    Task<McpToolResult> CallToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available resources
    /// </summary>
    Task<IEnumerable<McpResource>> GetResourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get resource content
    /// </summary>
    Task<McpResourceContent> GetResourceContentAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start the server
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the server
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}