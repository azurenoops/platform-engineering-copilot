using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Mcp;

/// <summary>
/// Maps MCP JSON-RPC methods (tools/list, tools/call) to HTTP endpoints with
/// auth metadata (requiresAuthentication, pimTierRequired) per mcp-tools.md.
///
/// While the ModelContextProtocol SDK handles SSE transport via MapMcp(),
/// this bridge provides a REST-like HTTP facade for clients that prefer
/// traditional HTTP POST endpoints over SSE — useful for admin dashboards,
/// health probes, and direct tool invocation from REST clients.
/// </summary>
public static class McpHttpBridge
{
    /// <summary>
    /// Maps MCP bridge endpoints to the application pipeline.
    /// Call from Program.cs: <c>app.MapMcpBridge();</c>
    /// </summary>
    public static IEndpointRouteBuilder MapMcpBridge(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/mcp")
            .WithTags("MCP Bridge");

        group.MapGet("/tools", ListToolsAsync)
            .WithName("ListMcpTools")
            .WithDescription("List all available MCP tools with auth metadata");

        group.MapPost("/tools/{toolName}/call", CallToolAsync)
            .WithName("CallMcpTool")
            .WithDescription("Invoke an MCP tool by name");

        group.MapGet("/tools/{toolName}", GetToolInfoAsync)
            .WithName("GetMcpToolInfo")
            .WithDescription("Get detailed info about a specific MCP tool");

        group.MapGet("/agents", ListAgentsAsync)
            .WithName("ListMcpAgents")
            .WithDescription("List all registered agents with capabilities");

        return endpoints;
    }

    /// <summary>
    /// GET /mcp/tools — List all available tools with auth metadata.
    /// Maps to JSON-RPC tools/list.
    /// </summary>
    private static IResult ListToolsAsync(
        PlatformOrchestrator orchestrator,
        ILogger<PlatformOrchestrator> logger)
    {
        var agents = orchestrator.Agents;
        var tools = new List<McpToolDescriptor>();

        foreach (var agent in agents)
        {
            var toolMetadata = agent.GetToolMetadata();
            foreach (var meta in toolMetadata)
            {
                tools.Add(new McpToolDescriptor
                {
                    Name = meta.Name,
                    Description = meta.Description,
                    AgentId = agent.AgentId,
                    AgentName = agent.AgentName,
                    Parameters = meta.Parameters,
                    Metadata = new McpToolAuthMetadata
                    {
                        RequiresAuthentication = meta.RequiresAuthentication,
                        PimTierRequired = meta.PimTierRequired.ToString()
                    }
                });
            }
        }

        logger.LogInformation("Listed {ToolCount} tools from {AgentCount} agents",
            tools.Count, agents.Count);

        return Results.Json(new McpToolListResponse
        {
            Tools = tools,
            TotalCount = tools.Count
        }, McpBridgeJsonContext.Default.McpToolListResponse);
    }

    /// <summary>
    /// POST /mcp/tools/{toolName}/call — Invoke a tool by name.
    /// Maps to JSON-RPC tools/call.
    /// </summary>
    private static async Task<IResult> CallToolAsync(
        string toolName,
        HttpContext httpContext,
        PlatformOrchestrator orchestrator,
        ILogger<PlatformOrchestrator> logger)
    {
        // Parse request body
        McpToolCallRequest? request;
        try
        {
            request = await httpContext.Request.ReadFromJsonAsync<McpToolCallRequest>();
        }
        catch (JsonException)
        {
            return Results.BadRequest(new McpErrorResponse
            {
                ErrorCode = "VALIDATION_ERROR",
                Message = "Invalid JSON request body.",
                Suggestion = "Ensure the request body is valid JSON with an 'arguments' object."
            });
        }

        var arguments = request?.Arguments ?? new Dictionary<string, object?>();

        // Find the tool across all agents
        var agents = orchestrator.Agents;
        BaseAgent? targetAgent = null;

        foreach (var agent in agents)
        {
            var toolMeta = agent.GetToolMetadata();
            if (toolMeta.Any(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
            {
                targetAgent = agent;
                break;
            }
        }

        if (targetAgent is null)
        {
            logger.LogWarning("Tool '{ToolName}' not found", toolName);
            return Results.NotFound(new McpErrorResponse
            {
                ErrorCode = "TOOL_NOT_FOUND",
                Message = $"Tool '{toolName}' is not registered.",
                Suggestion = "Use GET /mcp/tools to list available tools."
            });
        }

        try
        {
            var result = await targetAgent.ExecuteToolAsync(toolName, arguments);
            return Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new McpErrorResponse
            {
                ErrorCode = "AUTH_REQUIRED",
                Message = ex.Message,
                Suggestion = "Ensure CAC/PIV authentication is active and PIM tier is elevated."
            }, statusCode: 401);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new McpErrorResponse
            {
                ErrorCode = "TOOL_NOT_FOUND",
                Message = ex.Message,
                Suggestion = "Use GET /mcp/tools to list available tools."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing tool '{ToolName}'", toolName);
            return Results.Json(new McpErrorResponse
            {
                ErrorCode = "TOOL_EXECUTION_ERROR",
                Message = $"Error executing tool '{toolName}': {ex.Message}",
                Suggestion = "Check tool parameters and try again."
            }, statusCode: 500);
        }
    }

    /// <summary>
    /// GET /mcp/tools/{toolName} — Get detailed tool info with auth metadata.
    /// </summary>
    private static IResult GetToolInfoAsync(
        string toolName,
        PlatformOrchestrator orchestrator)
    {
        var agents = orchestrator.Agents;

        foreach (var agent in agents)
        {
            var toolMeta = agent.GetToolMetadata();
            var tool = toolMeta.FirstOrDefault(t =>
                t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

            if (tool is not null)
            {
                return Results.Json(new McpToolDescriptor
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    AgentId = agent.AgentId,
                    AgentName = agent.AgentName,
                    Parameters = tool.Parameters,
                    Metadata = new McpToolAuthMetadata
                    {
                        RequiresAuthentication = tool.RequiresAuthentication,
                        PimTierRequired = tool.PimTierRequired.ToString()
                    }
                }, McpBridgeJsonContext.Default.McpToolDescriptor);
            }
        }

        return Results.NotFound(new McpErrorResponse
        {
            ErrorCode = "TOOL_NOT_FOUND",
            Message = $"Tool '{toolName}' is not registered.",
            Suggestion = "Use GET /mcp/tools to list available tools."
        });
    }

    /// <summary>
    /// GET /mcp/agents — List all registered agents.
    /// </summary>
    private static IResult ListAgentsAsync(PlatformOrchestrator orchestrator)
    {
        var agents = orchestrator.Agents;
        var descriptors = agents.Select(a => new McpAgentDescriptor
        {
            AgentId = a.AgentId,
            AgentName = a.AgentName,
            Description = a.Description,
            Keywords = a.Keywords.ToList(),
            RequiredPimTier = a.RequiredPimTier.ToString(),
            ToolCount = a.GetToolMetadata().Count()
        }).ToList();

        return Results.Json(new McpAgentListResponse
        {
            Agents = descriptors,
            TotalCount = descriptors.Count
        }, McpBridgeJsonContext.Default.McpAgentListResponse);
    }
}

// ─── DTOs ───

public class McpToolListResponse
{
    public List<McpToolDescriptor> Tools { get; set; } = [];
    public int TotalCount { get; set; }
}

public class McpToolDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public McpToolAuthMetadata Metadata { get; set; } = new();
}

public class McpToolAuthMetadata
{
    public bool RequiresAuthentication { get; set; }
    public string PimTierRequired { get; set; } = "None";
}

public class McpToolCallRequest
{
    public Dictionary<string, object?> Arguments { get; set; } = [];
}

public class McpAgentListResponse
{
    public List<McpAgentDescriptor> Agents { get; set; } = [];
    public int TotalCount { get; set; }
}

public class McpAgentDescriptor
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = [];
    public string RequiredPimTier { get; set; } = "None";
    public int ToolCount { get; set; }
}

public class McpErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
}

/// <summary>JSON serializer context for MCP bridge DTOs (AOT-compatible).</summary>
[JsonSerializable(typeof(McpToolListResponse))]
[JsonSerializable(typeof(McpToolDescriptor))]
[JsonSerializable(typeof(McpAgentListResponse))]
[JsonSerializable(typeof(McpAgentDescriptor))]
[JsonSerializable(typeof(McpErrorResponse))]
[JsonSerializable(typeof(McpToolCallRequest))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class McpBridgeJsonContext : JsonSerializerContext
{
}
