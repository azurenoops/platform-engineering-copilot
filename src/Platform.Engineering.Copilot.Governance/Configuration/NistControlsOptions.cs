using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Governance.Configuration;

/// <summary>
/// Configuration options for NIST controls service
/// </summary>
public class NistControlsOptions
{
    public const string SectionName = "NistControls";

    /// <summary>
    /// Base URL for NIST OSCAL content repository
    /// </summary>
    [Required]
    public string BaseUrl { get; set; } = "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json";

    /// <summary>
    /// HTTP client timeout in seconds
    /// </summary>
    [Range(10, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Cache duration in hours
    /// </summary>
    [Range(1, 168)] // 1 hour to 1 week
    public int CacheDurationHours { get; set; } = 24;

    /// <summary>
    /// Maximum retry attempts for failed requests
    /// </summary>
    [Range(1, 5)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in seconds (exponential backoff base)
    /// </summary>
    [Range(1, 60)]
    public int RetryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Enable offline fallback mode
    /// </summary>
    public bool EnableOfflineFallback { get; set; } = true;

    /// <summary>
    /// Path to offline NIST controls JSON file (relative to content root)
    /// </summary>
    public string? OfflineFallbackPath { get; set; } = "Data/nist-800-53-fallback.json";

    /// <summary>
    /// Enable detailed request/response logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// NIST catalog version to target (empty for latest)
    /// </summary>
    public string? TargetVersion { get; set; }
}