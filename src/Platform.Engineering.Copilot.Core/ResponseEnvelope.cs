using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Core;

/// <summary>
/// Platform-wide response envelope per FR-079 and compliance-tools.md.
/// All tool responses wrap their data in this schema.
/// </summary>
/// <typeparam name="T">The response data type.</typeparam>
public class ResponseEnvelope<T>
{
    /// <summary>"success" or "error".</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "success";

    /// <summary>The response payload.</summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>Execution metadata.</summary>
    [JsonPropertyName("metadata")]
    public ResponseMetadata Metadata { get; set; } = new();

    /// <summary>Error details (populated only when status = "error").</summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorResponse? Error { get; set; }

    /// <summary>Pagination info (populated only for list responses).</summary>
    [JsonPropertyName("pagination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationInfo? Pagination { get; set; }

    /// <summary>Create a success envelope.</summary>
    public static ResponseEnvelope<T> Success(
        T data,
        string toolName,
        long executionTimeMs,
        PaginationInfo? pagination = null)
    {
        return new ResponseEnvelope<T>
        {
            Status = "success",
            Data = data,
            Metadata = new ResponseMetadata
            {
                ToolName = toolName,
                ExecutionTimeMs = executionTimeMs,
                Timestamp = DateTimeOffset.UtcNow
            },
            Pagination = pagination
        };
    }

    /// <summary>Create an error envelope.</summary>
    public static ResponseEnvelope<T> Fail(
        string toolName,
        string errorCode,
        string errorMessage,
        long executionTimeMs,
        string? details = null)
    {
        return new ResponseEnvelope<T>
        {
            Status = "error",
            Data = default,
            Error = new ErrorResponse
            {
                Code = errorCode,
                Message = errorMessage,
                Details = details
            },
            Metadata = new ResponseMetadata
            {
                ToolName = toolName,
                ExecutionTimeMs = executionTimeMs,
                Timestamp = DateTimeOffset.UtcNow
            }
        };
    }

    /// <summary>Serialize to JSON.</summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, ResponseEnvelopeSerializerContext.Default);
    }
}

/// <summary>
/// Execution metadata included in every response envelope.
/// </summary>
public class ResponseMetadata
{
    /// <summary>Name of the tool that produced this response.</summary>
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Wall-clock execution time in milliseconds.</summary>
    [JsonPropertyName("executionTimeMs")]
    public long ExecutionTimeMs { get; set; }

    /// <summary>UTC timestamp when the response was generated.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Error details for failed tool executions.
/// </summary>
public class ErrorResponse
{
    /// <summary>Machine-readable error code (e.g., "VALIDATION_ERROR", "AUTH_REQUIRED").</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional additional details (e.g., stack trace in dev, validation errors).</summary>
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; set; }
}

/// <summary>
/// Pagination information for list responses.
/// </summary>
public class PaginationInfo
{
    /// <summary>Current page number (1-based).</summary>
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    /// <summary>Number of items per page.</summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 25;

    /// <summary>Total number of items across all pages.</summary>
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    /// <summary>Total number of pages.</summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;

    /// <summary>Whether there is a next page.</summary>
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether there is a previous page.</summary>
    [JsonPropertyName("hasPreviousPage")]
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// JSON serializer context for optimized AOT-compatible serialization.
/// </summary>
internal static class ResponseEnvelopeSerializerContext
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
