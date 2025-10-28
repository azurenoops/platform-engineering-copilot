using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Mcp.Tools;
using Platform.Engineering.Copilot.Mcp.Models;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Mcp.Server;

/// <summary>
/// MCP server that exposes the Platform Engineering Copilot's multi-agent orchestrator via stdio
/// Handles MCP protocol communication (JSONRPC over stdin/stdout) for GitHub Copilot, Claude Desktop, and other AI tools
/// </summary>
public class McpServer
{
    private readonly PlatformEngineeringCopilotTools _chatTool;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(PlatformEngineeringCopilotTools chatTool, ILogger<McpServer> logger)
    {
        _chatTool = chatTool;
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
                    Tools = new ToolsCapabilities { ListChanged = false }
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
    /// Handle tools list request - expose platform engineering tools
    /// </summary>
    private Task<McpResponse> HandleToolsListAsync(McpRequest request)
    {
        var tools = new List<McpTool>
        {
            new McpTool
            {
                Name = "platform_engineering_chat",
                Description = "Process platform engineering requests through the multi-agent orchestrator. Supports infrastructure provisioning, compliance scanning, cost analysis, resource discovery, environment management, and mission onboarding. The orchestrator automatically selects and coordinates the appropriate specialized agents.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        message = new
                        {
                            type = "string",
                            description = "The user's platform engineering request (e.g., 'Generate a Bicep template for AKS', 'Check NIST compliance', 'Estimate costs')"
                        },
                        conversationId = new
                        {
                            type = "string",
                            description = "Optional conversation ID to maintain context across multiple requests"
                        }
                    },
                    required = new[] { "message" }
                }
            },
            new McpTool
            {
                Name = "get_conversation_history",
                Description = "Retrieve the conversation history for a specific conversation, including all messages and tools used.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        conversationId = new
                        {
                            type = "string",
                            description = "The conversation ID to retrieve history for"
                        }
                    },
                    required = new[] { "conversationId" }
                }
            },
            new McpTool
            {
                Name = "get_proactive_suggestions",
                Description = "Get AI-generated suggestions for next actions based on the current conversation context.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        conversationId = new
                        {
                            type = "string",
                            description = "The conversation ID to generate suggestions for"
                        }
                    },
                    required = new[] { "conversationId" }
                }
            }
        };
        
        return Task.FromResult(new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                tools = tools
            }
        });
    }

    /// <summary>
    /// Handle tool call request - execute through multi-agent orchestrator
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

            _logger.LogInformation("Executing tool: {ToolName}", toolCall.Name);

            // Convert arguments to nullable dictionary
            var args = toolCall.Arguments?.ToDictionary(
                kvp => kvp.Key,
                kvp => (object?)kvp.Value);

            McpToolResult result = toolCall.Name switch
            {
                "platform_engineering_chat" => await ExecutePlatformChatAsync(args),
                "get_conversation_history" => await ExecuteGetHistoryAsync(args),
                "get_proactive_suggestions" => await ExecuteGetSuggestionsAsync(args),
                _ => new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new McpContent
                        {
                            Type = "text",
                            Text = $"Unknown tool: {toolCall.Name}"
                        }
                    },
                    IsError = true
                }
            };

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
    /// Execute platform engineering chat through orchestrator
    /// </summary>
    private async Task<McpToolResult> ExecutePlatformChatAsync(Dictionary<string, object?>? arguments)
    {
        try
        {
            var message = arguments?.GetValueOrDefault("message")?.ToString() ?? "";
            var conversationId = arguments?.GetValueOrDefault("conversationId")?.ToString();

            if (string.IsNullOrEmpty(message))
            {
                return new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new McpContent { Type = "text", Text = "Error: message is required" }
                    },
                    IsError = true
                };
            }

            var result = await _chatTool.ProcessRequestAsync(message, conversationId);

            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = result.Response
                    }
                },
                IsError = !result.Success
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing platform chat");
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"Error: {ex.Message}" }
                },
                IsError = true
            };
        }
    }

    /// <summary>
    /// Get conversation history
    /// </summary>
    private async Task<McpToolResult> ExecuteGetHistoryAsync(Dictionary<string, object?>? arguments)
    {
        try
        {
            var conversationId = arguments?.GetValueOrDefault("conversationId")?.ToString();

            if (string.IsNullOrEmpty(conversationId))
            {
                return new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new McpContent { Type = "text", Text = "Error: conversationId is required" }
                    },
                    IsError = true
                };
            }

            var result = await _chatTool.GetConversationHistoryAsync(conversationId);

            var historyText = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = historyText
                    }
                },
                IsError = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation history");
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"Error: {ex.Message}" }
                },
                IsError = true
            };
        }
    }

    /// <summary>
    /// Get proactive suggestions
    /// </summary>
    private async Task<McpToolResult> ExecuteGetSuggestionsAsync(Dictionary<string, object?>? arguments)
    {
        try
        {
            var conversationId = arguments?.GetValueOrDefault("conversationId")?.ToString();

            if (string.IsNullOrEmpty(conversationId))
            {
                return new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new McpContent { Type = "text", Text = "Error: conversationId is required" }
                    },
                    IsError = true
                };
            }

            var result = await _chatTool.GetProactiveSuggestionsAsync(conversationId);

            var suggestionsText = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = suggestionsText
                    }
                },
                IsError = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting proactive suggestions");
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"Error: {ex.Message}" }
                },
                IsError = true
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