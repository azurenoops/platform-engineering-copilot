using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Base class for all Supervisor Semantic Kernel plugins
/// Provides common utilities and logging
/// </summary>
public abstract class BaseSupervisorPlugin
{
    protected readonly ILogger _logger;
    protected readonly Kernel _kernel;

    protected BaseSupervisorPlugin(ILogger logger, Kernel kernel)
    {
        _logger = logger;
        _kernel = kernel;
    }

    /// <summary>
    /// Format McpToolResult as user-friendly string
    /// </summary>
    protected string FormatToolResult(McpToolResult result)
    {
        if (result.IsSuccess)
        {
            return result.Content?.ToString() ?? "Operation completed successfully";
        }

        return $"❌ **Error:** {result.Error}\n\nPlease check the logs for more details.";
    }

    /// <summary>
    /// Create standardized error response
    /// </summary>
    protected string CreateErrorResponse(string operation, Exception ex)
    {
        _logger.LogError(ex, "Error in {Operation}", operation);
        return $"❌ **Failed to {operation}**\n\n**Error:** {ex.Message}";
    }
}
