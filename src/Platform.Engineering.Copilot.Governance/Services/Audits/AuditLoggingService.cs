using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Governance.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Interface for comprehensive audit logging
/// </summary>
public interface IAuditLoggingService
{
    Task<string> LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task<AuditSearchResult> SearchAsync(AuditSearchQuery query, CancellationToken cancellationToken = default);
    Task<ResourceAuditTrail> GetResourceAuditTrailAsync(string resourceId, CancellationToken cancellationToken = default);
    Task<AuditReport> GenerateReportAsync(string reportType, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<List<AuditAnomaly>> DetectAnomaliesAsync(DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<bool> ArchiveLogsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
    Task<AuditConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default);
    Task UpdateConfigurationAsync(AuditConfiguration configuration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Comprehensive audit logging service with compliance and security features
/// </summary>
public class AuditLoggingService : IAuditLoggingService
{
    private readonly ILogger<AuditLoggingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly List<AuditLogEntry> _auditStore; // In-memory for demo, use database in production
    private AuditConfiguration _auditConfig;
    private readonly SemaphoreSlim _storeLock = new(1, 1);

    public AuditLoggingService(
        ILogger<AuditLoggingService> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _auditStore = new List<AuditLogEntry>();
        _auditConfig = LoadConfiguration();
    }

    public async Task<string> LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate entry
            ValidateAuditEntry(entry);

            // Apply security measures
            ApplySecurityMeasures(entry);

            // Check compliance requirements
            await CheckComplianceRequirementsAsync(entry, cancellationToken);

            // Store audit log
            await _storeLock.WaitAsync(cancellationToken);
            try
            {
                _auditStore.Add(entry);
            }
            finally
            {
                _storeLock.Release();
            }

            // Apply rules and trigger actions
            await ApplyAuditRulesAsync(entry, cancellationToken);

            // Log to external systems if configured
            await ForwardToExternalSystemsAsync(entry, cancellationToken);

            _logger.LogInformation("Audit log entry created: {EntryId} - {EventType}", entry.EntryId, entry.EventType);

            return entry.EntryId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit entry for event {EventType}", entry.EventType);
            throw;
        }
    }

    public async Task<AuditSearchResult> SearchAsync(AuditSearchQuery query, CancellationToken cancellationToken = default)
    {
        await _storeLock.WaitAsync(cancellationToken);
        try
        {
            var filteredEntries = _auditStore.AsEnumerable();

            // Apply date filters
            if (query.StartDate.HasValue)
                filteredEntries = filteredEntries.Where(e => e.Timestamp >= query.StartDate.Value);
            
            if (query.EndDate.HasValue)
                filteredEntries = filteredEntries.Where(e => e.Timestamp <= query.EndDate.Value);

            // Apply other filters
            if (query.EventTypes.Any())
                filteredEntries = filteredEntries.Where(e => query.EventTypes.Contains(e.EventType));

            if (query.ActorIds.Any())
                filteredEntries = filteredEntries.Where(e => query.ActorIds.Contains(e.ActorId));

            if (query.ResourceIds.Any())
                filteredEntries = filteredEntries.Where(e => query.ResourceIds.Contains(e.ResourceId));

            if (query.Severities.Any())
                filteredEntries = filteredEntries.Where(e => query.Severities.Contains(e.Severity));

            if (!string.IsNullOrEmpty(query.SearchText))
            {
                var searchLower = query.SearchText.ToLower();
                filteredEntries = filteredEntries.Where(e => 
                    e.Description.ToLower().Contains(searchLower) ||
                    e.Action.ToLower().Contains(searchLower) ||
                    e.EventType.ToLower().Contains(searchLower));
            }

            // Apply tag filters
            if (query.TagFilters.Any())
            {
                filteredEntries = filteredEntries.Where(e => 
                    query.TagFilters.All(tf => e.Tags.ContainsKey(tf.Key) && e.Tags[tf.Key] == tf.Value));
            }

            // Sort
            var sorted = query.SortBy switch
            {
                "EventType" => query.SortDescending 
                    ? filteredEntries.OrderByDescending(e => e.EventType) 
                    : filteredEntries.OrderBy(e => e.EventType),
                "Severity" => query.SortDescending 
                    ? filteredEntries.OrderByDescending(e => e.Severity) 
                    : filteredEntries.OrderBy(e => e.Severity),
                "ActorName" => query.SortDescending 
                    ? filteredEntries.OrderByDescending(e => e.ActorName) 
                    : filteredEntries.OrderBy(e => e.ActorName),
                _ => query.SortDescending 
                    ? filteredEntries.OrderByDescending(e => e.Timestamp) 
                    : filteredEntries.OrderBy(e => e.Timestamp)
            };

            var totalCount = sorted.Count();
            var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

            // Paginate
            var entries = sorted
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            // Calculate counts
            var eventTypeCounts = sorted.GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count());
            
            var severityCounts = sorted.GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            return new AuditSearchResult
            {
                Entries = entries,
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalPages = totalPages,
                EventTypeCounts = eventTypeCounts,
                SeverityCounts = severityCounts
            };
        }
        finally
        {
            _storeLock.Release();
        }
    }

    public async Task<ResourceAuditTrail> GetResourceAuditTrailAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        await _storeLock.WaitAsync(cancellationToken);
        try
        {
            var resourceEntries = _auditStore
                .Where(e => e.ResourceId == resourceId)
                .OrderBy(e => e.Timestamp)
                .ToList();

            if (!resourceEntries.Any())
            {
                return new ResourceAuditTrail
                {
                    ResourceId = resourceId,
                    Timeline = new List<AuditLogEntry>()
                };
            }

            var actionCounts = resourceEntries
                .GroupBy(e => e.Action)
                .ToDictionary(g => g.Key, g => g.Count());

            var uniqueActors = resourceEntries
                .Select(e => e.ActorId)
                .Distinct()
                .ToList();

            return new ResourceAuditTrail
            {
                ResourceId = resourceId,
                ResourceType = resourceEntries.First().ResourceType,
                Timeline = resourceEntries,
                FirstActivity = resourceEntries.First().Timestamp,
                LastActivity = resourceEntries.Last().Timestamp,
                TotalEvents = resourceEntries.Count,
                ActionCounts = actionCounts,
                UniqueActors = uniqueActors
            };
        }
        finally
        {
            _storeLock.Release();
        }
    }

    public async Task<AuditReport> GenerateReportAsync(
        string reportType, 
        DateTimeOffset startDate, 
        DateTimeOffset endDate, 
        CancellationToken cancellationToken = default)
    {
        await _storeLock.WaitAsync(cancellationToken);
        try
        {
            var entries = _auditStore
                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToList();

            var report = new AuditReport
            {
                StartDate = startDate,
                EndDate = endDate,
                ReportType = reportType,
                Summary = new Dictionary<string, object>(),
                Insights = new List<AuditInsight>(),
                Anomalies = new List<AuditAnomaly>(),
                Violations = new List<ComplianceViolation>()
            };

            // Generate summary statistics
            report.Summary["TotalEvents"] = entries.Count;
            report.Summary["UniqueActors"] = entries.Select(e => e.ActorId).Distinct().Count();
            report.Summary["UniqueResources"] = entries.Select(e => e.ResourceId).Distinct().Count();
            report.Summary["FailureRate"] = entries.Count > 0 
                ? (entries.Count(e => e.Result == "Failed") * 100.0 / entries.Count) 
                : 0;

            // Generate insights based on report type
            switch (reportType.ToLower())
            {
                case "security":
                    await GenerateSecurityInsightsAsync(report, entries, cancellationToken);
                    break;
                case "compliance":
                    await GenerateComplianceInsightsAsync(report, entries, cancellationToken);
                    break;
                case "activity":
                    await GenerateActivityInsightsAsync(report, entries, cancellationToken);
                    break;
                default:
                    await GenerateGeneralInsightsAsync(report, entries, cancellationToken);
                    break;
            }

            // Detect anomalies
            var anomalies = await DetectAnomaliesInEntriesAsync(entries, cancellationToken);
            report.Anomalies.AddRange(anomalies);

            // Find compliance violations
            var violations = await FindComplianceViolationsAsync(entries, cancellationToken);
            report.Violations.AddRange(violations);

            return report;
        }
        finally
        {
            _storeLock.Release();
        }
    }

    public async Task<List<AuditAnomaly>> DetectAnomaliesAsync(
        DateTimeOffset startDate, 
        DateTimeOffset endDate, 
        CancellationToken cancellationToken = default)
    {
        await _storeLock.WaitAsync(cancellationToken);
        try
        {
            var entries = _auditStore
                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToList();

            return await DetectAnomaliesInEntriesAsync(entries, cancellationToken);
        }
        finally
        {
            _storeLock.Release();
        }
    }

    public async Task<bool> ArchiveLogsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        await _storeLock.WaitAsync(cancellationToken);
        try
        {
            var logsToArchive = _auditStore
                .Where(e => e.Timestamp < olderThan)
                .ToList();

            if (!logsToArchive.Any())
                return true;

            // In production, archive to blob storage or data warehouse
            // For demo, we'll just remove them from memory
            foreach (var log in logsToArchive)
            {
                _auditStore.Remove(log);
            }

            _logger.LogInformation("Archived {Count} audit logs older than {Date}", 
                logsToArchive.Count, olderThan);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving audit logs");
            return false;
        }
        finally
        {
            _storeLock.Release();
        }
    }

    public Task<AuditConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_auditConfig);
    }

    public Task UpdateConfigurationAsync(AuditConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _auditConfig = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        // Log configuration change
        var configChangeEntry = new AuditLogEntry
        {
            EventType = "ConfigurationChange",
            EventCategory = "Security",
            Severity = AuditSeverity.High,
            ActorId = "System",
            ActorName = "Audit Service",
            ActorType = "System",
            ResourceId = "AuditConfiguration",
            ResourceType = "Configuration",
            ResourceName = "Audit Configuration",
            Action = "Update",
            Description = "Audit configuration updated",
            Result = "Success"
        };

        return LogAsync(configChangeEntry, cancellationToken);
    }

    #region Private Methods

    private AuditConfiguration LoadConfiguration()
    {
        // Load from configuration or use defaults
        return new AuditConfiguration
        {
            EnableDetailedLogging = _configuration.GetValue<bool>("Audit:EnableDetailedLogging", true),
            CaptureRequestBody = _configuration.GetValue<bool>("Audit:CaptureRequestBody", false),
            CaptureResponseBody = _configuration.GetValue<bool>("Audit:CaptureResponseBody", false),
            RetentionDays = _configuration.GetValue<int>("Audit:RetentionDays", 365),
            EnableRealTimeAlerts = _configuration.GetValue<bool>("Audit:EnableRealTimeAlerts", true),
            SensitiveFields = _configuration.GetSection("Audit:SensitiveFields").Get<List<string>>() ?? new List<string> 
            { 
                "password", "secret", "key", "token", "authorization" 
            },
            ExcludedPaths = _configuration.GetSection("Audit:ExcludedPaths").Get<List<string>>() ?? new List<string> 
            { 
                "/health", "/metrics" 
            }
        };
    }

    private void ValidateAuditEntry(AuditLogEntry entry)
    {
        if (string.IsNullOrEmpty(entry.EventType))
            throw new ArgumentException("EventType is required");
        
        if (string.IsNullOrEmpty(entry.ActorId))
            throw new ArgumentException("ActorId is required");
        
        if (string.IsNullOrEmpty(entry.Action))
            throw new ArgumentException("Action is required");
    }

    private void ApplySecurityMeasures(AuditLogEntry entry)
    {
        // Hash sensitive data if configured
        if (_auditConfig.SensitiveFields.Any())
        {
            HashSensitiveData(entry);
        }

        // Add integrity checksum
        entry.Metadata["IntegrityChecksum"] = CalculateIntegrityChecksum(entry);

        // Add timestamp precision
        entry.Metadata["TimestampPrecision"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    }

    private void HashSensitiveData(AuditLogEntry entry)
    {
        // Hash sensitive fields in metadata
        foreach (var field in _auditConfig.SensitiveFields)
        {
            if (entry.Metadata.ContainsKey(field))
            {
                entry.Metadata[field] = HashValue(entry.Metadata[field]);
            }
        }

        // Hash sensitive change details
        if (entry.ChangeDetails != null)
        {
            foreach (var field in _auditConfig.SensitiveFields)
            {
                if (entry.ChangeDetails.OldValues.ContainsKey(field))
                {
                    entry.ChangeDetails.OldValues[field] = "[REDACTED]";
                }
                if (entry.ChangeDetails.NewValues.ContainsKey(field))
                {
                    entry.ChangeDetails.NewValues[field] = "[REDACTED]";
                }
            }
        }
    }

    private string HashValue(string value)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private string CalculateIntegrityChecksum(AuditLogEntry entry)
    {
        var data = JsonSerializer.Serialize(new
        {
            entry.EventType,
            entry.ActorId,
            entry.ResourceId,
            entry.Action,
            entry.Timestamp
        });

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private async Task CheckComplianceRequirementsAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        // Check if this action requires compliance tracking
        if (entry.ComplianceContext != null && entry.ComplianceContext.RequiresReview)
        {
            // In production, trigger compliance workflow
            _logger.LogWarning("Compliance review required for event {EventType} on resource {ResourceId}", 
                entry.EventType, entry.ResourceId);
        }

        // Check for immediate compliance violations
        if (entry.ComplianceContext?.Violations.Any() == true)
        {
            await HandleComplianceViolationsAsync(entry, cancellationToken);
        }
    }

    private async Task HandleComplianceViolationsAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        foreach (var violation in entry.ComplianceContext!.Violations)
        {
            _logger.LogCritical("Compliance violation detected: {Violation} in event {EventType}", 
                violation, entry.EventType);
            
            // In production, trigger alerts and remediation
        }
        
        await Task.CompletedTask;
    }

    private async Task ApplyAuditRulesAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        foreach (var rule in _auditConfig.Rules.Values)
        {
            if (MatchesRule(entry, rule))
            {
                await ExecuteRuleActionsAsync(entry, rule, cancellationToken);
            }
        }
    }

    private bool MatchesRule(AuditLogEntry entry, AuditRule rule)
    {
        // Check severity
        if (entry.Severity < rule.MinimumSeverity)
            return false;

        // Check event pattern (simple contains for demo)
        if (!string.IsNullOrEmpty(rule.EventPattern) && !entry.EventType.Contains(rule.EventPattern))
            return false;

        // Check required tags
        if (rule.RequiredTags.Any() && !rule.RequiredTags.All(t => entry.Tags.ContainsKey(t)))
            return false;

        return true;
    }

    private async Task ExecuteRuleActionsAsync(AuditLogEntry entry, AuditRule rule, CancellationToken cancellationToken)
    {
        foreach (var action in rule.Actions)
        {
            switch (action.ToLower())
            {
                case "alert":
                    await SendAlertAsync(entry, cancellationToken);
                    break;
                case "archive":
                    await ArchiveEntryAsync(entry, cancellationToken);
                    break;
                case "forward":
                    await ForwardToSiemAsync(entry, cancellationToken);
                    break;
            }
        }
    }

    private async Task SendAlertAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        _logger.LogWarning("AUDIT ALERT: {EventType} by {ActorName} on {ResourceId}", 
            entry.EventType, entry.ActorName, entry.ResourceId);
        
        // In production, send to alerting system
        await Task.CompletedTask;
    }

    private async Task ArchiveEntryAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        // In production, archive to long-term storage
        await Task.CompletedTask;
    }

    private async Task ForwardToSiemAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        // In production, forward to SIEM system
        await Task.CompletedTask;
    }

    private async Task ForwardToExternalSystemsAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        // Forward to Azure Monitor, Splunk, etc. based on configuration
        await Task.CompletedTask;
    }

    private async Task<List<AuditAnomaly>> DetectAnomaliesInEntriesAsync(List<AuditLogEntry> entries, CancellationToken cancellationToken)
    {
        var anomalies = new List<AuditAnomaly>();

        // Detect unusual activity patterns
        var activityByActor = entries.GroupBy(e => e.ActorId);
        
        foreach (var actorGroup in activityByActor)
        {
            var actorEntries = actorGroup.OrderBy(e => e.Timestamp).ToList();
            
            // Detect rapid-fire actions
            for (int i = 1; i < actorEntries.Count; i++)
            {
                var timeDiff = actorEntries[i].Timestamp - actorEntries[i-1].Timestamp;
                if (timeDiff.TotalSeconds < 1 && actorEntries.Count > 10)
                {
                    anomalies.Add(new AuditAnomaly
                    {
                        Type = "RapidFireActions",
                        Description = $"Actor {actorGroup.Key} performed {actorEntries.Count} actions in rapid succession",
                        ConfidenceScore = 0.8,
                        RelatedEntries = actorEntries.Select(e => e.EntryId).ToList()
                    });
                    break;
                }
            }

            // Detect unusual access times
            var nightTimeAccess = actorEntries.Where(e => 
                e.Timestamp.Hour < 6 || e.Timestamp.Hour > 22).ToList();
            
            if (nightTimeAccess.Count > actorEntries.Count * 0.5)
            {
                anomalies.Add(new AuditAnomaly
                {
                    Type = "UnusualAccessTime",
                    Description = $"Actor {actorGroup.Key} has {nightTimeAccess.Count} actions during non-business hours",
                    ConfidenceScore = 0.6,
                    RelatedEntries = nightTimeAccess.Select(e => e.EntryId).ToList()
                });
            }
        }

        // Detect privilege escalation patterns
        var privilegeChanges = entries.Where(e => 
            e.EventType.Contains("RoleAssignment") || 
            e.EventType.Contains("Permission")).ToList();
        
        if (privilegeChanges.Count > 10)
        {
            anomalies.Add(new AuditAnomaly
            {
                Type = "PotentialPrivilegeEscalation",
                Description = $"Detected {privilegeChanges.Count} privilege-related changes",
                ConfidenceScore = 0.7,
                RelatedEntries = privilegeChanges.Select(e => e.EntryId).ToList()
            });
        }

        await Task.CompletedTask;
        return anomalies;
    }

    private async Task<List<ComplianceViolation>> FindComplianceViolationsAsync(List<AuditLogEntry> entries, CancellationToken cancellationToken)
    {
        var violations = new List<ComplianceViolation>();

        // Check for unauthorized access attempts
        var unauthorizedAccess = entries.Where(e => 
            e.Result == "Failed" && 
            (e.FailureReason.Contains("Unauthorized") || e.FailureReason.Contains("Forbidden"))).ToList();

        foreach (var entry in unauthorizedAccess)
        {
            violations.Add(new ComplianceViolation
            {
                OccurredAt = entry.Timestamp,
                ControlId = "AC-2",
                PolicyName = "Access Control",
                Description = "Unauthorized access attempt detected",
                Severity = "High",
                ActorId = entry.ActorId,
                ResourceId = entry.ResourceId,
                Context = new Dictionary<string, object>
                {
                    ["EventType"] = entry.EventType,
                    ["FailureReason"] = entry.FailureReason
                }
            });
        }

        // Check for data exfiltration patterns
        var dataExports = entries.Where(e => 
            e.EventType.Contains("Export") || 
            e.EventType.Contains("Download") ||
            e.Action.Contains("Export")).ToList();

        if (dataExports.Count > 50)
        {
            violations.Add(new ComplianceViolation
            {
                OccurredAt = DateTimeOffset.UtcNow,
                ControlId = "AC-4",
                PolicyName = "Information Flow Enforcement",
                Description = "Potential data exfiltration pattern detected",
                Severity = "Critical",
                ActorId = string.Join(", ", dataExports.Select(e => e.ActorId).Distinct()),
                ResourceId = "Multiple",
                Context = new Dictionary<string, object>
                {
                    ["ExportCount"] = dataExports.Count,
                    ["TimeRange"] = $"{dataExports.Min(e => e.Timestamp)} - {dataExports.Max(e => e.Timestamp)}"
                }
            });
        }

        await Task.CompletedTask;
        return violations;
    }

    private async Task GenerateSecurityInsightsAsync(AuditReport report, List<AuditLogEntry> entries, CancellationToken cancellationToken)
    {
        // Failed authentication attempts
        var failedAuth = entries.Where(e => 
            e.EventType.Contains("Authentication") && e.Result == "Failed").ToList();
        
        if (failedAuth.Any())
        {
            report.Insights.Add(new AuditInsight
            {
                Type = "SecurityRisk",
                Description = $"Detected {failedAuth.Count} failed authentication attempts",
                Impact = failedAuth.Count > 10 ? 0.8 : 0.4,
                AffectedResources = failedAuth.Select(e => e.ResourceId).Distinct().ToList()
            });
        }

        // Privilege escalations
        var privEsc = entries.Where(e => 
            e.SecurityContext?.IsPrivilegedAction == true).ToList();
        
        if (privEsc.Count > 5)
        {
            report.Insights.Add(new AuditInsight
            {
                Type = "PrivilegeActivity",
                Description = $"High volume of privileged actions: {privEsc.Count}",
                Impact = 0.7,
                AffectedResources = privEsc.Select(e => e.ResourceId).Distinct().ToList()
            });
        }

        await Task.CompletedTask;
    }

    private async Task GenerateComplianceInsightsAsync(AuditReport report, List<AuditLogEntry> entries, CancellationToken cancellationToken)
    {
        // Compliance violations
        var violations = entries.Where(e => 
            e.ComplianceContext?.Violations.Any() == true).ToList();
        
        if (violations.Any())
        {
            var violationCount = violations.SelectMany(e => e.ComplianceContext!.Violations).Count();
            report.Insights.Add(new AuditInsight
            {
                Type = "ComplianceIssue",
                Description = $"Found {violationCount} compliance violations across {violations.Count} events",
                Impact = 0.9,
                AffectedResources = violations.Select(e => e.ResourceId).Distinct().ToList()
            });
        }

        // Control coverage
        var controlsCovered = entries
            .Where(e => e.ComplianceContext?.ControlIds.Any() == true)
            .SelectMany(e => e.ComplianceContext!.ControlIds)
            .Distinct()
            .Count();

        report.Insights.Add(new AuditInsight
        {
            Type = "ControlCoverage",
            Description = $"Audit logs cover {controlsCovered} unique compliance controls",
            Impact = controlsCovered > 50 ? 0.2 : 0.6,
            Data = new Dictionary<string, object> { ["ControlCount"] = controlsCovered }
        });

        await Task.CompletedTask;
    }

    private async Task GenerateActivityInsightsAsync(AuditReport report, List<AuditLogEntry> entries, CancellationToken cancellationToken)
    {
        // Peak activity times
        var hourlyActivity = entries.GroupBy(e => e.Timestamp.Hour)
            .OrderByDescending(g => g.Count())
            .First();

        report.Insights.Add(new AuditInsight
        {
            Type = "ActivityPattern",
            Description = $"Peak activity occurs at {hourlyActivity.Key}:00 with {hourlyActivity.Count()} events",
            Impact = 0.3,
            Data = new Dictionary<string, object> 
            { 
                ["PeakHour"] = hourlyActivity.Key,
                ["EventCount"] = hourlyActivity.Count()
            }
        });

        // Most active resources
        var topResources = entries.GroupBy(e => e.ResourceId)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new { ResourceId = g.Key, Count = g.Count() })
            .ToList();

        report.Insights.Add(new AuditInsight
        {
            Type = "ResourceActivity",
            Description = $"Top 5 most active resources account for {topResources.Sum(r => r.Count)} events",
            Impact = 0.4,
            AffectedResources = topResources.Select(r => r.ResourceId).ToList()
        });

        await Task.CompletedTask;
    }

    private async Task GenerateGeneralInsightsAsync(AuditReport report, List<AuditLogEntry> entries, CancellationToken cancellationToken)
    {
        await GenerateActivityInsightsAsync(report, entries, cancellationToken);
        await GenerateSecurityInsightsAsync(report, entries, cancellationToken);
        await GenerateComplianceInsightsAsync(report, entries, cancellationToken);
    }

    #endregion
}