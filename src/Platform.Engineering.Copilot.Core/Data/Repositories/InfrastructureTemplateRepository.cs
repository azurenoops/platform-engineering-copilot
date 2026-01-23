using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// EF Core implementation of environment template repository
/// </summary>
public class InfrastructureTemplateRepository : IInfrastructureTemplateRepository
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<InfrastructureTemplateRepository> _logger;

    public InfrastructureTemplateRepository(
        PlatformEngineeringCopilotContext context,
        ILogger<InfrastructureTemplateRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ==================== Template Operations ====================

    public async Task<InfrastructureTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .Include(t => t.Files)
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive, cancellationToken);
    }

    public async Task<InfrastructureTemplate?> GetByIdWithFilesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .Include(t => t.Files)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<InfrastructureTemplate?> GetActiveByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .Include(t => t.Files)
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, cancellationToken);
    }

    public Task<InfrastructureTemplate?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        // Alias for GetActiveByNameAsync
        return GetActiveByNameAsync(name, cancellationToken);
    }

    public async Task<InfrastructureTemplate?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .Include(t => t.Files)
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureTemplate>> GetByConversationIdAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .Include(t => t.Files)
            .Where(t => t.IsActive && t.Tags != null && t.Tags.Contains(conversationId))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureTemplate>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureTemplate>> GetByTypeAsync(string templateType, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .Where(t => t.IsActive && t.TemplateType == templateType)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureTemplate>> SearchByTagsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchTerm.ToLowerInvariant();
        
        return await _context.InfrastructureTemplates
            .Where(t => t.IsActive && 
                       t.Tags != null && 
                       t.Tags.ToLower().Contains(normalizedSearch))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureTemplate>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchTerm.ToLowerInvariant();
        
        return await _context.InfrastructureTemplates
            .Where(t => t.IsActive && 
                       (t.Name.ToLower().Contains(normalizedSearch) ||
                        (t.Description != null && t.Description.ToLower().Contains(normalizedSearch)) ||
                        (t.Tags != null && t.Tags.ToLower().Contains(normalizedSearch))))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InfrastructureTemplate>> GetSoftDeletedByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .Where(t => t.Name == name && !t.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsActiveAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .AnyAsync(t => t.Name == name && t.IsActive, cancellationToken);
    }

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InfrastructureTemplates
            .CountAsync(t => t.IsActive, cancellationToken);
    }

    public async Task<InfrastructureTemplate> AddAsync(InfrastructureTemplate template, CancellationToken cancellationToken = default)
    {
        template.Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id;
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;

        _context.InfrastructureTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created InfrastructureTemplate {TemplateId}: {TemplateName}", template.Id, template.Name);

        return template;
    }

    public Task<InfrastructureTemplate> CreateAsync(InfrastructureTemplate template, CancellationToken cancellationToken = default)
    {
        // Alias for AddAsync
        return AddAsync(template, cancellationToken);
    }

    public async Task CleanupSoftDeletedByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var softDeletedTemplates = await _context.InfrastructureTemplates
            .Where(t => t.Name == name && !t.IsActive)
            .ToListAsync(cancellationToken);

        if (!softDeletedTemplates.Any())
            return;

        _logger.LogInformation("Found {Count} soft-deleted template(s) with name {TemplateName}, cleaning up", 
            softDeletedTemplates.Count, name);

        // Unlink deployments from these templates
        foreach (var softDeleted in softDeletedTemplates)
        {
            await UnlinkDeploymentsAsync(softDeleted.Id, cancellationToken);
            
            // Delete associated files
            var files = await _context.TemplateFiles
                .Where(f => f.TemplateId == softDeleted.Id)
                .ToListAsync(cancellationToken);
            _context.TemplateFiles.RemoveRange(files);
        }

        _context.InfrastructureTemplates.RemoveRange(softDeletedTemplates);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleaned up {Count} soft-deleted template(s)", softDeletedTemplates.Count);
    }

    public async Task<InfrastructureTemplate> UpdateAsync(InfrastructureTemplate template, CancellationToken cancellationToken = default)
    {
        template.UpdatedAt = DateTime.UtcNow;

        _context.InfrastructureTemplates.Update(template);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated InfrastructureTemplate {TemplateId}: {TemplateName}", template.Id, template.Name);

        return template;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _context.InfrastructureTemplates.FindAsync(new object[] { id }, cancellationToken);
        if (template == null)
            return false;

        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Soft deleted InfrastructureTemplate {TemplateId}", id);
        return true;
    }

    public async Task<bool> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _context.InfrastructureTemplates
            .Include(t => t.Files)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            
        if (template == null)
            return false;

        // Delete associated files first
        if (template.Files.Any())
        {
            _context.TemplateFiles.RemoveRange(template.Files);
        }

        _context.InfrastructureTemplates.Remove(template);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Hard deleted InfrastructureTemplate {TemplateId} with {FileCount} files", id, template.Files.Count);
        return true;
    }

    public async Task<int> HardDeleteRangeAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var templates = await _context.InfrastructureTemplates
            .Include(t => t.Files)
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(cancellationToken);

        if (!templates.Any())
            return 0;

        // Delete all associated files
        var allFiles = templates.SelectMany(t => t.Files).ToList();
        if (allFiles.Any())
        {
            _context.TemplateFiles.RemoveRange(allFiles);
        }

        _context.InfrastructureTemplates.RemoveRange(templates);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Hard deleted {Count} InfrastructureTemplates", templates.Count);
        return templates.Count;
    }

    // ==================== Template File Operations ====================

    public async Task<IReadOnlyList<TemplateFile>> GetFilesAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        return await _context.TemplateFiles
            .Where(f => f.TemplateId == templateId)
            .OrderBy(f => f.FilePath)
            .ToListAsync(cancellationToken);
    }

    public async Task<TemplateFile> AddFileAsync(TemplateFile file, CancellationToken cancellationToken = default)
    {
        file.Id = file.Id == Guid.Empty ? Guid.NewGuid() : file.Id;
        file.CreatedAt = DateTime.UtcNow;

        _context.TemplateFiles.Add(file);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added TemplateFile {FileId} to template {TemplateId}", file.Id, file.TemplateId);

        return file;
    }

    public async Task<IReadOnlyList<TemplateFile>> AddFilesAsync(IEnumerable<TemplateFile> files, CancellationToken cancellationToken = default)
    {
        var fileList = files.ToList();
        foreach (var file in fileList)
        {
            file.Id = file.Id == Guid.Empty ? Guid.NewGuid() : file.Id;
            file.CreatedAt = DateTime.UtcNow;
        }

        await _context.TemplateFiles.AddRangeAsync(fileList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added {Count} TemplateFiles", fileList.Count);

        return fileList;
    }

    public async Task<int> DeleteFilesAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var files = await _context.TemplateFiles
            .Where(f => f.TemplateId == templateId)
            .ToListAsync(cancellationToken);

        if (!files.Any())
            return 0;

        _context.TemplateFiles.RemoveRange(files);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} files from template {TemplateId}", files.Count, templateId);
        return files.Count;
    }

    // ==================== Template Version Operations ====================

    public async Task<IReadOnlyList<TemplateVersion>> GetVersionsAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        return await _context.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        return await _context.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TemplateVersion> AddVersionAsync(TemplateVersion version, CancellationToken cancellationToken = default)
    {
        version.Id = version.Id == Guid.Empty ? Guid.NewGuid() : version.Id;
        version.CreatedAt = DateTime.UtcNow;

        _context.TemplateVersions.Add(version);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added TemplateVersion {VersionId} for template {TemplateId}", version.Id, version.TemplateId);

        return version;
    }

    // ==================== Deployment Reference Operations ====================

    public async Task<int> UnlinkDeploymentsAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var deployments = await _context.InfrastructureDeployments
            .Where(d => d.TemplateId == templateId)
            .ToListAsync(cancellationToken);

        if (!deployments.Any())
            return 0;

        foreach (var deployment in deployments)
        {
            deployment.TemplateId = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Unlinked {Count} deployments from template {TemplateId}", deployments.Count, templateId);
        return deployments.Count;
    }

    // ==================== Expiration Operations ====================

    public async Task<IReadOnlyList<InfrastructureTemplate>> GetExpiredTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.InfrastructureTemplates
            .Include(t => t.Files)
            .Where(t => t.ExpiresAt != null && t.ExpiresAt < now)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredTemplates = await _context.InfrastructureTemplates
            .Include(t => t.Files)
            .Where(t => t.ExpiresAt != null && t.ExpiresAt < now)
            .ToListAsync(cancellationToken);

        if (!expiredTemplates.Any())
            return 0;

        var templateIds = expiredTemplates.Select(t => t.Id).ToList();
        
        _logger.LogInformation("Deleting {Count} expired templates", expiredTemplates.Count);

        // Unlink deployments first
        foreach (var templateId in templateIds)
        {
            await UnlinkDeploymentsAsync(templateId, cancellationToken);
        }

        // Delete all associated files
        var allFiles = expiredTemplates.SelectMany(t => t.Files).ToList();
        if (allFiles.Any())
        {
            _context.TemplateFiles.RemoveRange(allFiles);
            _logger.LogDebug("Deleted {Count} files from expired templates", allFiles.Count);
        }

        // Delete template versions
        var versions = await _context.TemplateVersions
            .Where(v => templateIds.Contains(v.TemplateId))
            .ToListAsync(cancellationToken);
        if (versions.Any())
        {
            _context.TemplateVersions.RemoveRange(versions);
        }

        // Delete the templates
        _context.InfrastructureTemplates.RemoveRange(expiredTemplates);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully deleted {Count} expired templates with {FileCount} files", 
            expiredTemplates.Count, allFiles.Count);
        
        return expiredTemplates.Count;
    }
}
