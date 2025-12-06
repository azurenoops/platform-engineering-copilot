using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Discovery.Core.Configuration;

/// <summary>
/// Configuration options for the Discovery Agent
/// </summary>
public class DiscoveryAgentOptions
{
    public const string SectionName = "DiscoveryAgent";

    /// <summary>
    /// Temperature setting for AI model (0.0-1.0). Lower = more deterministic, higher = more creative
    /// </summary>
    [Range(0.0, 1.0)]
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Maximum tokens for AI model responses
    /// </summary>
    [Range(100, 128000)]
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Enable health monitoring for discovered resources
    /// </summary>
    public bool EnableHealthMonitoring { get; set; } = true;

    /// <summary>
    /// Enable performance metrics collection
    /// </summary>
    public bool EnablePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Enable dependency mapping between resources
    /// </summary>
    public bool EnableDependencyMapping { get; set; } = true;

    /// <summary>
    /// Discovery-specific settings
    /// </summary>
    public DiscoveryOptions Discovery { get; set; } = new();

    /// <summary>
    /// Health monitoring settings
    /// </summary>
    public HealthMonitoringOptions HealthMonitoring { get; set; } = new();
}

/// <summary>
/// Discovery-specific settings
/// </summary>
public class DiscoveryOptions
{
    /// <summary>
    /// Cache duration for discovery results in minutes
    /// </summary>
    [Range(1, 1440)]
    public int CacheDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum number of resources to return per query
    /// </summary>
    [Range(1, 10000)]
    public int MaxResourcesPerQuery { get; set; } = 1000;

    /// <summary>
    /// Include deleted/soft-deleted resources in discovery results
    /// </summary>
    public bool IncludeDeletedResources { get; set; } = false;

    /// <summary>
    /// Required tags that resources should have
    /// </summary>
    public List<string> RequiredTags { get; set; } = new();
}

/// <summary>
/// Health monitoring settings
/// </summary>
public class HealthMonitoringOptions
{
    /// <summary>
    /// How often to refresh health metrics (in minutes)
    /// </summary>
    [Range(1, 1440)]
    public int RefreshIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// How long to retain metrics data (in days)
    /// </summary>
    [Range(1, 365)]
    public int MetricsRetentionDays { get; set; } = 90;

    /// <summary>
    /// Performance metrics to collect
    /// </summary>
    public List<string> PerformanceMetrics { get; set; } = new() { "CPU", "Memory", "Network", "Disk" };
}
