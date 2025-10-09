using System;
using System.Collections.Generic;

namespace Platform.Engineering.Copilot.Core.Models.EnvironmentManagement
{
    // ========== HEALTH & STATUS MODELS ==========
    
    /// <summary>
    /// Comprehensive health report for an environment
    /// </summary>
    public class EnvironmentHealthReport
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public HealthStatus OverallHealth { get; set; }
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        public List<HealthCheck> Checks { get; set; } = new();
        public List<HealthAlert> Alerts { get; set; } = new();
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
    
    /// <summary>
    /// Individual health check result
    /// </summary>
    public class HealthCheck
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        public string? Details { get; set; }
    }
    
    /// <summary>
    /// Health alert information
    /// </summary>
    public class HealthAlert
    {
        public string Id { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public string? ResolutionSuggestion { get; set; }
    }
    
    /// <summary>
    /// Environment metrics data
    /// </summary>
    public class EnvironmentMetrics
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public PerformanceMetrics Performance { get; set; } = new();
        public ResourceMetrics Resources { get; set; } = new();
        public RequestMetrics Requests { get; set; } = new();
        public ErrorMetrics Errors { get; set; } = new();
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Performance metrics
    /// </summary>
    public class PerformanceMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public double NetworkInMbps { get; set; }
        public double NetworkOutMbps { get; set; }
        public double AverageResponseTimeMs { get; set; }
    }
    
    /// <summary>
    /// Resource utilization metrics
    /// </summary>
    public class ResourceMetrics
    {
        public int TotalNodes { get; set; }
        public int RunningPods { get; set; }
        public int TotalPods { get; set; }
        public double AllocatedCpuCores { get; set; }
        public double AllocatedMemoryGb { get; set; }
        public double AvailableCpuCores { get; set; }
        public double AvailableMemoryGb { get; set; }
    }
    
    /// <summary>
    /// Request metrics
    /// </summary>
    public class RequestMetrics
    {
        public long TotalRequests { get; set; }
        public double RequestsPerSecond { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public double SuccessRate { get; set; }
    }
    
    /// <summary>
    /// Error metrics
    /// </summary>
    public class ErrorMetrics
    {
        public long TotalErrors { get; set; }
        public double ErrorRate { get; set; }
        public Dictionary<string, int> ErrorsByType { get; set; } = new();
        public List<ErrorSummary> RecentErrors { get; set; } = new();
    }
    
    /// <summary>
    /// Error summary
    /// </summary>
    public class ErrorSummary
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTime LastOccurred { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Current environment status
    /// </summary>
    public class EnvironmentStatus
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public EnvironmentType Type { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastModified { get; set; }
        public EnvironmentConfiguration Configuration { get; set; } = new();
        public string Endpoint { get; set; } = string.Empty;
        public Dictionary<string, string> Tags { get; set; } = new();
    }
    
    /// <summary>
    /// Environment configuration details
    /// </summary>
    public class EnvironmentConfiguration
    {
        public string Location { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int? Replicas { get; set; }
        public ScaleSettings? ScaleSettings { get; set; }
        public bool MonitoringEnabled { get; set; }
        public bool LoggingEnabled { get; set; }
        public ComplianceSettings? ComplianceSettings { get; set; }
        public Dictionary<string, string> CustomSettings { get; set; } = new();
    }
    
    /// <summary>
    /// Environment summary for listing
    /// </summary>
    public class EnvironmentSummary
    {
        public string Name { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public EnvironmentType Type { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public HealthStatus Health { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
        public decimal? EstimatedMonthlyCost { get; set; }
    }
    
    /// <summary>
    /// Environment discovery result
    /// </summary>
    public class EnvironmentDiscoveryResult
    {
        public string Name { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public EnvironmentType Type { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsManaged { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
        public List<string> RelatedResources { get; set; } = new();
    }
}
