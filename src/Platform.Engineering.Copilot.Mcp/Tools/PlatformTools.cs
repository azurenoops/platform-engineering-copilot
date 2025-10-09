using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Mcp.Models;
using Platform.Engineering.Copilot.Mcp.Services;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Mcp.Tools;

/// <summary>
/// MCP tools that act as a thin proxy to Platform.API
/// This service fetches tools dynamically from Platform.API and delegates all execution to it.
/// Implements the adapter pattern to convert between MCP protocol (STDIO) and Platform.API (HTTP REST).
/// </summary>
public class PlatformTools
{
    private readonly PlatformApiClient _apiClient;
    private readonly ILogger<PlatformTools> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public PlatformTools(PlatformApiClient apiClient, ILogger<PlatformTools> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Get all available MCP tools dynamically from Platform.API
    /// Converts Platform.API ToolInfo format to MCP McpTool format
    /// </summary>
    public async Task<List<McpTool>> GetToolsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching tools from Platform.API");
            
            var toolList = await _apiClient.GetToolsAsync();
            
            var mcpTools = toolList.Tools.Select(tool => new McpTool
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = tool.InputSchema
            }).ToList();

            _logger.LogInformation("Converted {Count} Platform.API tools to MCP format", mcpTools.Count);
            return mcpTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tools from Platform.API");
            return new List<McpTool>();
        }
    }

    /// <summary>
    /// Execute a tool by delegating to Platform.API
    /// Converts MCP tool call format to Platform.API execution request
    /// </summary>
    public async Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object?> arguments)
    {
        try
        {
            _logger.LogInformation("Executing tool {ToolName} via Platform.API", toolName);
            
            var response = await _apiClient.ExecuteToolAsync(toolName, arguments);
            
            if (response.Success)
            {
                // Convert successful result to MCP format
                var resultText = response.Result switch
                {
                    string str => str,
                    _ => JsonSerializer.Serialize(response.Result, _jsonOptions)
                };

                return new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new McpContent
                        {
                            Type = "text",
                            Text = resultText
                        }
                    }
                };
            }
            else
            {
                // Convert error to MCP format
                _logger.LogWarning("Tool {ToolName} execution failed: {Error}", toolName, response.Error);
                
                return new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new McpContent
                        {
                            Type = "text",
                            Text = $"Error: {response.Error ?? "Unknown error"}"
                        }
                    },
                    IsError = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Exception executing tool: {ex.Message}"
                    }
                },
                IsError = true
            };
        }
    }
}
