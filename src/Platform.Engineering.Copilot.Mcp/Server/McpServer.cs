using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Mcp.Models;
using Platform.Engineering.Copilot.Mcp.Services;
using Platform.Engineering.Copilot.Mcp.Tools;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Mcp.Server;

/// <summary>
/// MCP server that acts as a thin proxy to Platform.API
/// Handles MCP protocol communication (STDIO) and delegates all tool operations to Platform.API (HTTP)
/// </summary>
public class McpServer
{
    private readonly PlatformTools _platformTools;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(PlatformTools platformTools, ILogger<McpServer> logger)
    {
        _platformTools = platformTools;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Start the MCP server
    /// </summary>
    public async Task StartAsync()
    {
        _logger.LogInformation("Starting Platform Engineering MCP Server");

        try
        {
            // Read from stdin and write to stdout for MCP communication
            using var reader = new StreamReader(Console.OpenStandardInput());
            using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                try
                {
                    var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                    if (request != null)
                    {
                        var response = await HandleRequestAsync(request);
                        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                        await writer.WriteLineAsync(responseJson);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON received: {Line}", line);
                    var errorResponse = new McpResponse
                    {
                        Id = 0,
                        Error = new McpError
                        {
                            Code = -32700,
                            Message = "Parse error"
                        }
                    };
                    var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                    await writer.WriteLineAsync(errorJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request: {Line}", line);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MCP server");
            throw;
        }
    }

    /// <summary>
    /// Handle incoming MCP request
    /// </summary>
    private async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        try
        {
            _logger.LogDebug("Handling request: {Method}", request.Method);

            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => await HandleToolsListAsync(request),
                "tools/call" => await HandleToolCallAsync(request),
                "ping" => HandlePing(request),
                _ => new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32601,
                        Message = $"Method not found: {request.Method}"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request {Method}", request.Method);
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = "Internal error",
                    Data = ex.Message
                }
            };
        }
    }

    /// <summary>
    /// Handle initialize request
    /// </summary>
    private McpResponse HandleInitialize(McpRequest request)
    {
        _logger.LogInformation("Client initialized MCP connection");

        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new McpServerCapabilities
                {
                    Tools = new { }
                },
                serverInfo = new
                {
                    name = "Platform Engineering MCP Server",
                    version = "1.0.0"
                }
            }
        };
    }

    /// <summary>
    /// Handle tools list request - dynamically fetch from Platform.API
    /// </summary>
    private async Task<McpResponse> HandleToolsListAsync(McpRequest request)
    {
        var tools = await _platformTools.GetToolsAsync();
        
        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                tools = tools
            }
        };
    }

    /// <summary>
    /// Handle tool call request - delegate to Platform.API
    /// </summary>
    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        try
        {
            var toolCall = JsonSerializer.Deserialize<McpToolCall>(
                JsonSerializer.Serialize(request.Params, _jsonOptions), 
                _jsonOptions);

            if (toolCall == null)
            {
                return new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32602,
                        Message = "Invalid tool call parameters"
                    }
                };
            }

            var result = await _platformTools.ExecuteToolAsync(toolCall.Name, toolCall.Arguments ?? new Dictionary<string, object?>());

            return new McpResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool call");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = "Tool execution failed",
                    Data = ex.Message
                }
            };
        }
    }

    /// <summary>
    /// Handle ping request
    /// </summary>
    private McpResponse HandlePing(McpRequest request)
    {
        return new McpResponse
        {
            Id = request.Id,
            Result = "pong"
        };
    }
}