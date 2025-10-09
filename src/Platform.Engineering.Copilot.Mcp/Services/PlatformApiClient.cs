using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Net.Http.Json;

namespace Platform.Engineering.Copilot.Mcp.Services;

/// <summary>
/// HTTP client service for communicating with the Platform.API
/// This client provides a thin proxy layer between the MCP server (STDIO protocol)
/// and the Platform.API (HTTP REST), allowing Claude Desktop to access all platform tools.
/// </summary>
public class PlatformApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlatformApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public PlatformApiClient(HttpClient httpClient, ILogger<PlatformApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Get all available tools from Platform.API
    /// Calls GET /api/tools endpoint
    /// </summary>
    public async Task<ToolListResponse> GetToolsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching tools from Platform.API");
            
            var response = await _httpClient.GetAsync("/api/tools");
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ToolListResponse>(responseContent, _jsonOptions);
            
            if (result == null)
            {
                _logger.LogWarning("Platform.API returned null tool list");
                return new ToolListResponse { Tools = new List<ToolInfo>() };
            }

            _logger.LogInformation("Retrieved {Count} tools from Platform.API", result.Tools.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tools from Platform.API");
            throw;
        }
    }

    /// <summary>
    /// Execute a tool via Platform.API
    /// Calls POST /api/tools/execute endpoint
    /// </summary>
    public async Task<ToolExecutionResponse> ExecuteToolAsync(string toolName, Dictionary<string, object?> arguments)
    {
        try
        {
            _logger.LogInformation("Executing tool {ToolName} via Platform.API", toolName);
            
            var request = new ToolExecutionRequest
            {
                ToolName = toolName,
                Parameters = arguments.ToDictionary(
                    kv => kv.Key, 
                    kv => kv.Value ?? (object)string.Empty)
            };

            var response = await _httpClient.PostAsJsonAsync("/api/tools/execute", request, _jsonOptions);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ToolExecutionResponse>(responseContent, _jsonOptions);
            
            if (result == null)
            {
                _logger.LogWarning("Platform.API returned null execution response for tool {ToolName}", toolName);
                return new ToolExecutionResponse
                {
                    Success = false,
                    Error = "No response from Platform.API"
                };
            }

            _logger.LogInformation("Tool {ToolName} execution completed. Success: {Success}", 
                toolName, result.Success);
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error executing tool {ToolName}", toolName);
            return new ToolExecutionResponse
            {
                Success = false,
                Error = $"HTTP error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return new ToolExecutionResponse
            {
                Success = false,
                Error = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Send a chat query to Platform.API
    /// Calls POST /api/chat/query endpoint
    /// </summary>
    public async Task<ChatQueryResponse> SendChatQueryAsync(string query, string[]? context = null)
    {
        try
        {
            _logger.LogInformation("Sending chat query to Platform.API");
            
            var request = new ChatQueryRequest
            {
                Query = query,
                Context = context
            };

            var response = await _httpClient.PostAsJsonAsync("/api/chat/query", request, _jsonOptions);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ChatQueryResponse>(responseContent, _jsonOptions);
            
            if (result == null)
            {
                _logger.LogWarning("Platform.API returned null chat response");
                return new ChatQueryResponse
                {
                    Success = false,
                    Error = "No response from Platform.API"
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending chat query to Platform.API");
            return new ChatQueryResponse
            {
                Success = false,
                Error = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Upload a document to Platform.API
    /// Calls POST /api/documents/upload endpoint
    /// </summary>
    public async Task<string> UploadDocumentAsync(string fileName, byte[] fileContent, string contentType)
    {
        try
        {
            _logger.LogInformation("Uploading document {FileName} to Platform.API", fileName);
            
            using var content = new MultipartFormDataContent();
            using var fileContentWrapper = new ByteArrayContent(fileContent);
            fileContentWrapper.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(fileContentWrapper, "file", fileName);

            var response = await _httpClient.PostAsync("/api/documents/upload", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent, _jsonOptions);
            
            var documentId = result?["documentId"]?.ToString() 
                ?? throw new InvalidOperationException("No document ID returned from Platform.API");
            
            _logger.LogInformation("Document uploaded successfully. ID: {DocumentId}", documentId);
            return documentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document {FileName} to Platform.API", fileName);
            throw;
        }
    }
}

/// <summary>
/// API Models matching Platform.API contracts
/// These DTOs ensure proper serialization when calling Platform.API endpoints
/// </summary>

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
