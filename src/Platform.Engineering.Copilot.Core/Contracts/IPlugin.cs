namespace Platform.Engineering.Copilot.Core.Contracts;

/// <summary>
/// Contract for extensible plugin system
/// </summary>
public interface IPluginSystem
{
    /// <summary>
    /// Load plugins from the specified directory
    /// </summary>
    Task LoadPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all loaded plugins
    /// </summary>
    IEnumerable<IPlugin> GetLoadedPlugins();

    /// <summary>
    /// Register a plugin instance
    /// </summary>
    void RegisterPlugin(IPlugin plugin);

    /// <summary>
    /// Unload a specific plugin
    /// </summary>
    Task UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base contract for all plugins
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Unique plugin identifier
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Plugin name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Plugin version
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Plugin description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Initialize the plugin
    /// </summary>
    Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispose of plugin resources
    /// </summary>
    Task DisposeAsync();
}

/// <summary>
/// Contract for tool plugins
/// </summary>
public interface IToolPlugin : IPlugin, IMcpToolHandler
{
}

/// <summary>
/// Contract for resource plugins
/// </summary>
public interface IResourcePlugin : IPlugin, IMcpResourceHandler
{
}

/// <summary>
/// Contract for gateway plugins that can communicate with external MCPs
/// </summary>
public interface IGatewayPlugin : IPlugin
{
    /// <summary>
    /// Connect to an external MCP server
    /// </summary>
    Task<bool> ConnectAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the external MCP server
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if connected to external MCP server
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Get tools from the external MCP server
    /// </summary>
    Task<IEnumerable<Models.McpTool>> GetExternalToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forward a tool call to the external MCP server
    /// </summary>
    Task<Models.McpToolResult> ForwardToolCallAsync(Models.McpToolCall toolCall, CancellationToken cancellationToken = default);
}