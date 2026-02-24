using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.BackgroundServices;

/// <summary>
/// Background service that periodically syncs Git-linked templates based on configured interval.
/// </summary>
public class GitTemplateSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GitTemplateSyncBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public GitTemplateSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<GitTemplateSyncBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalMinutes = configuration.GetValue("GitSync:IntervalMinutes", 60);
        _interval = TimeSpan.FromMinutes(Math.Max(intervalMinutes, 1));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Git Template Sync background service started. Interval: {Interval}", _interval);

        using var timer = new PeriodicTimer(_interval);

        // Wait for the first interval before starting
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogDebug("Running scheduled Git template sync");

                IServiceScope scope;
                try
                {
                    scope = _scopeFactory.CreateScope();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to create service scope for Git template sync");
                    continue;
                }

                using (scope)
                {
                    try
                    {
                        var gitSyncService = scope.ServiceProvider.GetRequiredService<IGitTemplateSyncService>();
                        var result = await gitSyncService.SyncAllAsync(stoppingToken);

                        _logger.LogInformation("Git template sync completed: {@Result}", result);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Error during Git template sync");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Git Template Sync background service stopping");
        }
    }
}
