using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// EF Core implementation of environment template repository
/// </summary>
public class EnvironmentTemplateRepository : IEnvironmentTemplateRepository
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<EnvironmentTemplateRepository> _logger;

    public EnvironmentTemplateRepository(
        PlatformEngineeringCopilotContext context,
        ILogger<EnvironmentTemplateRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ==================== Template Operations ====================

    public async Task<EnvironmentTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .Include(t => t.Files)
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive, cancellationToken);
    }

    public async Task<EnvironmentTemplate?> GetByIdWithFilesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .Include(t => t.Files)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<EnvironmentTemplate?> GetActiveByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .Include(t => t.Files)
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, cancellationToken);
    }

    public Task<EnvironmentTemplate?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        // Alias for GetActiveByNameAsync
        return GetActiveByNameAsync(name, cancellationToken);
    }

    public async Task<EnvironmentTemplate?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .Include(t => t.Files)
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnvironmentTemplate>> GetByConversationIdAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .Include(t => t.Files)
            .Where(t => t.IsActive && t.Tags != null && t.Tags.Contains(conversationId))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnvironmentTemplate>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnvironmentTemplate>> GetByTypeAsync(string templateType, CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .Where(t => t.IsActive && t.TemplateType == templateType)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnvironmentTemplate>> SearchByTagsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchTerm.ToLowerInvariant();
        
        return await _context.EnvironmentTemplates
            .Where(t => t.IsActive && 
                       t.Tags != null && 
                       t.Tags.ToLower().Contains(normalizedSearch))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnvironmentTemplate>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchTerm.ToLowerInvariant();
        
        return await _context.EnvironmentTemplates
            .Where(t => t.IsActive && 
                       (t.Name.ToLower().Contains(normalizedSearch) ||
                        (t.Description != null && t.Description.ToLower().Contains(normalizedSearch)) ||
                        (t.Tags != null && t.Tags.ToLower().Contains(normalizedSearch))))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnvironmentTemplate>> GetSoftDeletedByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .Where(t => t.Name == name && !t.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsActiveAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .AnyAsync(t => t.Name == name && t.IsActive, cancellationToken);
    }

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EnvironmentTemplates
            .CountAsync(t => t.IsActive, cancellationToken);
    }

    public async Task<EnvironmentTemplate> AddAsync(EnvironmentTemplate template, CancellationToken cancellationToken = default)
    {
        template.Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id;
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;

        _context.EnvironmentTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created EnvironmentTemplate {TemplateId}: {TemplateName}", template.Id, template.Name);

        return template;
    }

    public Task<EnvironmentTemplate> CreateAsync(EnvironmentTemplate template, CancellationToken cancellationToken = default)
    {
        // Alias for AddAsync
        return AddAsync(template, cancellationToken);
    }

    public async Task CleanupSoftDeletedByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var softDeletedTemplates = await _context.EnvironmentTemplates
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

        _context.EnvironmentTemplates.RemoveRange(softDeletedTemplates);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleaned up {Count} soft-deleted template(s)", softDeletedTemplates.Count);
    }

    public async Task<EnvironmentTemplate> UpdateAsync(EnvironmentTemplate template, CancellationToken cancellationToken = default)
    {
        template.UpdatedAt = DateTime.UtcNow;

        _context.EnvironmentTemplates.Update(template);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated EnvironmentTemplate {TemplateId}: {TemplateName}", template.Id, template.Name);

        return template;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _context.EnvironmentTemplates.FindAsync(new object[] { id }, cancellationToken);
        if (template == null)
            return false;

        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Soft deleted EnvironmentTemplate {TemplateId}", id);
        return true;
    }

    public async Task<bool> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _context.EnvironmentTemplates
            .Include(t => t.Files)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            
        if (template == null)
            return false;

        // Delete associated files first
        if (template.Files.Any())
        {
            _context.TemplateFiles.RemoveRange(template.Files);
        }

        _context.EnvironmentTemplates.Remove(template);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Hard deleted EnvironmentTemplate {TemplateId} with {FileCount} files", id, template.Files.Count);
        return true;
    }

    public async Task<int> HardDeleteRangeAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var templates = await _context.EnvironmentTemplates
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

        _context.EnvironmentTemplates.RemoveRange(templates);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Hard deleted {Count} EnvironmentTemplates", templates.Count);
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
        var deployments = await _context.EnvironmentDeployments
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

    public async Task<IReadOnlyList<EnvironmentTemplate>> GetExpiredTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.EnvironmentTemplates
            .Include(t => t.Files)
            .Where(t => t.ExpiresAt != null && t.ExpiresAt < now)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredTemplates = await _context.EnvironmentTemplates
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
        _context.EnvironmentTemplates.RemoveRange(expiredTemplates);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully deleted {Count} expired templates with {FileCount} files", 
            expiredTemplates.Count, allFiles.Count);
        
        return expiredTemplates.Count;
    }
}
