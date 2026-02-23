using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Observability;

/// <summary>
/// Structured metrics emission for agent/tool invocations — p50/p95/p99 latency,
/// error rate, throughput, active sessions per FR-076.
/// Thread-safe, lock-free for hot paths.
/// </summary>
public class MetricsService
{
    private readonly ILogger<MetricsService> _logger;
    private readonly ConcurrentDictionary<string, ToolMetrics> _toolMetrics = new();
    private long _totalInvocations;
    private long _totalErrors;
    private long _activeSessions;

    public MetricsService(ILogger<MetricsService> logger)
    {
        _logger = logger;
    }

    /// <summary>Record a tool invocation start. Returns a disposable scope that records completion.</summary>
    public InvocationScope BeginInvocation(string agentId, string toolName)
    {
        Interlocked.Increment(ref _totalInvocations);
        Interlocked.Increment(ref _activeSessions);

        var key = $"{agentId}/{toolName}";
        var metrics = _toolMetrics.GetOrAdd(key, _ => new ToolMetrics(agentId, toolName));
        metrics.IncrementInvocations();

        return new InvocationScope(this, metrics, Stopwatch.StartNew());
    }

    internal void CompleteInvocation(ToolMetrics metrics, long elapsedMs, bool isError)
    {
        Interlocked.Decrement(ref _activeSessions);
        metrics.RecordLatency(elapsedMs);

        if (isError)
        {
            Interlocked.Increment(ref _totalErrors);
            metrics.IncrementErrors();
        }
    }

    /// <summary>Get aggregated metrics for all tools.</summary>
    public PlatformMetricsSnapshot GetSnapshot()
    {
        return new PlatformMetricsSnapshot
        {
            TotalInvocations = Interlocked.Read(ref _totalInvocations),
            TotalErrors = Interlocked.Read(ref _totalErrors),
            ActiveSessions = Interlocked.Read(ref _activeSessions),
            ErrorRate = _totalInvocations > 0
                ? (double)Interlocked.Read(ref _totalErrors) / Interlocked.Read(ref _totalInvocations)
                : 0,
            Tools = _toolMetrics.Values.Select(m => m.GetSnapshot()).ToList(),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Get metrics for a specific tool.</summary>
    public ToolMetricsSnapshot? GetToolMetrics(string agentId, string toolName)
    {
        var key = $"{agentId}/{toolName}";
        return _toolMetrics.TryGetValue(key, out var metrics) ? metrics.GetSnapshot() : null;
    }

    /// <summary>Disposable scope for tracking invocation duration.</summary>
    public class InvocationScope : IDisposable
    {
        private readonly MetricsService _service;
        private readonly ToolMetrics _metrics;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;
        private bool _isError;

        internal InvocationScope(MetricsService service, ToolMetrics metrics, Stopwatch stopwatch)
        {
            _service = service;
            _metrics = metrics;
            _stopwatch = stopwatch;
        }

        /// <summary>Mark this invocation as failed.</summary>
        public void MarkError() => _isError = true;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stopwatch.Stop();
            _service.CompleteInvocation(_metrics, _stopwatch.ElapsedMilliseconds, _isError);
        }
    }
}

/// <summary>Per-tool metrics accumulator. Thread-safe.</summary>
public class ToolMetrics
{
    public string AgentId { get; }
    public string ToolName { get; }

    private long _invocations;
    private long _errors;
    private readonly ConcurrentBag<long> _latencies = [];

    public ToolMetrics(string agentId, string toolName)
    {
        AgentId = agentId;
        ToolName = toolName;
    }

    public void IncrementInvocations() => Interlocked.Increment(ref _invocations);
    public void IncrementErrors() => Interlocked.Increment(ref _errors);
    public void RecordLatency(long ms) => _latencies.Add(ms);

    public ToolMetricsSnapshot GetSnapshot()
    {
        var latencyList = _latencies.ToArray();
        Array.Sort(latencyList);

        return new ToolMetricsSnapshot
        {
            AgentId = AgentId,
            ToolName = ToolName,
            Invocations = Interlocked.Read(ref _invocations),
            Errors = Interlocked.Read(ref _errors),
            ErrorRate = _invocations > 0 ? (double)_errors / _invocations : 0,
            P50LatencyMs = GetPercentile(latencyList, 0.50),
            P95LatencyMs = GetPercentile(latencyList, 0.95),
            P99LatencyMs = GetPercentile(latencyList, 0.99),
            AvgLatencyMs = latencyList.Length > 0 ? latencyList.Average() : 0
        };
    }

    private static double GetPercentile(long[] sorted, double percentile)
    {
        if (sorted.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }
}

/// <summary>Snapshot of all platform metrics at a point in time.</summary>
public class PlatformMetricsSnapshot
{
    public long TotalInvocations { get; set; }
    public long TotalErrors { get; set; }
    public long ActiveSessions { get; set; }
    public double ErrorRate { get; set; }
    public List<ToolMetricsSnapshot> Tools { get; set; } = [];
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>Per-tool metrics snapshot.</summary>
public class ToolMetricsSnapshot
{
    public string AgentId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public long Invocations { get; set; }
    public long Errors { get; set; }
    public double ErrorRate { get; set; }
    public double P50LatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public double AvgLatencyMs { get; set; }
}
