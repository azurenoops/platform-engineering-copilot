using System;
using System.Collections.Generic;

namespace Platform.Engineering.Copilot.Governance.Models;

/// <summary>
/// Represents an audit log entry for compliance and security tracking
/// </summary>
public class AuditLogEntry
{
    public string EntryId { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string EventCategory { get; set; } = string.Empty;
    public AuditSeverity Severity { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty; // User, System, Service
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty; // Success, Failed, Partial
    public string FailureReason { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public Dictionary<string, string> Tags { get; set; } = new();
    public AuditChangeDetails? ChangeDetails { get; set; }
    public ComplianceContext? ComplianceContext { get; set; }
    public SecurityContext? SecurityContext { get; set; }
}

public enum AuditSeverity
{
    Informational,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Details about changes made during an audited action
/// </summary>
public class AuditChangeDetails
{
    public Dictionary<string, object> OldValues { get; set; } = new();
    public Dictionary<string, object> NewValues { get; set; } = new();
    public List<string> ChangedFields { get; set; } = new();
    public string ChangeType { get; set; } = string.Empty; // Create, Update, Delete, Read
    public string ChangeReason { get; set; } = string.Empty;
}

/// <summary>
/// Compliance context for audit entries
/// </summary>
public class ComplianceContext
{
    public List<string> ControlIds { get; set; } = new();
    public string ComplianceFramework { get; set; } = string.Empty;
    public string ComplianceStatus { get; set; } = string.Empty;
    public List<string> Violations { get; set; } = new();
    public bool RequiresReview { get; set; }
}

/// <summary>
/// Security context for audit entries
/// </summary>
public class SecurityContext
{
    public string ThreatLevel { get; set; } = string.Empty;
    public List<string> SecurityPolicies { get; set; } = new();
    public bool IsPrivilegedAction { get; set; }
    public bool RequiresMfa { get; set; }
    public string AuthenticationMethod { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// Configuration for audit logging
/// </summary>
public class AuditConfiguration
{
    public bool EnableDetailedLogging { get; set; } = true;
    public bool CaptureRequestBody { get; set; } = false;
    public bool CaptureResponseBody { get; set; } = false;
    public List<string> SensitiveFields { get; set; } = new();
    public List<string> ExcludedPaths { get; set; } = new();
    public int RetentionDays { get; set; } = 365;
    public bool EnableRealTimeAlerts { get; set; } = true;
    public Dictionary<string, AuditRule> Rules { get; set; } = new();
}

/// <summary>
/// Rule for audit logging
/// </summary>
public class AuditRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EventPattern { get; set; } = string.Empty;
    public AuditSeverity MinimumSeverity { get; set; }
    public List<string> RequiredTags { get; set; } = new();
    public List<string> Actions { get; set; } = new(); // Alert, Archive, Forward
}

/// <summary>
/// Query parameters for searching audit logs
/// </summary>
public class AuditSearchQuery
{
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public List<string> EventTypes { get; set; } = new();
    public List<string> ActorIds { get; set; } = new();
    public List<string> ResourceIds { get; set; } = new();
    public List<AuditSeverity> Severities { get; set; } = new();
    public string? SearchText { get; set; }
    public Dictionary<string, string> TagFilters { get; set; } = new();
    public int PageSize { get; set; } = 100;
    public int PageNumber { get; set; } = 1;
    public string SortBy { get; set; } = "Timestamp";
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Result of an audit log search
/// </summary>
public class AuditSearchResult
{
    public List<AuditLogEntry> Entries { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public Dictionary<string, int> EventTypeCounts { get; set; } = new();
    public Dictionary<AuditSeverity, int> SeverityCounts { get; set; } = new();
}

/// <summary>
/// Audit trail for a specific resource
/// </summary>
public class ResourceAuditTrail
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public List<AuditLogEntry> Timeline { get; set; } = new();
    public DateTimeOffset FirstActivity { get; set; }
    public DateTimeOffset LastActivity { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, int> ActionCounts { get; set; } = new();
    public List<string> UniqueActors { get; set; } = new();
}

/// <summary>
/// Audit report for compliance purposes
/// </summary>
public class AuditReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public Dictionary<string, object> Summary { get; set; } = new();
    public List<AuditInsight> Insights { get; set; } = new();
    public List<AuditAnomaly> Anomalies { get; set; } = new();
    public List<ComplianceViolation> Violations { get; set; } = new();
}

/// <summary>
/// Insight derived from audit analysis
/// </summary>
public class AuditInsight
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Impact { get; set; }
    public List<string> AffectedResources { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Detected anomaly in audit patterns
/// </summary>
public class AuditAnomaly
{
    public string AnomalyId { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public List<string> RelatedEntries { get; set; } = new();
    public Dictionary<string, object> Evidence { get; set; } = new();
}

/// <summary>
/// Compliance violation detected in audit logs
/// </summary>
public class ComplianceViolation
{
    public string ViolationId { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset OccurredAt { get; set; }
    public string ControlId { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
}