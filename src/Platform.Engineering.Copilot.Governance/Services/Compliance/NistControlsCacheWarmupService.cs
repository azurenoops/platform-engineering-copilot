using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Governance.Configuration;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Background service for warming up NIST controls cache and performing validation
/// </summary>
public class NistControlsCacheWarmupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NistControlsCacheWarmupService> _logger;
    private readonly NistControlsOptions _options;

    public NistControlsCacheWarmupService(
        IServiceProvider serviceProvider,
        ILogger<NistControlsCacheWarmupService> logger,
        IOptions<NistControlsOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for application to fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        _logger.LogInformation("Starting NIST controls cache warmup service");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var nistService = scope.ServiceProvider.GetRequiredService<INistControlsService>();
                var validationService = scope.ServiceProvider.GetRequiredService<IComplianceValidationService>();

                // Warm up the cache
                _logger.LogDebug("Warming up NIST controls cache");
                var catalog = await nistService.GetCatalogAsync(stoppingToken);
                
                if (catalog != null)
                {
                    var controlCount = catalog.Groups?.SelectMany(g => g.Controls ?? []).Count() ?? 0;
                    _logger.LogInformation("Successfully warmed up NIST controls cache with {ControlCount} controls", controlCount);

                    // Perform validation
                    var configValidation = await validationService.ValidateConfigurationAsync(stoppingToken);
                    var mappingValidation = await validationService.ValidateControlMappingsAsync(stoppingToken);

                    if (!configValidation.IsValid)
                    {
                        _logger.LogWarning("Configuration validation issues detected: {Errors}",
                            string.Join("; ", configValidation.Errors));
                    }

                    if (mappingValidation.Warnings.Any())
                    {
                        _logger.LogWarning("Control mapping validation warnings: {Warnings}",
                            string.Join("; ", mappingValidation.Warnings));
                    }
                }
                else
                {
                    _logger.LogError("Failed to warm up NIST controls cache - catalog is null");
                }

                // Wait for the cache duration before next refresh
                var nextRefresh = TimeSpan.FromHours(_options.CacheDurationHours * 0.9); // Refresh at 90% of cache lifetime
                _logger.LogDebug("Next NIST controls cache refresh in {Duration}", nextRefresh);
                
                await Task.Delay(nextRefresh, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during NIST controls cache warmup");
                
                // Wait before retrying
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("NIST controls cache warmup service stopped");
    }
}