namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Azure Resource Monitor Health-related models for comprehensive resource health monitoring
/// </summary>


public class ResourceHealthSummary
{
    public string SubscriptionId { get; set; } = string.Empty;
    public int TotalResources { get; set; }
    public int HealthyResources { get; set; }
    public int UnhealthyResources { get; set; }
    public int UnknownResources { get; set; }
    public int DegradedResources { get; set; }
    public double OverallHealthPercentage { get; set; }
    public DateTime LastUpdated { get; set; }
    public int CriticalIssues { get; set; }
    public List<HealthTrend> HealthTrends { get; set; } = new();
}

public class ResourceHealthStatus
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public ResourceAvailabilityState AvailabilityState { get; set; }
    public string? Summary { get; set; }
    public string? DetailedStatus { get; set; }
    public DateTime OccurredDateTime { get; set; }
    public string? ReasonType { get; set; }
    public DateTime? RootCauseAttributionTime { get; set; }
    public DateTime? ResolutionETA { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class HealthTrend
{
    public DateTime Date { get; set; }
    public int HealthyResources { get; set; }
    public int UnhealthyResources { get; set; }
    public int TotalResources { get; set; }
    public double HealthPercentage { get; set; }
}

public enum ResourceAvailabilityState
{
    Available,
    Unavailable,
    Degraded,
    Unknown
}

/// <summary>
/// Represents a resource monitor health request
/// </summary>
public class ResourceMonitorHealthRequest
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string? ResourceGroupName { get; set; }
    public List<string> ResourceTypes { get; set; } = new();
    public bool IncludeHealthy { get; set; } = false;
    public int? MaxResults { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// Represents a resource monitor health response
/// </summary>
public class ResourceMonitorHealthResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ResourceHealthInfo> HealthStatuses { get; set; } = new();
    public ResourceHealthSummaryInfo Summary { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Represents health information for a specific Azure resource
/// </summary>
public class ResourceHealthInfo
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Subscription { get; set; } = string.Empty;
    public ResourceHealthState HealthState { get; set; }
    public string HealthDescription { get; set; } = string.Empty;
    public DateTime LastHealthCheck { get; set; }
    public List<ResourceHealthEvent> RecentEvents { get; set; } = new();
    public Dictionary<string, object> HealthMetrics { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public DateTime? NextScheduledCheck { get; set; }
    public string? IncidentId { get; set; }
}

/// <summary>
/// Represents a health event for a resource
/// </summary>
public class ResourceHealthEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ResourceHealthEventSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
    public ResourceHealthState StateChange { get; set; }
    public string? RootCause { get; set; }
    public DateTime? ExpectedResolution { get; set; }
    public List<string> AffectedServices { get; set; } = new();
    public Dictionary<string, object> EventData { get; set; } = new();
}

/// <summary>
/// Summary information for resource health across a scope
/// </summary>
public class ResourceHealthSummaryInfo
{
    public int TotalResources { get; set; }
    public int HealthyResources { get; set; }
    public int UnhealthyResources { get; set; }
    public int DegradedResources { get; set; }
    public int UnknownResources { get; set; }
    public double HealthPercentage { get; set; }
    public List<ResourceHealthTrend> HealthTrends { get; set; } = new();
    public List<ResourceHealthAlert> ActiveAlerts { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public string OverallHealthStatus { get; set; } = string.Empty;
}

/// <summary>
/// Represents health trend data over time
/// </summary>
public class ResourceHealthTrend
{
    public DateTime Timestamp { get; set; }
    public int HealthyCount { get; set; }
    public int UnhealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public double HealthPercentage { get; set; }
    public string Period { get; set; } = string.Empty; // hourly, daily, weekly
}

/// <summary>
/// Represents a health alert for monitoring
/// </summary>
public class ResourceHealthAlert
{
    public string AlertId { get; set; } = Guid.NewGuid().ToString();
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public ResourceHealthAlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public ResourceHealthAlertState State { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
    public Dictionary<string, object> AlertData { get; set; } = new();
}

/// <summary>
/// Represents detailed health monitoring dashboard data
/// </summary>
public class ResourceHealthDashboardData
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Title { get; set; } = "Azure Resource Health Dashboard";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public ResourceHealthSummaryInfo Summary { get; set; } = new();
    public List<ResourceHealthInfo> CriticalResources { get; set; } = new();
    public List<ResourceHealthAlert> RecentAlerts { get; set; } = new();
    public List<ResourceHealthTrend> HealthTrends { get; set; } = new();
    public List<ResourceHealthRecommendation> Recommendations { get; set; } = new();
    public Dictionary<string, int> ResourceTypeBreakdown { get; set; } = new();
    public Dictionary<string, int> LocationBreakdown { get; set; } = new();
    public double OverallHealthScore { get; set; }
    public string HealthScoreGrade { get; set; } = string.Empty; // A, B, C, D, F
}

// Alias for backward compatibility with IAzureResourceHealthService interface
public class ResourceHealthDashboard : ResourceHealthDashboardData
{
}

/// <summary>
/// Health trend analysis data (stub for gateway service)
/// </summary>
public class HealthTrendAnalysis
{
    public HealthTrendDirection TrendDirection { get; set; }
    public double ChangePercentage { get; set; }
    public int PeriodDays { get; set; }
    public List<string> Insights { get; set; } = new();
}

public enum HealthTrendDirection
{
    Improving,
    Stable,
    Declining
}

/// <summary>
/// Represents a health monitoring recommendation
/// </summary>
public class ResourceHealthRecommendation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ResourceHealthRecommendationType Type { get; set; }
    public ResourceHealthRecommendationPriority Priority { get; set; }
    public List<string> AffectedResources { get; set; } = new();
    public string Impact { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = new();
    public TimeSpan EstimatedImplementationTime { get; set; }
    public string? DocumentationLink { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

#region Enums

/// <summary>
/// States for resource health
/// </summary>
public enum ResourceHealthState
{
    Healthy,
    Unhealthy,
    Degraded,
    Unknown,
    Maintenance
}

/// <summary>
/// Severity levels for health events
/// </summary>
public enum ResourceHealthEventSeverity
{
    Critical,
    High,
    Medium,
    Low,
    Informational
}

/// <summary>
/// Severity levels for health alerts
/// </summary>
public enum ResourceHealthAlertSeverity
{
    Critical,
    Warning,
    Informational
}

/// <summary>
/// States for health alerts
/// </summary>
public enum ResourceHealthAlertState
{
    Active,
    Resolved,
    Suppressed,
    Acknowledged
}

/// <summary>
/// Types of health recommendations
/// </summary>
public enum ResourceHealthRecommendationType
{
    Performance,
    Availability,
    Security,
    Cost,
    Configuration,
    Maintenance
}

/// <summary>
/// Priority levels for recommendations
/// </summary>
public enum ResourceHealthRecommendationPriority
{
    Critical,
    High,
    Medium,
    Low
}

#endregion