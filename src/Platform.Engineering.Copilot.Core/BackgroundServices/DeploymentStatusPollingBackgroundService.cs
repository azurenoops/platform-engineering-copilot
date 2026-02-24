using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.BackgroundServices;

/// <summary>
/// Background service that polls for in-progress deployments and updates their status.
/// </summary>
public class DeploymentStatusPollingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeploymentStatusPollingBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public DeploymentStatusPollingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeploymentStatusPollingBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalSeconds = configuration.GetValue("DeploymentPolling:IntervalSeconds", 30);
        _interval = TimeSpan.FromSeconds(Math.Max(intervalSeconds, 5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deployment Status Polling background service started. Interval: {Interval}", _interval);

        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    _logger.LogDebug("Polling deployment statuses");

                    using var scope = _scopeFactory.CreateScope();
                    var envService = scope.ServiceProvider.GetRequiredService<IProvisionedEnvironmentService>();
                    var result = await envService.RefreshAllProvisioningAsync(stoppingToken);

                    _logger.LogDebug("Deployment status polling completed: {@Result}", result);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error during deployment status polling");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Deployment Status Polling background service stopping");
        }
    }
}
