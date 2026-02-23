using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Platform.Engineering.Copilot.Core.Observability;

/// <summary>
/// Middleware that assigns and propagates a Guid correlation ID through all
/// agent calls. The ID is:
/// - Read from X-Correlation-ID header if present (trusted upstream)
/// - Generated as a new Guid if absent
/// - Set on the response header
/// - Made available via <see cref="CorrelationIdAccessor"/> for injection into logs
/// Per FR-077.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        if (context.Request.Headers.TryGetValue(HeaderName, out var existingId)
            && !StringValues.IsNullOrEmpty(existingId)
            && Guid.TryParse(existingId!, out _))
        {
            correlationId = existingId!;
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
        }

        // Store in HttpContext.Items for downstream access
        context.Items[HeaderName] = correlationId;

        // Set response header
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Enrich Serilog log context with correlation ID
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }
}

/// <summary>
/// Accessor to retrieve the current correlation ID from HttpContext.
/// Inject via DI as scoped.
/// </summary>
public class CorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Get the current correlation ID, or generate one if not in HTTP context.</summary>
    public string GetCorrelationId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var id) == true
            && id is string correlationId)
        {
            return correlationId;
        }

        return Guid.NewGuid().ToString();
    }
}
