using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Represents an MCP tool that can be called by the client
/// </summary>
public record McpTool
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonDocument Schema { get; init; }
}

/// <summary>
/// Represents a request to call an MCP tool
/// </summary>
public record McpToolCall
{
    public required string Name { get; init; }
    public required Dictionary<string, object?> Arguments { get; init; }
    public string? RequestId { get; init; }
}

/// <summary>
/// Represents the result of an MCP tool call
/// </summary>
public record McpToolResult
{
    public required string ToolName { get; init; }
    public required bool IsSuccess { get; init; }
    public object? Content { get; init; }
    public string? Error { get; init; }
    public string? RequestId { get; init; }
}

/// <summary>
/// Represents an MCP resource that can be accessed
/// </summary>
public record McpResource
{
    public required string Uri { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? MimeType { get; init; }
}

/// <summary>
/// Represents the content of an MCP resource
/// </summary>
public record McpResourceContent
{
    public required string Uri { get; init; }
    public required string MimeType { get; init; }
    public required object Content { get; init; }
}

/// <summary>
/// Standard MCP server capabilities
/// </summary>
public record McpServerCapabilities
{
    public bool Tools { get; init; } = true;
    public bool Resources { get; init; } = true;
    public bool Prompts { get; init; } = false;
    public bool Logging { get; init; } = true;
}

/// <summary>
/// MCP server information
/// </summary>
public record McpServerInfo
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required McpServerCapabilities Capabilities { get; init; }
}