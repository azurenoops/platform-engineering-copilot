using System.Diagnostics;

namespace Platform.Engineering.Copilot.Core.Services.Logging;

/// <summary>
/// Provides correlation context for distributed tracing and structured logging.
/// Thread-safe via AsyncLocal storage.
/// </summary>
public interface ICorrelationContext
{
    /// <summary>
    /// Gets the current correlation ID.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Gets the current request ID.
    /// </summary>
    string? RequestId { get; }

    /// <summary>
    /// Gets the current user ID.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the current subscription ID.
    /// </summary>
    string? SubscriptionId { get; }

    /// <summary>
    /// Gets the current session ID.
    /// </summary>
    string? SessionId { get; }

    /// <summary>
    /// Gets additional context properties.
    /// </summary>
    IReadOnlyDictionary<string, string> Properties { get; }

    /// <summary>
    /// Sets a correlation ID (or generates one if not provided).
    /// </summary>
    void SetCorrelationId(string? correlationId = null);

    /// <summary>
    /// Sets the request ID.
    /// </summary>
    void SetRequestId(string requestId);

    /// <summary>
    /// Sets the user ID.
    /// </summary>
    void SetUserId(string userId);

    /// <summary>
    /// Sets the subscription ID for Azure operations.
    /// </summary>
    void SetSubscriptionId(string subscriptionId);

    /// <summary>
    /// Sets the session ID.
    /// </summary>
    void SetSessionId(string sessionId);

    /// <summary>
    /// Sets a custom property.
    /// </summary>
    void SetProperty(string key, string value);

    /// <summary>
    /// Gets all properties as a dictionary for logging enrichment.
    /// </summary>
    Dictionary<string, object> GetLoggingProperties();

    /// <summary>
    /// Creates a scope with the current correlation context.
    /// </summary>
    IDisposable BeginScope();
}

/// <summary>
/// Default implementation of correlation context using AsyncLocal storage.
/// </summary>
public class CorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<CorrelationContextData> _current = new();

    private CorrelationContextData Current => _current.Value ??= new CorrelationContextData();

    public string CorrelationId => Current.CorrelationId;
    public string? RequestId => Current.RequestId;
    public string? UserId => Current.UserId;
    public string? SubscriptionId => Current.SubscriptionId;
    public string? SessionId => Current.SessionId;
    public IReadOnlyDictionary<string, string> Properties => Current.Properties;

    public void SetCorrelationId(string? correlationId = null)
    {
        Current.CorrelationId = string.IsNullOrWhiteSpace(correlationId) 
            ? GenerateCorrelationId() 
            : correlationId;
        
        // Set Activity for distributed tracing
        Activity.Current?.SetTag("correlation.id", Current.CorrelationId);
    }

    public void SetRequestId(string requestId)
    {
        Current.RequestId = requestId;
        Activity.Current?.SetTag("request.id", requestId);
    }

    public void SetUserId(string userId)
    {
        Current.UserId = userId;
        Activity.Current?.SetTag("user.id", userId);
    }

    public void SetSubscriptionId(string subscriptionId)
    {
        Current.SubscriptionId = subscriptionId;
        Activity.Current?.SetTag("azure.subscription.id", subscriptionId);
    }

    public void SetSessionId(string sessionId)
    {
        Current.SessionId = sessionId;
        Activity.Current?.SetTag("session.id", sessionId);
    }

    public void SetProperty(string key, string value)
    {
        Current.Properties[key] = value;
        Activity.Current?.SetTag(key, value);
    }

    public Dictionary<string, object> GetLoggingProperties()
    {
        var props = new Dictionary<string, object>
        {
            ["CorrelationId"] = CorrelationId
        };

        if (!string.IsNullOrEmpty(RequestId))
            props["RequestId"] = RequestId;

        if (!string.IsNullOrEmpty(UserId))
            props["UserId"] = UserId;

        if (!string.IsNullOrEmpty(SubscriptionId))
            props["SubscriptionId"] = SubscriptionId;

        if (!string.IsNullOrEmpty(SessionId))
            props["SessionId"] = SessionId;

        foreach (var prop in Properties)
        {
            props[prop.Key] = prop.Value;
        }

        return props;
    }

    public IDisposable BeginScope()
    {
        return new CorrelationScope(this);
    }

    private static string GenerateCorrelationId()
    {
        // Use Activity ID if available (for distributed tracing compatibility)
        if (Activity.Current?.Id != null)
        {
            return Activity.Current.Id;
        }

        // Generate a unique ID with timestamp prefix for sortability
        return $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
    }

    private class CorrelationContextData
    {
        public string CorrelationId { get; set; } = GenerateCorrelationId();
        public string? RequestId { get; set; }
        public string? UserId { get; set; }
        public string? SubscriptionId { get; set; }
        public string? SessionId { get; set; }
        public Dictionary<string, string> Properties { get; } = new();
    }

    private class CorrelationScope : IDisposable
    {
        private readonly ICorrelationContext _context;
        private readonly Dictionary<string, object> _props;

        public CorrelationScope(ICorrelationContext context)
        {
            _context = context;
            _props = context.GetLoggingProperties();
        }

        public void Dispose()
        {
            // Scope cleanup if needed
        }
    }
}
