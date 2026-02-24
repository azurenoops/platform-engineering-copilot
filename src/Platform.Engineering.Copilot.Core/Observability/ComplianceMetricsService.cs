using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Platform.Engineering.Copilot.Core.Observability;

/// <summary>
/// Provides distributed tracing (via ActivitySource) and metrics (via Meter)
/// for NIST compliance catalog operations (FR-034, FR-035, FR-036, US7).
/// 
/// ActivitySource creates spans for catalog fetch, cache lookup, and query operations.
/// Meter exposes Counter&lt;long&gt; for operation counts and Histogram&lt;double&gt; for durations.
/// Compatible with OpenTelemetry exporters for Prometheus, Jaeger, and Azure Monitor.
/// </summary>
public class ComplianceMetricsService : IDisposable
{
    /// <summary>
    /// Source name following OpenTelemetry naming convention.
    /// </summary>
    public const string ActivitySourceName = "Platform.Engineering.Copilot.Compliance";

    /// <summary>
    /// Meter name following OpenTelemetry naming convention.
    /// </summary>
    public const string MeterName = "Platform.Engineering.Copilot.Compliance";

    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    /// <summary>Counter for catalog fetch operations (GitHub, fallback, embedded).</summary>
    public Counter<long> CatalogFetchCount { get; }

    /// <summary>Counter for cache hit operations.</summary>
    public Counter<long> CacheHitCount { get; }

    /// <summary>Counter for cache miss operations.</summary>
    public Counter<long> CacheMissCount { get; }

    /// <summary>Counter for control query operations.</summary>
    public Counter<long> ControlQueryCount { get; }

    /// <summary>Counter for operation errors.</summary>
    public Counter<long> ErrorCount { get; }

    /// <summary>Histogram for catalog fetch duration in milliseconds.</summary>
    public Histogram<double> CatalogFetchDuration { get; }

    /// <summary>Histogram for control query duration in milliseconds.</summary>
    public Histogram<double> ControlQueryDuration { get; }

    public ComplianceMetricsService()
    {
        _activitySource = new ActivitySource(ActivitySourceName);
        _meter = new Meter(MeterName);

        CatalogFetchCount = _meter.CreateCounter<long>(
            "compliance.catalog.fetch.count",
            description: "Number of catalog fetch operations");

        CacheHitCount = _meter.CreateCounter<long>(
            "compliance.cache.hit.count",
            description: "Number of cache hits during catalog lookups");

        CacheMissCount = _meter.CreateCounter<long>(
            "compliance.cache.miss.count",
            description: "Number of cache misses during catalog lookups");

        ControlQueryCount = _meter.CreateCounter<long>(
            "compliance.control.query.count",
            description: "Number of control query operations");

        ErrorCount = _meter.CreateCounter<long>(
            "compliance.error.count",
            description: "Number of errors during compliance operations");

        CatalogFetchDuration = _meter.CreateHistogram<double>(
            "compliance.catalog.fetch.duration",
            unit: "ms",
            description: "Duration of catalog fetch operations in milliseconds");

        ControlQueryDuration = _meter.CreateHistogram<double>(
            "compliance.control.query.duration",
            unit: "ms",
            description: "Duration of control query operations in milliseconds");
    }

    /// <summary>
    /// Starts an Activity span for a catalog fetch operation.
    /// </summary>
    public Activity? StartCatalogFetchActivity(string source)
    {
        var activity = _activitySource.StartActivity("CatalogFetch", ActivityKind.Client);
        activity?.SetTag("compliance.source", source);
        return activity;
    }

    /// <summary>
    /// Starts an Activity span for a control query operation.
    /// </summary>
    public Activity? StartControlQueryActivity(string operationName)
    {
        var activity = _activitySource.StartActivity($"ControlQuery.{operationName}", ActivityKind.Internal);
        activity?.SetTag("compliance.operation", operationName);
        return activity;
    }

    /// <summary>
    /// Records the result of a catalog fetch operation on the activity span.
    /// </summary>
    public static void RecordFetchResult(Activity? activity, bool success, int controlCount, bool cacheHit, bool fallbackUsed)
    {
        if (activity == null) return;

        activity.SetTag("cache.hit", cacheHit);
        activity.SetTag("success", success);
        activity.SetTag("control.count", controlCount);
        activity.SetTag("fallback.used", fallbackUsed);

        if (!success)
        {
            activity.SetStatus(ActivityStatusCode.Error, "Catalog fetch failed");
        }
    }

    /// <summary>
    /// Records an error on the activity span.
    /// </summary>
    public static void RecordError(Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetTag("error", true);
        activity.SetTag("error.type", exception.GetType().Name);
        activity.SetTag("error.message", exception.Message);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }
}
