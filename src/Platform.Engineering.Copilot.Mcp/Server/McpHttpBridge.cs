using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Mcp.Tools;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Mcp.Server;

/// <summary>
/// HTTP bridge for MCP server - exposes MCP tools via REST endpoints
/// Provides a generic HTTP interface that any web application can use
/// 
/// Dual-mode operation:
/// 1. Stdio mode: GitHub Copilot/Claude spawn this as subprocess, use stdin/stdout
/// 2. HTTP mode: Any web app calls HTTP endpoints
/// </summary>
public class McpHttpBridge
{
    private readonly PlatformEngineeringCopilotTools _chatTool;
    private readonly ILogger<McpHttpBridge> _logger;

    public McpHttpBridge(PlatformEngineeringCopilotTools chatTool, ILogger<McpHttpBridge> logger)
    {
        _chatTool = chatTool;
        _logger = logger;
    }

    /// <summary>
    /// Configure HTTP endpoints for MCP tools using WebApplication minimal APIs
    /// </summary>
    public void MapHttpEndpoints(WebApplication app)
    {
        // Process chat request through multi-agent orchestrator
        app.MapPost("/mcp/chat", async (HttpContext context) =>
        {
            try
            {
                var requestBody = await JsonSerializer.DeserializeAsync<ChatRequest>(
                    context.Request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (requestBody == null || string.IsNullOrEmpty(requestBody.Message))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Message is required" });
                    return;
                }

                _logger.LogInformation("HTTP: Processing chat request for conversation: {ConversationId}", 
                    requestBody.ConversationId ?? "new");

                var result = await _chatTool.ProcessRequestAsync(
                    requestBody.Message,
                    requestBody.ConversationId,
                    requestBody.Context,
                    context.RequestAborted);

                await context.Response.WriteAsJsonAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        });

        // Health check
        app.MapGet("/health", () => Results.Ok(new 
        { 
            status = "healthy", 
            mode = "dual (http+stdio)",
            server = "Platform Engineering Copilot MCP",
            version = "1.0.0"
        }));

        _logger.LogInformation("✅ MCP HTTP Bridge endpoints configured");
        _logger.LogInformation("   POST   /mcp/chat - Process chat request");
        _logger.LogInformation("   GET    /health - Health check");
    }
}

// Generic request model
public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}
