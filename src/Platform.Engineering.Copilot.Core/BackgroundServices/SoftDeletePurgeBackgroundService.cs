using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data;

namespace Platform.Engineering.Copilot.Core.BackgroundServices;

/// <summary>
/// Background service that automatically purges soft-deleted records older than 30 days.
/// Runs daily using BackgroundService + PeriodicTimer pattern.
/// </summary>
public class SoftDeletePurgeBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SoftDeletePurgeBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public SoftDeletePurgeBackgroundService(IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<SoftDeletePurgeBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        var hours = configuration.GetValue("SoftDeletePurge:IntervalHours", 24);
        _interval = TimeSpan.FromHours(hours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SoftDeletePurgeBackgroundService starting with interval {Interval}", _interval);

        // Initial delay before first purge
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<PlatformEngineeringCopilotContext>();
                var cutoff = DateTimeOffset.UtcNow.AddDays(-30);

                // Purge soft-deleted templates older than 30 days
                var expiredTemplates = await context.ServiceTemplates
                    .IgnoreQueryFilters()
                    .Where(t => t.IsDeleted && t.DeletedAt < cutoff)
                    .ToListAsync(stoppingToken);

                if (expiredTemplates.Count > 0)
                {
                    context.ServiceTemplates.RemoveRange(expiredTemplates);
                    _logger.LogInformation("Purged {Count} soft-deleted templates older than 30 days",
                        expiredTemplates.Count);
                }

                // Purge soft-deleted environments older than 30 days
                var expiredEnvironments = await context.ProvisionedEnvironments
                    .IgnoreQueryFilters()
                    .Where(e => e.IsDeleted && e.DeletedAt < cutoff)
                    .ToListAsync(stoppingToken);

                if (expiredEnvironments.Count > 0)
                {
                    context.ProvisionedEnvironments.RemoveRange(expiredEnvironments);
                    _logger.LogInformation("Purged {Count} soft-deleted environments older than 30 days",
                        expiredEnvironments.Count);
                }

                if (expiredTemplates.Count > 0 || expiredEnvironments.Count > 0)
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during soft-delete purge cycle");
            }
        }

        _logger.LogInformation("SoftDeletePurgeBackgroundService stopped");
    }
}
