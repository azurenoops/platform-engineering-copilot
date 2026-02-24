using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Core.Observability;

/// <summary>
/// Health check for the NIST Controls service (FR-028 through FR-033, FR-046).
/// Reports catalog version, control validation status, and response time.
/// Returns Healthy, Degraded, or Unhealthy based on catalog state.
/// </summary>
public class NistControlsHealthCheck : IHealthCheck
{
    private readonly INistService _nistService;
    private readonly NistControlsOptions _options;
    private readonly ILogger<NistControlsHealthCheck> _logger;

    /// <summary>
    /// Controls validated during health check — representative sample from different families.
    /// </summary>
    private static readonly string[] TestControlIds = ["AC-3", "SC-13", "AU-2"];

    /// <summary>
    /// Maximum acceptable response time in seconds before status degrades.
    /// </summary>
    private const double MaxAcceptableResponseTimeSeconds = 5.0;

    public NistControlsHealthCheck(
        INistService nistService,
        IOptions<NistControlsOptions> options,
        ILogger<NistControlsHealthCheck> logger)
    {
        _nistService = nistService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Get catalog version
            var version = await _nistService.GetVersionAsync(cancellationToken);

            // Validate test controls
            var validCount = 0;
            foreach (var controlId in TestControlIds)
            {
                if (await _nistService.ValidateControlIdAsync(controlId, cancellationToken))
                    validCount++;
            }

            sw.Stop();
            var responseTimeMs = sw.Elapsed.TotalMilliseconds;

            // Build health data
            var data = new Dictionary<string, object>
            {
                ["version"] = version,
                ["validControlCount"] = validCount,
                ["totalTestControls"] = TestControlIds.Length,
                ["responseTimeMs"] = Math.Round(responseTimeMs, 2),
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
                ["cacheDurationHours"] = _options.CacheDurationHours,
                ["offlineFallbackEnabled"] = _options.EnableOfflineFallback
            };

            // Determine health status
            if (validCount == TestControlIds.Length &&
                sw.Elapsed.TotalSeconds < MaxAcceptableResponseTimeSeconds &&
                version != "Unknown")
            {
                return HealthCheckResult.Healthy(
                    $"NIST Controls service healthy — {validCount}/{TestControlIds.Length} controls valid, version: {version}",
                    data);
            }

            if (validCount == 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"NIST Controls service unhealthy — no test controls valid, version: {version}",
                    data: data);
            }

            // Degraded: partial controls valid, or slow response, or unknown version
            var reasons = new List<string>();
            if (validCount < TestControlIds.Length)
                reasons.Add($"{validCount}/{TestControlIds.Length} controls valid");
            if (sw.Elapsed.TotalSeconds >= MaxAcceptableResponseTimeSeconds)
                reasons.Add($"response time {responseTimeMs:F0}ms exceeds {MaxAcceptableResponseTimeSeconds}s threshold");
            if (version == "Unknown")
                reasons.Add("catalog version unknown");

            return HealthCheckResult.Degraded(
                $"NIST Controls service degraded — {string.Join("; ", reasons)}",
                data: data);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "NIST Controls health check failed");

            return HealthCheckResult.Unhealthy(
                "NIST Controls service unhealthy — exception during health check",
                ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["responseTimeMs"] = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                    ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
                });
        }
    }
}
