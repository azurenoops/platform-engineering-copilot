using System.ComponentModel;
using Platform.Engineering.Copilot.Agents.Configuration.Tools;

namespace Platform.Engineering.Copilot.Mcp.Tools;

/// <summary>
/// MCP tools for Platform Engineering Copilot configuration operations.
/// Wraps Agent Framework configuration tools for exposure via the MCP protocol
/// (GitHub Copilot, Claude Desktop, etc.)
/// </summary>
public class ConfigurationMcpTools
{
    private readonly ConfigurationTool _configurationTool;

    public ConfigurationMcpTools(ConfigurationTool configurationTool)
    {
        _configurationTool = configurationTool ?? throw new ArgumentNullException(nameof(configurationTool));
    }

    /// <summary>
    /// Configure the default Azure subscription for all operations
    /// </summary>
    [Description("Configure the default Azure subscription for all Platform Engineering Copilot operations. " +
                 "Actions: 'set' to configure a subscription, 'get' to show current, 'clear' to remove default. " +
                 "This setting persists across sessions.")]
    public async Task<string> ConfigureSubscriptionAsync(
        string action,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = action,
            ["subscriptionId"] = subscriptionId
        };
        return await _configurationTool.ExecuteAsync(args, cancellationToken);
    }
}
