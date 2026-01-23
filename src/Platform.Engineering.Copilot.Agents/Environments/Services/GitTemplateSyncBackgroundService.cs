using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.TemplateMatching;

namespace Platform.Engineering.Copilot.Agents.Environments.Services;

/// <summary>
/// Background service that periodically syncs templates from Git repositories.
/// Ensures the database cache stays up-to-date with the Git source of truth.
/// </summary>
public class GitTemplateSyncBackgroundService : BackgroundService
{
    private readonly ILogger<GitTemplateSyncBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GitSyncOptions _options;
    private readonly TimeSpan _checkInterval;

    public GitTemplateSyncBackgroundService(
        ILogger<GitTemplateSyncBackgroundService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<GitSyncOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? new GitSyncOptions();
        
        // Check for templates needing sync every 5 minutes by default
        _checkInterval = TimeSpan.FromMinutes(_options.CheckIntervalMinutes > 0 
            ? _options.CheckIntervalMinutes 
            : 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoSyncEnabled)
        {
            _logger.LogInformation("🔄 Git template auto-sync is disabled");
            return;
        }

        _logger.LogInformation("🔄 Git template sync background service started (interval: {Interval})",
            _checkInterval);

        // Wait a bit before first sync to let the app fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncTemplatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Git template sync cycle");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("🔄 Git template sync background service stopped");
    }

    private async Task SyncTemplatesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        
        var repository = scope.ServiceProvider.GetRequiredService<IServiceTemplateRepository>();
        var syncService = scope.ServiceProvider.GetRequiredService<IGitTemplateSyncService>();

        // Get templates that need syncing
        var templatesNeedingSync = await repository.GetTemplatesNeedingSyncAsync(cancellationToken);
        
        if (templatesNeedingSync.Count == 0)
        {
            _logger.LogDebug("🔄 No templates need Git sync at this time");
            return;
        }

        _logger.LogInformation("🔄 Starting background Git sync for {Count} templates", 
            templatesNeedingSync.Count);

        var synced = 0;
        var updated = 0;
        var failed = 0;

        foreach (var template in templatesNeedingSync)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var result = await syncService.SyncTemplateAsync(template.Id, false, cancellationToken);
                
                if (result.Success)
                {
                    synced++;
                    if (result.WasUpdated)
                    {
                        updated++;
                        _logger.LogInformation("✅ Template {Name} updated from Git (commit: {Sha})",
                            template.Name, result.CommitSha?[..7] ?? "unknown");
                        
                        // Add audit log entry
                        await AddAuditLogAsync(repository, template.Id, template.Name, 
                            "GitSync", result.CommitSha, cancellationToken);
                    }
                }
                else
                {
                    failed++;
                    _logger.LogWarning("⚠️ Failed to sync template {Name}: {Message}",
                        template.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Error syncing template {Name}", template.Name);
            }

            // Small delay between templates to avoid overwhelming APIs
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        _logger.LogInformation("🔄 Background Git sync complete: {Synced} synced, {Updated} updated, {Failed} failed",
            synced, updated, failed);
    }

    private async Task AddAuditLogAsync(
        IServiceTemplateRepository repository,
        Guid templateId,
        string templateName,
        string action,
        string? commitSha,
        CancellationToken cancellationToken)
    {
        try
        {
            var auditEntry = new Core.Data.Entities.ServiceTemplateAuditEntity
            {
                Id = Guid.NewGuid(),
                EntityId = templateId,
                EntityType = "ServiceTemplate",
                EntityName = templateName,
                Action = action,
                PerformedBy = "GitSyncBackgroundService",
                Timestamp = DateTime.UtcNow,
                Details = $"Template '{templateName}' synced from Git. Commit: {commitSha ?? "unknown"}",
                ServiceTemplateId = templateId,
                NewValuesJson = commitSha != null ? $"{{\"commitSha\":\"{commitSha}\"}}" : null
            };

            await repository.AddAuditEntryAsync(auditEntry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add audit entry for Git sync");
        }
    }
}
