using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Repositories;

namespace Platform.Engineering.Copilot.Core.Services.TemplateGeneration;

/// <summary>
/// Background service that periodically cleans up expired templates.
/// Templates expire 30 minutes after creation by default.
/// Runs every 5 minutes to check for and delete expired templates.
/// </summary>
public class TemplateCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TemplateCleanupBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval;
    private readonly bool _enabled;
    
    public TemplateCleanupBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<TemplateCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Read configuration with defaults
        var cleanupIntervalMinutes = configuration.GetValue("InfrastructureAgent:TemplateExpiration:CleanupIntervalMinutes", 5);
        _cleanupInterval = TimeSpan.FromMinutes(cleanupIntervalMinutes);
        _enabled = configuration.GetValue("InfrastructureAgent:TemplateExpiration:Enabled", true);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Template cleanup background service is disabled");
            return;
        }
        
        _logger.LogInformation(
            "Template cleanup background service started. Cleanup runs every {Interval} minutes", 
            _cleanupInterval.TotalMinutes);
        
        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTemplatesAsync(stoppingToken);
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during template cleanup");
                // Wait before retrying to avoid tight error loop
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        
        _logger.LogInformation("Template cleanup background service stopped");
    }
    
    private async Task CleanupExpiredTemplatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var templateRepository = scope.ServiceProvider.GetRequiredService<IEnvironmentTemplateRepository>();
            
            var deletedCount = await templateRepository.DeleteExpiredTemplatesAsync(cancellationToken);
            
            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Template cleanup completed. Removed {Count} expired template(s)", 
                    deletedCount);
            }
            else
            {
                _logger.LogDebug("Template cleanup completed. No expired templates found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired templates");
            throw;
        }
    }
}
