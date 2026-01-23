using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;

namespace Platform.Engineering.Copilot.Agents.Environments.Services;

/// <summary>
/// Options for the deployment status polling service
/// </summary>
public class DeploymentPollingOptions
{
    /// <summary>
    /// Whether auto-polling is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Interval in seconds between polling cycles (default: 30 seconds)
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Initial delay in seconds before starting polling (default: 10 seconds)
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 10;
}

/// <summary>
/// Background service that periodically polls Azure for deployment status updates.
/// Updates environments from "Provisioning" to "Running" or "Failed" when deployments complete.
/// </summary>
public class DeploymentStatusPollingBackgroundService : BackgroundService
{
    private readonly ILogger<DeploymentStatusPollingBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DeploymentPollingOptions _options;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _initialDelay;

    public DeploymentStatusPollingBackgroundService(
        ILogger<DeploymentStatusPollingBackgroundService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<DeploymentPollingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? new DeploymentPollingOptions();
        
        // Poll every 30 seconds by default
        _pollInterval = TimeSpan.FromSeconds(_options.IntervalSeconds > 0 
            ? _options.IntervalSeconds 
            : 30);
        
        // Initial delay before first poll
        _initialDelay = TimeSpan.FromSeconds(_options.InitialDelaySeconds > 0
            ? _options.InitialDelaySeconds
            : 10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("⏳ Deployment status polling is disabled");
            return;
        }

        _logger.LogInformation("⏳ Deployment status polling service started (interval: {Interval}s)",
            _pollInterval.TotalSeconds);

        // Wait a bit before first poll to let the app fully start
        await Task.Delay(_initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollDeploymentStatusesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during deployment status polling cycle");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("⏳ Deployment status polling service stopped");
    }

    private async Task PollDeploymentStatusesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        
        var environmentService = scope.ServiceProvider.GetRequiredService<IProvisionedEnvironmentService>();

        // Refresh status for all provisioning environments
        var results = await environmentService.RefreshAllProvisioningEnvironmentsAsync(cancellationToken);

        // Log summary
        var changedCount = results.Count(r => r.StatusChanged);
        if (results.Count > 0)
        {
            _logger.LogInformation("⏳ Polled {Total} provisioning environments, {Changed} status changes",
                results.Count, changedCount);
            
            foreach (var result in results.Where(r => r.StatusChanged))
            {
                _logger.LogInformation("📝 Environment {Name}: {OldStatus} → {NewStatus}",
                    result.EnvironmentName ?? result.EnvironmentId,
                    result.PreviousStatus,
                    result.CurrentStatus);
            }
        }
        else
        {
            _logger.LogDebug("⏳ No environments in Provisioning state");
        }
    }
}
