using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Services.Logging;

/// <summary>
/// Logger wrapper that enriches log entries with correlation context.
/// </summary>
/// <typeparam name="T">The type whose name is used for the logger category name.</typeparam>
public class CorrelatedLogger<T> : ILogger<T>
{
    private readonly ILogger<T> _innerLogger;
    private readonly ICorrelationContext _correlationContext;

    public CorrelatedLogger(ILogger<T> innerLogger, ICorrelationContext correlationContext)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
        _correlationContext = correlationContext ?? throw new ArgumentNullException(nameof(correlationContext));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // Combine correlation context with any provided state
        var correlationScope = _innerLogger.BeginScope(_correlationContext.GetLoggingProperties());
        var stateScope = _innerLogger.BeginScope(state);
        
        return new CompositeDisposable(correlationScope, stateScope);
    }

    public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Always log with correlation context
        using (_innerLogger.BeginScope(_correlationContext.GetLoggingProperties()))
        {
            _innerLogger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    private class CompositeDisposable : IDisposable
    {
        private readonly IDisposable?[] _disposables;

        public CompositeDisposable(params IDisposable?[] disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable?.Dispose();
            }
        }
    }
}

/// <summary>
/// Extension methods for adding correlated logging to DI.
/// </summary>
public static class CorrelatedLoggingExtensions
{
    /// <summary>
    /// Creates a logger with correlation context enrichment.
    /// </summary>
    public static ILogger<T> CreateCorrelatedLogger<T>(
        this ILoggerFactory loggerFactory,
        ICorrelationContext correlationContext)
    {
        var innerLogger = loggerFactory.CreateLogger<T>();
        return new CorrelatedLogger<T>(innerLogger, correlationContext);
    }
}

/// <summary>
/// Structured log message templates for compliance operations.
/// Use these for consistent, queryable log messages.
/// </summary>
public static class LogMessageTemplates
{
    // Assessment operations
    public const string AssessmentStarted = "Starting compliance assessment for subscription {SubscriptionId}, framework {Framework}, correlation {CorrelationId}";
    public const string AssessmentCompleted = "Completed compliance assessment for subscription {SubscriptionId}: {TotalControls} controls, {PassedControls} passed, {FailedControls} failed, score {Score}%, correlation {CorrelationId}";
    public const string AssessmentFailed = "Compliance assessment failed for subscription {SubscriptionId}: {Error}, correlation {CorrelationId}";

    // Control evaluation
    public const string ControlEvaluationStarted = "Evaluating control {ControlId} for subscription {SubscriptionId}, correlation {CorrelationId}";
    public const string ControlEvaluationCompleted = "Control {ControlId} evaluation: {Status}, {FindingsCount} findings, correlation {CorrelationId}";

    // Remediation operations
    public const string RemediationStarted = "Starting remediation {ExecutionId} for finding {FindingId} on resource {ResourceId}, correlation {CorrelationId}";
    public const string RemediationCompleted = "Remediation {ExecutionId} completed: {Status}, duration {DurationMs}ms, correlation {CorrelationId}";
    public const string RemediationFailed = "Remediation {ExecutionId} failed: {Error}, correlation {CorrelationId}";
    public const string RemediationRolledBack = "Remediation {ExecutionId} rolled back: {Reason}, correlation {CorrelationId}";

    // Evidence operations
    public const string EvidenceCollected = "Collected evidence {EvidenceId} for control {ControlId}, type {EvidenceType}, correlation {CorrelationId}";
    public const string EvidenceStored = "Stored evidence package {PackageId}, {ItemCount} items, {SizeBytes} bytes, correlation {CorrelationId}";

    // Scanning operations
    public const string ScanStarted = "Starting {ScanType} scan for {Target}, correlation {CorrelationId}";
    public const string ScanCompleted = "Scan completed for {Target}: {VulnerabilityCount} vulnerabilities found, score {Score}%, correlation {CorrelationId}";

    // Azure operations
    public const string AzureResourceScanned = "Scanned Azure resource {ResourceId}, type {ResourceType}, correlation {CorrelationId}";
    public const string AzureOperationFailed = "Azure operation failed for resource {ResourceId}: {Error}, correlation {CorrelationId}";
}
