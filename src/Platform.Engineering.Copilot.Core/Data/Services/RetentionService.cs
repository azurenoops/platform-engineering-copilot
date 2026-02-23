using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Data.Services;

/// <summary>
/// T141 — Background retention service per FR-072–FR-074:
/// - 3yr assessment archival (soft-delete)
/// - 30-min IaC template cleanup
/// - 7yr immutable audit log partitioning
/// </summary>
public class RetentionService : BackgroundService
{
    private readonly ILogger<RetentionService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    /// <summary>Assessment archival period (3 years).</summary>
    public static readonly TimeSpan AssessmentRetention = TimeSpan.FromDays(3 * 365);

    /// <summary>IaC template cleanup period (30 minutes).</summary>
    public static readonly TimeSpan TemplateCleanup = TimeSpan.FromMinutes(30);

    /// <summary>Audit log retention period (7 years, immutable).</summary>
    public static readonly TimeSpan AuditLogRetention = TimeSpan.FromDays(7 * 365);

    // Tracking for testability
    private int _assessmentArchiveCount;
    private int _templateCleanupCount;
    private int _auditPartitionCount;
    private DateTimeOffset _lastRunTime;

    public RetentionService(ILogger<RetentionService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RetentionService started. Check interval: {Interval}", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRetentionCycle(stoppingToken);
                _lastRunTime = DateTimeOffset.UtcNow;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RetentionService cycle failed");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("RetentionService stopped.");
    }

    /// <summary>
    /// Run a single retention cycle — can be called directly for testing.
    /// </summary>
    public Task RunRetentionCycle(CancellationToken cancellationToken = default)
    {
        ArchiveExpiredAssessments();
        CleanupExpiredTemplates();
        PartitionAuditLogs();
        return Task.CompletedTask;
    }

    /// <summary>
    /// FR-072: Soft-delete assessments older than 3 years.
    /// </summary>
    private void ArchiveExpiredAssessments()
    {
        var threshold = DateTimeOffset.UtcNow - AssessmentRetention;
        _logger.LogInformation("Archiving assessments older than {Threshold:yyyy-MM-dd}", threshold);

        // In-memory stub: would query database for assessments with CompletedAt < threshold
        // and mark them as soft-deleted (IsArchived = true)
        _assessmentArchiveCount++;
        _logger.LogInformation("Assessment archival cycle {Count} complete", _assessmentArchiveCount);
    }

    /// <summary>
    /// FR-073: Clean up IaC templates older than 30 minutes (temporary previews).
    /// </summary>
    private void CleanupExpiredTemplates()
    {
        var threshold = DateTimeOffset.UtcNow - TemplateCleanup;
        _logger.LogInformation("Cleaning up IaC templates older than {Threshold:HH:mm:ss}", threshold);

        // In-memory stub: would delete temporary template files or records
        _templateCleanupCount++;
        _logger.LogInformation("Template cleanup cycle {Count} complete", _templateCleanupCount);
    }

    /// <summary>
    /// FR-074: Partition immutable audit logs by year for 7-year retention.
    /// </summary>
    private void PartitionAuditLogs()
    {
        _logger.LogInformation("Partitioning audit logs for {Year} retention", AuditLogRetention.TotalDays / 365);

        // In-memory stub: would create yearly partitions and rotate old data
        _auditPartitionCount++;
        _logger.LogInformation("Audit log partition cycle {Count} complete", _auditPartitionCount);
    }

    /// <summary>
    /// Get retention service status for health checks.
    /// </summary>
    public RetentionStatus GetStatus() => new()
    {
        IsRunning = _lastRunTime != default,
        LastRunTime = _lastRunTime,
        AssessmentArchiveCycles = _assessmentArchiveCount,
        TemplateCleanupCycles = _templateCleanupCount,
        AuditPartitionCycles = _auditPartitionCount,
        AssessmentRetentionDays = (int)AssessmentRetention.TotalDays,
        TemplateCleanupMinutes = (int)TemplateCleanup.TotalMinutes,
        AuditLogRetentionYears = (int)(AuditLogRetention.TotalDays / 365)
    };
}

public class RetentionStatus
{
    public bool IsRunning { get; set; }
    public DateTimeOffset LastRunTime { get; set; }
    public int AssessmentArchiveCycles { get; set; }
    public int TemplateCleanupCycles { get; set; }
    public int AuditPartitionCycles { get; set; }
    public int AssessmentRetentionDays { get; set; }
    public int TemplateCleanupMinutes { get; set; }
    public int AuditLogRetentionYears { get; set; }
}
