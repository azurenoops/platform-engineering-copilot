using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// EF Core repository implementation for Provisioned Environment operations.
/// </summary>
public class ProvisionedEnvironmentRepository : IProvisionedEnvironmentRepository
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<ProvisionedEnvironmentRepository> _logger;

    public ProvisionedEnvironmentRepository(
        PlatformEngineeringCopilotContext context,
        ILogger<ProvisionedEnvironmentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Environment CRUD

    public async Task<ProvisionedEnvironmentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
    }

    public async Task<ProvisionedEnvironmentEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return null;

        return await GetByIdAsync(guid, cancellationToken);
    }

    public async Task<ProvisionedEnvironmentEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .FirstOrDefaultAsync(e => e.Name == name && !e.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .Where(e => e.SubscriptionId == subscriptionId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetByTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .Where(e => e.TemplateId == templateId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .Where(e => e.Status == status && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProvisionedEnvironmentEntity>> SearchAsync(
        string? keyword = null,
        Guid? templateId = null,
        string? subscriptionId = null,
        string? status = null,
        string? ownerEmail = null,
        bool includeDeleted = false,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ProvisionedEnvironments.AsQueryable();

        if (!includeDeleted)
            query = query.Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.ToLower();
            query = query.Where(e =>
                e.Name.ToLower().Contains(lowerKeyword) ||
                e.DisplayName.ToLower().Contains(lowerKeyword) ||
                (e.Description != null && e.Description.ToLower().Contains(lowerKeyword)));
        }

        if (templateId.HasValue)
            query = query.Where(e => e.TemplateId == templateId.Value);

        if (!string.IsNullOrWhiteSpace(subscriptionId))
            query = query.Where(e => e.SubscriptionId == subscriptionId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        if (!string.IsNullOrWhiteSpace(ownerEmail))
            query = query.Where(e => e.OwnerEmail == ownerEmail);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetWithDriftAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .Where(e => e.HasDrift && !e.IsDeleted && e.Status == "Running")
            .OrderByDescending(e => e.DriftCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProvisionedEnvironmentEntity> CreateAsync(ProvisionedEnvironmentEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Guid.Empty)
            entity.Id = Guid.NewGuid();

        entity.CreatedAt = DateTime.UtcNow;

        _context.ProvisionedEnvironments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created provisioned environment {Id}: {Name}", entity.Id, entity.Name);
        return entity;
    }

    public async Task<ProvisionedEnvironmentEntity> UpdateAsync(ProvisionedEnvironmentEntity entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;

        _context.ProvisionedEnvironments.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated provisioned environment {Id}: {Name}", entity.Id, entity.Name);
        return entity;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, string deletedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ProvisionedEnvironments
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity == null)
            return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = deletedBy;
        entity.Status = "Deleted";

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Soft deleted provisioned environment {Id}: {Name} by {DeletedBy}", id, entity.Name, deletedBy);
        return true;
    }

    public async Task<bool> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ProvisionedEnvironments
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity == null)
            return false;

        _context.ProvisionedEnvironments.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Hard deleted provisioned environment {Id}: {Name}", id, entity.Name);
        return true;
    }

    #endregion

    #region Expiration Management

    public async Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetExpiringAsync(int withinDays, CancellationToken cancellationToken = default)
    {
        var expirationDate = DateTime.UtcNow.AddDays(withinDays);

        return await _context.ProvisionedEnvironments
            .Where(e => e.ExpiresAt.HasValue && 
                        e.ExpiresAt <= expirationDate && 
                        e.Status == "Running" && 
                        !e.IsDeleted)
            .OrderBy(e => e.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProvisionedEnvironmentEntity>> GetExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _context.ProvisionedEnvironments
            .Where(e => e.ExpiresAt.HasValue && 
                        e.ExpiresAt < now && 
                        e.Status == "Running" && 
                        !e.IsDeleted)
            .OrderBy(e => e.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Audit Log

    public async Task<EnvironmentAuditEntity> AddAuditEntryAsync(EnvironmentAuditEntity entry, CancellationToken cancellationToken = default)
    {
        if (entry.Id == Guid.Empty)
            entry.Id = Guid.NewGuid();

        entry.Timestamp = DateTime.UtcNow;

        _context.EnvironmentAuditLogs.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);

        return entry;
    }

    public async Task<IReadOnlyList<EnvironmentAuditEntity>> GetAuditEntriesAsync(
        Guid environmentId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentAuditLogs
            .Where(a => a.EnvironmentId == environmentId)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Statistics

    public async Task<Dictionary<string, int>> GetCountByStatusAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .Where(e => !e.IsDeleted)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var query = _context.ProvisionedEnvironments.AsQueryable();
        
        if (!includeDeleted)
            query = query.Where(e => !e.IsDeleted);

        return await query.CountAsync(cancellationToken);
    }

    public async Task<int> GetDriftCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProvisionedEnvironments
            .Where(e => e.HasDrift && !e.IsDeleted && e.Status == "Running")
            .CountAsync(cancellationToken);
    }

    public async Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ProvisionedEnvironments
            .Where(e => e.Name == name && !e.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(e => e.Id != excludeId.Value);

        return !await query.AnyAsync(cancellationToken);
    }

    #endregion
}
