using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Audits;

namespace Platform.Engineering.Copilot.Mcp.Middleware;

/// <summary>
/// Middleware for automatic audit logging of MCP HTTP requests and responses
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;
    private readonly IAuditLoggingService _auditService;
    private AuditConfiguration? _config;
    private readonly HashSet<string> _sensitiveHeaders = new()
    {
        "Authorization",
        "X-API-Key",
        "Cookie",
        "Set-Cookie"
    };

    public AuditLoggingMiddleware(
        RequestDelegate next,
        ILogger<AuditLoggingMiddleware> logger,
        IAuditLoggingService auditService)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        // Load config lazily in InvokeAsync to avoid deadlocks
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Load config on first request
        _config ??= await _auditService.GetConfigurationAsync();

        // Skip if path is excluded
        if (_config.ExcludedPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var auditEntry = await CreateAuditEntryAsync(context);

        // Capture request body if configured
        string? requestBody = null;
        if (_config.CaptureRequestBody && context.Request.ContentLength > 0)
        {
            requestBody = await CaptureRequestBodyAsync(context.Request);
        }

        // Store original response body stream
        var originalResponseBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            // Process the request
            await _next(context);

            // Capture response
            stopwatch.Stop();
            await CaptureResponseAsync(context, auditEntry, responseBodyStream, stopwatch.Elapsed);

            // Copy the response body back to the original stream
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalResponseBodyStream);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await HandleExceptionAsync(context, ex, auditEntry, stopwatch.Elapsed);
            throw;
        }
        finally
        {
            context.Response.Body = originalResponseBodyStream;
        }

        // Add request/response details
        if (requestBody != null)
        {
            auditEntry.Metadata["RequestBody"] = SanitizeJson(requestBody);
        }

        // Log the audit entry
        await _auditService.LogAsync(auditEntry);
    }

    private async Task<AuditLogEntry> CreateAuditEntryAsync(HttpContext context)
    {
        var user = context.User;
        var request = context.Request;

        var entry = new AuditLogEntry
        {
            EventType = $"API.{request.Method}",
            EventCategory = "WebRequest",
            Severity = DetermineSeverity(request.Method, request.Path),
            ActorId = GetActorId(user),
            ActorName = GetActorName(user),
            ActorType = user.Identity?.IsAuthenticated == true ? "User" : "Anonymous",
            ResourceId = request.Path,
            ResourceType = "API Endpoint",
            ResourceName = $"{request.Method} {request.Path}",
            Action = request.Method,
            Description = $"API call to {request.Method} {request.Path}",
            IpAddress = GetClientIpAddress(context),
            UserAgent = request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown",
            SessionId = context.TraceIdentifier,
            CorrelationId = GetCorrelationId(context),
            Metadata = new Dictionary<string, string>
            {
                ["Method"] = request.Method,
                ["Path"] = request.Path,
                ["QueryString"] = request.QueryString.ToString(),
                ["ContentType"] = request.ContentType ?? "none",
                ["ContentLength"] = request.ContentLength?.ToString() ?? "0",
                ["Scheme"] = request.Scheme,
                ["Host"] = request.Host.ToString()
            },
            Tags = new Dictionary<string, string>
            {
                ["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                ["Service"] = "Platform.MCP"
            }
        };

        // Add headers (excluding sensitive ones)
        foreach (var header in request.Headers.Where(h => !_sensitiveHeaders.Contains(h.Key)))
        {
            entry.Metadata[$"Header.{header.Key}"] = header.Value.FirstOrDefault() ?? string.Empty;
        }

        // Add security context
        if (user.Identity?.IsAuthenticated == true)
        {
            entry.SecurityContext = new SecurityContext
            {
                IsPrivilegedAction = IsPrivilegedAction(request.Method, request.Path),
                AuthenticationMethod = user.Identity.AuthenticationType ?? "Unknown",
                Permissions = user.Claims
                    .Where(c => c.Type == ClaimTypes.Role || c.Type.Contains("permission"))
                    .Select(c => c.Value)
                    .ToList()
            };
        }

        return entry;
    }

    private async Task<string> CaptureRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        
        using var reader = new StreamReader(
            request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        return body;
    }

    private async Task CaptureResponseAsync(
        HttpContext context,
        AuditLogEntry auditEntry,
        MemoryStream responseBodyStream,
        TimeSpan duration)
    {
        auditEntry.Result = GetResultFromStatusCode(context.Response.StatusCode);
        auditEntry.Metadata["StatusCode"] = context.Response.StatusCode.ToString();
        auditEntry.Metadata["Duration"] = duration.TotalMilliseconds.ToString("F2");

        if (auditEntry.Result == "Failed")
        {
            auditEntry.FailureReason = GetFailureReason(context.Response.StatusCode);
        }

        // Capture response body if configured
        if (_config?.CaptureResponseBody == true && responseBodyStream.Length > 0)
        {
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(responseBodyStream, leaveOpen: true);
            var responseBody = await reader.ReadToEndAsync();
            responseBodyStream.Seek(0, SeekOrigin.Begin);

            if (!string.IsNullOrEmpty(responseBody))
            {
                auditEntry.Metadata["ResponseBody"] = SanitizeJson(responseBody);
            }
        }

        // Add response headers
        foreach (var header in context.Response.Headers.Where(h => !_sensitiveHeaders.Contains(h.Key)))
        {
            auditEntry.Metadata[$"Response.Header.{header.Key}"] = header.Value.FirstOrDefault() ?? string.Empty;
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception,
        AuditLogEntry auditEntry,
        TimeSpan duration)
    {
        auditEntry.Result = "Failed";
        auditEntry.FailureReason = exception.Message;
        auditEntry.Severity = AuditSeverity.High;
        auditEntry.Metadata["ExceptionType"] = exception.GetType().Name;
        auditEntry.Metadata["Duration"] = duration.TotalMilliseconds.ToString("F2");
        
        if (_config?.EnableDetailedLogging == true)
        {
            auditEntry.Metadata["StackTrace"] = exception.StackTrace ?? "No stack trace";
        }

        _logger.LogError(exception, "Unhandled exception in API request {Method} {Path}", 
            context.Request.Method, context.Request.Path);

        await Task.CompletedTask;
    }

    private AuditSeverity DetermineSeverity(string method, PathString path)
    {
        // DELETE operations are higher severity
        if (method == "DELETE")
            return AuditSeverity.High;

        // Modifications to critical resources
        if ((method == "PUT" || method == "POST" || method == "PATCH") && 
            (path.Value?.Contains("/admin") == true || 
             path.Value?.Contains("/security") == true ||
             path.Value?.Contains("/compliance") == true))
        {
            return AuditSeverity.Medium;
        }

        // Read operations
        if (method == "GET" || method == "HEAD")
            return AuditSeverity.Informational;

        return AuditSeverity.Low;
    }

    private string GetActorId(ClaimsPrincipal user)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
            return "anonymous";

        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               user.FindFirst("sub")?.Value ??
               user.FindFirst("id")?.Value ??
               "unknown";
    }

    private string GetActorName(ClaimsPrincipal user)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
            return "Anonymous User";

        return user.FindFirst(ClaimTypes.Name)?.Value ??
               user.FindFirst("name")?.Value ??
               user.Identity?.Name ??
               "Unknown User";
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP (when behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null)
        {
            // Handle IPv4-mapped IPv6 addresses
            if (remoteIp.IsIPv4MappedToIPv6)
            {
                return remoteIp.MapToIPv4().ToString();
            }
            return remoteIp.ToString();
        }

        return "unknown";
    }

    private string GetCorrelationId(HttpContext context)
    {
        // Check for existing correlation ID in headers
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ??
                           context.Request.Headers["X-Request-ID"].FirstOrDefault();

        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Items["CorrelationId"] = correlationId;
        }

        return correlationId;
    }

    private bool IsPrivilegedAction(string method, PathString path)
    {
        // Define privileged actions
        var privilegedPaths = new[] { "/admin", "/security", "/compliance", "/users", "/roles", "/permissions" };
        var privilegedMethods = new[] { "DELETE", "PUT", "PATCH" };

        return privilegedMethods.Contains(method) && 
               privilegedPaths.Any(p => path.Value?.Contains(p) == true);
    }

    private string GetResultFromStatusCode(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 300 => "Success",
            >= 300 and < 400 => "Redirect",
            401 or 403 => "Unauthorized",
            >= 400 and < 500 => "ClientError",
            >= 500 => "ServerError",
            _ => "Unknown"
        };
    }

    private string GetFailureReason(int statusCode)
    {
        return statusCode switch
        {
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            409 => "Conflict",
            422 => "Unprocessable Entity",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            504 => "Gateway Timeout",
            _ => $"HTTP {statusCode}"
        };
    }

    private string SanitizeJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        try
        {
            var doc = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
            
            SanitizeJsonElement(doc.RootElement, writer);
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            // If not valid JSON, truncate if too long
            return json.Length > 1000 ? json.Substring(0, 1000) + "..." : json;
        }
    }

    private void SanitizeJsonElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    if (_config?.SensitiveFields.Any(f => property.Name.Contains(f, StringComparison.OrdinalIgnoreCase)) == true)
                    {
                        writer.WriteString(property.Name, "[REDACTED]");
                    }
                    else
                    {
                        writer.WritePropertyName(property.Name);
                        SanitizeJsonElement(property.Value, writer);
                    }
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    SanitizeJsonElement(item, writer);
                }
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}