using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.API.Models;

namespace Platform.Engineering.Copilot.API.Services;

/// <summary>
/// Platform tool service that provides unified access to all platform management tools and operations.
/// This is a thin orchestration layer that aggregates tool handlers and delegates to them.
/// For intelligent chat queries, use ChatController's intelligent-query endpoint which uses
/// IntelligentChatService for AI-powered intent classification and tool routing.
/// </summary>
[Obsolete("PlatformToolService is obsolete. Use Semantic Kernel plugins (Platform.Engineering.Copilot.Core.Plugins) and IntelligentChatService_v2 instead. " +
          "This service depends on the removed Extensions project tools (IMcpToolHandler). " +
          "Will be removed in a future release.", error: false)]
public class PlatformToolService
{
    private readonly ILogger<PlatformToolService> _logger;

    public PlatformToolService(ILogger<PlatformToolService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get list of all available tools from all registered handlers
    /// </summary>
    public async Task<ToolListResponse> GetAvailableToolsAsync()
    {
        _logger.LogWarning("PlatformToolService is obsolete. Use Semantic Kernel plugins instead.");
        
        return await Task.FromResult(new ToolListResponse 
        { 
            Tools = new List<ToolInfo>() 
        });
    }

    /// <summary>
    /// Execute a specific tool by name with provided parameters
    /// </summary>
    public async Task<ToolExecutionResponse> ExecuteToolAsync(ToolExecutionRequest request)
    {
        _logger.LogWarning("PlatformToolService is obsolete. Use Semantic Kernel plugins and IntelligentChatService_v2 instead.");

        return await Task.FromResult(new ToolExecutionResponse
        {
            Success = false,
            Error = "PlatformToolService is obsolete. Please use the /api/chat/intelligent-query endpoint with IntelligentChatService_v2 and Semantic Kernel plugins instead."
        });
    }
}
