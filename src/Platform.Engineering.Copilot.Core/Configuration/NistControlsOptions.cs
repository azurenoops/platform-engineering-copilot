using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Core.Configuration;

/// <summary>
/// Validated configuration POCO bound from the "NistControls" section of appsettings.json.
/// Drives caching TTL, retry policy, offline fallback, and observability behavior.
/// </summary>
public class NistControlsOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "NistControls";

    /// <summary>
    /// Base URL for the NIST OSCAL GitHub catalog download.
    /// </summary>
    [Required]
    public string BaseUrl { get; set; } = "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json";

    /// <summary>
    /// Specific catalog version to target; used as cache key suffix when non-null.
    /// </summary>
    public string? TargetVersion { get; set; }

    /// <summary>
    /// HTTP client timeout in seconds.
    /// </summary>
    [Range(10, 300)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// IMemoryCache absolute expiration in hours.
    /// </summary>
    [Range(1, 168)]
    public int CacheDurationHours { get; set; } = 24;

    /// <summary>
    /// Polly retry count for failed HTTP requests.
    /// </summary>
    [Range(1, 5)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Exponential backoff base delay in seconds.
    /// </summary>
    [Range(1, 60)]
    public int RetryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Whether to attempt loading from the offline fallback file when remote fetch fails.
    /// </summary>
    public bool EnableOfflineFallback { get; set; } = true;

    /// <summary>
    /// Relative path to the offline fallback JSON file (resolved from content root).
    /// </summary>
    public string? OfflineFallbackPath { get; set; } = "Data/nist-800-53-fallback.json";

    /// <summary>
    /// Whether to use IMemoryCache with configurable TTL for catalog data.
    /// </summary>
    public bool EnableMemoryCache { get; set; } = true;

    /// <summary>
    /// Whether to enable verbose HTTP and lookup logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; }
}
