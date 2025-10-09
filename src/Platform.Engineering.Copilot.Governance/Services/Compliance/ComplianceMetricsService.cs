using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Metrics and telemetry for ATO compliance services
/// </summary>
public class ComplianceMetricsService : IDisposable
{
    private readonly ILogger<ComplianceMetricsService> _logger;
    private readonly Meter _meter;
    private readonly Counter<int> _scanRequestCounter;
    private readonly Counter<int> _findingCounter;
    private readonly Counter<int> _remediationCounter;
    private readonly Counter<int> _nistApiCallCounter;
    private readonly Histogram<double> _scanDurationHistogram;
    private readonly Histogram<double> _nistApiDurationHistogram;
    private readonly UpDownCounter<int> _activeScanCounter;

    public ComplianceMetricsService(ILogger<ComplianceMetricsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _meter = new Meter("Platform.Engineering.Copilot.Governance.Compliance", "1.0.0");
        
        // Counters
        _scanRequestCounter = _meter.CreateCounter<int>(
            "compliance_scans_total",
            description: "Total number of compliance scans requested");
            
        _findingCounter = _meter.CreateCounter<int>(
            "compliance_findings_total", 
            description: "Total number of compliance findings detected");
            
        _remediationCounter = _meter.CreateCounter<int>(
            "compliance_remediations_total",
            description: "Total number of compliance remediations attempted");
            
        _nistApiCallCounter = _meter.CreateCounter<int>(
            "nist_api_calls_total",
            description: "Total number of NIST API calls made");
        
        // Histograms
        _scanDurationHistogram = _meter.CreateHistogram<double>(
            "compliance_scan_duration_seconds",
            "seconds",
            "Duration of compliance scans");
            
        _nistApiDurationHistogram = _meter.CreateHistogram<double>(
            "nist_api_call_duration_seconds",
            "seconds", 
            "Duration of NIST API calls");
        
        // Up/Down Counters
        _activeScanCounter = _meter.CreateUpDownCounter<int>(
            "compliance_active_scans",
            description: "Number of currently active compliance scans");
    }

    public void RecordScanRequest(string subscriptionId, string resourceGroup, List<string> frameworks)
    {
        var tags = new TagList
        {
            { "subscription_id", subscriptionId },
            { "resource_group", resourceGroup },
            { "frameworks", string.Join(",", frameworks) }
        };
        
        _scanRequestCounter.Add(1, tags);
        _activeScanCounter.Add(1, tags);
        
        _logger.LogDebug("Recorded scan request for {ResourceGroup} with frameworks: {Frameworks}",
            resourceGroup, string.Join(", ", frameworks));
    }

    public void RecordScanCompletion(string subscriptionId, string resourceGroup, TimeSpan duration, 
        int findingCount, bool success)
    {
        var tags = new TagList
        {
            { "subscription_id", subscriptionId },
            { "resource_group", resourceGroup },
            { "success", success.ToString().ToLowerInvariant() }
        };
        
        _scanDurationHistogram.Record(duration.TotalSeconds, tags);
        _activeScanCounter.Add(-1, tags);
        
        if (findingCount > 0)
        {
            _findingCounter.Add(findingCount, tags);
        }
        
        _logger.LogInformation("Recorded scan completion for {ResourceGroup}: {Duration}ms, {FindingCount} findings, Success: {Success}",
            resourceGroup, duration.TotalMilliseconds, findingCount, success);
    }

    public void RecordFindings(List<AtoFinding> findings)
    {
        foreach (var finding in findings)
        {
            var tags = new TagList
            {
                { "resource_type", finding.ResourceType ?? "unknown" },
                { "finding_type", finding.FindingType.ToString() },
                { "severity", finding.Severity.ToString() },
                { "is_remediable", finding.IsRemediable.ToString().ToLowerInvariant() }
            };
            
            _findingCounter.Add(1, tags);
        }
        
        _logger.LogDebug("Recorded {FindingCount} findings", findings.Count);
    }

    public void RecordRemediation(string findingId, string remediationType, bool success, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "remediation_type", remediationType },
            { "success", success.ToString().ToLowerInvariant() }
        };
        
        _remediationCounter.Add(1, tags);
        
        _logger.LogInformation("Recorded remediation for {FindingId}: Type={RemediationType}, Success={Success}, Duration={Duration}ms",
            findingId, remediationType, success, duration.TotalMilliseconds);
    }

    public void RecordNistApiCall(string operation, bool success, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "success", success.ToString().ToLowerInvariant() }
        };
        
        _nistApiCallCounter.Add(1, tags);
        _nistApiDurationHistogram.Record(duration.TotalSeconds, tags);
        
        _logger.LogDebug("Recorded NIST API call: {Operation}, Success: {Success}, Duration: {Duration}ms",
            operation, success, duration.TotalMilliseconds);
    }

    public void RecordNistControlValidation(string controlId, bool isValid)
    {
        var tags = new TagList
        {
            { "control_id", controlId },
            { "is_valid", isValid.ToString().ToLowerInvariant() }
        };
        
        _logger.LogDebug("Recorded NIST control validation: {ControlId} = {IsValid}", controlId, isValid);
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}

/// <summary>
/// Enhanced logging extensions for structured compliance logging
/// </summary>
public static class ComplianceLoggingExtensions
{
    private static readonly Action<ILogger, string, string, int, Exception?> _scanStarted =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Information,
            new EventId(1001, "ScanStarted"),
            "ATO compliance scan started for resource group {ResourceGroupName} in subscription {SubscriptionId} with {FrameworkCount} frameworks");

    private static readonly Action<ILogger, string, string, int, double, Exception?> _scanCompleted =
        LoggerMessage.Define<string, string, int, double>(
            LogLevel.Information,
            new EventId(1002, "ScanCompleted"),
            "ATO compliance scan completed for resource group {ResourceGroupName} in subscription {SubscriptionId}. Found {FindingCount} findings in {DurationMs}ms");

    private static readonly Action<ILogger, string, string, string, Exception?> _scanFailed =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(1003, "ScanFailed"),
            "ATO compliance scan failed for resource group {ResourceGroupName} in subscription {SubscriptionId}: {ErrorMessage}");

    private static readonly Action<ILogger, string, string, string, Exception?> _findingDetected =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(1004, "FindingDetected"),
            "Compliance finding detected: {FindingType} severity {Severity} for resource {ResourceId}");

    private static readonly Action<ILogger, string, string, bool, Exception?> _remediationAttempted =
        LoggerMessage.Define<string, string, bool>(
            LogLevel.Information,
            new EventId(1005, "RemediationAttempted"),
            "Remediation attempted for finding {FindingId} using {RemediationType}, Success: {Success}");

    private static readonly Action<ILogger, string, string, double, Exception?> _nistApiCalled =
        LoggerMessage.Define<string, string, double>(
            LogLevel.Debug,
            new EventId(1006, "NistApiCalled"),
            "NIST API called: {Operation} for {ControlId} completed in {DurationMs}ms");

    public static void LogScanStarted(this ILogger logger, string resourceGroupName, string subscriptionId, int frameworkCount)
        => _scanStarted(logger, resourceGroupName, subscriptionId, frameworkCount, null);

    public static void LogScanCompleted(this ILogger logger, string resourceGroupName, string subscriptionId, 
        int findingCount, TimeSpan duration)
        => _scanCompleted(logger, resourceGroupName, subscriptionId, findingCount, duration.TotalMilliseconds, null);

    public static void LogScanFailed(this ILogger logger, string resourceGroupName, string subscriptionId, 
        string errorMessage, Exception? exception = null)
        => _scanFailed(logger, resourceGroupName, subscriptionId, errorMessage, exception);

    public static void LogFindingDetected(this ILogger logger, AtoFinding finding)
        => _findingDetected(logger, finding.FindingType.ToString(), finding.Severity.ToString(), 
            finding.ResourceId ?? "unknown", null);

    public static void LogRemediationAttempted(this ILogger logger, string findingId, string remediationType, bool success)
        => _remediationAttempted(logger, findingId, remediationType, success, null);

    public static void LogNistApiCalled(this ILogger logger, string operation, string controlId, TimeSpan duration)
        => _nistApiCalled(logger, operation, controlId, duration.TotalMilliseconds, null);
}

/// <summary>
/// Activity source for distributed tracing
/// </summary>
public static class ComplianceActivitySource
{
    private static readonly ActivitySource _activitySource = new("Platform.Engineering.Copilot.Governance.Compliance", "1.0.0");

    public static ActivitySource Instance => _activitySource;

    public static Activity? StartScanActivity(string resourceGroupName, string subscriptionId)
    {
        var activity = _activitySource.StartActivity("compliance.scan");
        activity?.SetTag("resource.group", resourceGroupName);
        activity?.SetTag("subscription.id", subscriptionId);
        return activity;
    }

    public static Activity? StartNistApiActivity(string operation, string? controlId = null)
    {
        var activity = _activitySource.StartActivity("nist.api.call");
        activity?.SetTag("nist.operation", operation);
        if (!string.IsNullOrEmpty(controlId))
            activity?.SetTag("nist.control.id", controlId);
        return activity;
    }

    public static Activity? StartRemediationActivity(string findingId, string remediationType)
    {
        var activity = _activitySource.StartActivity("compliance.remediation");
        activity?.SetTag("finding.id", findingId);
        activity?.SetTag("remediation.type", remediationType);
        return activity;
    }
}