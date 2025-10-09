using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.API.Models;
using Platform.Engineering.Copilot.API.Services;

namespace Platform.Engineering.Copilot.API.Controllers;

/// <summary>
/// REST API controller for platform engineering tools and operations.
/// Provides endpoints for discovering available tools, executing tool operations,
/// and managing platform infrastructure through standardized tool interfaces.
/// Supports infrastructure provisioning, monitoring, security scanning, and compliance operations.
/// </summary>
[Obsolete("ToolsController is obsolete. Use /api/chat/intelligent-query endpoint with IntelligentChatService_v2 and Semantic Kernel plugins instead. " +
          "This controller depends on the obsolete PlatformToolService and removed Extensions project tools. " +
          "Will be removed in a future release.", error: false)]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ToolsController : ControllerBase
{
    private readonly ILogger<ToolsController> _logger;
    private readonly PlatformToolService _toolService;

    /// <summary>
    /// Initializes a new instance of the ToolsController.
    /// </summary>
    /// <param name="logger">Logger for API operation diagnostics</param>
    /// <param name="toolService">Platform tool service for tool orchestration</param>
    public ToolsController(ILogger<ToolsController> logger, PlatformToolService toolService)
    {
        _logger = logger;
        _toolService = toolService;
    }

    /// <summary>
    /// Get all available platform engineering tools
    /// </summary>
    /// <returns>List of available tools with their schemas</returns>
    [HttpGet]
    public async Task<ActionResult<ToolListResponse>> GetTools()
    {
        try
        {
            var result = await _toolService.GetAvailableToolsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available tools");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Execute a specific tool with provided parameters
    /// </summary>
    /// <param name="request">Tool execution request containing tool name and parameters</param>
    /// <returns>Tool execution result</returns>
    [HttpPost("execute")]
    public async Task<ActionResult<ToolExecutionResponse>> ExecuteTool([FromBody] ToolExecutionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ToolName))
            {
                return BadRequest("Tool name is required");
            }

            var result = await _toolService.ExecuteToolAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", request.ToolName);
            return StatusCode(500, new ToolExecutionResponse
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Get information about a specific tool
    /// </summary>
    /// <param name="toolName">Name of the tool</param>
    /// <returns>Tool information including schema</returns>
    [HttpGet("{toolName}")]
    public async Task<ActionResult<ToolInfo>> GetTool(string toolName)
    {
        try
        {
            var allTools = await _toolService.GetAvailableToolsAsync();
            var tool = allTools.Tools.FirstOrDefault(t => 
                t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
            
            if (tool == null)
            {
                return NotFound($"Tool '{toolName}' not found");
            }

            return Ok(tool);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tool {ToolName}", toolName);
            return StatusCode(500, "Internal server error");
        }
    }
}