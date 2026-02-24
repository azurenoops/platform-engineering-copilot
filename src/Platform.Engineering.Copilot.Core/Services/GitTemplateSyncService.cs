using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Service for importing and syncing templates from Git repositories.
/// Stub implementation — connects to Git provider for real sync in production.
/// </summary>
public class GitTemplateSyncService : IGitTemplateSyncService
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<GitTemplateSyncService> _logger;

    public GitTemplateSyncService(PlatformEngineeringCopilotContext context, ILogger<GitTemplateSyncService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ServiceTemplate> ImportFromGitAsync(string gitRepoUrl, string? branch = null,
        string? filePath = null, string? name = null, string? category = null, bool gitAutoSync = false,
        int gitSyncIntervalMinutes = 60, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing template from Git: {Url}, branch={Branch}, path={Path}",
            gitRepoUrl, branch ?? "main", filePath);

        // Extract template name from URL if not provided
        var templateName = name ?? ExtractNameFromUrl(gitRepoUrl, filePath);

        var template = new ServiceTemplate
        {
            TemplateId = Guid.NewGuid(),
            Name = templateName,
            DisplayName = templateName,
            Description = $"Imported from {gitRepoUrl}",
            Category = category ?? "Imported",
            Version = "1.0.0",
            Format = TemplateFormat.Bicep,
            Status = TemplateStatus.Draft,
            Content = $"// Imported from {gitRepoUrl}\n// Branch: {branch ?? "main"}\n// Path: {filePath ?? "/"}",
            ParametersJson = "{}",
            GitPath = $"{gitRepoUrl}#{branch ?? "main"}:{filePath ?? "/"}",
            GitCommitSha = "stub-sha-pending-sync",
            GitAutoSync = gitAutoSync,
            GitSyncIntervalMinutes = gitSyncIntervalMinutes,
            CreatedBy = "system",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.ServiceTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Imported template {TemplateId} '{Name}' from Git", template.TemplateId, template.Name);
        return template;
    }

    public async Task<ServiceTemplate> SyncAsync(Guid templateId, bool force = false,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        if (string.IsNullOrWhiteSpace(template.GitPath))
            throw new InvalidOperationException($"Template {templateId} is not linked to a Git repository.");

        _logger.LogInformation("Syncing template {TemplateId} from Git (force={Force})", templateId, force);

        // Stub: In production, fetch latest content from Git
        template.GitCommitSha = $"sync-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return template;
    }

    public async Task<object> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var gitLinkedTemplates = await _context.ServiceTemplates
            .Where(t => t.GitAutoSync && !string.IsNullOrEmpty(t.GitPath))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Syncing {Count} Git-linked templates", gitLinkedTemplates.Count);

        var results = new List<object>();
        var syncErrors = new List<object>();

        foreach (var template in gitLinkedTemplates)
        {
            try
            {
                await SyncAsync(template.TemplateId, false, cancellationToken);
                results.Add(new { templateId = template.TemplateId, name = template.Name, status = "Synced" });
            }
            catch (Exception ex)
            {
                syncErrors.Add(new { templateId = template.TemplateId, name = template.Name, error = ex.Message });
                _logger.LogWarning(ex, "Failed to sync template {TemplateId}", template.TemplateId);
            }
        }

        return new
        {
            totalCount = gitLinkedTemplates.Count,
            syncedCount = results.Count,
            errorCount = syncErrors.Count,
            results,
            errors = syncErrors
        };
    }

    public async Task<object> GetGitStatusAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        return new
        {
            templateId,
            templateName = template.Name,
            isGitLinked = !string.IsNullOrWhiteSpace(template.GitPath),
            gitPath = template.GitPath,
            gitCommitSha = template.GitCommitSha,
            gitAutoSync = template.GitAutoSync,
            gitSyncIntervalMinutes = template.GitSyncIntervalMinutes,
            lastSyncedAt = template.UpdatedAt,
            isSynced = true // Stub: in production, compare local SHA with remote
        };
    }

    public async Task<ServiceTemplate> ResetParametersAsync(Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        template.ParametersJson = "{}";
        template.ParametersOverridden = false;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Reset parameters for template {TemplateId}", templateId);
        return template;
    }

    private static string ExtractNameFromUrl(string url, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var fileName = filePath.Split('/').Last();
            var nameWithoutExt = fileName.Contains('.') ? fileName[..fileName.LastIndexOf('.')] : fileName;
            return nameWithoutExt;
        }

        // Extract repo name from URL
        var segments = url.TrimEnd('/').Split('/');
        var repoName = segments.LastOrDefault() ?? "imported-template";
        return repoName.EndsWith(".git") ? repoName[..^4] : repoName;
    }
}
