namespace Platform.Engineering.Copilot.API.Models;

public class ToolExecutionRequest
{
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class ToolExecutionResponse
{
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
    public string[]? FollowUpSuggestions { get; set; }
}

public class ToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> InputSchema { get; set; } = new();
}

public class ToolListResponse
{
    public List<ToolInfo> Tools { get; set; } = new();
}

public class ChatQueryRequest
{
    public string Query { get; set; } = string.Empty;
    public string[]? Context { get; set; }
}

public class ChatQueryResponse
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public string? Error { get; set; }
    public string[]? SuggestedActions { get; set; }
    public ToolInfo[]? RecommendedTools { get; set; }
    public ToolExecutionResponse? ExecutionDetails { get; set; }
}