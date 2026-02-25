using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Compliance.Services;

/// <summary>
/// Background hosted service that proactively warms the NIST catalog cache at startup
/// and periodically refreshes it before TTL expiration (FR-021 through FR-027).
/// </summary>
public class NistControlsCacheWarmupService : BackgroundService
{
    private readonly INistService _nistService;
    private readonly NistControlsOptions _options;
    private readonly ILogger<NistControlsCacheWarmupService> _logger;

    /// <summary>
    /// Delay before first warmup attempt after application start (default 10 seconds).
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Retry delay when warmup fails (5 minutes).
    /// </summary>
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Critical control IDs that must be validated after cache warmup.
    /// </summary>
    private static readonly string[] CriticalControlIds =
    [
        "SC-13", "SC-28", "AC-3", "AC-6", "SC-7",
        "AC-4", "AU-2", "SI-4", "CP-9", "CP-10", "IA-5"
    ];

    public NistControlsCacheWarmupService(
        INistService nistService,
        IOptions<NistControlsOptions> options,
        ILogger<NistControlsCacheWarmupService> logger)
    {
        _nistService = nistService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NIST Controls cache warmup service starting. Startup delay: {Delay}s",
            StartupDelay.TotalSeconds);

        try
        {
            // Configurable startup delay to let the application finish initialization
            await Task.Delay(StartupDelay, stoppingToken);

            // Initial warmup
            await WarmCacheAsync(stoppingToken);

            // Proactive refresh loop at 90% of TTL
            var refreshInterval = TimeSpan.FromHours(_options.CacheDurationHours * 0.9);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Next NIST catalog refresh in {Hours:F1}h", refreshInterval.TotalHours);
                await Task.Delay(refreshInterval, stoppingToken);

                await WarmCacheAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("NIST Controls cache warmup service shutting down gracefully");
        }
    }

    private async Task WarmCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting NIST catalog cache warmup");

            // Populate the cache by calling GetCatalogAsync
            var snapshot = await _nistService.GetCatalogAsync(cancellationToken);

            if (snapshot == null)
            {
                _logger.LogWarning("NIST catalog warmup completed but catalog is empty. Retrying in {Minutes}m",
                    FailureRetryDelay.TotalMinutes);

                await Task.Delay(FailureRetryDelay, cancellationToken);
                snapshot = await _nistService.GetCatalogAsync(cancellationToken);
            }

            if (snapshot != null)
            {
                _logger.LogInformation(
                    "NIST catalog cache warmed successfully. Version: {Version}, Controls: {Count}, Families: {Families}, Source: {Source}",
                    snapshot.Version, snapshot.TotalControls, snapshot.FamilyCount, snapshot.Source);

                // Validate critical controls
                await ValidateCriticalControlsAsync(cancellationToken);
            }
            else
            {
                _logger.LogError("NIST catalog warmup failed — catalog could not be loaded");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Propagate graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NIST catalog cache warmup failed. Will retry in {Minutes}m",
                FailureRetryDelay.TotalMinutes);

            try
            {
                await Task.Delay(FailureRetryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down during retry delay
            }
        }
    }

    private async Task ValidateCriticalControlsAsync(CancellationToken cancellationToken)
    {
        var validCount = 0;
        var missingControls = new List<string>();

        foreach (var controlId in CriticalControlIds)
        {
            var isValid = await _nistService.ValidateControlIdAsync(controlId, cancellationToken);
            if (isValid)
            {
                validCount++;
            }
            else
            {
                missingControls.Add(controlId);
            }
        }

        if (missingControls.Count > 0)
        {
            _logger.LogWarning(
                "NIST catalog warmup: {Missing} of {Total} critical controls not found: {Controls}",
                missingControls.Count, CriticalControlIds.Length, string.Join(", ", missingControls));
        }
        else
        {
            _logger.LogInformation(
                "NIST catalog warmup: All {Count} critical controls validated successfully",
                CriticalControlIds.Length);
        }
    }
}
