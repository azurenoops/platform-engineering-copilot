using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// EF Core repository implementation for Service Template operations.
/// </summary>
public class ServiceTemplateRepository : IServiceTemplateRepository
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<ServiceTemplateRepository> _logger;

    // Archived status constant (entity uses Status field, not IsArchived bool)
    private const string ArchivedStatus = "Archived";
    private const string DeprecatedStatus = "Deprecated";
    private const string PublishedStatus = "Published";

    public ServiceTemplateRepository(
        PlatformEngineeringCopilotContext context,
        ILogger<ServiceTemplateRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Template CRUD

    public async Task<ServiceTemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<ServiceTemplateEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(id, out var guid))
        {
            return await GetByIdAsync(guid, cancellationToken);
        }
        return null;
    }

    public async Task<ServiceTemplateEntity?> GetByNameAsync(string name, string? version = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ServiceTemplates.AsNoTracking().Where(t => t.Name == name);

        if (!string.IsNullOrEmpty(version))
        {
            query = query.Where(t => t.Version == version);
        }
        else
        {
            // Get the latest version
            query = query.OrderByDescending(t => t.Version);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceTemplateEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .AsNoTracking()
            .Where(t => t.Status != ArchivedStatus)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceTemplateEntity>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .AsNoTracking()
            .Where(t => t.Status == status && t.Status != ArchivedStatus)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceTemplateEntity>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .AsNoTracking()
            .Where(t => t.Category == category && t.Status != ArchivedStatus)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceTemplateEntity>> SearchAsync(
        string? keyword = null,
        string? category = null,
        string? status = null,
        string? format = null,
        bool includeDeprecated = false,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ServiceTemplates.AsNoTracking().Where(t => t.Status != ArchivedStatus);

        // Exclude deprecated unless explicitly requested
        if (!includeDeprecated)
        {
            query = query.Where(t => t.Status != DeprecatedStatus);
        }

        // Apply keyword filter (search across multiple fields)
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(lowerKeyword) ||
                (t.DisplayName != null && t.DisplayName.ToLower().Contains(lowerKeyword)) ||
                (t.Description != null && t.Description.ToLower().Contains(lowerKeyword)) ||
                (t.Keywords != null && t.Keywords.ToLower().Contains(lowerKeyword)));
        }

        // Apply category filter
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category == category);
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        // Apply format filter
        if (!string.IsNullOrWhiteSpace(format))
        {
            query = query.Where(t => t.Format == format);
        }

        return await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceTemplateEntity>> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .AsNoTracking()
            .Where(t => t.Status == PublishedStatus)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceTemplateEntity> CreateAsync(ServiceTemplateEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        _context.ServiceTemplates.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created service template {TemplateId} - {TemplateName}", entity.Id, entity.Name);

        return entity;
    }

    public async Task<ServiceTemplateEntity> UpdateAsync(ServiceTemplateEntity entity, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ServiceTemplates.FindAsync(new object[] { entity.Id }, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException($"Template with ID {entity.Id} not found");
        }

        // Explicitly copy all updatable properties
        existing.Name = entity.Name;
        existing.DisplayName = entity.DisplayName;
        existing.Description = entity.Description;
        existing.Version = entity.Version;
        existing.Category = entity.Category;
        existing.Format = entity.Format;
        existing.Status = entity.Status;
        existing.MainTemplateContent = entity.MainTemplateContent;
        existing.AdditionalFilesJson = entity.AdditionalFilesJson;
        existing.ParametersJson = entity.ParametersJson;
        existing.ParametersOverridden = entity.ParametersOverridden;
        existing.GuardrailsJson = entity.GuardrailsJson;
        existing.DefaultTagsJson = entity.DefaultTagsJson;
        
        // Git source properties
        existing.GitRepositoryUrl = entity.GitRepositoryUrl;
        existing.GitBranch = entity.GitBranch;
        existing.GitPath = entity.GitPath;
        existing.GitCommitSha = entity.GitCommitSha;
        existing.LastSyncedFromGit = entity.LastSyncedFromGit;
        existing.GitAutoSync = entity.GitAutoSync;
        existing.GitSyncIntervalMinutes = entity.GitSyncIntervalMinutes;
        
        // Approval properties
        existing.RequiresApproval = entity.RequiresApproval;
        existing.ApprovalSource = entity.ApprovalSource;
        existing.ApprovedBy = entity.ApprovedBy;
        existing.ApprovedAt = entity.ApprovedAt;
        existing.ApprovalComments = entity.ApprovalComments;
        existing.ExternalApprovalId = entity.ExternalApprovalId;
        existing.ExternalApprovalUrl = entity.ExternalApprovalUrl;
        
        // Deployment configuration
        existing.DeploymentScope = entity.DeploymentScope;
        
        // Metadata
        existing.ComplianceFrameworks = entity.ComplianceFrameworks;
        existing.EnforceCompliance = entity.EnforceCompliance;
        existing.Keywords = entity.Keywords;
        existing.UseCases = entity.UseCases;
        existing.AiSelectionHint = entity.AiSelectionHint;
        existing.DefaultExpirationDays = entity.DefaultExpirationDays;
        existing.VersionHistoryJson = entity.VersionHistoryJson;
        
        // Usage stats
        existing.DeploymentCount = entity.DeploymentCount;
        existing.LastDeployedAt = entity.LastDeployedAt;
        
        // Audit
        existing.UpdatedBy = entity.UpdatedBy;
        existing.UpdatedAt = DateTime.UtcNow;

        // Explicitly mark as modified to ensure EF Core's change tracker detects 
        // our property changes (FindAsync can return cached entities)
        _context.Entry(existing).State = EntityState.Modified;
        
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated service template {TemplateId} - {TemplateName}", entity.Id, entity.Name);

        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ServiceTemplates.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _context.ServiceTemplates.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted service template {TemplateId} - {TemplateName}", id, entity.Name);

        return true;
    }

    #endregion

    #region Audit Log

    public async Task<ServiceTemplateAuditEntity> AddAuditEntryAsync(ServiceTemplateAuditEntity entry, CancellationToken cancellationToken = default)
    {
        if (entry.Id == Guid.Empty)
        {
            entry.Id = Guid.NewGuid();
        }

        entry.Timestamp = DateTime.UtcNow;

        _context.ServiceTemplateAuditLogs.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);

        return entry;
    }

    public async Task<IReadOnlyList<ServiceTemplateAuditEntity>> GetAuditEntriesAsync(
        Guid entityId,
        string? entityType = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ServiceTemplateAuditLogs
            .AsNoTracking()
            .Where(a => a.EntityId == entityId);

        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Statistics

    public async Task<Dictionary<string, int>> GetCountByStatusAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .AsNoTracking()
            .Where(t => t.Status != ArchivedStatus)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetCountByCategoryAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .AsNoTracking()
            .Where(t => t.Status != ArchivedStatus)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetCountByFormatAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .AsNoTracking()
            .Where(t => t.Status != ArchivedStatus)
            .GroupBy(t => t.Format)
            .Select(g => new { Format = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Format, x => x.Count, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .AsNoTracking()
            .Where(t => t.Status != ArchivedStatus)
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }

    public async Task IncrementDeploymentCountAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken);
        if (entity != null)
        {
            entity.DeploymentCount++;
            entity.LastDeployedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    #endregion

    #region Git Sync

    public async Task<IReadOnlyList<ServiceTemplateEntity>> GetTemplatesNeedingSyncAsync(CancellationToken cancellationToken = default)
    {
        var syncThreshold = DateTime.UtcNow.AddHours(-1); // Sync every hour

        return await _context.ServiceTemplates
            .AsNoTracking()
            .Where(t => t.GitAutoSync &&
                       !string.IsNullOrEmpty(t.GitRepositoryUrl) &&
                       t.Status != ArchivedStatus &&
                       (t.LastSyncedFromGit == null || t.LastSyncedFromGit < syncThreshold))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateGitSyncTimestampAsync(Guid templateId, string? commitSha, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken);
        if (entity != null)
        {
            entity.LastSyncedFromGit = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(commitSha))
            {
                entity.GitCommitSha = commitSha;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    #endregion
}
