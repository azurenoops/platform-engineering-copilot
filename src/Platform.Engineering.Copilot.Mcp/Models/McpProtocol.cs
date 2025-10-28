using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Mcp.Models;

/// <summary>
/// MCP stdio protocol models (JSONRPC 2.0 over stdin/stdout)
/// These are the wire-format models for MCP protocol communication
/// </summary>

/// <summary>
/// MCP protocol message base class
/// </summary>
public abstract class McpMessage
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
}

/// <summary>
/// MCP request message (JSONRPC 2.0)
/// </summary>
public class McpRequest : McpMessage
{
    [JsonPropertyName("id")]
    public object Id { get; set; } = null!;

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

/// <summary>
/// MCP response message (JSONRPC 2.0)
/// </summary>
public class McpResponse : McpMessage
{
    [JsonPropertyName("id")]
    public object Id { get; set; } = null!;

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

/// <summary>
/// MCP error details
/// </summary>
public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

/// <summary>
/// MCP tool call (from client)
/// </summary>
public class McpToolCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }
}

/// <summary>
/// MCP tool result (to client)
/// </summary>
public class McpToolResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

/// <summary>
/// MCP content (text/image/resource)
/// </summary>
public class McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

/// <summary>
/// MCP tool definition
/// </summary>
public class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public object InputSchema { get; set; } = new { };
}

/// <summary>
/// MCP server capabilities
/// </summary>
public class McpServerCapabilities
{
    [JsonPropertyName("experimental")]
    public object? Experimental { get; set; }

    [JsonPropertyName("logging")]
    public object? Logging { get; set; }

    [JsonPropertyName("prompts")]
    public object? Prompts { get; set; }

    [JsonPropertyName("resources")]
    public object? Resources { get; set; }

    [JsonPropertyName("tools")]
    public ToolsCapabilities? Tools { get; set; }
}

/// <summary>
/// Tools capabilities
/// </summary>
public class ToolsCapabilities
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}
